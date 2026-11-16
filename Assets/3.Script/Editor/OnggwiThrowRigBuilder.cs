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
        private static readonly Vector2[] _pixels = { new Vector2(128,128), new Vector2(128,128), new Vector2(177,140), new Vector2(177,95), new Vector2(165,32) };
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
                var content=AssetDatabase.LoadAssetAtPath<StageContentSet>("Assets/2.Model/Prefabs/Config/StageContentSet.asset");setup=new SerializedObject(content);entries=setup.FindProperty("_monsters");int last=entries.arraySize;entries.arraySize++;entry=entries.GetArrayElementAtIndex(last);entry.FindPropertyRelative("Code").intValue=3005;entry.FindPropertyRelative("Prefab").objectReferenceValue=result.GetComponent<MonsterRuntime>();setup.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(content);
                AssetDatabase.SaveAssetIfDirty(mesh);AssetDatabase.SaveAssetIfDirty(motion);AssetDatabase.SaveAssetIfDirty(controller);
            }
            finally {if(rig!=null)UnityEngine.Object.DestroyImmediate(rig);EditorSceneManager.ClosePreviewScene(scene);PrefabUtility.UnloadPrefabContents(gameplay);}
        }
        private static Sprite MakeSprite(Sprite source,Transform[] bones,Transform root)
        {
            var sprite=Sprite.Create(source.texture,source.rect,source.pivot/source.rect.size,source.pixelsPerUnit,0,SpriteMeshType.FullRect);sprite.name="Onggwi_ThrowSprite";
            const int steps=26;var positions=new Vector3[(steps+1)*(steps+1)];var uv=new Vector2[positions.Length];var weights=new BoneWeight[positions.Length];var indices=new List<ushort>();
            for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
            {
                int i=y*(steps+1)+x;var p=new Vector2(x*source.rect.width/steps,y*source.rect.height/steps);positions[i]=(p-source.pivot)/source.pixelsPerUnit;uv[i]=(p+source.rect.position)/new Vector2(source.texture.width,source.texture.height);
                float arm=Mathf.SmoothStep(0,1,Mathf.InverseLerp(135,166,p.x))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(138,165,p.y)));
                float lower=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(78,115,p.y));float body=(1-arm)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,60,p.y));
                weights[i]=new BoneWeight{boneIndex0=0,weight0=1-arm-body,boneIndex1=1,weight1=body,boneIndex2=2,weight2=arm*(1-lower),boneIndex3=3,weight3=arm*lower};
                if(x<steps&&y<steps){ushort a=(ushort)i,b=(ushort)(i+1),c=(ushort)(i+steps+1),d=(ushort)(i+steps+2);indices.Add(a);indices.Add(c);indices.Add(b);indices.Add(b);indices.Add(c);indices.Add(d);}
            }
            sprite.SetVertexCount(positions.Length);
            using(var data=new NativeArray<Vector3>(positions,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.Position,data);
            using(var data=new NativeArray<Vector2>(uv,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.TexCoord0,data);
            using(var data=new NativeArray<ushort>(indices.ToArray(),Allocator.Temp))sprite.SetIndices(data);
            var definitions=new SpriteBone[bones.Length];var poses=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++){definitions[i]=new SpriteBone{name=bones[i].name,parentId=_parents[i],position=bones[i].localPosition,rotation=Quaternion.identity,length=.1f};poses[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;}
            sprite.SetBones(definitions);using(var data=new NativeArray<Matrix4x4>(poses,Allocator.Temp))sprite.SetBindPoses(data);
            using(var data=new NativeArray<BoneWeight>(weights,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.BlendWeight,data);return sprite;
        }
        private static AnimationClip MakeClip(Transform[] bones,Vector2[] positions)
        {
            var clip=new AnimationClip{name="Onggwi_Throw",frameRate=10,wrapMode=WrapMode.ClampForever};var curves=new AnimationCurve[bones.Length,4];for(int i=0;i<bones.Length;i++)for(int j=0;j<4;j++)curves[i,j]=new AnimationCurve();
            var times=new[]{0f,.2f,.4f,.5f,.7f,1.2f,1.7f};var targets=new[]{_pixels[4],new Vector2(215,145),new Vector2(180,215),new Vector2(65,165),new Vector2(65,105),new Vector2(145,45),_pixels[4]};
            Vector2 upper=positions[3]-positions[2],lower=positions[4]-positions[3];
            for(int frame=0;frame<=17;frame++)
            {
                float time=frame/10f;int segment=0;while(segment<times.Length-2&&time>times[segment+1])segment++;var target=Vector2.Lerp(targets[segment],targets[segment+1],Mathf.InverseLerp(times[segment],times[segment+1],time));target=(target-new Vector2(128,128))/100f;
                for(int i=0;i<bones.Length;i++){bones[i].localPosition=i==0?Vector3.zero:(Vector3)(positions[i]-positions[_parents[i]]);bones[i].localRotation=Quaternion.identity;}
                Vector2 origin=bones[2].position,delta=target-origin;float a=upper.magnitude,b=lower.magnitude,d=Mathf.Clamp(delta.magnitude,.001f,a+b-.0001f);var direction=delta.normalized;float along=(a*a+d*d-b*b)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var elbow=origin+direction*along+new Vector2(-direction.y,direction.x)*height;
                bones[2].rotation=Quaternion.Euler(0,0,(Mathf.Atan2(elbow.y-origin.y,elbow.x-origin.x)-Mathf.Atan2(upper.y,upper.x))*Mathf.Rad2Deg);Vector2 actual=bones[3].position;bones[3].rotation=Quaternion.Euler(0,0,(Mathf.Atan2(target.y-actual.y,target.x-actual.x)-Mathf.Atan2(lower.y,lower.x))*Mathf.Rad2Deg);
                for(int i=0;i<bones.Length;i++){var q=bones[i].localRotation;for(int j=0;j<4;j++)curves[i,j].AddKey(time,q[j]);}
            }
            for(int i=0;i<bones.Length;i++)for(int j=0;j<4;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],bones[0].parent),typeof(Transform),"m_LocalRotation."+"xyzw"[j]),curves[i,j]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.stopTime=1.7f;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();return clip;
        }
    }
}
#endif
