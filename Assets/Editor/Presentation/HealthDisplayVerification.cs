using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using HumanGhost.Editor.Data;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HumanGhost.Editor.Presentation
{
    // 원본 씬을 저장하지 않고 Play 중 만든 객체만 검사·정리합니다.
    public static class HealthDisplayVerification
    {
        private const string _batchKey = "HumanGhost.HealthVerification.Batch";
        private const string _completeKey = "HumanGhost.HealthVerification.Complete";
        private const string _successKey = "HumanGhost.HealthVerification.Success";
        private const string _anchorName = "PlayerHealthAnchor";
        private static bool _running;

        [InitializeOnLoadMethod]
        private static void ResumeBatch()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(_batchKey, false) && SessionState.GetBool(_completeKey, false)
                && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += FinishBatch;
        }

        public static void RunBatch()
        {
            SessionState.SetBool(_batchKey, true);
            SessionState.SetBool(_completeKey, false);
            SessionState.SetBool(_successKey, false);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(_batchKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) RunInPlayMode();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(_completeKey, false)) FinishBatch();
        }

        private static void FinishBatch()
        {
            bool success = SessionState.GetBool(_successKey, false);
            SessionState.EraseBool(_batchKey);
            SessionState.EraseBool(_completeKey);
            SessionState.EraseBool(_successKey);
            EditorApplication.Exit(success ? 0 : 1);
        }

        [MenuItem("인귀/검사/앵커 체력 표시 연결 검사", true)]
        private static bool CanRun() => EditorApplication.isPlaying && !EditorApplication.isPaused && !_running;

        [MenuItem("인귀/검사/앵커 체력 표시 연결 검사")]
        public static async void RunInPlayMode()
        {
            if (_running || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            _running = true;
            bool success = false;
            GameObject canvasRoot = null;
            HealthDisplayGroup firstGroup = null, secondGroup = null;
            int checks = 0;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                checks++;
            }
            void Fails(Action action, string message)
            {
                try { action(); }
                catch (InvalidOperationException) { checks++; return; }
                throw new InvalidOperationException(message);
            }
            try
            {
                GameDataAssets assets = AssetDatabase.LoadAssetAtPath<GameDataAssets>(GameDataAssetBuilder.AssetPath);
                var bootstrapper = new DataBootstrapper(assets.CreateSource(), path => path);
                IReadOnlyDataCatalog data = await bootstrapper.InitializeAsync(CancellationToken.None);
                var prefabs = AssetDatabase.LoadAssetAtPath<HealthDisplayPrefabs>("Assets/Data/HealthDisplayPrefabs.asset");
                HealthDisplayConfig configured = data.GetTable<HealthDisplayConfig>().All.FirstOrDefault();
                if (configured == null) throw new InvalidOperationException("배포된 체력 표시 설정에 검사할 행이 없습니다.");
                Check(prefabs.Get(configured.PrefabKey) != null, "파일명 키로 실제 프리팹을 선택하지 못했습니다.");
                Player definition = data.GetTable<Player>().All.FirstOrDefault();
                if (definition == null) throw new InvalidOperationException("배포된 플레이어 데이터에 검사할 행이 없습니다.");
                PlayerActor NewPlayer() => new PlayerActor(definition, new[] {
                    new AttackSpec(1,1,1,0,0,0,0), new AttackSpec(1,1,1,0,0,0,0),
                    new AttackSpec(1,1,1,0,0,0,0), new AttackSpec(1,1,1,0,0,0,0), new AttackSpec(1,1,1,0,0,0,0)
                }, true, definition.StaminaMax);
                PlayerActor firstPlayer = NewPlayer(), secondPlayer = NewPlayer();
                var fixture = new HealthCatalog(new HealthDisplayConfig(1, configured.PrefabKey, _anchorName, 333, 22, true));
                canvasRoot = new GameObject("AnchorVerificationCanvas", typeof(RectTransform), typeof(Canvas));
                canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var layout = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/UI/HealthDisplayAnchorLayout.prefab");
                GameObject firstParent = Object.Instantiate(layout, canvasRoot.transform, false);
                GameObject secondParent = Object.Instantiate(layout, canvasRoot.transform, false);
                secondParent.SetActive(false);
                var firstAnchors = new SpawnAnchors(firstParent.transform);
                var secondAnchors = new SpawnAnchors(secondParent.transform);
                Transform firstAnchor = firstAnchors.Get(_anchorName), secondAnchor = secondAnchors.Get(_anchorName);
                Check(firstAnchor != secondAnchor && firstAnchor.IsChildOf(firstParent.transform)
                    && secondAnchor.IsChildOf(secondParent.transform), "다른 부모 인스턴스의 앵커가 섞였습니다.");
                firstGroup = HealthDisplayGroup.Create(fixture, prefabs, firstPlayer, firstAnchors);
                secondGroup = HealthDisplayGroup.Create(fixture, prefabs, secondPlayer, secondAnchors);
                HealthDisplay firstView = firstParent.GetComponentInChildren<HealthDisplay>(true);
                HealthDisplay secondView = secondParent.GetComponentInChildren<HealthDisplay>(true);
                Check(firstView.IsBound && secondView.IsBound, "생성 후 플레이어 연결이 누락됐습니다.");
                Check(((RectTransform)firstView.transform).sizeDelta == new Vector2(333, 22), "데이터의 표시 크기가 적용되지 않았습니다.");
                Canvas.ForceUpdateCanvases();
                Check(Vector3.Distance(firstView.transform.position, firstAnchor.position) < 0.001f, "표시 피벗과 앵커 위치가 다릅니다.");
                firstAnchor.localPosition += new Vector3(70, -35, 0);
                Check(Vector3.Distance(firstView.transform.position, firstAnchor.position) < 0.001f, "표시가 앵커 이동을 따라가지 않습니다.");
                RectTransform Fill(HealthDisplay view) => (RectTransform)new SerializedObject(view).FindProperty("_fill").objectReferenceValue;
                float inactiveRatio = Fill(secondView).anchorMax.x;
                Check(Mathf.Approximately(Fill(firstView).anchorMax.x, 1f), "최초 체력 표시가 잘못됐습니다.");
                firstPlayer.ReceiveHit(1);
                Check(Mathf.Approximately(Fill(firstView).anchorMax.x, (float)firstPlayer.Health / definition.HP), "피격 이벤트가 화면에 반영되지 않았습니다.");
                Check(Mathf.Approximately(Fill(secondView).anchorMax.x, inactiveRatio), "다른 플레이어의 이벤트가 비활성 표시에 섞였습니다.");
                secondPlayer.ReceiveHit(1);
                Check(Mathf.Approximately(Fill(secondView).anchorMax.x, inactiveRatio), "비활성 부모 아래에서 체력 이벤트를 소비했습니다.");
                secondParent.SetActive(true);
                Check(Mathf.Approximately(Fill(secondView).anchorMax.x, (float)secondPlayer.Health / definition.HP), "부모 활성화 시 최신 체력으로 동기화하지 않았습니다.");
                firstParent.SetActive(false);
                float hiddenRatio = Fill(firstView).anchorMax.x;
                firstPlayer.Tick(definition.HitIFrame + definition.HitStunTime);
                firstPlayer.ReceiveHit(1);
                Check(Mathf.Approximately(Fill(firstView).anchorMax.x, hiddenRatio), "비활성 중 체력 이벤트를 소비했습니다.");
                firstParent.SetActive(true);
                Check(Mathf.Approximately(Fill(firstView).anchorMax.x, (float)firstPlayer.Health / definition.HP), "재활성화 시 체력이 갱신되지 않았습니다.");
                Fails(() => firstAnchors.Get("MissingAnchor"), "누락 앵커를 허용했습니다.");
                var duplicate = new GameObject(_anchorName, typeof(RectTransform));
                duplicate.transform.SetParent(firstParent.transform, false);
                Fails(() => new SpawnAnchors(firstParent.transform).Get(_anchorName), "중복 앵커를 허용했습니다.");
                Object.Destroy(duplicate);
                var nested = new GameObject("NestedAnchor", typeof(RectTransform));
                nested.transform.SetParent(firstAnchor, false);
                Check(new SpawnAnchors(firstParent.transform).Get(nested.name) == nested.transform, "중첩 앵커를 찾지 못했습니다.");
                Object.Destroy(nested);
                firstGroup.Dispose(); firstGroup.Dispose(); firstGroup = null;
                Check(firstParent != null && firstAnchor != null && !firstView.IsBound && !firstView.gameObject.activeInHierarchy,
                    "종료 시 외부 부모를 제거했거나 구독·활성 상태를 정리하지 못했습니다.");
                await NextFrameAsync();
                Check(firstView == null && secondView != null && secondView.IsBound, "다른 그룹까지 제거했거나 지연 제거가 완료되지 않았습니다.");
                var broken = new HealthCatalog(
                    new HealthDisplayConfig(1, configured.PrefabKey, _anchorName, 333, 22, true),
                    new HealthDisplayConfig(2, configured.PrefabKey, "MissingAnchor", 333, 22, true));
                Fails(() => HealthDisplayGroup.Create(broken, prefabs, firstPlayer, new SpawnAnchors(firstParent.transform)),
                    "일부 생성 후 앵커 누락이 실패로 전달되지 않았습니다.");
                Check(firstParent.GetComponentsInChildren<HealthDisplay>(true).All(view => !view.IsBound && !view.gameObject.activeInHierarchy),
                    "실패한 생성의 객체가 활성 상태이거나 구독이 남았습니다.");
                await NextFrameAsync();
                Check(firstParent.GetComponentsInChildren<HealthDisplay>(true).Length == 0, "실패한 생성의 객체가 남았습니다.");
                var hidden = new HealthCatalog(new HealthDisplayConfig(1, configured.PrefabKey, "MissingAnchor", 333, 22, false));
                using (HealthDisplayGroup.Create(hidden, prefabs, firstPlayer, new SpawnAnchors(firstParent.transform)))
                    Check(firstParent.GetComponentsInChildren<HealthDisplay>(true).Length == 0, "숨긴 행을 생성했습니다.");
                Transform removedAnchor = secondAnchor;
                Object.Destroy(secondParent);
                await NextFrameAsync();
                Fails(() => secondAnchors.Get(_anchorName), "제거된 앵커 참조를 허용했습니다.");
                secondGroup.Dispose(); secondGroup = null;
                Check(removedAnchor == null && secondView == null, "부모 제거 시 표시가 남았습니다.");
                Debug.Log($"앵커 체력 표시 연결 검사 {checks}개를 통과했습니다.");
                success = true;
            }
            catch (Exception error) { Debug.LogError($"앵커 체력 표시 연결 검사 실패: {error}"); }
            finally
            {
                firstGroup?.Dispose(); secondGroup?.Dispose();
                if (canvasRoot != null) Object.Destroy(canvasRoot);
                _running = false;
                string report = Environment.GetEnvironmentVariable("HUMANGHOST_HEALTH_VERIFY_REPORT");
                if (!string.IsNullOrWhiteSpace(report))
                    File.WriteAllText(report, $"성공={success}\n통과={checks}\n");
                if (SessionState.GetBool(_batchKey, false))
                {
                    SessionState.SetBool(_successKey, success);
                    SessionState.SetBool(_completeKey, true);
                    EditorApplication.ExitPlaymode();
                }
            }
        }

        // 지연 제거를 실제 다음 프레임에서 확인합니다. 게임 기능의 갱신 루프가 아닙니다.
        private static async Task NextFrameAsync()
        {
            int frame = Time.frameCount;
            while (Time.frameCount == frame)
            {
                if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play가 종료되어 앵커 검사를 중단했습니다.");
                await Task.Yield();
            }
        }

        // 검사 행만 교체하며 실제 배포 데이터와 읽기 전용 저장소는 변경하지 않습니다.
        private sealed class HealthCatalog : IReadOnlyDataCatalog
        {
            private readonly DataTable<HealthDisplayConfig> _table;
            public HealthCatalog(params HealthDisplayConfig[] rows) => _table = new DataTable<HealthDisplayConfig>(rows, row => row.ID);
            public IReadOnlyDataTable<T> GetTable<T>() => (IReadOnlyDataTable<T>)(object)_table;
            public T GetDefinition<T>() where T : class => throw new InvalidOperationException("체력 표시 검사에는 단일 JSON 정의가 없습니다.");
        }
    }
}
