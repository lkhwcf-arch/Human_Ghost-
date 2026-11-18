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
                        entry.FindPropertyRelative("_bottom").floatValue = name == "BossGameplay" ? BossFeetBottom(sprite, pixels) : Bottom(sprite, pixels);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        // 쇄문아비 1페이즈는 앞으로 짚은 팔(손톱)·몽둥이가 몸통보다 아래로 내려옵니다. 가장 아래 픽셀 대신
        // 몸통 중앙 열(가로 50~70%)의 가장 아래를 발 기준으로 씁니다. 공격으로 몸이 뜬 칸은 최저점보다 13px 넘게 올리지 않습니다.
        // [정수화 미적용 이유] 편집기 도구가 스프라이트 폭 비율로 발 위치를 굽는 값입니다. 실행 중 규칙에 쓰지 않습니다.
        private const float _bossFeetLeft = .5f, _bossFeetRight = .7f;
        private const int _bossFeetMaxLiftPixels = 13;
        private static float BossFeetBottom(Sprite sprite, Dictionary<Texture2D, Color32[]> pixels)
        {
            float lowest = Bottom(sprite, pixels);
            Color32[] data = pixels[sprite.texture];
            Rect rect = sprite.rect; int width = sprite.texture.width;
            int from = Mathf.FloorToInt(rect.width * _bossFeetLeft), to = Mathf.CeilToInt(rect.width * _bossFeetRight);
            for (int y = 0; y < (int)rect.height; y++)
                for (int x = from; x < to; x++)
                    if (data[((int)rect.y + y) * width + (int)rect.x + x].a > 25)
                        return Mathf.Min((y - sprite.pivot.y) / sprite.pixelsPerUnit, lowest + _bossFeetMaxLiftPixels / sprite.pixelsPerUnit);
            return lowest;
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
