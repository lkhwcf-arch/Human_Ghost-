using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    internal static class PreviewHitDummyMenu
    {
        private static GameplayPreview Find() => Object.FindAnyObjectByType<GameplayPreview>();

        [MenuItem("인귀/테스트/이펙트 점검용 허수귀 생성", true)]
        private static bool CanSpawn() => EditorApplication.isPlaying && !EditorApplication.isPaused &&
            Find() != null && Find().CanEditorSpawnHitDummy;

        [MenuItem("인귀/테스트/이펙트 점검용 허수귀 생성")]
        private static void Spawn() => Find()?.SpawnEditorHitDummy();

        [MenuItem("인귀/테스트/이펙트 점검용 허수귀 제거", true)]
        private static bool CanRemove() => EditorApplication.isPlaying && Find() != null && Find().HasEditorHitDummy;

        [MenuItem("인귀/테스트/이펙트 점검용 허수귀 제거")]
        private static void Remove() => Find()?.ClearEditorHitDummy();
    }
}
