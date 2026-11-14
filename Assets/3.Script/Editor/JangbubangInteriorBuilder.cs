#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 기존 방의 지형·상호작용 표식을 보존하고 공급된 실내 표시만 조립합니다.
    public static class JangbubangInteriorBuilder
    {
        private const string _source = "Assets/4.Sprite/JangbuBangNew/1-2. 장부방/";
        private const string _folder = "Assets/5.Animation/Environment/Jangbubang";
        private const string _map = "Assets/2.Model/Prefabs/Map/Map_Jangbubang_K9.prefab";

        [MenuItem("Human Ghost/Environment/Rebuild Jangbubang Interior")]
        public static void Rebuild()
        {
            EnsureFolder(_folder + "/Sprites");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_source + "OBJ/Anim_Candle.png");
            if (texture == null || texture.width != 768 || texture.height != 768)
                throw new InvalidOperationException("장부방 촛불 시트는 256px 셀 3×3 형식이어야 합니다.");
            var keys = new ObjectReferenceKeyframe[9];
            for (int i = 0; i < keys.Length; i++)
            {
                string path = _folder + "/Sprites/Candle_" + i.ToString("D2") + ".asset";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    // 모든 셀의 금속 받침 하단은 y44입니다. 첫 셀의 번짐·불티로 피벗이 흔들리지 않게 합니다.
                    sprite = Sprite.Create(texture, new Rect(i % 3 * 256, (2 - i / 3) * 256, 256, 256),
                        new Vector2(.5f, 44f / 256f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = "Candle_" + i.ToString("D2"); AssetDatabase.CreateAsset(sprite, path);
                }
                keys[i] = new ObjectReferenceKeyframe { time = i / 10f, value = sprite };
            }
            string clipPath = _folder + "/CandleLoop.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip { name = "CandleLoop" }; AssetDatabase.CreateAsset(clip, clipPath); }
            clip.ClearCurves(); clip.frameRate = 10f; clip.wrapMode = WrapMode.Loop;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.startTime = 0f; settings.stopTime = .9f;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            string controllerPath = _folder + "/CandleLoop.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var state = controller.layers[0].stateMachine.AddState("CandleLoop"); state.motion = clip; state.writeDefaultValues = false;
                controller.layers[0].stateMachine.defaultState = state;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(_map);
            try
            {
                root.transform.Find("Background/Sky/Backdrop").GetComponent<SpriteRenderer>().enabled = false;
                root.transform.Find("Building").gameObject.SetActive(false);
                root.transform.Find("Object/ST01_JB_Candle").GetComponent<SpriteRenderer>().enabled = false;
                root.transform.Find("Object/ST01_JB_Mat").GetComponent<SpriteRenderer>().enabled = false;
                Transform interior = Child(root.transform, "InteriorNew");
                var oldRenderer = root.transform.Find("Object/ST01_JB_Desk").GetComponent<SpriteRenderer>();
                Material material = oldRenderer.sharedMaterial;
                var background = Display(interior, "InteriorBackdrop", Single("Background/BG_Dohwagol_LedgerRoom_Firelit.png"), material, 41);
                background.transform.localPosition = new Vector3(6.55f, 4.7648f, 0f); background.transform.localScale = new Vector3(.6f, .6f, 1f);
                Place(root.transform.Find("Object/ST01_JB_Desk").GetComponent<SpriteRenderer>(), Single("OBJ/Prop_02_LedgerDesk.png"), 6.5f, 2f, .6f, 45);
                Place(root.transform.Find("Object/ST01_JB_Chest").GetComponent<SpriteRenderer>(), Single("OBJ/Prop_03_WoodenChest.png"), 9.7f, 2f, .6f, 45);
                var shelf = Display(interior, "Bookshelf", Single("OBJ/Prop_04_Bookshelf.png"), material, 43);
                Place(shelf, shelf.sprite, 11.1f, 2f, .62f, 43);
                Candle(interior, "DeskCandle", (Sprite)keys[0].value, material, controller, 5.55f, 2.78f, .4f);
                Candle(interior, "ChestCandle", (Sprite)keys[0].value, material, controller, 9.7f, 2.9f, .4f);
                PrefabUtility.SaveAsPrefabAsset(root, _map);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void Candle(Transform parent, string name, Sprite sprite, Material material, RuntimeAnimatorController controller, float x, float y, float scale)
        {
            var renderer = Display(parent, name, sprite, material, 46);
            renderer.transform.localPosition = new Vector3(x, y, 0f); renderer.transform.localScale = Vector3.one * scale;
            var animator = renderer.GetComponent<Animator>(); if (animator == null) animator = renderer.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        }
        private static void Place(SpriteRenderer renderer, Sprite sprite, float x, float floor, float scale, int order)
        {
            renderer.sprite = sprite; renderer.enabled = true; renderer.drawMode = SpriteDrawMode.Simple; renderer.color = Color.white; renderer.sortingOrder = order;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.localPosition = new Vector3(x - sprite.bounds.center.x * scale, floor - sprite.bounds.min.y * scale, 0f);
        }
        private static SpriteRenderer Display(Transform parent, string name, Sprite sprite, Material material, int order)
        {
            Transform t = Child(parent, name); var renderer = t.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = t.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = order; renderer.color = Color.white;
            return renderer;
        }
        private static Sprite Single(string relative)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(_source + relative)) if (asset is Sprite sprite) return sprite;
            throw new InvalidOperationException("장부방 공급 Sprite가 없습니다: " + relative);
        }
        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name); if (child != null) return child;
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/'); EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
#endif
