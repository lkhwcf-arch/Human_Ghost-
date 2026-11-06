using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Gameplay
{
    // 맵·플레이어를 소유한 조립자가 구현합니다. 준비 중에는 입력·AI 등 게임 진행을 정지해야 합니다.
    public interface ISaveGameRuntime
    {
        SaveSnapshot Capture();
        Task PrepareAsync(SaveSnapshot snapshot, CancellationToken cancellation);
        void Apply(SaveSnapshot snapshot);
        void SetPlayable(bool playable);
    }

    // 실행 순서만 관리합니다. 씬·리소스 경로·상태 필드 복사·파일 처리는 각 담당자에게 둡니다.
    public sealed class SaveGameFlow
    {
        private readonly SaveSession _session;
        private readonly ISaveGameRuntime _runtime;
        private bool _busy;

        public bool IsBusy => _busy || _session.IsSaving;
        public bool IsPlayable { get; private set; }
        public bool IsManualSaveOpen { get; private set; }
        public bool ManualSaveEnabled => _session.ManualSaveEnabled;
        public event Action<SaveSnapshot> Ready;

        public IReadOnlyList<SaveSlotEntry> GetSlots(bool includeEmpty) => _session.GetSlots(includeEmpty);

        public bool TryOpenManualSave()
        {
            if (!ManualSaveEnabled || IsBusy || !IsPlayable || IsManualSaveOpen) return false;
            _runtime.SetPlayable(false);
            IsPlayable = false;
            IsManualSaveOpen = true;
            return true;
        }

        public bool CloseManualSave(bool resume = true)
        {
            if (!IsManualSaveOpen || (resume && IsBusy)) return false;
            if (resume) _runtime.SetPlayable(true);
            IsManualSaveOpen = false;
            IsPlayable = resume;
            return true;
        }

        public SaveGameFlow(SaveSession session, ISaveGameRuntime runtime)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        // 저장을 다시 읽지 않는 일반 맵 이동입니다. 준비와 임시 상태 전달은 호출자가 마칩니다.
        public bool ResumePrepared()
        {
            if (IsBusy || IsManualSaveOpen || !_session.CurrentSlot.HasValue) return false;
            _runtime.SetPlayable(true);
            IsPlayable = true;
            return true;
        }

        public Task<bool> NewGameAsync(SaveSnapshot initial, CancellationToken cancellation = default)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            SaveSnapshot snapshot = initial.WithSavePoints(Array.Empty<int>());
            return RestoreAsync(snapshot, () => _session.NewGameAsync(snapshot), cancellation);
        }

        public Task<bool> ContinueAsync(int? slot = null, CancellationToken cancellation = default)
        {
            if (IsBusy || IsManualSaveOpen) return Task.FromResult(false);
            SaveRecord record = _session.GetRecords().FirstOrDefault(item => !slot.HasValue || item.Slot == slot.Value);
            if (record == null) return Task.FromResult(false);
            return RestoreAsync(record.Snapshot,
                () => Task.FromResult(_session.TryContinue(record.Slot, out _)), cancellation);
        }

        public Task<bool> SaveManualAsync(int slot, bool overwriteConfirmed)
        {
            if (!_session.ManualSaveEnabled) return Task.FromResult(false);
            return CaptureAndSaveAsync(snapshot => _session.SaveManualAsync(slot, overwriteConfirmed, snapshot), IsManualSaveOpen);
        }

        public Task<bool> SaveAutomaticAsync(int savePointId, bool repeat = false) => IsManualSaveOpen ? Task.FromResult(false) :
            CaptureAndSaveAsync(snapshot => _session.SaveAutomaticAsync(savePointId, snapshot, repeat));

        private async Task<bool> CaptureAndSaveAsync(Func<SaveSnapshot, Task<bool>> save, bool allowPaused = false)
        {
            if (IsBusy || (!IsPlayable && !allowPaused)) return false;
            _busy = true;
            try { return await save(_runtime.Capture()); }
            finally { _busy = false; }
        }

        private async Task<bool> RestoreAsync(SaveSnapshot snapshot, Func<Task<bool>> commit,
            CancellationToken cancellation)
        {
            if (IsBusy || IsManualSaveOpen) return false;
            cancellation.ThrowIfCancellationRequested();
            _busy = true;
            IsPlayable = false;
            try
            {
                _runtime.SetPlayable(false);
                await _runtime.PrepareAsync(snapshot, cancellation);
                // 준비 담당이 취소를 늦게 확인해도 취소된 장면에 상태를 적용하지 않습니다.
                cancellation.ThrowIfCancellationRequested();
                _runtime.Apply(snapshot);
                cancellation.ThrowIfCancellationRequested();
                if (!await commit()) return false;
                // 파일 기록 중 취소돼도 성공한 파일은 유지하되 종료된 장면의 입력은 켜지 않습니다.
                cancellation.ThrowIfCancellationRequested();
                _runtime.SetPlayable(true);
                IsPlayable = true;
                // 완료 소비자 예외는 준비 실패로 바꾸거나 성공한 상태를 되돌리지 않습니다.
                Ready?.Invoke(snapshot);
                return true;
            }
            finally { _busy = false; }
        }
    }
}
