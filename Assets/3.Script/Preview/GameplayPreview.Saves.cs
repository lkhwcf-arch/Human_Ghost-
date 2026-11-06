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
        private CancellationTokenSource _saveLifetime;
        private bool _saveLocked;
        private bool _saveFailed;

        private void ImportEntryProgress()
        {
            var context = GameSaveContext.Current;
            SaveSnapshot snapshot = context != null && context.EntryPending ? context.Entry?.Snapshot : null;
            if (snapshot == null) return;
            if (snapshot.Hp == 0) throw new InvalidOperationException("사망 상태 기록으로는 이어하기를 시작할 수 없습니다.");
            _catalog.GetTable<Stage>().Get(snapshot.StageId);
            foreach (int id in snapshot.OpenedGates) _catalog.GetTable<GateConfig>().Get(id);
            PreviewSession.Reset();
            PreviewSession.CurrentStage = snapshot.StageId;
            foreach (int id in snapshot.ClearedStages) PreviewSession.Progress.Clear(id);
            if (snapshot.ClearedStages.Contains(BossStage)) PreviewSession.DefeatedBoss.Add(BossStage);
            PreviewSession.OpenedGates.UnionWith(snapshot.OpenedGates);
            PreviewSession.ActiveShrines.UnionWith(snapshot.ActiveShrines);
            PreviewSession.ViewedCutscenes.UnionWith(snapshot.ViewedCutscenes);
            PreviewSession.ShrineId = snapshot.ShrineId;
            PreviewSession.CheckpointPassed = snapshot.ShrineId != 0;
        }

        private async void ConnectSaves()
        {
            _saveLifetime = new CancellationTokenSource();
            CancellationToken cancellation = _saveLifetime.Token;
            _saveFailed = false;
            GameSaveContext sceneContext = GameSaveContext.Current;
            try
            {
                SetPlayable(false);
                GameSaveContext context = GameSaveContext.Current;
                if (context == null)
                {
                    var store = new JsonSaveStore(GameSaveContext.DirectoryPath("Saves/Demo"), 1);
                    var records = await store.ReadAsync();
                    cancellation.ThrowIfCancellationRequested();
                    RequireSaveRuntime();
                    context = GameSaveContext.Create(records, store, 1);
                    GameSaveContext.Set(context);
                    sceneContext = context;
                }
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_manualSavePrefab != null)
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

        private async void SaveAtPoint(int savePointId)
        {
            if (_saveFlow == null) return;
            try { await _saveFlow.SaveAutomaticAsync(savePointId); }
            catch (Exception error)
            {
                if (this != null && isActiveAndEnabled)
                    Debug.LogException(new InvalidOperationException("자동 저장에 실패했습니다. 기존 저장 파일은 유지합니다.", error), this);
            }
        }

        public SaveSnapshot Capture()
        {
            RequireSaveRuntime();
            if (_actor.IsDead) throw new InvalidOperationException("사망 상태에서는 현재 위치를 저장할 수 없습니다.");
            return new SaveSnapshot(_stageId, PreviewSession.ShrineId,
                Mathf.RoundToInt(_player.transform.position.x / _worldUnitsPerTile * 100f),
                Mathf.RoundToInt(_player.transform.position.y / _worldUnitsPerTile * 100f),
                _actor.Health, _actor.HerbCount,
                PreviewSession.Progress.GetCleared().Concat(PreviewSession.DefeatedBoss),
                PreviewSession.ActiveShrines, PreviewSession.OpenedGates, PreviewSession.ViewedCutscenes, Array.Empty<int>());
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
            RestorePlayerVitals(snapshot.Hp, snapshot.Herbs);
            _player.SetPosition(new Vector3(snapshot.PositionX / 100f * _worldUnitsPerTile,
                snapshot.PositionY / 100f * _worldUnitsPerTile, _player.transform.position.z));
        }
        public void SetPlayable(bool playable)
        {
            RequireSaveRuntime();
            _saveLocked = !playable;
            if (_speed != null) _speed.State.SetPaused(!playable);
            SetGameInputLocked(!playable);
        }
        private void RequireSaveRuntime()
        {
            if (this == null || !isActiveAndEnabled || !_subscribed || _actor == null)
                throw new InvalidOperationException("게임 상태 복원 대상의 준비 수명이 끝났습니다.");
        }
        private void DisconnectSaves()
        {
            _saveLifetime?.Cancel();
            _saveLifetime?.Dispose();
            _saveLifetime = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_manualSaveHost != null)
            {
                _manualSaveHost.Disconnect(_saveFlow);
                _manualSaveHost = null;
            }
            if (_manualSaveRoot != null) Destroy(_manualSaveRoot);
            _manualSaveRoot = null;
#endif
            if (_saveLocked && _speed != null) _speed.State.SetPaused(false);
            _saveLocked = false;
            _saveFlow = null;
        }
    }
}
