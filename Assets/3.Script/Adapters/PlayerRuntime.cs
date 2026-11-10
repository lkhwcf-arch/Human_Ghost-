using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 제공된 초기화 완료 데이터와 실제 접지 관측을 받는 프리팹 연결점입니다.
    public sealed class PlayerRuntime : SpatialRuntime
    {
        [SerializeField] private int _playerId = 2001;
        [SerializeField] private PlayerCombatSettings _combat = null;
        [SerializeField] private PlayerLocomotionInput _input = null;
        private MillisecondClock _clock;
        private System.Random _hitRandom;
        public PlayerActor Actor { get; private set; }
        // 화면 전용 진행률입니다. 게임 이동 좌표와 분리하며 미사용 값은 -1입니다.
        public float ClimbProgress { get; private set; } = -1f;
        public void SetClimbProgress(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < -1f || progress > 1f)
                throw new ArgumentOutOfRangeException(nameof(progress));
            ClimbProgress = progress;
        }
        // 이 플레이어가 공격자일 때의 회피·치명 난수입니다.
        public Func<int, int, int> HitRandom { get; private set; }
        public event Action<string> PresentationCue;
        // 상태 ID가 없는 성공 동작(Climb/Save)의 표현 알림입니다. 전투 상태는 변경하지 않습니다.
        public void NotifyPresentationCue(string cue)
        {
            if (string.IsNullOrEmpty(cue)) throw new ArgumentException("동작 키가 필요합니다.", nameof(cue));
            PresentationCue?.Invoke(cue);
        }
        public event Action<PlayerActor> Initialized;
        public event Action Destroyed;

        // GameDataInitializer 완료 소비자가 호출합니다. 공급 초기화/저장소를 중복 생성하지 않습니다.
        public void Initialize(IReadOnlyDataCatalog catalog, int currentStamina, bool grounded)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Actor != null) throw new InvalidOperationException("이미 초기화된 플레이어입니다.");
            if (_combat == null || _input == null) throw new InvalidOperationException("전투 설정과 입력 어댑터를 연결하세요.");
            var actor = new PlayerActor(catalog.GetTable<Player>().Get(_playerId), _combat.CreateAttacks(), grounded, currentStamina);
            PlayerAction actions = null;
            foreach (var row in catalog.GetTable<PlayerAction>().All)
                if (row.PlayerID == _playerId)
                {
                    if (actions != null) throw new InvalidOperationException("플레이어의 추가 행동 정의가 중복됩니다.");
                    actions = row;
                }
            if (actions != null) actor.ConfigureActions(actions);
            actor.ActionCue += NotifyPresentationCue;
            _input.Initialize(actor);
            _hitRandom = new System.Random(Environment.TickCount ^ GetInstanceID());
            HitRandom = _hitRandom.Next;
            Actor = actor;
            _input.enabled = isActiveAndEnabled;
            Initialized?.Invoke(actor);
        }

        // 모든 연결 경로가 같은 기획 잠금·해금 판단을 거칩니다. 시험 모드도 기획 잠금은 우회하지 않습니다.
        public bool EnableSkill(Skill definition, ContentAccess access)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (Actor == null) throw new InvalidOperationException("플레이어 초기화가 필요합니다.");
            if (access == null) throw new ArgumentNullException(nameof(access));
            ContentAccessResult result = access.Evaluate(definition);
            if (result != ContentAccessResult.Allowed) return false;
            Actor.ConfigureSkill(_combat.CreateSkill(definition), definition.Cooldown, _combat.SkillDistance);
            return true;
        }

        public void EnableHerbs(Buff definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (Actor == null) throw new InvalidOperationException("플레이어 초기화가 필요합니다.");
            Actor.ConfigureHerbs(definition.Value, definition.UseTime, definition.Charges);
        }

        public void SetGrounded(bool grounded)
        {
            if (Actor == null) throw new InvalidOperationException("플레이어 초기화가 필요합니다.");
            bool previous = Actor.Locomotion.IsGrounded;
            Actor.Locomotion.SetGrounded(grounded);
            if (previous != grounded) NotifySpatialStateChanged();
        }

        private void OnEnable() { if (Actor != null) _input.enabled = true; }
        private void OnDisable()
        {
            if (_input != null) _input.enabled = false;
            // 비활성화 중 플레이어 Tick이 멈추므로 남은 일반몹 구속을 즉시 정리합니다.
            if (Actor?.BoundBy != null) Actor.ReleaseBind(Actor.BoundBy);
            Actor?.ReleaseChain();
            Actor?.CancelCombatAction();
            ClimbProgress = -1f;
        }
        private void OnDestroy()
        {
            if (Actor != null) Actor.ActionCue -= NotifyPresentationCue;
            Destroyed?.Invoke();
        }
        private void Update()
        {
            if (Actor == null) return;
            Actor.Tick(_clock.Advance(Time.deltaTime));
        }
    }
}
