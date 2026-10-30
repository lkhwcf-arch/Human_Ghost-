using System;
using System.Linq;
using HumanGhost.Data.Infrastructure;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanGhost.Editor.Data
{
    // 원본은 그대로 두고 빌드에 포함할 에셋 참조 목록만 갱신합니다.
    internal sealed class GameDataAssetBuilder : AssetPostprocessor, IPreprocessBuildWithReport
    {
        internal const string AssetPath = "Assets/Data/GameDataAssets.asset";
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void ScheduleRefresh() => EditorApplication.delayCall += RefreshWhenReady;

        private static void RefreshWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Refresh();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Any(path => path.StartsWith("Assets/Data/Csv/", StringComparison.Ordinal) || path.StartsWith("Assets/Data/Json/", StringComparison.Ordinal)))
                ScheduleRefresh();
        }

        public void OnPreprocessBuild(BuildReport report) => Refresh();

        [MenuItem("인귀/데이터/에셋 목록 갱신")]
        private static void RefreshFromMenu() => Refresh();

        internal static GameDataAssets Refresh()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameDataAssets>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GameDataAssets>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var serialized = new SerializedObject(asset);
            SetReferences(serialized.FindProperty("_csvFiles"), "Assets/Data/Csv");
            SetReferences(serialized.FindProperty("_jsonFiles"), "Assets/Data/Json");
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssets();
            return asset;
        }

        private static void SetReferences(SerializedProperty property, string folder)
        {
            TextAsset[] files = AssetDatabase.FindAssets("t:TextAsset", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<TextAsset>).ToArray();
            if (property.arraySize != files.Length) property.arraySize = files.Length;
            for (int index = 0; index < files.Length; index++)
                if (property.GetArrayElementAtIndex(index).objectReferenceValue != files[index])
                    property.GetArrayElementAtIndex(index).objectReferenceValue = files[index];
        }
    }
}
