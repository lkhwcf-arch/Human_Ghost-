using System;
using System.Collections.Generic;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HumanGhost.Gameplay.Adapters
{
    // 씬의 명시적 참조만 조립합니다. 화면·수집·게임 규칙은 각 담당에게 위임합니다.
    [DefaultExecutionOrder(-50)]
    public class ErrorPopupSceneBinding : MonoBehaviour
    {
        private const int _unavailableMessageTextId = 30330;
        [SerializeField, InspectorName("데이터 초기화")] private GameDataInitializer _initializer = null;
        [SerializeField, InspectorName("씬 이벤트 시스템")] private EventSystem _events = null;
        [SerializeField, InspectorName("에러 호스트 프리팹")] private ErrorPopupHost _prefab = null;
        [SerializeField, InspectorName("입력 잠금 대상")] private MonoBehaviour[] _inputTargets = Array.Empty<MonoBehaviour>();
        [SerializeField, InspectorName("게임 속도 (타이틀은 비움)")] private GameSpeedController _speed = null;
        [SerializeField, InspectorName("공통 타이틀 복귀 화면")] private HumanGhost.Gameplay.Presentation.LoadingScreen _returnScreen = null;
        private ErrorPopupHost _host;
        private readonly HashSet<object> _modalOwners = new HashSet<object>();
        private readonly object _errorOwner = new object();
        public bool IsErrorOpen => _host != null && _host.IsOpen;
        public event Action<bool> ErrorVisibilityChanged;
        public event Action ModalChanged;
        public bool HasOtherModalOwner(object owner)
        {
            foreach (object active in _modalOwners) if (!ReferenceEquals(active, owner)) return true;
            return false;
        }

        // 호출 경계의 실패는 자동 로그 수집을 기다리지 않고 상세 상태와 함께 보고합니다.
        public virtual void Report(Exception error, UnityEngine.Object context = null, string state = "")
        {
            if (_host != null && _host.isActiveAndEnabled)
            {
                try { _host.Report(error, context: context, state: state); }
                catch (Exception displayError) { ErrorPopupHost.WriteError(displayError, this); }
                return;
            }
            Debug.LogException(error, context);
        }

        public virtual void ReportUnavailable(Exception error, int messageTextId, int featureTextId,
            UnityEngine.Object context = null, string state = "")
        {
            if (_host == null || !_host.isActiveAndEnabled) { Debug.LogException(error, context); return; }
            try { _host.ReportUnavailable(error, messageTextId, featureTextId, context, state); }
            catch (Exception displayError)
            {
                ErrorPopupHost.WriteError(error, context);
                ErrorPopupHost.WriteError(displayError, this);
            }
        }

        public void ReportUnavailable(Exception error, int featureTextId, UnityEngine.Object context = null, string state = "")
            => ReportUnavailable(error, _unavailableMessageTextId, featureTextId, context, state);

        protected virtual void OnEnable()
        {
            if (_initializer == null || _events == null || _prefab == null)
                throw new InvalidOperationException("에러 팝업의 데이터 초기화·이벤트 시스템·호스트 프리팹을 연결해 주세요.");
            foreach (var target in _inputTargets)
                if (!(target is IModalInputTarget))
                    throw new InvalidOperationException("입력 잠금 대상에는 모달 입력 계약을 구현한 컴포넌트를 연결해 주세요.");
            _initializer.Initialized += Prepare;
            if (_initializer.Data != null) Prepare(_initializer.Data);
        }

        private void Prepare(IReadOnlyDataCatalog data)
        {
            if (_host != null || !isActiveAndEnabled) return;
            // 비활성 부모 아래에서 복제·초기화를 마치고 활성화합니다.
            var holder = new GameObject("ErrorPopupScope", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            try
            {
                _host = Instantiate(_prefab, holder.transform);
                _host.name = "ErrorPopupHost";
                _host.Initialize(_events, SetBlocked, data);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                foreach (var reset in GetComponents<DemoResetHost>())
                    reset.Initialize(data, this, _events, _host.Input, ValidateTitleReturn, ReturnToTitle);
#endif
                holder.SetActive(true);
            }
            catch
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                foreach (var reset in GetComponents<DemoResetHost>()) reset.ReleaseBinding();
#endif
                _host = null;
                Destroy(holder);
                throw;
            }
        }

        private void SetBlocked(bool blocked)
        {
            SetModalOwner(_errorOwner, blocked);
            ErrorVisibilityChanged?.Invoke(blocked);
        }

        private void ValidateTitleReturn()
        {
            if (_returnScreen == null || !Application.CanStreamedLevelBeLoaded("Title"))
                throw new InvalidOperationException("씬 공통 연결의 타이틀 복귀 화면과 빌드 목록의 Title 씬을 확인해 주세요.");
        }
        private void ReturnToTitle()
        {
            ValidateTitleReturn();
            Instantiate(_returnScreen).BeginFade("Title");
        }

        // 각 창이 자기 잠금만 해제하도록 모달 소유자를 구분합니다.
        public void SetModalOwner(object owner, bool blocked)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            bool previouslyOwned = _modalOwners.Contains(owner);
            if (blocked) _modalOwners.Add(owner); else _modalOwners.Remove(owner);
            try { ApplyModalState(); }
            catch (Exception error)
            {
                // 닫힌 창의 소유권은 되살리지 않습니다. 나머지 대상 해제는 이미 모두 시도했습니다.
                if (!blocked) throw;
                if (previouslyOwned) _modalOwners.Add(owner); else _modalOwners.Remove(owner);
                try { ApplyModalState(); }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("입력 잠금 변경과 복원에 실패했습니다.", error, rollbackError);
                }
                throw;
            }
        }

        private void ApplyModalState()
        {
            bool paused = _modalOwners.Count > 0;
            var actions = new List<Action> { () => { if (_speed != null) _speed.State.SetModalPaused(paused); } };
            foreach (var target in _inputTargets)
            {
                var current = target;
                actions.Add(() => { if (current != null) ((IModalInputTarget)current).SetModalBlocked(paused); });
            }
            actions.Add(() => ModalChanged?.Invoke());
            SubscriptionScope.Cleanup(actions.ToArray());
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var reset in GetComponents<DemoResetHost>()) reset.ReleaseBinding();
#endif
            if (_initializer != null) _initializer.Initialized -= Prepare;
            var host = _host;
            _host = null;
            _modalOwners.Clear();
            SubscriptionScope.Cleanup(
                () => { if (host != null) host.gameObject.SetActive(false); },
                () => { if (host != null) Destroy(host.transform.parent.gameObject); },
                ApplyModalState);
        }
    }
}
