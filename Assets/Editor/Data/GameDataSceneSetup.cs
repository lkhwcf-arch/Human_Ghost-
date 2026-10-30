using HumanGhost.Data.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HumanGhost.Editor.Data
{
    public static class GameDataSceneSetup
    {
        [MenuItem("인귀/데이터/현재 씬에 초기화 연결")]
        public static void ConfigureCurrentScene()
        {
            GameDataAssets assets = GameDataAssetBuilder.Refresh();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameDataInitializer initializer = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                initializer = root.GetComponentInChildren<GameDataInitializer>(true);
                if (initializer != null) break;
            }
            if (initializer == null)
            {
                var owner = new GameObject("데이터 초기화");
                Undo.RegisterCreatedObjectUndo(owner, "데이터 초기화 객체 추가");
                initializer = Undo.AddComponent<GameDataInitializer>(owner);
            }
            var serialized = new SerializedObject(initializer);
            serialized.FindProperty("_assets").objectReferenceValue = assets;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = initializer.gameObject;
            Debug.Log("현재 씬에 데이터 초기화 연결을 구성했습니다. 씬을 저장해 주세요.");
        }
    }
}
