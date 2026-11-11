using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    // 테스트 전용: Play 중 카메라 확대 배율을 슬라이더로 바꿉니다. 에셋·씬 값은 바꾸지 않습니다.
    internal sealed class PreviewCameraZoomWindow : EditorWindow
    {
        [MenuItem("인귀/테스트/카메라 포커스 배율 조절")]
        private static void Open() => GetWindow<PreviewCameraZoomWindow>("카메라 포커스 배율");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            PreviewFollowCamera camera = EditorApplication.isPlaying ? FindAnyObjectByType<PreviewFollowCamera>() : null;
            if (camera == null)
            {
                EditorGUILayout.HelpBox("Play 중 게임 맵에서 사용할 수 있습니다. 배율은 Play를 끝내면 1로 돌아갑니다.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("1보다 크면 확대, 작으면 축소입니다.");
            float scale = EditorGUILayout.Slider("배율", PreviewFollowCamera.TestZoomScale, PreviewFollowCamera.MinTestZoom, PreviewFollowCamera.MaxTestZoom);
            if (!Mathf.Approximately(scale, PreviewFollowCamera.TestZoomScale)) camera.SetTestZoom(scale);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("-0.1")) camera.SetTestZoom(PreviewFollowCamera.TestZoomScale - 0.1f);
                if (GUILayout.Button("1로 되돌리기")) camera.SetTestZoom(1f);
                if (GUILayout.Button("+0.1")) camera.SetTestZoom(PreviewFollowCamera.TestZoomScale + 0.1f);
            }
            float baseSize = camera.BaseSize;
            EditorGUILayout.LabelField("스테이지 기본 크기", baseSize.ToString("0.###"));
            EditorGUILayout.LabelField("현재 화면 크기", camera.GetComponent<Camera>().orthographicSize.ToString("0.###"));
            EditorGUILayout.HelpBox("마음에 드는 화면 크기는 StageContentSet의 CameraSize에 직접 넣어야 저장됩니다.", MessageType.None);
        }
    }
}
