using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Preview
{
    // 임시 육안 확인 장면 전용. 운영 물리·카메라·스폰/해체 종료 정책을 결정하지 않습니다.
    public sealed class GameplayPreview : MonoBehaviour
    {
        [SerializeField, InspectorName("데이터 초기화")] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("플레이어 런타임")] private PlayerRuntime _player = null;
        [SerializeField, InspectorName("보스 런타임")] private BossRuntime _boss = null;
        [SerializeField, InspectorName("일반몹 데이터 수신")] private MonsterRuntime[] _monsters = Array.Empty<MonsterRuntime>();
        [SerializeField, InspectorName("플레이어 이름표")] private TextMesh _playerLabel = null;
        [SerializeField, InspectorName("보스 이름표")] private TextMesh _bossLabel = null;
        [SerializeField, InspectorName("플레이어 사각형")] private SpriteRenderer _playerBody = null;
        [SerializeField, InspectorName("보스 사각형")] private SpriteRenderer _bossBody = null;
        [SerializeField, InspectorName("임시 공격 범위")] private SpriteRenderer _attackWindow = null;
        [Header("임시 표시 설정 (운영 수치 아님)")]
        [SerializeField, InspectorName("타일당 화면 거리"), Min(0.01f)] private float _worldUnitsPerTile = 1f;
        [SerializeField, InspectorName("임시 점프 시간 (초)"), Min(0.01f)] private float _jumpSeconds = 0.8f;
        [SerializeField, InspectorName("화면 이동 한계 (좌우)"), Min(1f)] private float _stageHalfWidth = 9f;
        [SerializeField, InspectorName("임시 해체 거리 (타일)"), Min(0f)] private float _executionRangeTiles = 1.75f;
        [SerializeField, InspectorName("시작 시 스태미나 최대로")] private bool _startWithFullStamina = true;
        [SerializeField, InspectorName("시작 스태미나"), Min(0)] private int _initialStamina = 0;
        [SerializeField, InspectorName("해체 확인 후 체세 회복")] private bool _restorePoiseAfterPreviewExecution = true;
        [SerializeField, InspectorName("플레이어 체력 표시 연결")] private HealthDisplayHost _healthDisplay = null;
        [SerializeField, InspectorName("체력 표시 프리팹 목록")] private HealthDisplayPrefabs _healthPrefabs = null;
        [SerializeField, InspectorName("체력 표시 앵커 묶음")] private Transform _healthAnchorRoot = null;
        [SerializeField, InspectorName("임시 보스 근접 거리 (타일)"), Min(0.01f)] private float _bossRangeTiles = 2.5f;
        [SerializeField, InspectorName("임시 보스 둘째 타 거리 (타일)"), Min(0.01f)] private float _bossSecondRangeTiles = 3.5f;
        private PreviewBossDriver _bossDriver;
        // 장면 내 양수 개체 ID(데이터 ID와 별개). 일반몹은 _firstMonsterTargetId부터 순번. 운영 스폰은 스폰 담당이 할당합니다.
        private const int _playerTargetId = 1;
        private const int _bossTargetId = 2;
        private const int _firstMonsterTargetId = 3;
        private PlayerLocomotionInput _input;
        private PlayerActor _actor;
        private float _groundY;
        private PreviewPlayerMotion _motion;
        private bool _subscribed;
        private int _facing = 1;
        private Color _playerColor;
        private Color _bossColor;
        private PreviewMonsterDriver[] _monsterDrivers = Array.Empty<PreviewMonsterDriver>();

        public bool IsReady => _subscribed;
        public PlayerRuntime Player => _player;
        public BossRuntime Boss => _boss;

        private void OnEnable()
        {
            if (_data == null || _player == null || _boss == null || _playerLabel == null ||
                _bossLabel == null || _playerBody == null || _bossBody == null || _attackWindow == null)
            {
                Debug.LogError("임시 확인 장면의 데이터·프리팹·표시 참조를 연결하세요.", this);
                enabled = false;
                return;
            }
            _data.Initialized += OnDataReady;
            if (_data.Data != null) OnDataReady(_data.Data);
        }

        private void OnDataReady(IReadOnlyDataCatalog catalog)
        {
            if (_subscribed) return;
            try
            {
                if (_player.Actor == null)
                {
                    _groundY = _player.transform.position.y;
                    Player definition = catalog.GetTable<Player>().Get(2001);
                    int stamina = _startWithFullStamina ? definition.StaminaMax : _initialStamina;
                    // 이 장면의 사각형은 표시용 바닥 높이에 놓여 있습니다.
                    _player.Initialize(catalog, stamina, true);
                }
                if (_boss.Actor == null) _boss.Initialize(catalog);
                DisposeMonsterDrivers();
                _monsterDrivers = new PreviewMonsterDriver[_monsters.Length];
                for (int i = 0; i < _monsters.Length; i++)
                {
                    MonsterRuntime monster = _monsters[i];
                    if (monster == null) throw new InvalidOperationException("일반몹 데이터 수신 참조를 연결하세요.");
                    if (!monster.IsInitialized) monster.Initialize(catalog);
                    _monsterDrivers[i] = new PreviewMonsterDriver(monster, _player, _playerBody, _attackWindow,
                        _firstMonsterTargetId + i, _playerTargetId, _worldUnitsPerTile);
                }
                _bossDriver?.Dispose();
                _bossDriver = _boss.Combat == null ? null : new PreviewBossDriver(_boss, _player, _bossBody, _playerBody,
                    _worldUnitsPerTile, _bossRangeTiles, _bossSecondRangeTiles);
                _actor = _player.Actor;
                if (_motion == null) _motion = new PreviewPlayerMotion(_player, _groundY);
                _input = _player.GetComponent<PlayerLocomotionInput>();
                if (_input == null) throw new InvalidOperationException("플레이어 입력 어댑터가 없습니다.");
                _input.JumpAccepted += OnJump;
                _actor.RollRequested += OnRoll;
                // 판정 창이 한 프레임 안에 열리고 닫혀도 열리는 순간 한 번은 접촉을 확인합니다.
                _actor.AttackWindowChanged += OnPlayerAttackWindowChanged;
                _subscribed = true;
                if (_healthDisplay != null) _healthDisplay.Connect(catalog, _healthPrefabs, _actor, _healthAnchorRoot);
                _playerColor = _playerBody.color;
                _bossColor = _bossBody.color;
                RefreshLabels();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_data != null) _data.Initialized -= OnDataReady;
            if (_subscribed)
            {
                if (_input != null) _input.JumpAccepted -= OnJump;
                _actor.RollRequested -= OnRoll;
                _actor.AttackWindowChanged -= OnPlayerAttackWindowChanged;
            }
            _bossDriver?.Dispose();
            _bossDriver = null;
            if (_healthDisplay != null) _healthDisplay.Disconnect();
            DisposeMonsterDrivers();
            _subscribed = false;
            if (_attackWindow != null) _attackWindow.enabled = false;
        }

        private void DisposeMonsterDrivers()
        {
            foreach (PreviewMonsterDriver driver in _monsterDrivers) driver?.Dispose();
            _monsterDrivers = Array.Empty<PreviewMonsterDriver>();
        }

        private void OnPlayerAttackWindowChanged(bool active)
        {
            if (active && _player.isActiveAndEnabled && _boss.isActiveAndEnabled) CheckPlayerHits();
        }

        // 임시 판정: 공격 범위 사각형과 보스/일반몹 사각형 겹침. 창 열림 순간과 매 프레임 호출하며 중복은 창 등록이 막습니다.
        private void CheckPlayerHits()
        {
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
            _attackWindow.transform.localPosition = new Vector3(_facing * 0.95f, 0f, 0.1f);
            if (!_actor.IsAttackActive) return;
            if (_attackWindow.bounds.Intersects(_bossBody.bounds))
                _actor.TryHitBoss(_bossTargetId, _boss.Actor, _player.HitRandom);
            foreach (PreviewMonsterDriver monster in _monsterDrivers) monster.CheckPlayerHit();
        }

        private void OnJump() => _motion.StartJump();

        private void OnRoll(int direction, int distance, int duration)
            => _motion.StartRoll(direction, distance, duration, _worldUnitsPerTile);

        private void LateUpdate()
        {
            if (!_subscribed || !_player.isActiveAndEnabled || !_boss.isActiveAndEnabled) return;
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
            _motion.Move(Time.deltaTime, _worldUnitsPerTile, _jumpSeconds, _stageHalfWidth);
            _attackWindow.enabled = _actor.IsAttackActive;
            // 몬스터 이동 후 플레이어 판정을 확인합니다(기존 순서 유지).
            foreach (PreviewMonsterDriver monster in _monsterDrivers)
                monster.Step(Time.deltaTime, _stageHalfWidth);
            _bossDriver?.Step(Time.deltaTime, _stageHalfWidth);
            CheckPlayerHits();
            HandlePreviewKeys();
            RefreshLabels();
        }

        private void HandlePreviewKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame)
                _actor.ReceiveHit(CombatMath.Damage(_boss.Actor.Definition, _actor.Definition, 0, false));
            if (keyboard.eKey.wasPressedThisFrame)
            {
                float distance = Vector2.Distance(_player.transform.position, _boss.transform.position);
                _actor.TryExecute(_boss.Actor, distance <= _executionRangeTiles * _worldUnitsPerTile);
            }
            // 연출 완료를 수동으로 대신하는 임시 키. 종료 체세 정책은 Inspector에서 명시합니다.
            if (keyboard.rKey.wasPressedThisFrame)
                _actor.CompleteExecution(_restorePoiseAfterPreviewExecution);
        }

        private void RefreshLabels()
        {
            PreviewPresentation.RefreshPlayer(_actor, _playerLabel, _playerBody, _playerColor);
            PreviewPresentation.RefreshBoss(_boss.Actor, _bossLabel, _bossBody, _bossColor);
            if (_boss.Combat != null && _boss.Combat.IsAttacking) _bossBody.color = new Color(1f, 0.2f, 0.2f);
        }

        // 상태 식별자와 동작은 유지하고 화면 표시만 번역합니다.
        internal static string GetStateLabel(StateKey state) => PreviewPresentation.GetStateLabel(state);
    }
}
