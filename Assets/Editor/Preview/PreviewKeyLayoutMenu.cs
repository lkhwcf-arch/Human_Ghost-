using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Preview.Editor
{
    // Play 중에만 임시 오브젝트로 키 배치 시험을 켜고 끕니다. 씬에 저장하지 않으며 Play 종료 시 함께 사라집니다.
    internal static class PreviewKeyLayoutMenu
    {
        private const string _sourcePath = "Assets/10.Input/InputSystem_Actions.inputactions";
        private const string _objectName = "PreviewKeyLayoutB";

        [MenuItem("인귀/테스트/키 배치 B 켜기 (WASD·J공격·K점프·L구르기)", true)]
        private static bool CanEnable() => EditorApplication.isPlaying && Object.FindAnyObjectByType<PreviewKeyLayout>() == null;

        [MenuItem("인귀/테스트/키 배치 B 켜기 (WASD·J공격·K점프·L구르기)")]
        private static void Enable()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(_sourcePath);
            if (source == null) { Debug.LogError($"입력 에셋을 찾지 못했습니다: {_sourcePath}"); return; }
            var host = new GameObject(_objectName) { hideFlags = HideFlags.DontSave };
            host.AddComponent<PreviewKeyLayout>().Initialize(source, PreviewKeyLayout.LayoutB);
        }

        [MenuItem("인귀/테스트/키 배치 B 끄기", true)]
        private static bool CanDisable() => EditorApplication.isPlaying && Object.FindAnyObjectByType<PreviewKeyLayout>() != null;

        [MenuItem("인귀/테스트/키 배치 B 끄기")]
        private static void Disable()
        {
            foreach (PreviewKeyLayout layout in Object.FindObjectsByType<PreviewKeyLayout>(FindObjectsSortMode.None))
                Object.Destroy(layout.gameObject);
        }
    }
}
