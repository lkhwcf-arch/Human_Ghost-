using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    internal static class PreviewStageTravelMenu
    {
        private static GameplayPreview Find() => Object.FindAnyObjectByType<GameplayPreview>();

        [MenuItem("인귀/테스트/장부방 입구로 이동", true)]
        [MenuItem("인귀/테스트/보스방 입구로 이동", true)]
        private static bool CanTravel() => EditorApplication.isPlaying && !EditorApplication.isPaused && Find() != null && Find().CanEditorTravel;

        [MenuItem("인귀/테스트/장부방 입구로 이동")]
        private static void Ledger() => Find()?.TravelToTestRoom(false);

        [MenuItem("인귀/테스트/보스방 입구로 이동")]
        private static void Boss() => Find()?.TravelToTestRoom(true);
    }
}
