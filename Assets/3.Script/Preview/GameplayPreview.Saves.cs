using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 현재 테스트 맵의 상태 소유자가 기존 저장 계약을 구현합니다. 파일·슬롯 규칙은 복제하지 않습니다.
    public sealed partial class GameplayPreview
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField, InspectorName("수동 저장 화면 프리팹")] private ManualSaveHost _manualSavePrefab = null;
        private ManualSaveHost _manualSaveHost;
        private GameObject _manualSaveRoot;
#endif
        private SaveGameFlow _saveFlow;
        private SaveSession _saveSession;
        private CancellationTokenSource _saveLifetime;
        private bool _saveLocked;
        private bool _saveFailed;
        private SubscriptionScope _progressScope;
        private bool _shrineCheckPending;
        private bool _shrineSaving;
        private bool _levelApplyPending, _upgradesApplyPending;
        private readonly System.Collections.Generic.Queue<(MonsterActor Actor, MonsterSpawnId Spawn)> _pendingMonsterRewards =
            new System.Collections.Generic.Queue<(MonsterActor, MonsterSpawnId)>();
        private bool _grantingMonsterRewards, _monsterRewardsFailed;

        private void ConnectProgress()
        {
            _progressScope?.Dispose();
            _levelApplyPending = false; _upgradesApplyPending = false;
            _pendingMonsterRewards.Clear(); _monsterRewardsFailed = false;
            var scope = new SubscriptionScope();
            _progressScope = scope;
            PlayerRuntime player = _player;
            Action moved = scope.Guard(() => _shrineCheckPending = true);
            scope.Add(() => player.SpatialChanged -= moved); player.SpatialChanged += moved;
            _shrineCheckPending = true;
            MonsterRewards rewards = _progress.Rewards;
            Action applyLevel = scope.Guard(() => _levelApplyPending = true);
            Action<int, int> leveledUp = (before, after) => applyLevel();
            scope.Add(() => rewards.LeveledUp -= leveledUp); rewards.LeveledUp += leveledUp;
            UpgradeSession upgrades = _progress.Upgrades;
            Action effectsChanged = scope.Guard(() => _upgradesApplyPending = true);
            scope.Add(() => upgrades.EffectsChanged -= effectsChanged); upgrades.EffectsChanged += effectsChanged;
            Action<int, int> currency = (id, balance) =>
            {
                if (!scope.IsDisposed && balance > 0) QueueFirstNotice(CurrencyFirstNotice(_catalog.GetTable<Currency>().Get(id)));
            };
            scope.Add(() => rewards.Wallet.Changed -= currency); rewards.Wallet.Changed += currency;
            Action skillChanged = scope.Guard(() => QueueFirstNotice(SkillFirstNotice()));
            scope.Add(() => _actor.DisplayChanged -= skillChanged); _actor.DisplayChanged += skillChanged;
            if (_stage == null) return;
            foreach (var pair in _stage.MonsterIds)
            {
                MonsterActor actor = pair.Key.Actor;
                MonsterSpawnId id = pair.Value;
                Action<int> changed = scope.Guard<int>(health =>
                {
                    // 사망 알림 안에서 다른 코어 명령을 호출하지 않습니다. 처형 양쪽 정리 뒤 조립자가 지급합니다.
                    if (health == 0 && !_progress.HasDefeatedMonster(id)) _pendingMonsterRewards.Enqueue((actor, id));
                });
                scope.Add(() => actor.HealthChanged -= changed); actor.HealthChanged += changed;
            }
        }

        private void FlushMonsterRewards()
        {
            if (_grantingMonsterRewards || _monsterRewardsFailed) return;
            _grantingMonsterRewards = true;
            try
            {
                while (_pendingMonsterRewards.Count > 0)
                {
                    var pending = _pendingMonsterRewards.Peek();
                    try
                    {
                        if (!_progress.HasDefeatedMonster(pending.Spawn) &&
                            !_progress.Rewards.TryGrant(pending.Spawn, pending.Actor.Definition.ID, _progress,
                                pending.Actor.WasExecuted ? _progress.Executions : null, _actor))
                            throw new InvalidOperationException($"몬스터 {pending.Actor.Definition.ID}의 보상을 확정하지 못했습니다.");
                    }
                    finally
                    {
                        if (_progress.HasDefeatedMonster(pending.Spawn)) _pendingMonsterRewards.Dequeue();
                    }
                }
            }
            catch { _monsterRewardsFailed = true; throw; }
            finally { _grantingMonsterRewards = false; }
        }

        // 완료 이벤트에서는 예약만 하고 조립자의 명령 순서에서 상태를 반영합니다.
        private void ApplyPendingProgress()
        {
            if (_progressScope == null || _progressScope.IsDisposed || _actor == null) return;
            if (_levelApplyPending)
            {
                _levelApplyPending = false;
                _actor.ApplyLevel(_progress.Rewards.CurrentLevel);
            }
            if (_upgradesApplyPending)
            {
                _upgradesApplyPending = false;
                _actor.ApplyUpgrades(_progress.Upgrades);
            }
        }

        private void ImportEntryProgress()
        {
            if (_progress.Rewards == null)
                _progress.ConnectRewards(new MonsterRewards(_catalog.GetTable<Currency>().All,
                    _catalog.GetTable<MonsterReward>().All, _catalog.GetTable<PlayerLevel>().All));
            if (_progress.Upgrades == null)
                _progress.ConnectUpgrades(new UpgradeSession(_progress.Rewards.Wallet,
                    _catalog.GetDefinition<ShrineDefinitions>(), _catalog.GetTable<Upgrade>().All));
            if (_progress.Galimgil == null)
                _progress.ConnectGalimgil(new GalimgilRewards(_catalog.GetDefinition<GalimgilRewardDefinition>(), _progress.Rewards.Wallet));
            if (_progress.Executions == null)
                _progress.ConnectExecutions(new ExecutionRewardSession(_catalog.GetTable<ExecuteReward>().All,
                    _catalog.GetTable<CorruptionStep>().All));
            var context = GameSaveContext.Current;
            SaveSnapshot snapshot = context != null && context.EntryPending ? context.Entry?.Snapshot : null;
            if (snapshot == null) return;
            if (snapshot.Hp == 0) throw new InvalidOperationException("사망 상태 기록으로는 이어하기를 시작할 수 없습니다.");
            _catalog.GetTable<Stage>().Get(snapshot.StageId);
            foreach (int id in snapshot.OpenedGates) _catalog.GetTable<GateConfig>().Get(id);
            _progress.Restore(snapshot);
        }

        private async void ConnectSaves()
        {
            _saveLifetime = new CancellationTokenSource();
            CancellationToken cancellation = _saveLifetime.Token;
            _saveFailed = false;
            GameSaveContext sceneContext = GameSaveContext.Current;
            try
            {
                GameSaveContext context = GameSaveContext.Current;
                if (context == null)
                {
                    var store = new JsonSaveStore(GameSaveContext.DirectoryPath("Saves/Demo"), 1);
                    var records = await store.ReadAsync();
                    cancellation.ThrowIfCancellationRequested();
                    RequireSaveRuntime();
                    var gates = _catalog.GetTable<GateConfig>();
                    context = GameSaveContext.Create(records, store, 1, id => gates.TryGet(id, out _), progress: _progress);
                    GameSaveContext.Set(context);
                    sceneContext = context;
                }
                _saveSession = context.Session;
                try { await context.Session.PendingWrite; }
                catch (Exception error)
                {
                    cancellation.ThrowIfCancellationRequested();
                    RequireSaveRuntime();
                    if (!context.Session.CurrentSlot.HasValue) throw;
                    Debug.LogException(new InvalidOperationException("이전 파일 기록은 실패했지만 현재 게임의 임시 진행 상태는 유지합니다.", error), this);
                }
                cancellation.ThrowIfCancellationRequested();
                RequireSaveRuntime();
                _saveFlow = new SaveGameFlow(context.Session, this);
                bool ready = context.EntryPending
                    ? context.Entry == null ? await _saveFlow.NewGameAsync(Capture(), cancellation)
                        : await _saveFlow.ContinueAsync(context.Entry.Slot, cancellation)
                    : _saveFlow.ResumePrepared();
                cancellation.ThrowIfCancellationRequested();
                RequireSaveRuntime();
                if (!ready) throw new InvalidOperationException("저장 세션의 게임 진입을 완료하지 못했습니다.");
                context.CompleteEntry();
                context.CompleteScene(true);
                QueueAvailableFirstNotices();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_manualSavePrefab != null && TestFeatureGate.IsEnabled)
                {
                    var holder = new GameObject("ManualSave");
                    _manualSaveRoot = holder;
                    holder.transform.SetParent(transform, false);
                    holder.SetActive(false);
                    try
                    {
                        _manualSaveHost = Instantiate(_manualSavePrefab, holder.transform);
                        _manualSaveHost.Connect(_saveFlow, _catalog, CanOpenManualSave);
                        holder.SetActive(true);
                    }
                    catch { Destroy(holder); _manualSaveHost = null; _manualSaveRoot = null; throw; }
                }
#endif
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this == null || !isActiveAndEnabled || cancellation.IsCancellationRequested) return;
                _saveFailed = true;
                sceneContext?.CompleteScene(false);
                Debug.LogException(new InvalidOperationException("게임 진입의 저장 준비·복원에 실패했습니다. 기존 기록은 유지합니다.", error), this);
            }
        }

        private bool CanOpenManualSave() => _saveFlow != null && _saveFlow.IsPlayable && CanPause() && (_pause == null || !_pause.IsOpen)
            && (_textbox == null || !_textbox.IsOpen) && !LoadingScreenActive();
        private static bool LoadingScreenActive() => HumanGhost.Gameplay.Presentation.LoadingScreen.IsActive;

        private async void SaveAtPoint(bool rest)
        {
            if (_shrineSaving || _modalBlocked || _leaving || _saveFlow == null || !_saveFlow.IsPlayable || _saveFlow.IsBusy || _actor.IsDead || _savePoint == null) return;
            int savePointId = _stageId;
            var lifetime = _saveLifetime;
            var flow = _saveFlow;
            SaveSession session = _saveSession;
            SaveSnapshot previous = session.CurrentSnapshot;
            _shrineSaving = true;
            try
            {
                _progress.ActivateShrine(savePointId);
                _savePoint.GetComponent<ShrineInteract>().SetActive(true);
                if (rest) RestorePlayerVitals(_actor.MaxHealth, _actor.HerbCapacity);
                await flow.SaveAutomaticAsync(savePointId, rest);
                if (rest && this != null && isActiveAndEnabled && ReferenceEquals(lifetime, _saveLifetime))
                {
                    // 파일 쓰기가 UI 닫힘보다 늦게 끝나도 일어서기 동작을 다시 앉기로 덮지 않습니다.
                    if (_shrinePrefab == null || (_shrineScreen != null && _shrineScreen.IsOpen))
                    {
                        _player.NotifyPresentationCue(_shrinePrefab == null ? "Save" : "Pray");
                        _playerAudio?.PlayAnimationSound("PLAYER_SAVE");
                    }
                }
            }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled && ReferenceEquals(lifetime, _saveLifetime))
                    Debug.LogException(new InvalidOperationException(ReferenceEquals(previous, session.CurrentSnapshot)
                        ? "자동 저장에 실패했습니다. 기존 저장 파일은 유지합니다."
                        : "자동 저장은 완료했지만 저장 완료 알림 처리에서 오류가 발생했습니다.", error), this);
            }
            finally
            {
                if (this != null && ReferenceEquals(lifetime, _saveLifetime)) _shrineSaving = false;
            }
        }

        public SaveSnapshot Capture()
        {
            RequireSaveRuntime();
            FlushMonsterRewards();
            ApplyPendingProgress();
            if (_monsterRewardsFailed) throw new InvalidOperationException("보상 처리 실패 상태에서는 저장하지 않습니다. 기존 저장은 유지합니다.");
            if (_actor.IsDead) throw new InvalidOperationException("사망 상태에서는 현재 위치를 저장할 수 없습니다.");
            return new SaveSnapshot(_stageId, _progress.ShrineId,
                IntegerPosition.ToData(_player.Position.X),
                IntegerPosition.ToData(_player.Position.Y),
                _actor.Health, _actor.HerbCount,
                _progress.Progress.GetCleared(),
                _progress.ActiveShrines, _progress.OpenedGates, _progress.ViewedCutscenes, Array.Empty<int>(),
                _progress.DefeatedMonsters, _progress.Rewards.Capture(), _progress.Upgrades.Capture(), _progress.Galimgil.Capture(), _progress.Executions.Capture(), _progress.ViewedNotices);
        }
        public Task PrepareAsync(SaveSnapshot snapshot, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            RequireSaveRuntime();
            if (snapshot.StageId != _stageId) throw new InvalidOperationException("기록의 스테이지를 조립한 뒤 복원해야 합니다.");
            return Task.CompletedTask;
        }
        public void Apply(SaveSnapshot snapshot)
        {
            RequireSaveRuntime();
            _actor.ApplyLevel(_progress.Rewards.CurrentLevel);
            RestorePlayerVitals(snapshot.Hp, snapshot.Herbs);
            _player.SetPosition(new IntegerPosition(IntegerPosition.FromData(snapshot.PositionX), IntegerPosition.FromData(snapshot.PositionY)));
        }
        public void SetPlayable(bool playable)
        {
            RequireSaveRuntime();
            if (playable) ConnectTelemetry();
            _saveLocked = !playable;
            if (_speed != null) _speed.State.SetPaused(!playable);
            SetGameInputLocked(!playable);
            foreach (var arrival in _galimgilArrivals) if (arrival != null) arrival.SetReady(playable);
        }
        private void RequireSaveRuntime()
        {
            if (this == null || !isActiveAndEnabled || !_subscribed || _actor == null)
                throw new InvalidOperationException("게임 상태 복원 대상의 준비 수명이 끝났습니다.");
        }
        private void DisconnectSaves()
        {
            var lifetime = _saveLifetime;
            var scope = _progressScope;
            _saveLifetime = null; _progressScope = null;
            _levelApplyPending = false; _upgradesApplyPending = false;
            SubscriptionScope.Cleanup(
                () => lifetime?.Cancel(),
                () => lifetime?.Dispose(),
                () => scope?.Dispose(),
                DisconnectFirstNotices, DisconnectTelemetry, DisconnectGalimgilArrivals, DisconnectShrine,
                () => _pendingMonsterRewards.Clear(),
                () => { if (_savePoint != null) _savePoint.GetComponent<ShrineInteract>().Connect(null); },
                () => { _shrineCheckPending = false; _shrineSaving = false; },
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                () => { var host = _manualSaveHost; _manualSaveHost = null; if (host != null) host.Disconnect(_saveFlow); },
                () => { var root = _manualSaveRoot; _manualSaveRoot = null; if (root != null) Destroy(root); },
#endif
                () => { if (_saveLocked && _speed != null) _speed.State.SetPaused(false); },
                () => { _saveLocked = false; _saveFlow = null; _saveSession = null; });
        }
    }
}
