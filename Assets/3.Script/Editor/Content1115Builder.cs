#if UNITY_EDITOR
using System;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 공급 원본/임포터를 보존하고 셀 단위 파생 Sprite와 클립만 생성합니다.
    public static class Content1115Builder
    {
        private const string _artFolder = "Assets/5.Animation/Characters/Onggwi";
        private const string _effectFolder = "Assets/5.Animation/Effects/NewHit";

        [MenuItem("Human Ghost/Animation/Rebuild 1115 Supplied Artwork")]
        public static void RebuildArtwork()
        {
            EnsureFolder(_artFolder); EnsureFolder(_effectFolder);
            BuildClip(_artFolder, "Onggwi_Idle", Cells("Onggui_Idle", 3, 2, 5), 10f, true);
            BuildClip(_artFolder, "Onggwi_Walk", Cells("Onggui_Walk", 3, 3, 9), 10f, true);
            BuildClip(_artFolder, "Onggwi_Hit", Cells("Onggui_Hit", 3, 2, 5), 10f, false);
            BuildClip(_artFolder, "Onggwi_Dead", Cells("Onggui_Dead", 4, 4, 13), 10f, false);
            BuildClip(_artFolder, "Onggwi_Ready", Cells("Onggui", 1, 1, 1), 10f, false);
            BuildClip(_artFolder, "Onggwi_Stone", Cells("Stone", 1, 1, 1), 10f, false);
            var hits = Grid("Assets/4.Sprite/newHitEffect.png", _effectFolder, "NewHit", 3, 2, 6);
            AnimationClip hit = BuildClip(_effectFolder, "NewHit", hits, 20f, false);
            string path = "Assets/2.Model/Prefabs/Effects/EnemyHitImpact.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var effect = new SerializedObject(root.GetComponent<SpriteClipEffect>());
                effect.FindProperty("_clip").objectReferenceValue = hit;
                effect.FindProperty("_variants").arraySize = 0;
                effect.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<SpriteRenderer>().sprite = hits[0];
                root.transform.localScale = new Vector3(.6f, .6f, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var content = new SerializedObject(AssetDatabase.LoadAssetAtPath<StageContentSet>("Assets/2.Model/Prefabs/Config/StageContentSet.asset"));
            string[] fields = { "_fieldFire", "_fieldIceLeft", "_fieldIceMiddle", "_fieldIceRight" };
            string[] files = { "FieldFire_Debris", "FieldIce_Left", "FieldIce_Middle", "FieldIce_Right" };
            for (int i = 0; i < fields.Length; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/4.Sprite/Map/FieldObject/" + files[i] + ".png");
                if (sprite == null) throw new InvalidOperationException("필드 그림이 없습니다: " + files[i]);
                content.FindProperty(fields[i]).objectReferenceValue = sprite;
            }
            content.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(content.targetObject); AssetDatabase.SaveAssets();
        }

        private static Sprite[] Cells(string name, int columns, int rows, int count)
            => Grid("Assets/4.Sprite/OngGwi/" + name + ".png", _artFolder, name, columns, rows, count);

        private static Sprite[] Grid(string path, string folder, string name, int columns, int rows, int count)
        {
            EnsureFolder(folder + "/Sprites");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width % columns != 0 || texture.height % rows != 0)
                throw new InvalidOperationException("공급 그림의 셀 규격이 다릅니다: " + path);
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                string target = folder + "/Sprites/" + name + "_" + i.ToString("D2") + ".asset";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                if (sprite == null)
                {
                    int width = texture.width / columns, height = texture.height / rows;
                    sprite = Sprite.Create(texture, new Rect((i % columns) * width, (rows - 1 - i / columns) * height, width, height),
                        new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = name + "_" + i.ToString("D2"); AssetDatabase.CreateAsset(sprite, target);
                }
                result[i] = sprite;
            }
            return result;
        }

        private static AnimationClip BuildClip(string folder, string name, Sprite[] sprites, float fps, bool loop)
        {
            string path = folder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.frameRate = fps;
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[Math.Min(i, sprites.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            settings.startTime = 0f; settings.stopTime = sprites.Length / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings); clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            EditorUtility.SetDirty(clip); return clip;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/'); string parent = folder.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
#endif
