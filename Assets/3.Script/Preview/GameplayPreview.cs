using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed partial class GameplayPreview : MonoBehaviour, IModalInputTarget, ISaveGameRuntime
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
        [SerializeField, InspectorName("결박 가르기 Skill ID"), Min(1)] private int _skillId = 7001;
        [SerializeField, InspectorName("약초 Buff ID"), Min(1)] private int _herbBuffId = 14001;
        [SerializeField, InspectorName("사망 뒤 재시작 사용")] private bool _restartOnDeath = true;
        [Header("상호작용 (K15)")]
        [SerializeField, InspectorName("상호작용")] private PlayerInteractor _interactor = null;
        [SerializeField, InspectorName("텍스트박스")] private TextboxScreen _textbox = null;
        [SerializeField, InspectorName("피난민 대사")] private NpcDialogue _refugeeDialogue = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [Header("피드백·HUD·일시정지 (없으면 끔)")]
        [SerializeField] private GameSpeedController _speed = null;
        [SerializeField] private PreviewFollowCamera _followCamera = null;
        [SerializeField] private GameAudio _audio = null;
        [SerializeField] private HudScreen _hud = null;
        [SerializeField] private PauseMenuHost _pause = null;
        [SerializeField] private PreviewParallax _parallax = null;
        [SerializeField, InspectorName("피드백 글자 프리팹")] private TextMesh _feedbackTextPrefab = null;
        [SerializeField, InspectorName("보스 게이지 표시")] private bool _showBossGauge = true;
        [SerializeField, InspectorName("Stage ID (리소스 목록이 없을 때)")] private int _stageId = 8001;
        [Header("JSON 스테이지 조립 (없으면 씬 배치 사용)")]
        [SerializeField, InspectorName("스테이지 리소스 목록")] private StageContentSet _content = null;
        [SerializeField, InspectorName("맵 기준 위치")] private Vector3 _mapOrigin = new Vector3(-10f, -2.2f, 0f);
        [SerializeField, InspectorName("타이틀 씬")] private string _titleScene = "Title";
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
        private PreviewParryFeedback _parryFeedback;
        [SerializeField] private SpriteClipEffect _parryImpactPrefab = null;
        [SerializeField] private SpriteClipEffect _hitImpactPrefab = null;
        private PlayerAnimationAudio _playerAudio;
        private readonly PreviewPresentation _presentation = new PreviewPresentation();
        private SubscriptionScope _interactionScope;
        private PreviewStageExit _exit;
        private PreviewStage _stage;
        private PreviewBindGates _gates;
        private Transform _markersRoot;
        private Transform _savePoint;
        private bool _bossActive = true;
        private IReadOnlyDataCatalog _catalog;
        private CombatPresence _combat;
        private string _normalMusic, _battleMusic;
        private int _lastHealth;
        private bool _bossHitThisFrame;
        private const int _combatHoldMilliseconds = 3000;
        private PreviewPlayerSprite _playerSprite;
        private SpatialRuntime _executingRuntime;
        private float _executionRemaining;

        private bool _modalBlocked;
        private bool _stageNamePending;

        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            BlockShrine(blocked);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_manualSaveHost != null) _manualSaveHost.SetModalBlocked(blocked);
#endif
            _motion?.DiscardBufferedInput();
            _input?.SetModalBlocked(blocked);
            if (_pause != null) _pause.SetModalBlocked(blocked);
            RefreshDialogueSkipHint();
        }

        public bool IsReady => _subscribed;
        private void OnParrySucceeded(int milliseconds) => _parryFeedback?.Show(milliseconds, _player.transform.position + Vector3.right * _actor.Facing * 0.6f);
        public PlayerRuntime Player => _player;
        public BossRuntime Boss => _boss;

        private void OnEnable()
        {
            // 판정 범위는 유지하고 테스트용 색상 표시만 빌드 종류에 따라 차단합니다.
            if (_attackWindow != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _attackWindow.forceRenderingOff = false;
#else
                _attackWindow.forceRenderingOff = true;
#endif
            }
            _input = _player != null ? _player.GetComponent<PlayerLocomotionInput>() : null;
            _input?.SetLocked(true);
            if (_data == null || _player == null || _boss == null || _playerLabel == null ||
                _bossLabel == null || _playerBody == null || _bossBody == null || _attackWindow == null || _input == null)
            {
                Debug.LogError("임시 확인 장면의 데이터·프리팹·표시 참조를 연결하세요.", this);
                enabled = false;
                return;
            }
            _data.Initialized += OnDataReady;
            if (_data.Data != null) OnDataReady(_data.Data);
        }

        private GameProgressState _progress;

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
                _player.ConfigureSpatialScale(_worldUnitsPerTile);
                _boss.ConfigureSpatialScale(_worldUnitsPerTile);
                _leaving = false;
                _catalog = catalog;
                _progress = GameSaveContext.Current?.Progress ?? new GameProgressState();
                ImportEntryProgress();
                BuildStage(catalog);
                if (_boss.Actor == null) _boss.Initialize(catalog);
                DisposeMonsterDrivers();
                _monsterDrivers = new PreviewMonsterDriver[_monsters.Length];
                for (int i = 0; i < _monsters.Length; i++)
                {
                    MonsterRuntime monster = _monsters[i];
                    if (monster == null) throw new InvalidOperationException("일반몹 데이터 수신 참조를 연결하세요.");
                    if (!monster.IsInitialized) monster.Initialize(catalog);
                    _monsterDrivers[i] = new PreviewMonsterDriver(monster, _player, _playerBody, _attackWindow,
                        _firstMonsterTargetId + i, _playerTargetId, _worldUnitsPerTile, ApplyMonsterHitEffects);
                }
                _bossDriver?.Dispose();
                _bossDriver = !_bossActive || _boss.Combat == null ? null : new PreviewBossDriver(_boss, _player, _bossBody, _playerBody,
                    _worldUnitsPerTile, _bossRangeTiles, _bossSecondRangeTiles, _bossSightTiles);
                _actor = _player.Actor;
                _actor.ApplyLevel(_progress.Rewards.CurrentLevel);
                _actor.ParrySucceeded += OnParrySucceeded;
                if (_motion == null) _motion = new PreviewPlayerMotion(_player, _groundY, _useMapCollision, _playerHalfSize, GetMarkers(),
                    _actor.Definition.JumpHeight, Mathf.RoundToInt(_jumpSeconds * 1000f),
                    key => _playerAudio?.PlayAnimationSound(key), _stageId == BossStage ? _stage?.MapRoot : null);
                _input.TryDropThrough = _motion.TryDropThrough;
                _input.SetModalBlocked(_modalBlocked);
                _input.JumpAccepted += OnJump;
                _input.JumpRejected += _motion.OnJumpRejected;
                _input.InteractPressed += OnInteract;
                _input.VerticalPressed += OnVertical;
                ConnectInteraction(catalog);
                MoveToArrival();
                // 걷기 표시는 장부방만, 나머지 전투 스테이지는 달리기 표시입니다.
                _playerSprite = _player.GetComponentInChildren<PreviewPlayerSprite>(true);
                _playerSprite?.SetNonCombatArea(_stageId == StageExitRules.LedgerStage);
                ConnectCutsceneData();
                ConnectBossPhaseData();
                ConnectStageObjects(catalog);
                ConnectGalimgilArrivals();
                if (_savePoint != null) _savePoint.GetComponent<ShrineInteract>().SetActive(_progress.HasActiveShrine(_stageId));
                ConnectGameLayer(catalog);
                _actor.RollRequested += OnRoll;
                _actor.SkillStarted += OnRoll;
                _player.EnableSkill(catalog.GetTable<Skill>().Get(_skillId), new ContentAccess(_progress.Progress.IsCleared));
                _player.EnableHerbs(catalog.GetTable<Buff>().Get(_herbBuffId));
                _actor.ApplyUpgrades(_progress.Upgrades);
                RestoreCarriedVitals();
                if (_audio != null)
                {
                    _playerAudio = _player.GetComponent<PlayerAnimationAudio>();
                    if (_playerAudio == null) _playerAudio = _player.gameObject.AddComponent<PlayerAnimationAudio>();
                    _playerAudio.Connect(_player, key => { if (_audio != null) _audio.PlayEffect(key); });
                    foreach (MonsterRuntime monster in _monsters)
                        monster.GetComponent<MonsterAnimationAudio>()?.Connect(monster, _audio.PlayEffect);
                }
                _deathRestart = _restartOnDeath ? new PreviewDeathRestart(gameObject.scene.name, _progress, () => _saveSession?.CurrentSnapshot) : null;
                // 판정 창이 한 프레임 안에 열리고 닫혀도 열리는 순간 한 번은 접촉을 확인합니다.
                _actor.AttackWindowChanged += OnPlayerAttackWindowChanged;
                _subscribed = true;
                if (_healthDisplay != null) _healthDisplay.Connect(catalog, _healthPrefabs, _actor, _healthAnchorRoot);
                _playerColor = _playerBody.color;
                _bossColor = _bossBody.color;
                RefreshLabels();
                SetPlayable(false);
                ConnectProgress();
                _boss.gameObject.SetActive(_bossActive);
                ConnectFields(catalog);
                _stage?.Activate();
                ApplyWalkableSpans();
                ConnectSaves();
                _stageNamePending = _textbox != null;
            }
            catch (Exception error)
            {
                GameSaveContext.Current?.CompleteScene(false);
                Debug.LogException(error, this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            try { DisconnectFields(); }
            catch (Exception error) { Debug.LogException(error, this); }
            _stageNamePending = false;
#if UNITY_EDITOR
            ClearEditorHitDummy();
            ClearEditorExecutionDummies();
#endif
            if (_actor != null) _actor.ParrySucceeded -= OnParrySucceeded;
            if (_playerAudio != null) _playerAudio.Disconnect();
            _playerAudio = null;
            foreach (MonsterRuntime monster in _monsters)
                if (monster != null) monster.GetComponent<MonsterAnimationAudio>()?.Disconnect();
            DisconnectBossPhase();
            DisconnectCutscenes();
            _deathRestart?.Dispose(); _deathRestart = null;
            _presentation.Reset();
            _feedback?.Dispose();
            _feedback = null;
            _parryFeedback?.Dispose(); _parryFeedback = null;
            DisconnectSaves();
            if (_data != null) _data.Initialized -= OnDataReady;
            // 초기화 도중 실패해도 이미 연결한 소비자는 반드시 해제합니다.
            if (_input != null)
            {
                _motion?.StopClimbing();
                _input.TryDropThrough = null;
                _input.JumpAccepted -= OnJump;
                if (_motion != null) _input.JumpRejected -= _motion.OnJumpRejected;
                _input.InteractPressed -= OnInteract;
                _input.VerticalPressed -= OnVertical;
            }
            if (_actor != null)
            {
                _actor.RollRequested -= OnRoll;
                _actor.SkillStarted -= OnRoll;
                _actor.AttackWindowChanged -= OnPlayerAttackWindowChanged;
            }
            DisconnectInteraction();
            _actor?.CancelExecution(); _executingRuntime = null;
            DisconnectGameLayer();
            _bossDriver?.Dispose();
            _bossDriver = null;
            if (_healthDisplay != null) _healthDisplay.Disconnect();
            DisposeMonsterDrivers();
            _subscribed = false;
            if (_actor != null && _actor.IsHanging) _actor.EndHang();
            _motion = null;
            _gates?.Dispose(); _gates = null;
            _stage?.Dispose(); _stage = null;
            _savePoint = null;
            _exit = null;
            if (_attackWindow != null) _attackWindow.enabled = false;
            _input?.SetLocked(true);
        }

        private Transform[] GetMarkers()
        {
            if (_markersRoot == null) return Array.Empty<Transform>();
            var edges = new Transform[_markersRoot.childCount];
            for (int i = 0; i < edges.Length; i++) edges[i] = _markersRoot.GetChild(i);
            return edges;
        }

        private void DisposeMonsterDrivers()
        {
            foreach (PreviewMonsterDriver driver in _monsterDrivers) driver?.Dispose();
            _monsterDrivers = Array.Empty<PreviewMonsterDriver>();
        }

        private void OnPlayerAttackWindowChanged(bool active)
        {
            if (active && _player.isActiveAndEnabled) CheckPlayerHits();
        }

        // 테스트 전용 공격 판정 배율입니다(길이·높이, 기본 1, Play마다 1). 플레이어 쪽 끝은 고정하고 길이는 앞으로만 늘립니다.
        public const float MinAttackScale = 0.25f, MaxAttackScale = 3f;
        public static float AttackLengthScale { get; private set; } = 1f;
        public static float AttackHeightScale { get; private set; } = 1f;
        private Vector3 _attackBaseScale;
        private bool _attackBaseKnown;
        // 기존 기준: 중심 0.95, 기본 길이의 절반만큼 플레이어 쪽 끝.
        private float AttackNearEdge => 0.95f - _attackBaseScale.x * 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAttackScale() => AttackLengthScale = AttackHeightScale = 1f;

        public static void SetTestAttackScale(float length, float height)
        {
            AttackLengthScale = Mathf.Clamp(length, MinAttackScale, MaxAttackScale);
            AttackHeightScale = Mathf.Clamp(height, MinAttackScale, MaxAttackScale);
        }

        // 월드 기준 현재 판정 크기입니다(테스트 창 표시용).
        public Vector2 AttackWindowWorldSize => _attackWindow != null && _attackWindow.sprite != null
            ? Vector2.Scale(_attackWindow.sprite.bounds.size, _attackWindow.transform.lossyScale) : Vector2.zero;

        private void ApplyAttackWindowSize()
        {
            if (!_attackBaseKnown) { _attackBaseScale = _attackWindow.transform.localScale; _attackBaseKnown = true; }
            _attackWindow.transform.localScale = new Vector3(_attackBaseScale.x * AttackLengthScale, _attackBaseScale.y * AttackHeightScale, _attackBaseScale.z);
        }

        // 임시 판정: 공격 범위 사각형과 보스/일반몹 사각형 겹침. 창 열림 순간과 매 프레임 호출하며 중복은 창 등록이 막습니다.
        private void CheckPlayerHits()
        {
            _facing = _actor.Facing;
            ApplyAttackWindowSize();
            _attackWindow.transform.localPosition = new Vector3(_facing * (AttackNearEdge + _attackWindow.transform.localScale.x * 0.5f), 0f, 0.1f);
#if UNITY_EDITOR
            CheckEditorHitDummy();
#endif
            if (!_actor.IsAttackActive) return;
            if (_bossActive && _attackWindow.bounds.Intersects(_bossBody.bounds))
                _feedback?.OnPlayerHit(_actor.TryHitBoss(_bossTargetId, _boss.Actor, _player.HitRandom), null, _boss.transform.position, _actor.Facing);
            foreach (PreviewMonsterDriver monster in _monsterDrivers)
            {
                HitOutcome outcome = monster.CheckPlayerHit();
                if (_feedback != null) _feedback.OnPlayerHit(outcome, monster.Sprite, monster.Position, _actor.Facing);
            }
        }

        private void OnJump() => _motion.StartJump();

        private void OnInteract() { if (_interactor != null) _interactor.Press(); else _motion.TryGrab(); }

        // 매달린 중 위·아래는 오르기·놓기이고, 그 밖의 위는 상호작용입니다(K03·K15).
        private void OnVertical(int direction)
        {
            if (_motion.TryUseLadder(direction)) return;
            if (_motion.IsHanging) _motion.OnVerticalPressed(direction);
            else if (direction > 0) OnInteract();
        }

        private void OnAdvance() { if (!_leaving && !LoadingScreen.IsActive && (_pause == null || !_pause.IsOpen)) _textbox.Advance(); }
        private void OnTextboxChanged(bool open) => SetGameInputLocked(open);

        private bool _loadingLocked;
        private void RefreshDialogueSkipHint()
        {
            if (_textbox != null) _textbox.BlockSkipHint(_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive ||
                _actor == null || _actor.IsDead || (_pause != null && _pause.IsOpen));
        }
        private void SetGameInputLocked(bool locked)
        {
            _loadingLocked = LoadingScreen.IsActive;
            _motion?.DiscardBufferedInput();
            _input.DiscardBufferedInput();
            _input.SetLocked(locked || _bossPhaseRunning || _loadingLocked || _saveLocked || _leaving || _actor.IsDead || (_textbox != null && _textbox.IsOpen) || (_pause != null && _pause.IsOpen));
            _fields?.SetReady(!_loadingLocked && !_saveLocked && !_leaving);
            RefreshDialogueSkipHint();
        }
        private void OnPlayerHealthChanged(int health) { if (_textbox != null) _textbox.Close(); }

        private void ConnectInteraction(IReadOnlyDataCatalog catalog)
        {
            if (_interactor == null || _textbox == null) return;
            DisconnectInteraction();
            _interactionScope = new SubscriptionScope();
            PlayerLocomotionInput input = _input;
            TextboxScreen textbox = _textbox;
            PlayerActor actor = _actor;
            SubscriptionScope scope = _interactionScope;
            textbox.ConnectSkipHint(new LanguageTextSource(catalog), LanguageSettings.Current);
            scope.Add(textbox.DisconnectSkipHint);
            RefreshDialogueSkipHint();
            var skip = new SkipKeyInput(
                () => !scope.IsDisposed && !_modalBlocked && !_saveLocked && !_leaving && !LoadingScreen.IsActive &&
                    (_pause == null || !_pause.IsOpen) && textbox != null && textbox.CanSkip(SkipScope.Dialogue),
                () => textbox.TrySkip(SkipScope.Dialogue));
            scope.Add(skip.Dispose);
            Action advance = scope.Guard(OnAdvance);
            Action<bool> openChanged = scope.Guard<bool>(OnTextboxChanged);
            Action<int> playerHealthChanged = scope.Guard<int>(OnPlayerHealthChanged);
            Action refresh = scope.Guard(RequestInteractionRefresh);
            Action<StateKey> stateChanged = scope.Guard<StateKey>(OnInteractionStateChanged);
            Action<int> monsterHealthChanged = scope.Guard<int>(OnInteractionHealthChanged);
            scope.Add(() => input.AdvancePressed -= advance); input.AdvancePressed += advance;
            scope.Add(() => textbox.OpenChanged -= openChanged); textbox.OpenChanged += openChanged;
            scope.Add(() => actor.HealthChanged -= playerHealthChanged); actor.HealthChanged += playerHealthChanged;
            var context = new InteractionContext(_player, _textbox, _progress.Progress, catalog, GoToStage);
            _interactor.Connect(context, () => _motion.CanGrab, _motion.TryGrab, CanExecute, TryExecute);
            if (_bossActive)
            {
                BossRuntime boss = _boss;
                scope.Add(() => boss.PositionChanged -= refresh); boss.PositionChanged += refresh;
                scope.Add(() => boss.Actor.States.Changed -= stateChanged); boss.Actor.States.Changed += stateChanged;
            }
            foreach (MonsterRuntime runtime in _monsters)
            {
                MonsterActor monster = runtime.Actor;
                scope.Add(() => monster.HealthChanged -= monsterHealthChanged); monster.HealthChanged += monsterHealthChanged;
                scope.Add(() => monster.States.Changed -= stateChanged); monster.States.Changed += stateChanged;
                scope.Add(() => runtime.SpatialChanged -= refresh); runtime.SpatialChanged += refresh;
            }
        }
        private void RequestInteractionRefresh() { if (_interactor != null) _interactor.RequestRefresh(); }
        private void OnInteractionStateChanged(StateKey state) => RequestInteractionRefresh();
        private void OnInteractionHealthChanged(int health) => RequestInteractionRefresh();

        private bool _leaving;

        private void GoToStage(int stageId, string anchor)
        {
            if (_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive || _loadingPrefab == null) return;
            var access = new ContentAccess(_progress.Progress.IsCleared);
            if (access.Evaluate(_catalog.GetTable<Stage>().Get(stageId)) != ContentAccessResult.Allowed) return;
            // 로딩 준비 실패 시 기존 임시 상태와 입력을 먼저 망가뜨리지 않습니다.
            try { Instantiate(_loadingPrefab).Begin(gameObject.scene.name, _catalog, GameSaveContext.Current?.BeginScene()); }
            catch (Exception error) { Debug.LogException(error, this); return; }
            _leaving = true;
            _progress.Travel(stageId, anchor, _actor.Health, _actor.HerbCount);
            SetGameInputLocked(true);
            Cursor.visible = false;
        }

        // 맵을 옮겨도 HP·약초는 회복되지 않습니다. 표시 연결 전이라 피격 연출도 나가지 않게 기준값을 맞춥니다.
        private void RestoreCarriedVitals()
        {
            if (_progress.CarriedHealth is int health)
            {
                RestorePlayerVitals(health, _progress.CarriedHerbs);
            }
            _progress.ClearCarriedVitals();
        }

        // 복원을 피해로 표시하지 않도록 표시 기준을 먼저 맞춥니다.
        private void RestorePlayerVitals(int health, int herbs)
        {
            _lastHealth = health;
            _actor.RestoreVitals(health, herbs);
        }

        // 리소스 목록이 있으면 현재 스테이지의 맵·플랫폼·몬스터를 JSON으로 조립합니다.
        private void BuildStage(IReadOnlyDataCatalog catalog)
        {
            _markersRoot = _hangEdgeRoot;
            if (_content == null) return;
            _stageId = _progress.CurrentStage;
            _stage = new PreviewStage(_content, _stageId, catalog, _mapOrigin, _progress);
            _markersRoot = _stage.Markers;
            _monsters = _stage.Monsters.ToArray();
            _showBossGauge = _stageId == BossStage;
            _bossActive = _stageId == BossStage && !_progress.HasDefeatedBoss(BossStage);
            if (_bossActive)
            {
                Transform spawn = new SpawnAnchors(_markersRoot).Get("Boss_Spawn");
                float half = _bossBody.transform.lossyScale.y * 0.5f;
                _boss.SetPosition(new Vector3(spawn.position.x, spawn.position.y + half, _boss.transform.position.z));
            }
            _boss.gameObject.SetActive(false);
            if (_interactor != null) _interactor.SetTargetRoot(_stage.Interactables);
            // 몬스터 걷는 구간이 관문에서 끊기도록 먼저 만듭니다.
            _gates = new PreviewBindGates(_markersRoot, _stageId, catalog, _progress);
        }

        private const int BossStage = 8004;
        private const string LedgerArrival = "Spawn_FromDohwagol";

        // 각 몬스터가 선 바닥의 끊기지 않은 구간을 찾습니다.
        private void ApplyWalkableSpans()
        {
            if (_stage == null) return;
            // 몬스터마다 수백 번 검사하므로 결과 목록 하나를 준비 범위 안에서 재사용합니다.
            var hits = new List<Collider2D>();
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
            for (int i = 0; i < _monsters.Length; i++)
            {
                SpriteRenderer body = _monsters[i].GetComponentInChildren<SpriteRenderer>(true);
                float half = body.transform.lossyScale.y * 0.5f;
                Vector2 feet = (Vector2)_monsters[i].transform.position + Vector2.down * half;
                _monsterDrivers[i].UseWalkableSpan(FindSpanEnd(feet, half, -1, filter, hits), FindSpanEnd(feet, half, 1, filter, hits));
            }
        }

        private static float FindSpanEnd(Vector2 feet, float halfHeight, int direction, ContactFilter2D filter, List<Collider2D> hits)
        {
            const float step = 0.25f;
            float x = feet.x;
            for (int i = 0; i < 800; i++)
            {
                float next = x + direction * step;
                // 앞이 막히거나 발밑이 비면 그 앞에서 멈춥니다.
                if (IsSolid(new Vector2(next, feet.y + halfHeight), filter, hits) || !IsSolid(new Vector2(next, feet.y - 0.1f), filter, hits)) break;
                x = next;
            }
            return x;
        }

        private static bool IsSolid(Vector2 point, ContactFilter2D filter, List<Collider2D> hits)
        {
            hits.Clear();
            Physics2D.OverlapPoint(point, filter, hits);
            foreach (Collider2D hit in hits)
                if (!hit.isTrigger && hit.GetComponentInParent<MonsterRuntime>() == null && hit.GetComponentInParent<PlayerRuntime>() == null) return true;
            return false;
        }

        // 맵 표식에 상호작용·관문·출구를 붙입니다. 표식 이름은 맵 프리팹 기준입니다.
        private void ConnectStageObjects(IReadOnlyDataCatalog catalog)
        {
            if (_stage == null) return;
            foreach (Transform marker in _markersRoot)
            {
                if (marker.name == "NPC_Refugee")
                {
                    if (_refugeeDialogue == null) throw new InvalidOperationException("피난민 대사 에셋을 연결해 주세요.");
                    AddTarget<NpcTalk>(marker).Configure(_refugeeDialogue.Speaker, _refugeeDialogue.Lines, _refugeeDialogue.Objective);
                }
                else if (marker.name == "Door_Jangbubang")
                    AddTarget<StageDoor>(marker).Configure(StageExitRules.LedgerStage, LedgerArrival, null);
                else if (marker.name == "Door_ToDohwagol")
                    AddTarget<StageDoor>(marker).Configure(StageExitRules.VillageStage, "Spawn_FromJangbubang", null);
                else if (marker.name == "Myeongbu")
                    AddTarget<LedgerInteract>(marker).Configure("(임시) 이미 살펴본 명부다.",
                        _ledgerCutsceneId == 0 ? null : PlayLedger);
                else if (marker.name == "Seonangdang")
                {
                    ShrineInteract shrine = AddTarget<ShrineInteract>(marker);
                    shrine.Connect(OpenShrine);
                    _savePoint = shrine.transform;
                }
                else if (marker.name == "Exit_ToBossRoom")
                    AddTarget<StageDoor>(marker).Configure(BossStage, "Spawn_FromHyeopgok", CanLeaveStage);
            }
            if (_interactor != null) _interactor.SetTargetRoot(_stage.Interactables);
            Stage stage = catalog.GetTable<Stage>().Get(_stageId);
            int previous = 0;
            foreach (Stage other in catalog.GetTable<Stage>().All)
                if (other.NextStage == _stageId && other.ID != StageExitRules.LedgerStage) previous = other.ID;
            // 맵 3은 문으로만, 보스방은 되돌아갈 수 없습니다(L01).
            int next = _stageId == 8003 ? 0 : stage.NextStage ?? 0;
            // K14: ContentEnabled가 N인 맵은 건너뜁니다.
            while (next != 0 && !catalog.GetTable<Stage>().Get(next).ContentEnabled) next = catalog.GetTable<Stage>().Get(next).NextStage ?? 0;
            if (_stageId == BossStage) previous = 0;
            if (_textbox != null)
                _exit = new PreviewStageExit(_player, _stage.Area, next, previous, StageName(_stageId), CanLeaveStage, GoToStage, _textbox,
                    _stageId == StageExitRules.VillageStage ? "장부방을 살펴보자" : "남은 요괴가 있다");
            if (_followCamera != null)
            {
                // 2026-11-11 사용자 확정: 보스방도 필드와 같은 확대 추적 구도를 씁니다(기존 L09_1 고정 구도 대체).
                _followCamera.SetArea(_stage.Area.min.x + 1f, _stage.Area.max.x - 1f, _stage.MapRoot.position.y + 4f, _stage.CameraSize);
            }
        }

        private T AddTarget<T>(Transform marker) where T : Component
        {
            var holder = new GameObject(marker.name + "_Target");
            holder.transform.SetParent(_stage.Interactables, false);
            holder.transform.position = marker.position + Vector3.up * _playerHalfSize.y;
            return holder.AddComponent<T>();
        }

        private int RemainingMonsters()
        {
            int remaining = 0;
            foreach (PreviewMonsterDriver monster in _monsterDrivers) if (!monster.IsDead) remaining++;
            return remaining;
        }

        private bool CanLeaveStage() => StageExitRules.CanLeave(_stageId,
            _progress.Progress.IsCleared(StageExitRules.LedgerStage));

        // 잡몹을 다 잡은 맵을 기록합니다. 맵 1은 명부, 보스방은 보스 처치가 기준이라 제외합니다.
        // 처치한 보스는 사망 재시작·재진입 때 다시 나오지 않습니다(K16add).
        private void StepStageClear()
        {
            if (_bossActive && _boss.Actor.IsDead) _progress.MarkBossDefeated(BossStage);
            TryStartEnding();
            if (_stage != null && _stageId != StageExitRules.VillageStage && _stageId != StageExitRules.LedgerStage && _stageId != BossStage && RemainingMonsters() == 0)
                _progress.Progress.Clear(_stageId);
        }

        private static string StageName(int stageId)
        {
            switch (stageId)
            {
                case 8001: return "Dohwagol";
                case 8002: return "Sangil";
                case 8003: return "Hyeopgok";
                case 8004: return "BossRoom";
                default: return "Jangbubang";
            }
        }

        // 위치 변경 때만 접촉을 확인합니다. 재접촉 자동 저장과 명시적인 휴식 요청은 구분합니다.
        private void StepSavePoint()
        {
            if (!_shrineCheckPending || _savePoint == null || _actor.IsDead || _saveFlow == null || !_saveFlow.IsPlayable || _saveFlow.IsBusy) return;
            _shrineCheckPending = false;
            SaveSnapshot saved = GameSaveContext.Current?.Session.CurrentSnapshot;
            if (saved != null && saved.UsedSavePoints.Contains(_stageId)) return;
            float radius = _worldUnitsPerTile;
            if (((Vector2)_player.transform.position - (Vector2)_savePoint.position).sqrMagnitude > radius * radius) return;
            SaveAtPoint(false);
        }

        private void DisconnectInteraction()
        {
            _interactionScope?.Dispose(); _interactionScope = null;
            if (_interactor != null) _interactor.Disconnect();
        }

        private IExecutionTarget FindExecutionTarget(out SpatialRuntime runtime)
        {
            runtime = null;
            if (_actor == null || !_actor.Locomotion.IsGrounded || !_actor.Locomotion.CanMove) return null;
            IExecutionTarget target = null;
            float nearest = float.PositiveInfinity;
            if (_bossActive) ConsiderExecution(_boss, _boss.Actor, ref runtime, ref target, ref nearest);
            foreach (MonsterRuntime monster in _monsters)
                ConsiderExecution(monster, monster.Actor, ref runtime, ref target, ref nearest);
#if UNITY_EDITOR
            foreach (MonsterRuntime dummy in _editorExecutionDummies)
                if (dummy != null) ConsiderExecution(dummy, dummy.Actor, ref runtime, ref target, ref nearest);
#endif
            return target;
        }

        private void ConsiderExecution(SpatialRuntime candidate, IExecutionTarget actor, ref SpatialRuntime runtime,
            ref IExecutionTarget target, ref float nearest)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || actor == null || !actor.IsExposed) return;
            Vector2 offset = candidate.transform.position - _player.transform.position;
            float distance = offset.sqrMagnitude;
            float range = _executionRangeTiles * _worldUnitsPerTile;
            if (offset.x * _actor.Facing < 0f || distance > range * range || distance >= nearest) return;
            SpriteRenderer body = candidate.GetComponentInChildren<SpriteRenderer>(true);
            if (body == null || body.bounds.min.y > _playerBody.bounds.max.y || body.bounds.max.y < _playerBody.bounds.min.y) return;
            runtime = candidate; target = actor; nearest = distance;
        }

        private bool CanExecute() => FindExecutionTarget(out _) != null;
        private bool TryExecute()
        {
            IExecutionTarget target = FindExecutionTarget(out SpatialRuntime runtime);
            if (target == null) return false;
            var victimSprite = runtime is MonsterRuntime ? runtime.GetComponentInChildren<PreviewMonsterSprite>(true) : null;
            _playerSprite?.SetExecutionAnimation(victimSprite?.PlayerExecution);
            float duration = _playerSprite != null ? _playerSprite.ExecutionDuration : 0f;
            if (duration <= 0f) throw new InvalidOperationException("플레이어 해체 애니메이션을 연결해 주세요.");
            if (runtime is MonsterRuntime)
            {
                float victimDuration = victimSprite?.ExecutionDuration ?? 0f;
                if (victimDuration <= 0f) throw new InvalidOperationException("일반몹 해체 애니메이션을 연결해 주세요.");
                duration = Mathf.Max(duration, victimDuration);
            }
            if (!_actor.TryExecute(target, true)) return false;
            _executingRuntime = runtime;
            _executionRemaining = duration;
            _feedback?.OnExecute();
            return true;
        }

        private void ConnectGameLayer(IReadOnlyDataCatalog catalog)
        {
            GameSettings settings = GameSettingsStore.Load();
            if (_audio != null) _audio.ConnectTable(catalog);
            if (_audio != null) _audio.Apply(settings);
            if (_followCamera != null) _followCamera.ShakeEnabled = settings.CameraShake;
            // 사용자 임시 요청: 인게임은 전투 여부와 관계없이 불 환경음만 반복합니다.
            _normalMusic = _battleMusic = "AMBIANCE_FIRE";
            if (_audio != null) _audio.PlayMusic(_normalMusic);
            _combat = new CombatPresence(_combatHoldMilliseconds);
            _lastHealth = _actor.Health;
            _feedback?.Dispose();
            _feedback = new PreviewCombatFeedback(_speed, _followCamera, _audio, _feedbackTextPrefab, LanguageTextSource.Select(catalog.GetTable<Language>().Get(30021)), _hitImpactPrefab);
            _parryFeedback?.Dispose();
            _parryFeedback = new PreviewParryFeedback(_speed, _followCamera, _audio, _parryImpactPrefab);
            if (_bossDriver != null)
            {
                _bossDriver.PlayerHit += OnBossHitPlayer;
                _bossDriver.HitResolving += OnBossHitResolving;
                _bossDriver.PlayerDodged += OnPlayerDodged;
            }
            foreach (PreviewMonsterDriver driver in _monsterDrivers) driver.PlayerDodged += OnPlayerDodged;
            if (_hud != null)
            {
                CanvasGroup healthGroup = null;
                if (_healthAnchorRoot != null)
                {
                    Canvas canvas = _healthAnchorRoot.GetComponentInParent<Canvas>();
                    if (canvas == null) throw new InvalidOperationException("체력 표시 앵커의 부모 Canvas가 없습니다.");
                    healthGroup = canvas.GetComponent<CanvasGroup>();
                    if (healthGroup == null) healthGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                }
                _hud.Connect(_actor, _boss.Actor, LanguageTextSource.Select(catalog.GetTable<Language>().Get(30006)), _boss.Actor.Poise, _showBossGauge && _bossActive, healthGroup);
                _hud.SetSkillName(LanguageTextSource.Select(catalog.GetTable<Language>().Get(catalog.GetTable<Skill>().Get(_skillId).TextID)));
                _actor.StaminaRefused += _hud.FlashStamina;
            }
            _actor.HealthChanged += OnGameLayerHealthChanged;
            if (_parallax != null && _stage != null) _parallax.Connect(catalog, _stageId, _stage.MapRoot);
            if (_pause != null) _pause.Connect(catalog, _speed, _audio, settings, CanPause, SetGameInputLocked, ReturnToTitle, OnSettingsChanged);
        }

        private void DisconnectGameLayer()
        {
            if (_parallax != null) _parallax.Disconnect();
            if (_pause != null) _pause.Disconnect();
            if (_bossDriver != null)
            {
                _bossDriver.PlayerHit -= OnBossHitPlayer;
                _bossDriver.HitResolving -= OnBossHitResolving;
                _bossDriver.PlayerDodged -= OnPlayerDodged;
            }
            foreach (PreviewMonsterDriver driver in _monsterDrivers)
                if (driver != null) driver.PlayerDodged -= OnPlayerDodged;
            if (_actor != null)
            {
                _actor.HealthChanged -= OnGameLayerHealthChanged;
                if (_hud != null) _actor.StaminaRefused -= _hud.FlashStamina;
            }
            if (_hud != null) _hud.Disconnect();
        }

        private void OnBossHitResolving(bool active) => _bossHitThisFrame = active;
        private void OnBossHitPlayer()
        {
            var combat = _boss.Combat; var reaction = combat.CurrentReaction;
            if (reaction == null || reaction.KnockbackDistance == 0) return;
            int direction = combat.CurrentPattern.Kind == BossPatternKind.CrawlCharge ? combat.Facing : Math.Sign(_player.transform.position.x - _boss.transform.position.x);
            if (direction == 0) direction = combat.Facing;
            _motion.StartKnockback(direction, ResolveFieldKnockbackDistance(_player, reaction.KnockbackDistance), reaction.KnockbackTime);
        }
        private void OnPlayerDodged() => _playerAudio?.PlayAnimationSound("PLAYER_BACK_EVADE");

        private void OnGameLayerHealthChanged(int health)
        {
            if (health >= _lastHealth) { _lastHealth = health; return; }
            _lastHealth = health;
            _combat?.Engage();
            _feedback?.OnPlayerDamaged(_bossHitThisFrame, _bossHitThisFrame && (_boss.Combat.CurrentReaction?.DownTime ?? 0) > 0, _playerBody);
            _bossHitThisFrame = false;
            if (_hud != null) _hud.FlashDamage();
        }

        // 사망·암전·해체·결박 가르기 중에는 열지 않습니다(O04·O04_1). 텍스트박스가 열려 있어도 열 수 있습니다.
        private bool CanPause() => !_bossPhaseRunning && !(_bossActive && _boss.Actor.IsTransitioning) && (!_saveLocked || _saveFailed) && !_modalBlocked && !_leaving && !LoadingScreen.IsActive && _actor != null && !_actor.IsDead && !PreviewScreenFade.IsActive && _actor.CurrentState != StateKey.Execute && _actor.CurrentState != StateKey.Skill1;

        private void ReturnToTitle()
        {
            if (_leaving || LoadingScreen.IsActive) return;
            if (_loadingPrefab == null) throw new InvalidOperationException("타이틀 복귀에 사용할 씬 이동 화면이 연결되지 않았습니다.");
            Instantiate(_loadingPrefab).BeginFade(_titleScene);
            _leaving = true;
            SetGameInputLocked(true);
        }

        private void OnSettingsChanged(GameSettings value)
        {
            LanguageSettings.Apply(value.Language);
            if (_textbox != null) _textbox.ConnectSkipHint(new LanguageTextSource(_catalog), value.Language);
            if (_catalog != null)
            {
                var text = new LanguageTextSource(_catalog);
                _feedback?.SetDodgeText(text.Read(30021));
                if (_hud != null)
                {
                    _hud.SetBossName(text.Read(30006));
                    _hud.SetSkillName(text.Read(_catalog.GetTable<Skill>().Get(_skillId).TextID));
                }
            }
            KeyboardControls.SetLayout(value.KeyboardLayout);
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
            _stage?.StepAmbushes(_player.transform.position.x);
            StepStageClear();
            StepSavePoint();
            _gates?.Step(_actor, _attackWindow.bounds, _playerBody.bounds, ShowGateHint);
            _feedback?.Step(Time.unscaledDeltaTime, Time.deltaTime);
        }

        private void ShowGateHint() { if (_textbox != null) _textbox.ShowObjective("결박이 길을 막고 있다. Z(결박 가르기)로 베어 내자"); }

        private void MoveToArrival()
        {
            string anchor = _progress.ArrivalAnchor;
            if (_markersRoot == null)
            {
                if (!string.IsNullOrEmpty(anchor)) throw new InvalidOperationException("요청한 도착 앵커를 찾을 표식 묶음이 없습니다: " + anchor);
                return;
            }
            if (string.IsNullOrEmpty(anchor))
            {
                if (_stage == null) return;
                // 장부방은 PlayerStart 없이 문 앞 표식만 둡니다.
                anchor = _stageId == StageExitRules.LedgerStage ? LedgerArrival : "PlayerStart";
            }
            // 명시한 도착 앵커가 잘못되면 시작점으로 대체하지 않고 진입 실패로 전달합니다.
            var anchors = new SpawnAnchors(_markersRoot);
            Vector3 point = anchors.Get(anchor).position;
            _player.SetPosition(new Vector3(point.x, point.y + _playerHalfSize.y, _player.transform.position.z));
            _progress.TakeArrival();
        }

        private void OnRoll(int direction, int distance, int duration)
            => _motion.StartRoll(direction, distance, duration);

        private void LateUpdate()
        {
            if (_actor != null && _actor.CurrentState == StateKey.Execute) StepExecution(0f);
            _parryFeedback?.Step(Time.unscaledDeltaTime);
            // 도착 준비 후에도 로딩 화면이 닫힐 때까지 이동·출구 판정을 재개하지 않습니다.
            if (_subscribed && _loadingLocked != LoadingScreen.IsActive) SetGameInputLocked(false);
            StepShrineClose();
            StepCutsceneFlow();
            if (StepBossPhase()) return;
            if (_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive || !_subscribed || !_player.isActiveAndEnabled || (_bossActive && !_boss.isActiveAndEnabled)) return;
            if (_stageNamePending && CanPause() && (_textbox == null || !_textbox.IsOpen))
            {
                _stageNamePending = false;
                try { _textbox.ShowStageName(LanguageTextSource.Select(_catalog.GetTable<Language>().Get(_catalog.GetTable<Stage>().Get(_stageId).TextID))); }
                catch (Exception error) { Debug.LogException(error, this); }
            }
            _facing = _actor.Facing;
            _deathRestart?.Step(_actor, Time.deltaTime);
            StepExecution(Time.deltaTime);
            // 암전 중 입력은 받지 않습니다(O09).
            if (_input.enabled == PreviewScreenFade.IsActive) _input.enabled = !PreviewScreenFade.IsActive;
            float combatMin = _useMapCollision ? _combatMinX : -_stageHalfWidth;
            float combatMax = _useMapCollision ? _combatMaxX : _stageHalfWidth;
            // 출구가 잠겨 있어도 맵 밖으로 나가 떨어지지 않게 맵 끝에서 막습니다.
            float moveMin = _stage != null ? _stage.Area.min.x + _playerHalfSize.x : _useMapCollision ? float.NegativeInfinity : -_stageHalfWidth;
            float moveMax = _stage != null ? _stage.Area.max.x - _playerHalfSize.x : _useMapCollision ? float.PositiveInfinity : _stageHalfWidth;
            _motion.SetVerticalInput(_input.VerticalDirection);
            _motion.Move(Time.deltaTime, moveMin, moveMax);
            _attackWindow.enabled = _actor.IsAttackActive;
            // 몬스터 이동 후 플레이어 판정을 확인합니다(기존 순서 유지).
            foreach (PreviewMonsterDriver monster in _monsterDrivers)
                if (monster.NeedsStep) monster.Step(Time.deltaTime, combatMin, combatMax);
            // 보스는 고정 한계 대신 실제 맵 범위에서 막습니다. 고정 한계는 맵1 기준이라 보스방 돌진이 벽을 지나쳤습니다.
            // 보스방 좌우 벽 두께 1(카메라 범위와 같은 기준)을 빼 벽 안쪽 면에서 멈추고 벽 충돌을 알립니다.
            _bossDriver?.Step(Time.deltaTime, _stage != null ? _stage.Area.min.x + 1f : combatMin, _stage != null ? _stage.Area.max.x - 1f : combatMax);
            StepFields();
            CheckPlayerHits();
            HandlePreviewKeys();
            RefreshLabels();
            StepGameLayer();
        }

        // 해체는 양쪽 클립이 끝날 때까지 유지합니다. 일시정지·히트스톱 동안은 흐르지 않습니다.
        private void StepExecution(float seconds)
        {
            if (_actor.CurrentState != StateKey.Execute) return;
            if (_player == null || !_player.isActiveAndEnabled || _executingRuntime == null || !_executingRuntime.isActiveAndEnabled)
            { _actor.CancelExecution(); _executingRuntime = null; return; }
            _executionRemaining -= seconds;
            if (_executionRemaining <= 0f)
            { _actor.CompleteExecution(_restorePoiseAfterPreviewExecution); _executingRuntime = null; }
        }

        private void HandlePreviewKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame)
                { if (_bossActive) _actor.ReceiveHit(CombatMath.Damage(_boss.Actor.Definition, _actor.CombatStats, 0, false)); }
        }

        private void RefreshLabels()
        {
            _presentation.RefreshPlayer(_actor, _playerLabel, _playerBody, _playerColor);
            if (!_bossActive) return;
            _presentation.RefreshBoss(_boss.Actor, _bossLabel, _bossBody, _bossColor);
            if (_boss.Combat != null && _boss.Combat.IsAttacking) _bossBody.color = new Color(1f, 0.2f, 0.2f);
        }

        // 상태 식별자와 동작은 유지하고 화면 표시만 번역합니다.
        internal static string GetStateLabel(StateKey state) => PreviewPresentation.GetStateLabel(state);
    }
}
