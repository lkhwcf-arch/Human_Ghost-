using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    [CustomEditor(typeof(MonsterRuntime))]
    [CanEditMultipleObjects]
    public sealed class MonsterRuntimeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"), new GUIContent("스크립트"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_monsterId"), new GUIContent("몬스터 데이터 ID"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(MonsterPreviewLabel))]
    [CanEditMultipleObjects]
    public sealed class MonsterPreviewLabelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"), new GUIContent("스크립트"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_runtime"), new GUIContent("일반몹 데이터 수신"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_label"), new GUIContent("한글 이름표"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_displayName"), new GUIContent("표시 이름"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
