using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 씬 사이에는 엔진 객체 대신 저장 세션과 진입 요청만 전달합니다.
    public class GameSaveContext
    {
        public static GameSaveContext Current { get; private set; }
        public SaveSession Session { get; }
        public JsonSaveStore Store { get; }
        public GameProgressState Progress { get; }
        public SaveRecord Entry { get; private set; }
        private TaskCompletionSource<bool> _sceneReady = new TaskCompletionSource<bool>();
        public Task<bool> SceneReady => _sceneReady.Task;
        public Task<bool> BeginScene() { _sceneReady = new TaskCompletionSource<bool>(); return _sceneReady.Task; }
        public void CompleteScene(bool success) => _sceneReady.TrySetResult(success);
        public bool EntryPending { get; private set; } = true;
        public GameSaveContext(SaveSession session, SaveRecord entry, JsonSaveStore store = null, GameProgressState progress = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Entry = entry;
            Store = store;
            Progress = progress ?? new GameProgressState();
        }
        public static void Set(GameSaveContext context) => Current = context;
        public void CompleteEntry() { EntryPending = false; Entry = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Current = null;

        public static SaveBuildMode Mode
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return SaveBuildMode.Demo;
#else
                return SaveBuildMode.Release;
#endif
            }
        }
        public static string DirectoryPath(string demoSubdirectory)
        {
            string root = Path.GetFullPath(Application.persistentDataPath) + Path.DirectorySeparatorChar;
            string subdirectory = Mode == SaveBuildMode.Demo ? demoSubdirectory : "Saves/Release";
            string path = Path.GetFullPath(Path.Combine(root, subdirectory));
            if (Path.IsPathRooted(subdirectory) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("저장 폴더는 게임 저장 경로의 하위 폴더여야 합니다.");
            return path;
        }
        public static GameSaveContext Create(IReadOnlyList<SaveRecord> records, JsonSaveStore store, int version,
            Func<int, bool> gateExists, SaveRecord entry = null, GameProgressState progress = null)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (gateExists == null) throw new ArgumentNullException(nameof(gateExists));
            // 파일은 건드리지 않고 새 세션이 소유할 사본만 준비합니다. 맵의 사전 복원도 같은 기록을 사용합니다.
            SaveRecord[] compatible = records.Select(record => record.WithExistingGates(gateExists)).ToArray();
            SaveRecord preparedEntry = entry == null ? null : compatible.Single(record => record.Slot == entry.Slot);
            var session = new SaveSession(Mode, version, store, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), compatible);
            return new GameSaveContext(session, preparedEntry, store, progress);
        }
    }
}
