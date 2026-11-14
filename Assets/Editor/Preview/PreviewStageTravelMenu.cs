using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    internal static class PreviewStageTravelMenu
    {
        private static GameplayPreview Find() => Object.FindAnyObjectByType<GameplayPreview>();
        // 1114 C6: 갈림길 번호는 StageID 8007입니다. 테스트 이동 대상만 지정하고 도착 앵커는 데이터에서 찾습니다.
        private const int GalimgilStage = 8007;

        [MenuItem("인귀/테스트/장부방 입구로 이동", true)]
        [MenuItem("인귀/테스트/보스방 입구로 이동", true)]
        [MenuItem("인귀/테스트/갈림길 입구로 이동", true)]
        private static bool CanTravel() => EditorApplication.isPlaying && !EditorApplication.isPaused && Find() != null && Find().CanEditorTravel;

        [MenuItem("인귀/테스트/장부방 입구로 이동")]
        private static void Ledger() => Find()?.TravelToTestRoom(false);

        [MenuItem("인귀/테스트/보스방 입구로 이동")]
        private static void Boss() => Find()?.TravelToTestRoom(true);

        [MenuItem("인귀/테스트/갈림길 입구로 이동")]
        private static void Galimgil() => Find()?.TravelToStageEntrance(GalimgilStage);
    }
}
