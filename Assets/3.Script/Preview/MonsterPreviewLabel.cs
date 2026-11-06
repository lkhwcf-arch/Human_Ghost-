using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 일반몹 이름표. 정의 수신 후 실행 코어의 현재 상태·HP를 표시합니다.
    [DefaultExecutionOrder(1100)]
    public class MonsterPreviewLabel : MonoBehaviour
    {
        [SerializeField] private MonsterRuntime _runtime = null;
        [SerializeField] private TextMesh _label = null;
        [SerializeField] private string _displayName = "일반 몬스터";
        private MonsterActor _actor;
        private SubscriptionScope _lifetimeScope, _actorScope;

        private void OnEnable()
        {
            if (_runtime == null || _label == null)
            {
                Debug.LogError("일반몹 데이터 수신부와 한글 이름표를 연결하세요.", this);
                enabled = false;
                return;
            }
            _lifetimeScope?.Dispose();
            _lifetimeScope = new SubscriptionScope();
            MonsterRuntime runtime = _runtime;
            SubscriptionScope lifetime = _lifetimeScope;
            System.Action<Monster> initialized = lifetime.Guard<Monster>(Connect);
            lifetime.Add(() => runtime.Initialized -= initialized);
            runtime.Initialized += initialized;
            if (_runtime.IsInitialized) Connect(_runtime.Definition);
        }

        private void OnDisable()
        {
            _lifetimeScope?.Dispose(); _lifetimeScope = null;
            Disconnect();
        }

        private void LateUpdate()
        {
            if (_actor != null && _actorScope != null && _actorScope.TryConsumeRefresh()) RefreshLabel(_runtime.Definition);
        }

        private void Connect(Monster definition)
        {
            Disconnect();
            _actor = _runtime.Actor;
            _actorScope = new SubscriptionScope();
            MonsterActor actor = _actor;
            SubscriptionScope scope = _actorScope;
            System.Action<int> healthChanged = value => scope.Invalidate();
            System.Action<StateKey> stateChanged = value => scope.Invalidate();
            System.Action<bool> attackChanged = value => scope.Invalidate();
            scope.Add(() => actor.HealthChanged -= healthChanged); actor.HealthChanged += healthChanged;
            scope.Add(() => actor.States.Changed -= stateChanged); actor.States.Changed += stateChanged;
            scope.Add(() => actor.AttackWindowChanged -= attackChanged); actor.AttackWindowChanged += attackChanged;
            scope.Invalidate();
        }
        private void Disconnect()
        {
            _actorScope?.Dispose(); _actorScope = null;
            _actor = null;
        }
        public void SetDisplayName(string displayName) { _displayName = displayName; _actorScope?.Invalidate(); }
        private void OnValidate() => _actorScope?.Invalidate();

        // 실행 코어의 현재 상태·현재 HP를 표시합니다.
        private void RefreshLabel(Monster definition)
        {
            MonsterActor actor = _runtime.Actor;
            string text = _displayName + "\n몬스터 " + definition.ID + "\n" + PreviewPresentation.GetStateLabel(actor.CurrentState) +
                (actor.IsSuperArmor ? " (슈퍼아머)" : "") + "\n체력 " + actor.Health;
            if (_label.text != text) _label.text = text;
        }
    }
}
