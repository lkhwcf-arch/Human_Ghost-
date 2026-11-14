#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    public static class EnemyGroundingBaker
    {
        [MenuItem("Human Ghost/Animation/Bake Enemy Sprite Ground Contacts")]
        public static void Rebuild()
        {
            var pixels = new Dictionary<Texture2D, Color32[]>();
            foreach (string name in new[] { "BossGameplay", "HusugwiGameplay", "JigegwiGameplay" })
            {
                string path = "Assets/2.Model/Prefabs/MOP/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponentInChildren<PreviewStateSprite>(true);
                    var data = new SerializedObject(view);
                    var sprites = new HashSet<Sprite>();
                    var fallback = data.FindProperty("_fallback").objectReferenceValue as Sprite;
                    if (fallback != null) sprites.Add(fallback);
                    SerializedProperty entries = data.FindProperty("_animations");
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var clip = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_clip").objectReferenceValue as AnimationClip;
                        if (clip == null) continue;
                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                                if (key.value is Sprite sprite) sprites.Add(sprite);
                    }
                    var contacts = data.FindProperty("_groundContacts");
                    contacts.arraySize = sprites.Count;
                    int row = 0;
                    foreach (var sprite in sprites.OrderBy(s => AssetDatabase.GetAssetPath(s)).ThenBy(s => s.name))
                    {
                        var entry = contacts.GetArrayElementAtIndex(row++);
                        entry.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
                        entry.FindPropertyRelative("_bottom").floatValue = Bottom(sprite, pixels);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        private static float Bottom(Sprite sprite, Dictionary<Texture2D, Color32[]> pixels)
        {
            Texture2D source = sprite.texture;
            if (!pixels.TryGetValue(source, out Color32[] data))
            {
                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var previous = RenderTexture.active;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                try
                {
                    Graphics.Blit(source, rt); RenderTexture.active = rt;
                    copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply();
                    data = copy.GetPixels32(); pixels.Add(source, data);
                }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(copy); }
            }
            Rect rect = sprite.rect;
            for (int y = 0; y < (int)rect.height; y++)
                for (int x = 0; x < (int)rect.width; x++)
                    if (data[((int)rect.y + y) * source.width + (int)rect.x + x].a > 25)
                        return (y - sprite.pivot.y) / sprite.pixelsPerUnit;
            throw new System.InvalidOperationException("No visible ground pixels: " + sprite.name);
        }
    }
}
#endif
