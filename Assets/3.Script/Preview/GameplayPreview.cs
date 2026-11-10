using System;
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
        // 해체 연출 원본 전까지 쓰는 개발 값입니다. 연출이 붙으면 그 완료 시점으로 교체합니다.
        private const float _executionSeconds = 0.9f;
        private float _executionRemaining;

        private bool _modalBlocked;

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
        }

        public bool IsReady => _subscribed;
        public PlayerRuntime Player => _player;
        public BossRuntime Boss => _boss;

        private void OnEnable()
        {
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
                        _firstMonsterTargetId + i, _playerTargetId, _worldUnitsPerTile);
                }
                _bossDriver?.Dispose();
                _bossDriver = !_bossActive || _boss.Combat == null ? null : new PreviewBossDriver(_boss, _player, _bossBody, _playerBody,
                    _worldUnitsPerTile, _bossRangeTiles, _bossSecondRangeTiles, _bossSightTiles);
                _actor = _player.Actor;
                _actor.ApplyLevel(_progress.Rewards.CurrentLevel);
                _actor.ParrySucceeded += OnParrySucceeded;
                if (_motion == null) _motion = new PreviewPlayerMotion(_player, _groundY, _useMapCollision, _playerHalfSize, GetMarkers(),
                    _actor.Definition.JumpHeight, Mathf.RoundToInt(_jumpSeconds * 1000f),
                    key => _playerAudio?.PlayAnimationSound(key));
                _input.TryDropThrough = _motion.TryDropThrough;
                _input.SetModalBlocked(_modalBlocked);
                _input.JumpAccepted += OnJump;
                _input.JumpRejected += _motion.OnJumpRejected;
                _input.InteractPressed += OnInteract;
                _input.VerticalPressed += OnVertical;
                ConnectInteraction(catalog);
                MoveToArrival();
                ConnectCutsceneData();
                ConnectStageObjects(catalog);
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
                _stage?.Activate();
                ApplyWalkableSpans();
                ConnectSaves();
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
            if (_playerAudio != null) _playerAudio.Disconnect();
            _playerAudio = null;
            DisconnectCutscenes();
            _deathRestart?.Dispose(); _deathRestart = null;
            _presentation.Reset();
            _feedback?.Dispose();
            _feedback = null;
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

        // 임시 판정: 공격 범위 사각형과 보스/일반몹 사각형 겹침. 창 열림 순간과 매 프레임 호출하며 중복은 창 등록이 막습니다.
        private void CheckPlayerHits()
        {
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
            _attackWindow.transform.localPosition = new Vector3(_facing * 0.95f, 0f, 0.1f);
            if (!_actor.IsAttackActive) return;
            if (_bossActive && _attackWindow.bounds.Intersects(_bossBody.bounds))
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
            if (_motion.TryUseLadder(direction)) return;
            if (_motion.IsHanging) _motion.OnVerticalPressed(direction);
            else if (direction > 0) OnInteract();
        }

        private void OnAdvance() { if (_pause == null || !_pause.IsOpen) _textbox.Advance(); }
        private void OnTextboxChanged(bool open) => SetGameInputLocked(open);

        private void SetGameInputLocked(bool locked)
        {
            _motion?.DiscardBufferedInput();
            _input.DiscardBufferedInput();
            _input.SetLocked(locked || _saveLocked || _leaving || _actor.IsDead || (_textbox != null && _textbox.IsOpen) || (_pause != null && _pause.IsOpen));
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
            }
        }
        private void RequestInteractionRefresh() { if (_interactor != null) _interactor.RequestRefresh(); }
        private void OnInteractionStateChanged(StateKey state) => RequestInteractionRefresh();
        private void OnInteractionHealthChanged(int health) => RequestInteractionRefresh();

        private bool _leaving;

        private void GoToStage(int stageId, string anchor)
        {
            if (_modalBlocked || _saveLocked || _leaving || _loadingPrefab == null) return;
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
            for (int i = 0; i < _monsters.Length; i++)
            {
                SpriteRenderer body = _monsters[i].GetComponentInChildren<SpriteRenderer>(true);
                float half = body.transform.lossyScale.y * 0.5f;
                Vector2 feet = (Vector2)_monsters[i].transform.position + Vector2.down * half;
                _monsterDrivers[i].UseWalkableSpan(FindSpanEnd(feet, half, -1), FindSpanEnd(feet, half, 1));
            }
        }

        private static float FindSpanEnd(Vector2 feet, float halfHeight, int direction)
        {
            const float step = 0.25f;
            float x = feet.x;
            for (int i = 0; i < 800; i++)
            {
                float next = x + direction * step;
                // 앞이 막히거나 발밑이 비면 그 앞에서 멈춥니다.
                if (IsSolid(new Vector2(next, feet.y + halfHeight)) || !IsSolid(new Vector2(next, feet.y - 0.1f))) break;
                x = next;
            }
            return x;
        }

        private static bool IsSolid(Vector2 point)
        {
            foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
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
                // 보스방은 바닥 아래를 비추지 않게 맵 기준 높이에 고정합니다.
                if (_stageId == BossStage) _followCamera.SetFixed(new Vector2(_stage.Area.center.x, _stage.MapRoot.position.y + _stage.CameraSize), _stage.CameraSize);
                else _followCamera.SetArea(_stage.Area.min.x + 1f, _stage.Area.max.x - 1f, _stage.MapRoot.position.y + 4f, _stage.CameraSize);
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

        private bool IsInExecutionRange()
            => _bossActive && Vector2.Distance(_player.transform.position, _boss.transform.position) <= _executionRangeTiles * _worldUnitsPerTile;
        private bool CanExecute() => _bossActive && _boss.Actor.IsExposed && IsInExecutionRange() && _actor.Locomotion.IsGrounded;
        private bool TryExecute()
        {
            if (!_actor.Locomotion.IsGrounded || !_actor.TryExecute(_boss.Actor, IsInExecutionRange())) return false;
            _feedback?.OnExecute();
            _executionRemaining = _executionSeconds;
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
            _feedback = new PreviewCombatFeedback(_speed, _followCamera, _audio, _feedbackTextPrefab, catalog.GetTable<Language>().Get(30021).KO);
            if (_bossDriver != null)
            {
                _bossDriver.PlayerHit += OnBossHitPlayer;
                _bossDriver.PlayerDodged += OnPlayerDodged;
            }
            foreach (PreviewMonsterDriver driver in _monsterDrivers) driver.PlayerDodged += OnPlayerDodged;
            if (_hud != null)
            {
                CanvasGroup healthGroup = _healthAnchorRoot != null ? _healthAnchorRoot.GetComponentInParent<Canvas>().GetComponent<CanvasGroup>() : null;
                if (healthGroup == null && _healthAnchorRoot != null) healthGroup = _healthAnchorRoot.GetComponentInParent<Canvas>().gameObject.AddComponent<CanvasGroup>();
                _hud.Connect(_actor, _boss.Actor, catalog.GetTable<Language>().Get(30006).KO, _boss.Actor.Poise, _showBossGauge && _bossActive, healthGroup);
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

        private void OnBossHitPlayer() => _bossHitThisFrame = true;
        private void OnPlayerDodged() => _playerAudio?.PlayAnimationSound("PLAYER_BACK_EVADE");

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
        private bool CanPause() => (!_saveLocked || _saveFailed) && !_modalBlocked && !_leaving && _actor != null && !_actor.IsDead && !PreviewScreenFade.IsActive && _actor.CurrentState != StateKey.Execute && _actor.CurrentState != StateKey.Skill1;

        private void ReturnToTitle()
        {
            if (_leaving) return;
            if (_loadingPrefab == null) throw new InvalidOperationException("타이틀 복귀에 사용할 씬 이동 화면이 연결되지 않았습니다.");
            Instantiate(_loadingPrefab).BeginFade(_titleScene);
            _leaving = true;
            SetGameInputLocked(true);
        }

        private void OnSettingsChanged(GameSettings value)
        {
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
            StepShrineClose();
            StepCutsceneFlow();
            if (_modalBlocked || _saveLocked || !_subscribed || !_player.isActiveAndEnabled || (_bossActive && !_boss.isActiveAndEnabled)) return;
            if (_actor.Locomotion.Direction != 0) _facing = _actor.Locomotion.Direction;
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
            _bossDriver?.Step(Time.deltaTime, combatMin, combatMax);
            CheckPlayerHits();
            HandlePreviewKeys();
            RefreshLabels();
            StepGameLayer();
        }

        // 해체는 완료 호출 전까지 플레이어·보스를 모두 멈춥니다. 일시정지·히트스톱 동안은 흐르지 않습니다.
        private void StepExecution(float seconds)
        {
            if (_actor.CurrentState != StateKey.Execute) return;
            _executionRemaining -= seconds;
            if (_executionRemaining <= 0f) _actor.CompleteExecution(_restorePoiseAfterPreviewExecution);
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
