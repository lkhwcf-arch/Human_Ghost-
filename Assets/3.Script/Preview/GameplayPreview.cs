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
        [SerializeField, InspectorName("임시 보스 인식 거리 (타일, 0=제한 없음)"), Min(0f)] private float _bossSightTiles = 0f;
        [SerializeField, InspectorName("결박 가르기 해금 상태로 시작")] private bool _skillUnlocked = true;
        [SerializeField, InspectorName("결박 가르기 Skill ID"), Min(1)] private int _skillId = 7001;
        [SerializeField, InspectorName("약초 Buff ID"), Min(1)] private int _herbBuffId = 14001;
        [SerializeField, InspectorName("사망 뒤 재시작 사용")] private bool _restartOnDeath = true;
        [Header("상호작용 (K15)")]
        [SerializeField, InspectorName("상호작용")] private PlayerInteractor _interactor = null;
        [SerializeField, InspectorName("텍스트박스")] private TextboxScreen _textbox = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [SerializeField, InspectorName("해금 맵 ID"), Min(1)] private int _skillUnlockStageId = 8005;
        [Header("피드백·HUD·일시정지 (없으면 끔)")]
        [SerializeField] private GameSpeedController _speed = null;
        [SerializeField] private PreviewFollowCamera _followCamera = null;
        [SerializeField] private GameAudio _audio = null;
        [SerializeField] private HudScreen _hud = null;
        [SerializeField] private PauseMenuHost _pause = null;
        [SerializeField] private PreviewParallax _parallax = null;
        [SerializeField, InspectorName("피드백 글자 프리팹")] private TextMesh _feedbackTextPrefab = null;
        [SerializeField, InspectorName("보스 게이지 표시")] private bool _showBossGauge = true;
        [SerializeField, InspectorName("Stage ID")] private int _stageId = 8001;
        [SerializeField, InspectorName("타이틀 씬")] private string _titleScene = "Title";
        [SerializeField, InspectorName("출구 앵커 이름")] private string _exitAnchor = "ExitRight";
        [Header("맵 이동 (맵이 없는 장면은 끔)")]
        [SerializeField, InspectorName("맵 충돌 사용")] private bool _useMapCollision = false;
        [SerializeField, InspectorName("플레이어 충돌 절반 크기")] private Vector2 _playerHalfSize = new Vector2(0.3f, 0.65f);
        [SerializeField, InspectorName("맵 표식 묶음 (모서리·낙하 구역)")] private Transform _hangEdgeRoot = null;
        [SerializeField, InspectorName("몹·보스 이동 왼쪽 한계")] private float _combatMinX = -9f;
        [SerializeField, InspectorName("몹·보스 이동 오른쪽 한계")] private float _combatMaxX = 9f;
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
        private PreviewDeathRestart _deathRestart;
        private PreviewCombatFeedback _feedback;
        private PreviewStageExit _exit;
        private CombatPresence _combat;
        private string _normalMusic, _battleMusic;
        private int _lastHealth;
        private bool _bossHitThisFrame;
        private const int _combatHoldMilliseconds = 3000;

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
                    _worldUnitsPerTile, _bossRangeTiles, _bossSecondRangeTiles, _bossSightTiles);
                _actor = _player.Actor;
                if (_motion == null) _motion = new PreviewPlayerMotion(_player, _groundY, _useMapCollision, _playerHalfSize, GetMarkers(),
                    _actor.Definition.JumpHeight / 100f * _worldUnitsPerTile, _jumpSeconds);
                _input = _player.GetComponent<PlayerLocomotionInput>();
                if (_input == null) throw new InvalidOperationException("플레이어 입력 어댑터가 없습니다.");
                _input.JumpAccepted += OnJump;
                _input.JumpRejected += _motion.OnJumpRejected;
                _input.InteractPressed += OnInteract;
                _input.VerticalPressed += OnVertical;
                ConnectInteraction(catalog);
                ConnectGameLayer(catalog);
                MoveToArrival();
                _actor.RollRequested += OnRoll;
                _actor.SkillStarted += OnRoll;
                if (_skillUnlocked || PreviewSession.Progress.IsCleared(_skillUnlockStageId)) _player.EnableSkill(catalog.GetTable<Skill>().Get(_skillId));
                _player.EnableHerbs(catalog.GetTable<Buff>().Get(_herbBuffId));
                _deathRestart = _restartOnDeath ? new PreviewDeathRestart(gameObject.scene.name) : null;
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
                if (_input != null)
                {
                    _input.JumpAccepted -= OnJump;
                    _input.JumpRejected -= _motion.OnJumpRejected;
                    _input.InteractPressed -= OnInteract;
                    _input.VerticalPressed -= OnVertical;
                    _input.AdvancePressed -= OnAdvance;
                }
                _actor.RollRequested -= OnRoll;
                _actor.SkillStarted -= OnRoll;
                _actor.AttackWindowChanged -= OnPlayerAttackWindowChanged;
            }
            DisconnectInteraction();
            DisconnectGameLayer();
            _bossDriver?.Dispose();
            _bossDriver = null;
            if (_healthDisplay != null) _healthDisplay.Disconnect();
            DisposeMonsterDrivers();
            _subscribed = false;
            if (_attackWindow != null) _attackWindow.enabled = false;
        }

        private Transform[] GetMarkers()
        {
            if (_hangEdgeRoot == null) return Array.Empty<Transform>();
            var edges = new Transform[_hangEdgeRoot.childCount];
            for (int i = 0; i < edges.Length; i++) edges[i] = _hangEdgeRoot.GetChild(i);
            return edges;
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
                _feedback?.OnPlayerHit(_actor.TryHitBoss(_bossTargetId, _boss.Actor, _player.HitRandom), null, _boss.transform.position);
            foreach (PreviewMonsterDriver monster in _monsterDrivers)
            {
                HitOutcome outcome = monster.CheckPlayerHit();
                if (_feedback != null) _feedback.OnPlayerHit(outcome, monster.Sprite, monster.Position);
            }
        }

        private void OnJump() => _motion.StartJump();

        private void OnInteract() { if (_interactor != null) _interactor.Press(); else _motion.TryGrab(); }

        // 매달린 중 위·아래는 오르기·놓기이고, 그 밖의 위는 상호작용입니다(K03·K15).
        private void OnVertical(int direction)
        {
            if (_motion.IsHanging) _motion.OnVerticalPressed(direction);
            else if (direction > 0) OnInteract();
        }

        private void OnAdvance() { if (_pause == null || !_pause.IsOpen) _textbox.Advance(); }
        private void OnTextboxChanged(bool open) => _input.SetLocked(open);
        private void OnPlayerHealthChanged(int health) { if (_textbox != null) _textbox.Close(); }

        private void ConnectInteraction(IReadOnlyDataCatalog catalog)
        {
            if (_interactor == null || _textbox == null) return;
            _input.AdvancePressed += OnAdvance;
            _textbox.OpenChanged += OnTextboxChanged;
            _actor.HealthChanged += OnPlayerHealthChanged;
            var context = new InteractionContext(_player, _textbox, PreviewSession.Progress, catalog, (scene, anchor) =>
            {
                PreviewSession.ArrivalAnchor = anchor;
                _input.SetLocked(true);
                Cursor.visible = false;
                Instantiate(_loadingPrefab).Begin(scene, catalog);
            });
            _interactor.Connect(context, () => _motion.CanGrab, _motion.TryGrab, CanExecute, TryExecute);
        }

        private void DisconnectInteraction()
        {
            if (_textbox != null) _textbox.OpenChanged -= OnTextboxChanged;
            if (_actor != null) _actor.HealthChanged -= OnPlayerHealthChanged;
            if (_interactor != null) _interactor.Disconnect();
        }

        private bool IsInExecutionRange()
            => Vector2.Distance(_player.transform.position, _boss.transform.position) <= _executionRangeTiles * _worldUnitsPerTile;
        private bool CanExecute() => _boss.Actor.IsExposed && IsInExecutionRange() && _actor.Locomotion.IsGrounded;
        private bool TryExecute()
        {
            if (!_actor.Locomotion.IsGrounded || !_actor.TryExecute(_boss.Actor, IsInExecutionRange())) return false;
            _feedback?.OnExecute();
            return true;
        }

        private void ConnectGameLayer(IReadOnlyDataCatalog catalog)
        {
            GameSettings settings = GameSettingsStore.Load();
            if (_audio != null) _audio.Apply(settings);
            if (_followCamera != null) _followCamera.ShakeEnabled = settings.CameraShake;
            Stage stage = catalog.GetTable<Stage>().Get(_stageId);
            _normalMusic = stage.BGM_Normal; _battleMusic = stage.BGM_Battle;
            if (_audio != null) _audio.PlayMusic(_normalMusic);
            _combat = new CombatPresence(_combatHoldMilliseconds);
            _lastHealth = _actor.Health;
            _feedback = new PreviewCombatFeedback(_speed, _followCamera, _audio, _feedbackTextPrefab, catalog.GetTable<Language>().Get(30021).KO);
            if (_bossDriver != null) _bossDriver.PlayerHit += OnBossHitPlayer;
            if (_hud != null)
            {
                CanvasGroup healthGroup = _healthAnchorRoot != null ? _healthAnchorRoot.GetComponentInParent<Canvas>().GetComponent<CanvasGroup>() : null;
                if (healthGroup == null && _healthAnchorRoot != null) healthGroup = _healthAnchorRoot.GetComponentInParent<Canvas>().gameObject.AddComponent<CanvasGroup>();
                _hud.Connect(_actor, _boss.Actor, catalog.GetTable<Language>().Get(30006).KO, _boss.Actor.Poise, _showBossGauge, healthGroup);
                _actor.StaminaRefused += _hud.FlashStamina;
            }
            _actor.HealthChanged += OnGameLayerHealthChanged;
            if (_parallax != null) _parallax.Connect(catalog);
            if (_pause != null) _pause.Connect(catalog, _speed, _audio, settings, CanPause, _input.SetLocked, ReturnToTitle, OnSettingsChanged);
            if (_textbox != null && _hangEdgeRoot != null && _hangEdgeRoot.Find(_exitAnchor) != null)
                _exit = new PreviewStageExit(_player, _hangEdgeRoot.Find(_exitAnchor), () => PreviewSession.Progress.IsCleared(_skillUnlockStageId), _textbox, "장부방을 살펴보자", "(임시) 다음 맵 미연결");
        }

        private void DisconnectGameLayer()
        {
            if (_pause != null) _pause.Disconnect();
            if (_bossDriver != null) _bossDriver.PlayerHit -= OnBossHitPlayer;
            if (_actor != null)
            {
                _actor.HealthChanged -= OnGameLayerHealthChanged;
                if (_hud != null) _actor.StaminaRefused -= _hud.FlashStamina;
            }
            if (_hud != null) _hud.Disconnect();
        }

        private void OnBossHitPlayer() => _bossHitThisFrame = true;

        private void OnGameLayerHealthChanged(int health)
        {
            if (health >= _lastHealth) { _lastHealth = health; return; }
            _lastHealth = health;
            _combat?.Engage();
            _feedback?.OnPlayerDamaged(_bossHitThisFrame);
            _bossHitThisFrame = false;
            if (_hud != null) _hud.FlashDamage();
        }

        // 사망·암전·해체·결박 가르기 중에는 열지 않습니다(O04·O04_1). 텍스트박스가 열려 있어도 열 수 있습니다.
        private bool CanPause() => !_actor.IsDead && !PreviewScreenFade.IsActive && _actor.CurrentState != StateKey.Execute && _actor.CurrentState != StateKey.Skill1;

        private void ReturnToTitle() => PreviewScreenFade.Run(0.3f, 0.1f, 0.3f, () => UnityEngine.SceneManagement.SceneManager.LoadScene(_titleScene));

        private void OnSettingsChanged(GameSettings value)
        {
            if (_audio != null) _audio.Apply(value);
            if (_followCamera != null) _followCamera.ShakeEnabled = value.CameraShake;
        }

        private void StepGameLayer()
        {
            if (_combat == null) return;
            bool engaged = _bossDriver != null && _bossDriver.IsEngaged;
            foreach (PreviewMonsterDriver monster in _monsterDrivers) engaged |= monster.IsEngaged;
            if (engaged) _combat.Engage();
            _combat.Tick(Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f));
            if (_audio != null) _audio.PlayMusic(_combat.InCombat ? _battleMusic : _normalMusic);
            if (_hud != null)
            {
                _hud.SetBright(_combat.InCombat && (_textbox == null || !_textbox.IsOpen));
                _hud.SetVisible(!PreviewScreenFade.IsActive);
            }
            _exit?.Step();
            _feedback?.Step(Time.unscaledDeltaTime);
        }

        private void MoveToArrival()
        {
            string anchor = PreviewSession.ArrivalAnchor;
            PreviewSession.ArrivalAnchor = null;
            if (string.IsNullOrEmpty(anchor) || _hangEdgeRoot == null) return;
            Vector3 point = new SpawnAnchors(_hangEdgeRoot).Get(anchor).position;
            _player.transform.position = new Vector3(point.x, point.y + _playerHalfSize.y, _player.transform.position.z);
        }

        private void OnRoll(int direction, int distance, int duration)
            => _motion.StartRoll(direction, distance, duration, _worldUnitsPerTile);

        private void LateUpdate()
        {
            if (!_subscribed || !_player.isActiveAndEnabled || !_boss.isActiveAndEnabled) return;
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
            _deathRestart?.Step(_actor, Time.deltaTime);
            // 암전 중 입력은 받지 않습니다(O09).
            if (_input.enabled == PreviewScreenFade.IsActive) _input.enabled = !PreviewScreenFade.IsActive;
            float combatMin = _useMapCollision ? _combatMinX : -_stageHalfWidth;
            float combatMax = _useMapCollision ? _combatMaxX : _stageHalfWidth;
            _motion.Move(Time.deltaTime, _worldUnitsPerTile, _useMapCollision ? float.NegativeInfinity : -_stageHalfWidth,
                _useMapCollision ? float.PositiveInfinity : _stageHalfWidth);
            _attackWindow.enabled = _actor.IsAttackActive;
            // 몬스터 이동 후 플레이어 판정을 확인합니다(기존 순서 유지).
            foreach (PreviewMonsterDriver monster in _monsterDrivers)
                monster.Step(Time.deltaTime, combatMin, combatMax);
            _bossDriver?.Step(Time.deltaTime, combatMin, combatMax);
            CheckPlayerHits();
            HandlePreviewKeys();
            RefreshLabels();
            StepGameLayer();
        }

        private void HandlePreviewKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame)
                _actor.ReceiveHit(CombatMath.Damage(_boss.Actor.Definition, _actor.Definition, 0, false));
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
