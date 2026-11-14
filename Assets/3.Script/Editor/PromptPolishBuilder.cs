#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    public static class PromptPolishBuilder
    {
        [MenuItem("Human Ghost/UI/Rebuild Interaction Prompt Backplate")]
        public static void RebuildPrompt()
        {
            const string folder = "Assets/2.Model/PromptStyle";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/2.Model", "PromptStyle");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/White.asset");
            if (texture == null)
            {
                texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "PromptWhite", filterMode = FilterMode.Point };
                texture.SetPixel(0, 0, Color.white); texture.Apply(); AssetDatabase.CreateAsset(texture, folder + "/White.asset");
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/Plate.asset");
            if (sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
                sprite.name = "PromptPlate"; AssetDatabase.CreateAsset(sprite, folder + "/Plate.asset");
            }
            const string path = "Assets/2.Model/Prefabs/UI/InteractPrompt.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var label = root.GetComponent<TextMesh>(); label.characterSize = .034f; label.fontStyle = FontStyle.Bold;
                label.color = new Color(1f, .97f, .9f, 1f);
                var textRenderer = root.GetComponent<Renderer>();
                var material = root.transform.Find("KeyIcon").GetComponent<SpriteRenderer>().sharedMaterial;
                Plate(root.transform, "Border", sprite, material, new Color(.5f, .4f, .24f, .95f), textRenderer.sortingOrder - 2);
                Plate(root.transform, "Backdrop", sprite, material, new Color(.025f, .02f, .016f, .9f), textRenderer.sortingOrder - 1);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static void Plate(Transform parent, string name, Sprite sprite, Material material, Color color, int order)
        {
            Transform t = parent.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            var renderer = t.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = t.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.color = color; renderer.sortingOrder = order;
            t.localPosition = new Vector3(0f, 0f, .03f);
        }
    }
}
#endif
