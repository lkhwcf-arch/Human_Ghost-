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

        [MenuItem("인귀/테스트/처형 테스트/허수귀 생성", true)]
        [MenuItem("인귀/테스트/처형 테스트/지게귀 생성", true)]
        private static bool CanSpawnExecution() => CanSpawn();

        [MenuItem("인귀/테스트/처형 테스트/허수귀 생성")]
        private static void SpawnExecutionHusugwi() => Find()?.SpawnEditorExecutionDummy(3001);

        [MenuItem("인귀/테스트/처형 테스트/지게귀 생성")]
        private static void SpawnExecutionJigegwi() => Find()?.SpawnEditorExecutionDummy(3002);

        [MenuItem("인귀/테스트/처형 테스트/모두 제거", true)]
        private static bool CanRemoveExecution() => EditorApplication.isPlaying && Find() != null && Find().HasEditorExecutionDummies;

        [MenuItem("인귀/테스트/처형 테스트/모두 제거")]
        private static void RemoveExecution() => Find()?.ClearEditorExecutionDummies();
    }
}
