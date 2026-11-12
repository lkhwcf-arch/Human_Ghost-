using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    // 테스트 전용: Play 중 카메라 확대·위치와 플레이어 공격 판정 길이·높이를 슬라이더로 바꿉니다. 에셋·씬 값은 바꾸지 않습니다.
    internal sealed class PreviewCameraZoomWindow : EditorWindow
    {
        [MenuItem("인귀/테스트/카메라 포커스 배율 조절")]
        [MenuItem("인귀/테스트/카메라 위치 조절")]
        [MenuItem("인귀/테스트/플레이어 공격 판정 크기 조절")]
        private static void Open() => GetWindow<PreviewCameraZoomWindow>("테스트 조절");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 중 게임 맵에서 사용할 수 있습니다. 다음 Play 시작 시 배율은 1, 위치 이동은 0으로 초기화됩니다.", MessageType.Info);
                return;
            }
            DrawCamera(FindAnyObjectByType<PreviewFollowCamera>());
            EditorGUILayout.Space();
            DrawAttack(FindAnyObjectByType<GameplayPreview>());
        }

        private static void DrawCamera(PreviewFollowCamera camera)
        {
            EditorGUILayout.LabelField("카메라 포커스", EditorStyles.boldLabel);
            if (camera == null) { EditorGUILayout.HelpBox("추적 카메라가 없습니다.", MessageType.None); return; }
            EditorGUILayout.LabelField("1보다 크면 확대, 작으면 축소입니다.");
            float scale = EditorGUILayout.Slider("배율", PreviewFollowCamera.TestZoomScale, PreviewFollowCamera.MinTestZoom, PreviewFollowCamera.MaxTestZoom);
            if (!Mathf.Approximately(scale, PreviewFollowCamera.TestZoomScale)) camera.SetTestZoom(scale);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("-0.1")) camera.SetTestZoom(PreviewFollowCamera.TestZoomScale - 0.1f);
                if (GUILayout.Button("1로 되돌리기")) camera.SetTestZoom(1f);
                if (GUILayout.Button("+0.1")) camera.SetTestZoom(PreviewFollowCamera.TestZoomScale + 0.1f);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카메라 위치", EditorStyles.boldLabel);
            Vector2 offset = EditorGUILayout.Vector2Field("기준 위치에서 이동", PreviewFollowCamera.TestPositionOffset);
            if (offset != PreviewFollowCamera.TestPositionOffset) camera.SetTestPositionOffset(offset);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("← 0.1")) camera.SetTestPositionOffset(PreviewFollowCamera.TestPositionOffset + Vector2.left * 0.1f);
                if (GUILayout.Button("→ 0.1")) camera.SetTestPositionOffset(PreviewFollowCamera.TestPositionOffset + Vector2.right * 0.1f);
                if (GUILayout.Button("↑ 0.1")) camera.SetTestPositionOffset(PreviewFollowCamera.TestPositionOffset + Vector2.up * 0.1f);
                if (GUILayout.Button("↓ 0.1")) camera.SetTestPositionOffset(PreviewFollowCamera.TestPositionOffset + Vector2.down * 0.1f);
            }
            if (GUILayout.Button("위치 이동 0으로")) camera.SetTestPositionOffset(Vector2.zero);
            Vector3 position = camera.transform.position;
            EditorGUILayout.LabelField("현재 카메라 위치", $"X {position.x:0.###} / Y {position.y:0.###}");
            EditorGUILayout.HelpBox("X는 좌우, Y는 상하 이동입니다. 0이면 기본 구도로 돌아갑니다. 테스트 위치는 저장하지 않습니다.", MessageType.None);
            EditorGUILayout.LabelField("스테이지 기본 크기", camera.BaseSize.ToString("0.###"));
            EditorGUILayout.LabelField("현재 화면 크기", camera.GetComponent<Camera>().orthographicSize.ToString("0.###"));
            EditorGUILayout.HelpBox("마음에 드는 화면 크기는 StageContentSet의 CameraSize에 직접 넣어야 저장됩니다.", MessageType.None);
        }

        private static void DrawAttack(GameplayPreview preview)
        {
            EditorGUILayout.LabelField("플레이어 공격 판정", EditorStyles.boldLabel);
            if (preview == null) { EditorGUILayout.HelpBox("게임 확인 장면이 없습니다.", MessageType.None); return; }
            EditorGUILayout.LabelField("길이는 플레이어 쪽 끝을 고정하고 앞으로만 늘어납니다. 높이는 위아래로 같이 늘어납니다.", EditorStyles.wordWrappedLabel);
            float length = EditorGUILayout.Slider("길이 배율", GameplayPreview.AttackLengthScale, GameplayPreview.MinAttackScale, GameplayPreview.MaxAttackScale);
            float height = EditorGUILayout.Slider("높이 배율", GameplayPreview.AttackHeightScale, GameplayPreview.MinAttackScale, GameplayPreview.MaxAttackScale);
            if (!Mathf.Approximately(length, GameplayPreview.AttackLengthScale) || !Mathf.Approximately(height, GameplayPreview.AttackHeightScale))
                GameplayPreview.SetTestAttackScale(length, height);
            if (GUILayout.Button("1로 되돌리기")) GameplayPreview.SetTestAttackScale(1f, 1f);
            Vector2 size = preview.AttackWindowWorldSize;
            EditorGUILayout.LabelField("현재 판정 크기 (월드)", $"길이 {size.x:0.###} × 높이 {size.y:0.###}");
            EditorGUILayout.HelpBox("판정은 공격 판정 창이 열린 동안 반투명 주황 사각형으로 보입니다. 정한 값은 씬의 PreviewAttackWindow 크기에 직접 넣어야 저장됩니다.", MessageType.None);
        }
    }
}
