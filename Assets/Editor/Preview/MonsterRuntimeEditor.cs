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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_patterns"), new GUIContent("패턴 설정"));
            serializedObject.ApplyModifiedProperties();
        }
    }

    // 기획·개발이 패턴 값을 한글 이름으로 수정하도록 표시합니다. 시간 ms, 거리 tile_x100, 속도 tile_x100/초.
    [CustomEditor(typeof(MonsterPatternSettings))]
    public sealed class MonsterPatternSettingsEditor : UnityEditor.Editor
    {
        private static readonly (string Field, string Label)[] _patternFields =
        {
            ("_key", "상태 (Attack1/Skill1)"), ("_range", "사용 거리"), ("_windup", "선딜 (예비 동작)"), ("_active", "판정 (돌진은 최대 돌진 시간)"),
            ("_recovery", "후딜"), ("_extraDamage", "추가 데미지"), ("_bindTime", "잡기 구속 상한 (0=연타로만 해제)"), ("_bindPresses", "구속 해제 공격 연타 횟수"),
            ("_chargeSpeed", "돌진 속도"), ("_wallStun", "벽 충돌 허점")
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty patterns = serializedObject.FindProperty("_patterns");
            EditorGUILayout.LabelField("패턴 (잡기: 연타 횟수>0, 돌진: 돌진 속도>0)", EditorStyles.boldLabel);
            patterns.arraySize = EditorGUILayout.IntField("패턴 수", patterns.arraySize);
            for (int i = 0; i < patterns.arraySize; i++)
            {
                SerializedProperty pattern = patterns.GetArrayElementAtIndex(i);
                EditorGUILayout.LabelField("패턴 " + (i + 1));
                using (new EditorGUI.IndentLevelScope())
                    foreach ((string field, string label) in _patternFields)
                        EditorGUILayout.PropertyField(pattern.FindPropertyRelative(field), new GUIContent(label));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hitStun"), new GUIContent("피격 경직"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_critHitStun"), new GUIContent("치명타 피격 경직"));
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
