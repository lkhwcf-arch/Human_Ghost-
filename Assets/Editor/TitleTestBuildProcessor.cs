#if UNITY_EDITOR
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.EditorTools
{
    // 출시 장면에 테스트 기능의 객체·문구·연결이 포함되지 않게 빌드 사본에서 제거합니다.
    // 에디터 Play에서도 "인귀/출시 빌드처럼 보기"가 켜져 있으면 로드된 씬 사본에 같은 제거를 적용합니다.
    public sealed class TitleTestBuildProcessor : IProcessSceneWithReport
    {
        private const string _releasePreviewMenu = "인귀/출시 빌드처럼 보기 (테스트 기능 끄기)";
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // report가 있으면 실제 빌드입니다. 개발 빌드만 건너뛰는 기존 조건을 그대로 유지합니다.
            if (report != null)
            {
                if ((report.summary.options & UnityEditor.BuildOptions.Development) != 0) return;
                Strip(scene, true);
                return;
            }
            // report가 없으면 에디터 Play의 씬 로드입니다. 원본 씬 파일은 바꾸지 않고 Play 중 사본만 정리합니다.
            if (BuildPipeline.isBuildingPlayer || !EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (TestFeatureGate.IsEnabled) return;
            Strip(scene, false);
        }

        private static void Strip(Scene scene, bool unpack)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var viewer in root.GetComponentsInChildren<DataLoadViewer>(true))
                { Unpack(viewer.gameObject, unpack); Object.DestroyImmediate(viewer); }
                foreach (var reset in root.GetComponentsInChildren<DemoResetHost>(true))
                { Unpack(reset.gameObject, unpack); reset.StripFromReleaseBuild(); }
                var manualScreens = root.GetComponentsInChildren<ManualSaveHost>(true);
                var slotScreens = root.GetComponentsInChildren<SaveSlotScreen>(true);
                foreach (var selection in root.GetComponentsInChildren<TitleSaveSelection>(true))
                {
                    Unpack(selection.gameObject, unpack);
                    selection.StripFromReleaseBuild();
                }
                foreach (var manualScreen in manualScreens)
                    if (manualScreen != null) { Unpack(manualScreen.gameObject, unpack); manualScreen.StripFromReleaseBuild(); }
                foreach (var slotScreen in slotScreens)
                    if (slotScreen != null) { Unpack(slotScreen.gameObject, unpack); Object.DestroyImmediate(slotScreen.gameObject); }
            }
        }

        [MenuItem(_releasePreviewMenu, false, 0)]
        private static void ToggleReleasePreview()
        {
            bool next = !TestFeatureGate.IsReleasePreview;
            TestFeatureGate.SetReleasePreview(next);
            Menu.SetChecked(_releasePreviewMenu, next);
            Debug.Log(next
                ? "출시 빌드 미리보기를 켰습니다. 다음 Play부터 테스트 기능을 끄고 출시 저장 모드로 실행합니다."
                : "출시 빌드 미리보기를 껐습니다. 다음 Play부터 테스트 기능을 사용합니다.");
        }

        [MenuItem(_releasePreviewMenu, true)]
        private static bool CanToggleReleasePreview()
        {
            Menu.SetChecked(_releasePreviewMenu, TestFeatureGate.IsReleasePreview);
            // Play 중 전환은 이미 생성·제거된 테스트 객체와 저장 모드가 섞이므로 막습니다.
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // Play 중에는 프리팹 연결 해제를 할 수 없고 사본만 정리하면 되므로 실제 빌드에서만 해제합니다.
        private static void Unpack(GameObject target, bool unpack)
        {
            if (!unpack) return;
            GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (instance != null)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }
}
#endif
