using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Adapters;
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
        private PlayerLocomotionInput _input;
        private PlayerActor _actor;
        private float _groundY;
        private float _jumpElapsed;
        private float _rollRemaining;
        private float _rollSpeed;
        private bool _jumping;
        private bool _subscribed;
        private int _facing = 1;
        private Color _playerColor;
        private Color _bossColor;
        private SpriteRenderer[] _monsterBodies = Array.Empty<SpriteRenderer>();
        private Color[] _monsterColors = Array.Empty<Color>();

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
                _monsterBodies = new SpriteRenderer[_monsters.Length];
                _monsterColors = new Color[_monsters.Length];
                for (int i = 0; i < _monsters.Length; i++)
                {
                    MonsterRuntime monster = _monsters[i];
                    if (monster == null) throw new InvalidOperationException("일반몹 데이터 수신 참조를 연결하세요.");
                    if (!monster.IsInitialized) monster.Initialize(catalog);
                    _monsterBodies[i] = monster.GetComponentInChildren<SpriteRenderer>();
                    if (_monsterBodies[i] == null) throw new InvalidOperationException("일반몹 임시 사각형이 없습니다.");
                    _monsterColors[i] = _monsterBodies[i].color;
                }
                _actor = _player.Actor;
                _input = _player.GetComponent<PlayerLocomotionInput>();
                if (_input == null) throw new InvalidOperationException("플레이어 입력 어댑터가 없습니다.");
                _input.JumpAccepted += OnJump;
                _actor.RollRequested += OnRoll;
                _subscribed = true;
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
            }
            _subscribed = false;
            if (_attackWindow != null) _attackWindow.enabled = false;
        }

        private void OnJump()
        {
            _jumpElapsed = 0f;
            _jumping = true;
        }

        private void OnRoll(int direction, int distance, int duration)
        {
            _rollRemaining = duration / 1000f;
            _rollSpeed = duration > 0 ? direction * distance / 100f * _worldUnitsPerTile / _rollRemaining : 0f;
        }

        private void LateUpdate()
        {
            if (!_subscribed || !_player.isActiveAndEnabled || !_boss.isActiveAndEnabled) return;
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
            MovePreview(Time.deltaTime);
            _attackWindow.transform.localPosition = new Vector3(_facing * 0.95f, 0f, 0.1f);
            _attackWindow.enabled = _actor.IsAttackActive;
            if (_actor.IsAttackActive && _attackWindow.bounds.Intersects(_bossBody.bounds))
                _actor.TryHitBoss(_boss.Actor.Definition.ID, _boss.Actor, false, false);
            for (int i = 0; i < _monsters.Length; i++) DriveMonster(i, Time.deltaTime);
            HandlePreviewKeys();
            RefreshLabels();
        }

        private void MovePreview(float seconds)
        {
            if (_actor.IsDead) { _rollRemaining = 0f; return; }
            Vector3 position = _player.transform.position;
            StateKey state = _actor.CurrentState;
            bool canFinishRoll = state == StateKey.Roll || state == StateKey.Idle ||
                state == StateKey.Walk || state == StateKey.Jump;
            if (_rollRemaining > 0f && canFinishRoll)
            {
                float step = Mathf.Min(seconds, _rollRemaining);
                position.x += _rollSpeed * step;
                _rollRemaining -= step;
            }
            else
            {
                _rollRemaining = 0f;
                position.x += _actor.Locomotion.HorizontalSpeed / 100f * _worldUnitsPerTile * seconds;
            }
            if (_jumping)
            {
                _jumpElapsed += seconds;
                float progress = Mathf.Clamp01(_jumpElapsed / _jumpSeconds);
                float height = _actor.Definition.JumpHeight / 100f * _worldUnitsPerTile;
                position.y = _groundY + 4f * height * progress * (1f - progress);
                if (progress >= 1f)
                {
                    _jumping = false;
                    position.y = _groundY;
                    _player.SetGrounded(true);
                }
            }
            position.x = Mathf.Clamp(position.x, -_stageHalfWidth, _stageHalfWidth);
            _player.transform.position = position;
        }

        // 임시 관측·이동·판정: 중심 거리, 화면 좌우 한계를 벽으로 사용. 회피/치명=false. 운영 물리·히트박스 정책이 아닙니다.
        private void DriveMonster(int index, float seconds)
        {
            MonsterRuntime runtime = _monsters[index];
            MonsterActor monster = runtime.Actor;
            SpriteRenderer body = _monsterBodies[index];
            if (monster == null || !runtime.isActiveAndEnabled) return;
            if (monster.IsDead) { body.color = Color.gray; return; }
            Vector3 position = runtime.transform.position;
            position.x += monster.Velocity / 100f * _worldUnitsPerTile * seconds;
            bool wall = Mathf.Abs(position.x) > _stageHalfWidth;
            position.x = Mathf.Clamp(position.x, -_stageHalfWidth, _stageHalfWidth);
            runtime.transform.position = position;
            if (wall) monster.NotifyWallHit();

            float offset = _player.transform.position.x - position.x;
            int distance = Mathf.RoundToInt(Mathf.Abs(offset) / _worldUnitsPerTile * 100f);
            if (_actor.IsDead) monster.ClearTarget();
            else monster.Observe(_actor.Definition.ID, distance, offset > 0f ? 1 : offset < 0f ? -1 : 0);

            if (monster.IsAttackActive)
            {
                MonsterPattern pattern = monster.CurrentPattern;
                // 추적용 수평 거리와 명중 조건을 구분: 근접·잡기도 사각형 높이가 겹쳐야 맞습니다(공중 회피).
                Bounds own = body.bounds, target = _playerBody.bounds;
                bool sameHeight = target.min.y <= own.max.y && target.max.y >= own.min.y;
                bool inFront = offset * monster.Facing >= 0f;
                bool touching = pattern.IsCharge ? own.Intersects(target) : sameHeight && inFront && distance <= pattern.Range;
                if (touching) monster.TryHitPlayer(_actor.Definition.ID, _actor, false, false);
            }
            // 대상 ID는 보스 정의 ID와 겹치지 않는 장면 내 순번입니다.
            if (_actor.IsAttackActive && _attackWindow.bounds.Intersects(body.bounds))
                _actor.TryHitMonster(index + 1, monster, false, false);
            body.color = monster.IsDead ? Color.gray : monster.CurrentState == StateKey.Stun ? new Color(0.75f, 0.35f, 0.95f) :
                monster.IsAttackActive ? new Color(1f, 0.3f, 0.3f) : _monsterColors[index];
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
            SetText(_playerLabel, "플레이어 " + _actor.Definition.ID + "\n" + GetStateLabel(_actor.CurrentState) +
                "\n체력 " + _actor.Health + "\n스태미나 " + _actor.Stamina.Current +
                (_actor.BoundBy != null ? "\n공격 연타 " + _actor.BindPressesRemaining : ""));
            SetText(_bossLabel, "보스 " + _boss.Actor.Definition.ID + "\n" + GetStateLabel(_boss.Actor.CurrentState) +
                "\n체력 " + _boss.Actor.Health + "\n체세 " + _boss.Actor.Poise);
            _playerBody.color = _actor.IsDead ? Color.gray : _actor.IsInvulnerable ?
                new Color(0.95f, 0.77f, 0.2f) : _actor.IsAttackActive ? new Color(1f, 0.55f, 0.2f) : _playerColor;
            _bossBody.color = _boss.Actor.IsDead ? Color.gray : _boss.Actor.IsExposed ?
                new Color(0.75f, 0.35f, 0.95f) : _boss.Actor.IsInvulnerable ? Color.yellow : _bossColor;
        }

        // 상태 식별자와 동작은 유지하고 화면 표시만 번역합니다.
        internal static string GetStateLabel(StateKey state)
        {
            switch (state)
            {
                case StateKey.Idle: return "대기";
                case StateKey.Walk: return "이동";
                case StateKey.Hit: return "피격";
                case StateKey.Attack1: return "공격 1타";
                case StateKey.Attack2: return "공격 2타";
                case StateKey.Attack3: return "공격 3타";
                case StateKey.Evade: return "회피";
                case StateKey.CritHit: return "치명타 피격";
                case StateKey.Skill1: return "기술 1";
                case StateKey.Hang: return "매달리기";
                case StateKey.Land: return "착지";
                case StateKey.Roll: return "구르기";
                case StateKey.Jump: return "점프";
                case StateKey.Exposed: return "급소 노출";
                case StateKey.Execute: return "해체";
                case StateKey.Dead: return "사망";
                case StateKey.Heal: return "회복";
                case StateKey.Acquire: return "획득";
                case StateKey.Stun: return "기절";
                case StateKey.Skill2: return "기술 2";
                case StateKey.Skill3: return "기술 3";
                case StateKey.Bound: return "구속";
                case StateKey.PhaseChange: return "단계 전환";
                default: return "알 수 없음";
            }
        }

        private static void SetText(TextMesh label, string value)
        {
            if (label.text != value) label.text = value;
        }
    }
}
