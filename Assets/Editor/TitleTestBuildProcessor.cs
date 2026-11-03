#if UNITY_EDITOR
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.EditorTools
{
    // 출시 장면에 테스트 기능의 객체·문구·연결이 포함되지 않게 빌드 사본에서 제거합니다.
    public sealed class TitleTestBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || (report.summary.options & UnityEditor.BuildOptions.Development) != 0) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var manualScreens = root.GetComponentsInChildren<ManualSaveHost>(true);
                var slotScreens = root.GetComponentsInChildren<SaveSlotScreen>(true);
                foreach (var selection in root.GetComponentsInChildren<TitleSaveSelection>(true))
                {
                    Unpack(selection.gameObject);
                    selection.StripFromReleaseBuild();
                }
                foreach (var manualScreen in manualScreens)
                    if (manualScreen != null) { Unpack(manualScreen.gameObject); manualScreen.StripFromReleaseBuild(); }
                foreach (var slotScreen in slotScreens)
                    if (slotScreen != null) { Unpack(slotScreen.gameObject); Object.DestroyImmediate(slotScreen.gameObject); }
            }
        }

        private static void Unpack(GameObject target)
        {
            GameObject instance = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (instance != null)
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }
}
#endif
