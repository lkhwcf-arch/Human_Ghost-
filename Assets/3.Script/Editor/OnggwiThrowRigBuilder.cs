#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace HumanGhost.EditorTools
{
    // 공급 그림을 보존하고 팔의 IK를 별도 메시·클립으로 굽습니다. 전투 수치는 기존 투척 설정을 재사용합니다.
    public static class OnggwiThrowRigBuilder
    {
        private const string _folder = "Assets/5.Animation/Characters/Onggwi/Throw";
        private static readonly string[] _names = { "Root", "Body", "UpperArm", "Forearm", "Hand" };
        private static readonly int[] _parents = { -1, 0, 1, 2, 3 };
        private static readonly Vector2[] _pixels = { new Vector2(128,128), new Vector2(128,128), new Vector2(135,111), new Vector2(175,96), new Vector2(154,36) };
        [MenuItem("인귀/애니메이션/옹귀 투척 리그 최초 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || AssetDatabase.IsValidFolder(_folder))
                throw new InvalidOperationException("Play 종료 후 최초 한 번만 생성합니다. 기존 리그는 Animation 창에서 수정해 주세요.");
            AssetDatabase.CreateFolder("Assets/5.Animation/Characters/Onggwi", "Throw");
            var gameplay = PrefabUtility.LoadPrefabContents("Assets/2.Model/Prefabs/MOP/HusugwiGameplay.prefab");
            var scene = EditorSceneManager.NewPreviewScene(); GameObject rig = null;
            try
            {
                gameplay.name = "OnggwiGameplay";
                var runtime = gameplay.GetComponent<MonsterRuntime>();
                var patterns = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MonsterPatternSettings>("Assets/2.Model/Prefabs/Config/JigegwiThrowerPatterns.asset"));
                patterns.name = "OnggwiPatterns";
                var setup = new SerializedObject(patterns); var entries = setup.FindProperty("_patterns");
                entries.MoveArrayElement(1, 0); entries.arraySize = 1;
                var entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("_key").intValue = 10004; entry.FindPropertyRelative("_minRange").intValue = 0;
                entry.FindPropertyRelative("_extraDamage").intValue = 0; setup.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(patterns, "Assets/2.Model/Prefabs/Config/OnggwiPatterns.asset");
                setup = new SerializedObject(runtime); setup.FindProperty("_monsterId").intValue = 3005;
                setup.FindProperty("_patterns").objectReferenceValue = patterns; setup.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(gameplay.GetComponent<MonsterAnimationAudio>());
                gameplay.GetComponent<MonsterPreviewLabel>().SetDisplayName("옹귀");
                var view = gameplay.GetComponent<PreviewMonsterSprite>(); var display = new SerializedObject(view);
                var normal = (SpriteRenderer)display.FindProperty("_renderer").objectReferenceValue;
                normal.transform.localScale = Vector3.one * .75f;
                var clips = new[] { "Idle", "Walk", "Hit", "Ready", "Hit", "Hit", "Dead", "Dead", "Hit" };
                var states = new[] {10001,10002,10003,10004,10008,10014,10015,10016,10019};
                var sprites = new HashSet<Sprite>(); Sprite source = null;
                entries = display.FindProperty("_animations"); entries.arraySize = states.Length;
                for (int i=0;i<states.Length;i++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/5.Animation/Characters/Onggwi/Onggwi_"+clips[i]+".anim");
                    var row = entries.GetArrayElementAtIndex(i); row.FindPropertyRelative("_state").intValue = states[i]; row.FindPropertyRelative("_clip").objectReferenceValue = clip;
                    row.FindPropertyRelative("_hitFrames").arraySize = 0;
                    foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding))
                    { var frame=key.value as Sprite; if(frame!=null){sprites.Add(frame);if(clips[i]=="Ready")source=frame;} }
                }
                normal.sprite = sprites.First(s=>s.name=="Onggui_Idle_00");
                display.FindProperty("_fallback").objectReferenceValue = normal.sprite; display.FindProperty("_frames").arraySize = 0;
                display.FindProperty("_playerExecution").objectReferenceValue = null;
                var stone = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/5.Animation/Characters/Onggwi/Sprites/Stone_00.asset");
                display.FindProperty("_projectileSprite").objectReferenceValue = stone;
                var pixels=new Dictionary<Texture2D,Color32[]>();
                var bottom=typeof(EnemyGroundingBaker).GetMethod("Bottom",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var contacts=display.FindProperty("_groundContacts"); contacts.arraySize=sprites.Count; int rowIndex=0;
                foreach(var sprite in sprites.OrderBy(s=>s.name)) { var row=contacts.GetArrayElementAtIndex(rowIndex++);row.FindPropertyRelative("_sprite").objectReferenceValue=sprite;row.FindPropertyRelative("_bottom").floatValue=(float)bottom.Invoke(null,new object[]{sprite,pixels}); }
                display.ApplyModifiedPropertiesWithoutUndo();
                rig=new GameObject("Onggwi_ThrowRig");rig.SetActive(false);SceneManager.MoveGameObjectToScene(rig,scene);
                var skinRenderer=rig.AddComponent<SpriteRenderer>();skinRenderer.sharedMaterial=normal.sharedMaterial;skinRenderer.sortingOrder=normal.sortingOrder;
                var bones=new Transform[_names.Length];var positions=new Vector2[_names.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    positions[i]=(_pixels[i]-source.pivot)/source.pixelsPerUnit;
                    bones[i]=new GameObject(_names[i]).transform;bones[i].SetParent(_parents[i]<0?rig.transform:bones[_parents[i]],false);
                    bones[i].localPosition=i==0?Vector3.zero:(Vector3)(positions[i]-positions[_parents[i]]);
                }
                var mesh=MakeSprite(source,bones,rig.transform);AssetDatabase.CreateAsset(mesh,_folder+"/Onggwi_ThrowSprite.asset");skinRenderer.sprite=mesh;
                var skin=rig.AddComponent<SpriteSkin>();skin.autoRebind=false;skin.alwaysUpdate=true;skin.SetRootBone(bones[0]);skin.SetBoneTransforms(bones);
                setup=new SerializedObject(skin);setup.FindProperty("m_Bounds").boundsValue=new Bounds(mesh.bounds.center,mesh.bounds.size*3f);setup.ApplyModifiedPropertiesWithoutUndo();
                var held=new GameObject("HeldStone",typeof(SpriteRenderer));held.transform.SetParent(bones[4],false);var heldRenderer=held.GetComponent<SpriteRenderer>();heldRenderer.sprite=stone;heldRenderer.sortingOrder=normal.sortingOrder+1;held.transform.localScale=Vector3.one*(.5f/(stone.bounds.size.x*.75f));
                var motion=MakeClip(bones,positions);AssetDatabase.CreateAsset(motion,_folder+"/Onggwi_Throw.anim");
                var controller=AnimatorController.CreateAnimatorControllerAtPath(_folder+"/Onggwi_Throw.controller");controller.AddMotion(motion);
                var animator=rig.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.enabled=false;
                for(int i=0;i<bones.Length;i++){bones[i].localRotation=Quaternion.identity;bones[i].localPosition=i==0?Vector3.zero:(Vector3)(positions[i]-positions[_parents[i]]);}
                rig.SetActive(true);var prefab=PrefabUtility.SaveAsPrefabAsset(rig,_folder+"/Onggwi_ThrowRig.prefab");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,gameplay.transform);instance.name="ThrowRig";instance.transform.localScale=normal.transform.localScale;
                var rigView=gameplay.AddComponent<MonsterThrowRigView>();setup=new SerializedObject(rigView);
                setup.FindProperty("_monster").objectReferenceValue=runtime;setup.FindProperty("_source").objectReferenceValue=normal;setup.FindProperty("_ground").objectReferenceValue=display.FindProperty("_executionGround").objectReferenceValue;
                setup.FindProperty("_skin").objectReferenceValue=instance.GetComponent<SpriteRenderer>();setup.FindProperty("_rig").objectReferenceValue=instance;setup.FindProperty("_hand").objectReferenceValue=instance.transform.Find("Root/Body/UpperArm/Forearm/Hand");
                setup.FindProperty("_heldStone").objectReferenceValue=instance.transform.Find("Root/Body/UpperArm/Forearm/Hand/HeldStone").gameObject;
                setup.FindProperty("_throw").objectReferenceValue=motion;setup.FindProperty("_sourceFeet").floatValue=(float)bottom.Invoke(null,new object[]{source,pixels});setup.ApplyModifiedPropertiesWithoutUndo();instance.SetActive(false);
                var result=PrefabUtility.SaveAsPrefabAsset(gameplay,"Assets/2.Model/Prefabs/MOP/OnggwiGameplay.prefab");
                var content=AssetDatabase.LoadAssetAtPath<StageContentSet>("Assets/2.Model/Prefabs/Config/StageContentSet.asset");setup=new SerializedObject(content);entries=setup.FindProperty("_monsters");int last=entries.arraySize;entries.arraySize++;entry=entries.GetArrayElementAtIndex(last);entry.FindPropertyRelative("_code").intValue=3005;entry.FindPropertyRelative("_prefab").objectReferenceValue=result.GetComponent<MonsterRuntime>();setup.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(content);
                AssetDatabase.SaveAssetIfDirty(mesh);AssetDatabase.SaveAssetIfDirty(motion);AssetDatabase.SaveAssetIfDirty(controller);
            }
            finally {if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);EditorSceneManager.ClosePreviewScene(scene);PrefabUtility.UnloadPrefabContents(gameplay);}
        }

        // 기존 GUID를 유지한 채 파생 메시·관절·클립만 다시 굽습니다. 원본 PNG/Importer와 전투 설정은 건드리지 않습니다.
        [MenuItem("인귀/애니메이션/옹귀 투척 리그 재생성")]
        public static void RebuildThrowRig()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료하고 리그를 재생성해 주세요.");
            var prefab = PrefabUtility.LoadPrefabContents(_folder + "/Onggwi_ThrowRig.prefab");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/5.Animation/Characters/Onggwi/Sprites/Onggui_00.asset");
                var bones = new Transform[_names.Length]; var positions = new Vector2[bones.Length];
                for (int b = 0; b < bones.Length; b++)
                {
                    bones[b] = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == _names[b]);
                    positions[b] = (_pixels[b] - source.pivot) / source.pixelsPerUnit;
                    bones[b].localPosition = b == 0 ? Vector3.zero : (Vector3)(positions[b] - positions[_parents[b]]);
                    bones[b].localRotation = Quaternion.identity; bones[b].localScale = Vector3.one;
                }
                var mesh = MakeSprite(source, bones, prefab.transform);
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(_folder + "/Onggwi_ThrowSprite.asset");
                EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
                var clip = MakeClip(bones, positions);
                var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_folder + "/Onggwi_Throw.anim");
                EditorUtility.CopySerialized(clip, existingClip); UnityEngine.Object.DestroyImmediate(clip);
                // 높이 던지기(1117): 같은 리그를 위쪽 각도로 놓는 클립. 없으면 만들고 있으면 GUID를 유지해 덮어씁니다.
                var high = MakeClip(bones, positions, true);
                var existingHigh = AssetDatabase.LoadAssetAtPath<AnimationClip>(_folder + "/Onggwi_ThrowHigh.anim");
                if (existingHigh == null) { AssetDatabase.CreateAsset(high, _folder + "/Onggwi_ThrowHigh.anim"); existingHigh = high; }
                else { EditorUtility.CopySerialized(high, existingHigh); UnityEngine.Object.DestroyImmediate(high); }
                EditorUtility.SetDirty(existingHigh); AssetDatabase.SaveAssetIfDirty(existingHigh);
                // 클립 생성 후 기본 자세를 저장합니다. 마지막 투척 자세를 bind pose로 저장하지 않습니다.
                for (int b = 0; b < bones.Length; b++) bones[b].localRotation = Quaternion.identity;
                prefab.GetComponent<SpriteRenderer>().sprite = existing;
                var skin = prefab.GetComponent<SpriteSkin>(); skin.SetRootBone(bones[0]); skin.SetBoneTransforms(bones);
                skin.alwaysUpdate = true;
                // 그리는 순서: 항아리 본체 < 돌 < 팔 전체(HandOverlay). 돌이 팔·손 뒤에 쥐어진 것처럼 보입니다.
                int order = prefab.GetComponent<SpriteRenderer>().sortingOrder;
                var held = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HeldStone").GetComponent<SpriteRenderer>();
                held.sortingOrder = order + 1;
                // 손가락 끝에 걸쳐 쥡니다. 팔 덮개(손가락)가 돌 안쪽 절반을 덮고, 손끝 바깥으로 나온 절반이 보입니다.
                held.transform.localPosition = (_palmPixel - _pixels[4]) / source.pixelsPerUnit;
                var hand = MakeSprite(source, bones, prefab.transform, true);
                var handAsset = AssetDatabase.LoadAssetAtPath<Sprite>(_folder + "/Onggwi_HandOverlaySprite.asset");
                if (handAsset == null) { AssetDatabase.CreateAsset(hand, _folder + "/Onggwi_HandOverlaySprite.asset"); handAsset = hand; }
                else { EditorUtility.CopySerialized(hand, handAsset); UnityEngine.Object.DestroyImmediate(hand); }
                var overlay = prefab.transform.Find(HandOverlayName);
                if (overlay == null) { overlay = new GameObject(HandOverlayName, typeof(SpriteRenderer)).transform; overlay.SetParent(prefab.transform, false); }
                var overlayRenderer = overlay.GetComponent<SpriteRenderer>();
                overlayRenderer.sharedMaterial = prefab.GetComponent<SpriteRenderer>().sharedMaterial;
                overlayRenderer.sprite = handAsset; overlayRenderer.sortingOrder = order + 2;
                var overlaySkin = overlay.GetComponent<SpriteSkin>() ?? overlay.gameObject.AddComponent<SpriteSkin>();
                overlaySkin.autoRebind = false; overlaySkin.alwaysUpdate = true;
                overlaySkin.SetRootBone(bones[0]); overlaySkin.SetBoneTransforms(bones);
                var overlayBounds = new SerializedObject(overlaySkin); overlayBounds.FindProperty("m_Bounds").boundsValue = new Bounds(existing.bounds.center, existing.bounds.size * 3f); overlayBounds.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(handAsset); AssetDatabase.SaveAssetIfDirty(handAsset);
                PrefabUtility.SaveAsPrefabAsset(prefab, _folder + "/Onggwi_ThrowRig.prefab");
                EditorUtility.SetDirty(existing); EditorUtility.SetDirty(existingClip);
                AssetDatabase.SaveAssetIfDirty(existing); AssetDatabase.SaveAssetIfDirty(existingClip);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        // 실제 팔 윤곽입니다. 몸통과 팔의 경계 정점을 복제해 팔을 움직일 때 항아리가 늘어나지 않습니다.
        private static readonly Vector2[] _armOutline = {
            new Vector2(129,121), new Vector2(181,105), new Vector2(189,98), new Vector2(186,83),
            new Vector2(169,43), new Vector2(182,43), new Vector2(182,0), new Vector2(110,0),
            new Vector2(110,43), new Vector2(142,49),
            new Vector2(152,74), new Vector2(162,94), new Vector2(129,104)
        };
        private static bool InArm(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = _armOutline.Length - 1; i < _armOutline.Length; j = i++)
            {
                Vector2 a = _armOutline[i], b = _armOutline[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
            }
            return inside;
        }
        public const string HandOverlayName = "HandOverlay";
        private static readonly Vector2 _palmPixel = new Vector2(148,8);
        // handOnly: 팔(위팔~손가락) 삼각형만 담은 덮개 메시. 같은 뼈·가중치를 써서 본체와 정확히 겹칩니다.
        private static Sprite MakeSprite(Sprite source, Transform[] bones, Transform root, bool handOnly = false)
        {
            var sprite = Sprite.Create(source.texture, source.rect, source.pivot/source.rect.size, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = handOnly ? "Onggwi_HandOverlaySprite" : "Onggwi_ThrowSprite";
            const int steps = 128;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight>();
            var indices = new List<ushort>(); var armIndices = new List<ushort>(); var lookup = new Dictionary<int, ushort>();
            Action<int,int,int> addVertex = (x,y,part) => {
                bool arm = part == 1;
                int key = (part*(steps+1)+y)*(steps+1)+x;
                if (!lookup.TryGetValue(key, out ushort index))
                {
                    index = checked((ushort)vertices.Count); lookup.Add(key,index);
                    var pixel = new Vector2(x*source.rect.width/steps,y*source.rect.height/steps);
                    vertices.Add((pixel-source.pivot)/source.pixelsPerUnit);
                    uv.Add(((part == 2 ? new Vector2(151.5f,140.5f) : pixel)+source.rect.position)/new Vector2(source.texture.width,source.texture.height));
                    // 팔꿈치 인접 13픽셀만 부드럽게 연결하고 나머지 몸통·뚜껑은 Root에 고정합니다.
                    float forearm = 1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(90,103,pixel.y));
                    weights.Add(arm ? new BoneWeight{boneIndex0=2,weight0=1f-forearm,boneIndex1=3,weight1=forearm}
                        : new BoneWeight{boneIndex0=0,weight0=1f});
                }
                if (arm) armIndices.Add(index); else indices.Add(index);
            };
            Action<int,int> addCell = (x,y) => {
                bool a=InArm(new Vector2((x+1f/3)*2,(y+1f/3)*2));
                if (handOnly)
                {
                    bool c=InArm(new Vector2((x+2f/3)*2,(y+2f/3)*2));
                    if (a) { addVertex(x,y,1); addVertex(x,y+1,1); addVertex(x+1,y,1); }
                    if (c) { addVertex(x+1,y,1); addVertex(x,y+1,1); addVertex(x+1,y+1,1); }
                    return;
                }
                if (a && y >= 22) { addVertex(x,y,2); addVertex(x,y+1,2); addVertex(x+1,y,2); }
                addVertex(x,y,a?1:0); addVertex(x,y+1,a?1:0); addVertex(x+1,y,a?1:0);
                bool b=InArm(new Vector2((x+2f/3)*2,(y+2f/3)*2));
                if (b && y >= 22) { addVertex(x+1,y,2); addVertex(x,y+1,2); addVertex(x+1,y+1,2); }
                addVertex(x+1,y,b?1:0); addVertex(x,y+1,b?1:0); addVertex(x+1,y+1,b?1:0);
            };
            // 움직이지 않는 항아리는 8px 격자, 윤곽이 분리되는 팔 주변만 2px 격자로 유지합니다.
            for (int y=0;y<steps;y+=4) for (int x=0;x<steps;x+=4)
            {
                if (x*2 >= 104 && x*2 <= 192 && y*2 <= 128)
                {
                    for(int sy=y;sy<y+4;sy++)for(int sx=x;sx<x+4;sx++)addCell(sx,sy);
                }
                else if (!handOnly)
                {
                    addVertex(x,y,0); addVertex(x,y+4,0); addVertex(x+4,y,0);
                    addVertex(x+4,y,0); addVertex(x,y+4,0); addVertex(x+4,y+4,0);
                }
            }
            indices.AddRange(armIndices);
            sprite.SetVertexCount(vertices.Count);
            using(var data=new NativeArray<Vector3>(vertices.ToArray(),Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.Position,data);
            using(var data=new NativeArray<Vector2>(uv.ToArray(),Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.TexCoord0,data);
            using(var data=new NativeArray<ushort>(indices.ToArray(),Allocator.Temp))sprite.SetIndices(data);
            var definitions=new SpriteBone[bones.Length]; var poses=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                definitions[i]=new SpriteBone{name=bones[i].name,parentId=_parents[i],position=bones[i].localPosition,rotation=Quaternion.identity,length=.1f};
                poses[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;
            }
            sprite.SetBones(definitions);
            using(var data=new NativeArray<Matrix4x4>(poses,Allocator.Temp))sprite.SetBindPoses(data);
            using(var data=new NativeArray<BoneWeight>(weights.ToArray(),Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.BlendWeight,data);
            return sprite;
        }
        private static AnimationClip MakeClip(Transform[] bones, Vector2[] positions, bool high = false)
        {
            var clip=new AnimationClip{name=high?"Onggwi_ThrowHigh":"Onggwi_Throw",frameRate=10,wrapMode=WrapMode.ClampForever};
            // 기본 자세는 왼쪽(-x)이 정면. 위팔 기본 방향 -20도, 아래팔은 위팔 기준 -89도 꺾여 있습니다.
            // 음수 회전으로 앞→머리 위(팔 일직선, 수직)까지 0.3초에 번쩍 들어 올리고, 살짝 뒤로 젖혔다가
            // 0.5초 발사 시점에 앞쪽 위로 뻗으며 놓고 앞아래로 따라 내려옵니다. 뒤로 도는 구간은 없습니다.
            // 아래팔 절대각: 앞수평 -180 → 수직 -270 → 젖힘 -300 → 발사 -200 → 앞아래 -135.
            var times=new[]{0f,.15f,.3f,.4f,.5f,.7f,1.2f,1.7f};
            var upper=new[]{0f,-131f,-250f,-241f,-180f,-86f,-30f,0f};
            var lower=new[]{0f,60f,89f,50f,89f,60f,10f,0f};
            // 높이 던지기: 머리 위에서 더 뒤로 젖혔다가(-270) 0.5초에 거의 수직(-235)으로 하늘에 놓고, 천천히 내립니다.
            if (high) { upper=new[]{0f,-131f,-250f,-270f,-235f,-215f,-60f,0f}; lower=new[]{0f,60f,80f,40f,89f,80f,30f,0f}; }
            foreach (int bone in new[]{2,3})
            {
                var values=bone==2?upper:lower;
                var curve=new AnimationCurve();
                for(int i=0;i<times.Length;i++)curve.AddKey(new Keyframe(times[i],values[i]));
                for(int i=0;i<curve.length;i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[bone],bones[0].parent),typeof(Transform),"localEulerAnglesRaw.z"),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.stopTime=1.7f;AnimationUtility.SetAnimationClipSettings(clip,settings);
            return clip;
        }
    }
}
#endif
