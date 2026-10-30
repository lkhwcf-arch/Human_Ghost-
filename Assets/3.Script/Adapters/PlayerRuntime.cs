using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 제공된 초기화 완료 데이터와 실제 접지 관측을 받는 프리팹 연결점입니다.
    public sealed class PlayerRuntime : MonoBehaviour
    {
        [SerializeField] private int _playerId = 2001;
        [SerializeField] private PlayerCombatSettings _combat = null;
        [SerializeField] private PlayerLocomotionInput _input = null;
        private double _remainder;
        public PlayerActor Actor { get; private set; }
        public event Action<PlayerActor> Initialized;

        // GameDataInitializer 완료 소비자가 호출합니다. 공급 초기화/저장소를 중복 생성하지 않습니다.
        public void Initialize(IReadOnlyDataCatalog catalog, int currentStamina, bool grounded)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Actor != null) throw new InvalidOperationException("이미 초기화된 플레이어입니다.");
            if (_combat == null || _input == null) throw new InvalidOperationException("전투 설정과 입력 어댑터를 연결하세요.");
            var actor = new PlayerActor(catalog.GetTable<Player>().Get(_playerId), _combat.CreateAttacks(), grounded, currentStamina);
            _input.Initialize(actor);
            Actor = actor;
            _input.enabled = isActiveAndEnabled;
            Initialized?.Invoke(actor);
        }

        public void SetGrounded(bool grounded)
        {
            if (Actor == null) throw new InvalidOperationException("플레이어 초기화가 필요합니다.");
            Actor.Locomotion.SetGrounded(grounded);
        }

        private void OnEnable() { if (Actor != null) _input.enabled = true; }
        private void OnDisable() { if (_input != null) _input.enabled = false; }
        private void Update()
        {
            if (Actor == null) return;
            double total = _remainder + Time.deltaTime * 1000.0;
            int milliseconds = (int)Math.Min(int.MaxValue, Math.Floor(total));
            _remainder = total - milliseconds;
            Actor.Tick(milliseconds);
        }
    }
}
