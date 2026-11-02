using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanGhost.Gameplay.Presentation;
using HumanGhost.Data.Csv;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HumanGhost.Editor.Data
{
    // 파일명 키 수집과 프리팹 연결 검사는 에디터에서 끝내고 실행 중에는 참조만 사용합니다.
    internal static class HealthDisplayPrefabBuilder
    {
        private const string _prefabFolder = "Assets/2.Model/Prefabs/UI";
        private const string _assetPath = "Assets/Data/HealthDisplayPrefabs.asset";
        internal static string PrefabFolder => _prefabFolder;

        [MenuItem("인귀/데이터/체력 표시 프리팹 목록 갱신")]
        internal static void Refresh()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var views = new List<HealthDisplay>();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal))
            {
                string key = Path.GetFileNameWithoutExtension(path);
                if (!keys.Add(key)) throw new BuildFailedException($"UI 프리팹 파일명이 중복됩니다: {key}");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = prefab.GetComponent<HealthDisplay>();
                if (view == null) continue;
                var fill = new SerializedObject(view).FindProperty("_fill").objectReferenceValue as RectTransform;
                if (!(view.transform is RectTransform) || fill == null || fill == view.transform || !fill.IsChildOf(view.transform)
                    || !view.enabled || !prefab.activeSelf)
                    throw new BuildFailedException($"체력 표시 프리팹의 루트·자식 막대·활성 설정을 확인해주세요: {path}");
                views.Add(view);
            }
            // 숫자·시트 규칙은 배포 도구가 담당합니다. 여기서는 Unity 컴포넌트 참조만 확인합니다.
            const string configPath = "Assets/Data/Csv/HealthDisplayConfig.csv";
            foreach (CsvRow row in new CsvParser().Read(File.ReadAllText(configPath), configPath).Rows)
                if (!views.Any(view => string.Equals(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(view)), row.Get("PrefabKey"), StringComparison.Ordinal)))
                    throw new BuildFailedException($"체력 표시 설정의 프리팹 루트에 HealthDisplay가 없습니다: {row.Get("PrefabKey")}");
            var asset = AssetDatabase.LoadAssetAtPath<HealthDisplayPrefabs>(_assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HealthDisplayPrefabs>();
                AssetDatabase.CreateAsset(asset, _assetPath);
            }
            var serialized = new SerializedObject(asset);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = views.Count;
            for (int index = 0; index < views.Count; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_key").stringValue = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(views[index]));
                entry.FindPropertyRelative("_prefab").objectReferenceValue = views[index];
            }
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
        }
    }
}
