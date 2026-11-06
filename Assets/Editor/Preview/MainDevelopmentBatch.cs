using System;
using System.IO;
using System.Linq;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.EditorTools
{
    public static class MainDevelopmentBatch
    {
        [MenuItem("인귀/검사/메인 개발 발판·대사 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Play 종료·컴파일 완료 후 실행해 주세요.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("현재 씬의 저장하지 않은 변경을 먼저 확인해 주세요.");
            foreach (int number in new[] { 3, 4, 5, 7, 8, 10, 12 }) BindDropPolicy(number);
            NpcDialogue dialogue = BuildDialogue();
            string previous = SceneManager.GetActiveScene().path;
            foreach (string name in new[] { "MapDohwagolTest", "GameplayPreview" })
            {
                string path = "Assets/1.Scene/" + name + ".unity";
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (GameplayPreview gameplay in root.GetComponentsInChildren<GameplayPreview>(true))
                {
                    var data = new SerializedObject(gameplay);
                    data.FindProperty("_refugeeDialogue").objectReferenceValue = dialogue;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            AssetDatabase.SaveAssets();
            Debug.Log("메인 개발 발판·대사 연결 완료. 애니메이션은 별도 담당 작업을 사용합니다.");
        }

        private static void BindDropPolicy(int number)
        {
            string[] ids = AssetDatabase.FindAssets("Air_Platform_" + number + " t:Prefab");
            string path = ids.Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == "Air_Platform_" + number);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var policy = root.GetComponent<DropThroughPlatform>();
                if (policy == null) policy = root.AddComponent<DropThroughPlatform>();
                var data = new SerializedObject(policy);
                data.FindProperty("_allowed").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static NpcDialogue BuildDialogue()
        {
            const string path = "Assets/2.Model/Prefabs/Config/RefugeeDialogue.asset";
            var dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(path);
            if (dialogue == null) { dialogue = ScriptableObject.CreateInstance<NpcDialogue>(); AssetDatabase.CreateAsset(dialogue, path); }
            var data = new SerializedObject(dialogue);
            data.FindProperty("_speaker").stringValue = "부상당한 피난민";
            data.FindProperty("_objective").stringValue = "장부방을 살펴보자";
            string[] lines = {
                "허억… 허억… 사, 살려주시오…",
                "마을이… 마을이 온통 피로 물들었소…",
                "사람들이 비명을 지르더니… 하나둘 사라졌소.",
                "그것들은… 사람의 모습을 하고 있었지만, 사람이 아니었소…",
                "제발… 더 들어가지 마시오. 살아서 돌아온 자가 없소…"
            };
            var target = data.FindProperty("_lines");
            target.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) target.GetArrayElementAtIndex(i).stringValue = lines[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return dialogue;
        }
    }
}

