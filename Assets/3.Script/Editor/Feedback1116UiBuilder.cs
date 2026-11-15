#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using HumanGhost.Gameplay.Presentation;

namespace HumanGhost.EditorTools
{
    public static class Feedback1116UiBuilder
    {
        [MenuItem("Human Ghost/UI/Rebuild Feedback 1116 HUD and Audio")]
        public static void Rebuild()
        {
            const string hudPath = "Assets/2.Model/Prefabs/UI/HudScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                var hud = root.GetComponent<HudScreen>(); var serialized = new SerializedObject(hud);
                var reference = (Text)serialized.FindProperty("_herbs").objectReferenceValue;
                Transform previous = root.transform.Find("Currencies");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var panel = new GameObject("Currencies", typeof(RectTransform)); panel.SetActive(false);
                var area = (RectTransform)panel.transform; area.SetParent(root.transform, false);
                area.anchorMin = area.anchorMax = new Vector2(0f, 1f); area.pivot = new Vector2(0f, 1f);
                area.anchoredPosition = new Vector2(180f, -150f); area.sizeDelta = new Vector2(420f, 48f);
                serialized.FindProperty("_soul").objectReferenceValue = Currency(area, "Hon", "Hon", 0f, reference);
                serialized.FindProperty("_coins").objectReferenceValue = Currency(area, "Yeopjeon", "Yeopjeon", 210f, reference);
                serialized.ApplyModifiedPropertiesWithoutUndo(); panel.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, hudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string audioPath = "Assets/2.Model/Prefabs/Config/GameAudio.prefab";
            root = PrefabUtility.LoadPrefabContents(audioPath);
            try
            {
                var audio = root.GetComponent<HumanGhost.Gameplay.Adapters.GameAudio>(); var serialized = new SerializedObject(audio);
                var entries = serialized.FindProperty("_clips"); int index = -1;
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("_key").stringValue == "SFX_PARRY") index = i;
                if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_key").stringValue = "SFX_PARRY";
                entry.FindPropertyRelative("_clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/8.Audio/SFX/Player_Parry.wav");
                // 원본 Sound 표에는 아직 이 음원의 ID가 없습니다. 기존 프리팹 직접 클립 경로를 재사용합니다.
                entry.FindPropertyRelative("_soundId").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, audioPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string textboxPath = "Assets/2.Model/Prefabs/UI/TextboxScreen.prefab";
            root = PrefabUtility.LoadPrefabContents(textboxPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<TextboxScreen>());
                var banner = (GameObject)serialized.FindProperty("_objective").objectReferenceValue;
                var rect = (RectTransform)banner.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f); rect.pivot = new Vector2(.5f,1f);
                rect.anchoredPosition = new Vector2(0f,-190f); rect.sizeDelta = new Vector2(1080f,100f);
                var label = (Text)serialized.FindProperty("_objectiveText").objectReferenceValue;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 22; label.resizeTextMaxSize = 28;
                label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
                PrefabUtility.SaveAsPrefabAsset(root,textboxPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var content = AssetDatabase.LoadAssetAtPath<HumanGhost.Gameplay.Adapters.StageContentSet>("Assets/2.Model/Prefabs/Config/StageContentSet.asset");
            string mapPath = AssetDatabase.GetAssetPath(content.GetStage(8004).Map);
            root = PrefabUtility.LoadPrefabContents(mapPath);
            try
            {
                // 같은 깊이·같은 정렬값의 겹친 소품을 명시적인 순서로 분리합니다.
                root.transform.Find("Object/LanternPost").GetComponent<SpriteRenderer>().sortingOrder = 32;
                root.transform.Find("Object/WarDrum").GetComponent<SpriteRenderer>().sortingOrder = 33;
                PrefabUtility.SaveAsPrefabAsset(root,mapPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static Text Currency(RectTransform parent, string name, string icon, float x, Text reference)
        {
            var image = new GameObject(name + "Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var r = image.rectTransform; r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0f, .5f);
            r.anchoredPosition = new Vector2(x + 20f, 0f); r.sizeDelta = new Vector2(38f, 38f);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/4.Sprite/UI_Temp/Shrine/ShrineUI_Icon_" + icon + ".png");
            image.preserveAspect = true; image.raycastTarget = false;
            if (image.sprite == null) throw new System.InvalidOperationException("재화 아이콘 연결이 없습니다: " + name);
            var text = new GameObject(name + "Amount", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            r = text.rectTransform; r.SetParent(parent, false); r.anchorMin = r.anchorMax = new Vector2(0f, .5f);
            r.pivot = new Vector2(0f, .5f); r.anchoredPosition = new Vector2(x + 48f, 0f); r.sizeDelta = new Vector2(150f, 44f);
            text.font = reference.font; text.fontSize = 28; text.alignment = TextAnchor.MiddleLeft; text.color = Color.white;
            text.text = "0"; text.raycastTarget = false; text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = 28;
            return text;
        }
    }
}
#endif
