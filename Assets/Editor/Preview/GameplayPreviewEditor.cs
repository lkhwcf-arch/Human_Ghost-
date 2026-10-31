using System.Linq;
using System.Reflection;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview.Editor
{
    // 확인 장면의 설정을 팀원이 읽기 쉬운 한글 이름으로 표시합니다.
    [CustomEditor(typeof(GameplayPreview))]
    [CanEditMultipleObjects]
    public sealed class GameplayPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property, new GUIContent("스크립트"), true);
                    continue;
                }
                FieldInfo field = typeof(GameplayPreview).GetField(property.name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                InspectorNameAttribute label = field?.GetCustomAttributes(typeof(InspectorNameAttribute), true)
                    .OfType<InspectorNameAttribute>().FirstOrDefault();
                string name = label == null ? property.displayName : label.displayName;
                EditorGUILayout.PropertyField(property, new GUIContent(name, property.tooltip), true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
