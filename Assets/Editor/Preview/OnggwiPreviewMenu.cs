using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    // 수치·AI·보상 공급 전의 아트 확인 전용. 운영 몬스터 목록에 등록하지 않습니다.
    internal static class OnggwiPreviewMenu
    {
        private const string _prefabPath = "Assets/2.Model/Prefabs/MOP/OnggwiPreview.prefab";
        private static GameObject _instance;
        private static GameplayPreview Find() => UnityEngine.Object.FindFirstObjectByType<GameplayPreview>();

        [MenuItem("인귀/테스트/옹귀 애니메이션/생성", true)]
        private static bool CanSpawn() => EditorApplication.isPlaying && !EditorApplication.isPaused && Find() != null && Find().CanEditorSpawnHitDummy;

        [MenuItem("인귀/테스트/옹귀 애니메이션/생성")]
        private static void Spawn()
        {
            if (!CanSpawn()) return;
            var game = Find();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (prefab == null) throw new InvalidOperationException("옹귀 미리보기 프리팹을 연결해 주세요.");
            Vector3 position = game.Player.transform.position + Vector3.right * (game.Player.Actor.Facing * 2f);
            var floor = Physics2D.RaycastAll(position + Vector3.up * 3f, Vector2.down, 6f)
                .FirstOrDefault(hit => !hit.collider.isTrigger &&
                    hit.collider.GetComponentInParent<Adapters.PlayerRuntime>() == null &&
                    hit.collider.GetComponentInParent<Adapters.MonsterRuntime>() == null);
            if (floor.collider == null) { Debug.LogWarning("옹귀를 놓을 바닥이 없습니다."); return; }
            Remove();
            var scope = new GameObject("OnggwiAnimationTest");
            scope.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(scope, game.gameObject.scene);
            try
            {
                UnityEngine.Object.Instantiate(prefab, scope.transform);
                position.y = floor.point.y;
                scope.transform.position = position;
                _instance = scope;
                scope.SetActive(true);
            }
            catch { UnityEngine.Object.Destroy(scope); throw; }
        }

        [MenuItem("인귀/테스트/옹귀 애니메이션/대기")]
        private static void Idle() => Play("Idle");
        [MenuItem("인귀/테스트/옹귀 애니메이션/걷기")]
        private static void Walk() => Play("Walk");
        [MenuItem("인귀/테스트/옹귀 애니메이션/피격")]
        private static void Hit() => Play("Hit");
        [MenuItem("인귀/테스트/옹귀 애니메이션/사망")]
        private static void Dead() => Play("Dead");
        [MenuItem("인귀/테스트/옹귀 애니메이션/준비")]
        private static void Ready() => Play("Ready");
        private static void Play(string state)
        {
            if (EditorApplication.isPlaying && _instance != null)
                _instance.GetComponentInChildren<Animator>().Play(state, 0, 0f);
        }
        [MenuItem("인귀/테스트/옹귀 애니메이션/제거")]
        private static void Remove()
        {
            if (_instance != null) { _instance.SetActive(false); UnityEngine.Object.Destroy(_instance); }
            _instance = null;
        }
    }
}
