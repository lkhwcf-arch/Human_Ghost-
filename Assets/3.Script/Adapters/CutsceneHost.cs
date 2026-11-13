using System;
using System.Threading;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 공용 조립 경계입니다. 재생 결과는 보관하고 호출자가 자기 실행 흐름에서 소비합니다.
    public class CutsceneHost : MonoBehaviour
    {
        [SerializeField, InspectorName("컷씬 화면")] private CutsceneScreen _screen = null;
        [SerializeField, InspectorName("컷씬 입력")] private CutsceneInput _input = null;
        [SerializeField, InspectorName("씬 공통 모달 연결")] private ErrorPopupSceneBinding _modals = null;
        private SubscriptionScope _scope;
        private CancellationTokenSource _preparation;
        private CutsceneEndReason? _pending, _result;
        private bool _preparing;
        private int _prepareElapsedMs;
        private MillisecondClock _clock;
        public bool IsBusy => _scope != null;
        public static CutsceneHost Create(CutsceneHost prefab, Transform parent, ErrorPopupSceneBinding modals, out GameObject root)
        {
            if (prefab == null || parent == null || modals == null)
                throw new InvalidOperationException("공용 컷씬 프리팹·부모·모달 연결이 필요합니다.");
            var host = UiPrefabScope.Create(prefab, parent, out root);
            root.name = "CutsceneScope";
            try
            {
                host.ConnectModals(modals);
                root.SetActive(true);
                return host;
            }
            catch { Destroy(root); root = null; throw; }
        }
        public void ConnectModals(ErrorPopupSceneBinding modals)
        {
            if (IsBusy) throw new InvalidOperationException("컷씬 실행 중에는 모달 연결을 바꿀 수 없습니다.");
            _modals = modals != null ? modals : throw new ArgumentNullException(nameof(modals));
        }

        public bool TryPlay(IReadOnlyDataCatalog data, int cutsceneId, GameAudio audio = null, SkipScope? skipScope = null)
        {
            if (!isActiveAndEnabled || IsBusy || _result.HasValue) return false;
            if (_screen == null || !_screen.isActiveAndEnabled || _screen.IsRunning || _input == null
                || !_input.gameObject.activeInHierarchy || _input.Screen != _screen || _modals == null || !_modals.isActiveAndEnabled)
                throw new InvalidOperationException("컷씬 실행 담당의 활성 화면·같은 화면을 사용하는 입력·씬 공통 모달 연결을 확인해 주세요.");
            CutsceneSequence sequence = CutsceneSequenceSource.Create(data, cutsceneId);
            if (skipScope.HasValue)
            {
                _input.enabled = false;
                _input.SetScope(skipScope.Value);
            }
            _input.Validate();
            var scope = new SubscriptionScope();
            _scope = scope;
            _preparation = new CancellationTokenSource();
            _preparing = true; _prepareElapsedMs = 0; _clock = default;
            try
            {
                scope.Add(() => { if (_modals != null) _modals.SetModalOwner(this, false); });
                _modals.SetModalOwner(this, true);
                if (audio != null)
                {
                    scope.Add(() => { if (audio != null) audio.SetCutsceneOwner(this, false); });
                    audio.SetCutsceneOwner(this, true);
                }
                scope.Add(() => { if (_input != null) { _input.enabled = false; _input.SetBlocked(false); } });
                scope.Add(() => { if (_screen != null) _screen.Disconnect(); });
                Action<CutsceneEndReason> finished = scope.Guard<CutsceneEndReason>(reason => _pending = reason);
                scope.Add(() => { if (_screen != null) _screen.Finished -= finished; });
                _screen.Finished += finished;
                Action modalChanged = scope.Guard(() => SetErrorBlocked(_modals.HasOtherModalOwner(this)));
                scope.Add(() => { if (_modals != null) _modals.ModalChanged -= modalChanged; });
                _modals.ModalChanged += modalChanged;
                _input.enabled = false;
                var text = new LanguageTextSource(data);
                _screen.Connect(text, LanguageCode.Korean);
                _input.ConnectHint(text, LanguageCode.Korean);
                SetErrorBlocked(_modals.HasOtherModalOwner(this));
                Prepare(sequence, scope, _preparation.Token);
                return true;
            }
            catch { Complete(CutsceneEndReason.Failed); _result = null; throw; }
        }

        // 리소스 준비에만 비동기를 사용하고 늦은 완료는 끝난 수명에 접근하지 않습니다.
        private async void Prepare(CutsceneSequence sequence, SubscriptionScope scope, CancellationToken cancellation)
        {
            try
            {
                await _screen.PrepareAsync(sequence, cancellation);
                if (scope.IsDisposed || cancellation.IsCancellationRequested) return;
                if (this == null || !isActiveAndEnabled || _screen == null || !_screen.isActiveAndEnabled)
                    throw new InvalidOperationException("컷씬 준비 대상의 활성 수명이 종료됐습니다.");
                if (!_screen.Play(sequence)) throw new InvalidOperationException("컷씬 화면이 재생 요청을 수락하지 않았습니다.");
                _preparing = false;
                _input.enabled = true;
                if (!_input.IsReady) throw new InvalidOperationException("컷씬 입력의 활성화에 실패했습니다.");
            }
            catch (Exception error)
            {
                if (scope.IsDisposed) return;
                _pending = CutsceneEndReason.Failed;
                Debug.LogException(new InvalidOperationException("컷씬 리소스 준비 또는 재생 시작에 실패했습니다.", error), this);
            }
        }

        private void SetErrorBlocked(bool blocked)
        {
            if (_input != null) _input.SetBlocked(blocked);
            if (_screen != null) _screen.SetPaused(blocked);
        }

        protected virtual void LateUpdate()
        {
            if (IsBusy && _preparing && !_pending.HasValue)
            {
                _prepareElapsedMs += Math.Min(_clock.Advance(Time.unscaledDeltaTime), 30000 - _prepareElapsedMs);
                if (_prepareElapsedMs == 30000)
                {
                    _pending = CutsceneEndReason.Failed;
                    Debug.LogError("컷씬 주소 리소스 준비가 30000밀리초 안에 완료되지 않았습니다. 진행을 중단하고 잠금을 해제합니다.", this);
                }
            }
            if (_pending.HasValue) Complete(_pending.Value);
        }
        public bool TryTakeResult(out CutsceneEndReason reason)
        {
            reason = _result ?? CutsceneEndReason.Cancelled;
            if (!_result.HasValue) return false;
            _result = null;
            return true;
        }
        public void Cancel() { if (IsBusy) Complete(CutsceneEndReason.Cancelled); }
        private void Complete(CutsceneEndReason reason)
        {
            SubscriptionScope scope = _scope;
            _scope = null; _pending = null; _preparing = false;
            _preparation?.Cancel(); _preparation?.Dispose(); _preparation = null;
            try { scope?.Dispose(); }
            catch (Exception error) { reason = CutsceneEndReason.Failed; Debug.LogException(error, this); }
            finally { _result = reason; }
        }
        protected virtual void OnDisable() => Cancel();
    }
}
