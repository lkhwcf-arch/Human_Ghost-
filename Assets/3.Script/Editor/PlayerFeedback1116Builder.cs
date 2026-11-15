#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 공급 PNG/임포터와 기존 클립 GUID는 보존하고 공통 셀 기준의 표시만 갱신합니다.
    public static class PlayerFeedback1116Builder
    {
        private const string _folder = "Assets/5.Animation/Characters/Player/Feedback1116";
        private const string _clips = "Assets/5.Animation/Characters/Player/Clips/";

        [MenuItem("Human Ghost/Animation/Rebuild Player Hit and Jump 1116")]
        public static void Rebuild()
        {
            EnsureFolder(_folder + "/Sprites");
            Sprite[] hit = Cells("Player_Hit_Edit", 3, 2, 4);
            Sprite[] jump = Cells("Player_Jump_Edit", 3, 3, 7);
            Clip("Player_10003_Hit", hit, 0, 4);
            // 공중에는0~4만 사용합니다. 마지막 웅크림5~6은 실제 Land 상태에서 재생합니다.
            Clip("Player_10013_Jump", jump, 0, 5);
            Clip("Player_10011_Land", jump, 5, 2);
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] Cells(string name, int columns, int rows, int count)
        {
            string path = "Assets/4.Sprite/Player/" + name + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != columns * 256 || texture.height != rows * 256)
                throw new InvalidOperationException("플레이어 공급 그림은256px 셀 형식이어야 합니다: " + path);
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string target = _folder + "/Sprites/" + name + "_" + i.ToString("D2") + ".asset";
                var expected = new Rect(i % columns * 256, (rows - 1 - i / columns) * 256, 256, 256);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                if (sprite == null)
                {
                    sprite = Sprite.Create(texture, expected, new Vector2(.5f,.5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = name + "_" + i.ToString("D2");
                    AssetDatabase.CreateAsset(sprite, target);
                }
                if (sprite.texture != texture || sprite.rect != expected || sprite.pivot != new Vector2(128f,128f))
                    throw new InvalidOperationException("기존 파생 Sprite의 셀 기준을 확인하세요: " + target);
                result[i] = sprite;
            }
            return result;
        }

        private static void Clip(string name, Sprite[] sprites, int start, int count)
        {
            string path = _clips + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) throw new InvalidOperationException("기존 플레이어 클립이 없습니다: " + path);
            clip.ClearCurves(); clip.frameRate = 10f;
            var frames = new ObjectReferenceKeyframe[count];
            for (int i = 0; i < count; i++)
                frames[i] = new ObjectReferenceKeyframe { time = i / 10f, value = sprites[start + i] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false; settings.startTime = 0f; settings.stopTime = count / 10f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = WrapMode.ClampForever;
            EditorUtility.SetDirty(clip);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0,slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
        }
    }
}
#endif
