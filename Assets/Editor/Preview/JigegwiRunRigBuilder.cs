using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
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
    // 공급된 지게귀 그림을 참조하는 별도 메시·뼈·클립을 만듭니다. PNG와 기존 meta는 보존합니다.
    public static class JigegwiRunRigBuilder
    {
        private const string _folder = "Assets/5.Animation/Jigegwi_Run";
        private const string _gameplay = "Assets/2.Model/Prefabs/MOP/JigegwiGameplay.prefab";
        private const float _duration = .8f;
        private static readonly string[] _names = { "Root", "Body", "UpperArm", "Forearm", "BackThigh", "BackShin", "FrontThigh", "FrontShin" };
        private static readonly int[] _parents = { -1, 0, 1, 2, 1, 4, 1, 6 };
        // 기존 256픽셀 셀 기준의 관절입니다. 추출 Sprite의 잘린 rect·pivot은 아래에서 보정합니다.
        private static readonly Vector2[] _joints = { Vector2.zero, new Vector2(130,140), new Vector2(111,157), new Vector2(90,100),
            new Vector2(130,105), new Vector2(112,63), new Vector2(151,108), new Vector2(180,70) };

        [MenuItem("인귀/애니메이션/지게귀 달리기 리그 최초 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 리그를 생성해 주세요.");
            foreach (string name in new[] { "Jigegwi_RunSprite.asset", "Jigegwi_Run.anim", "Jigegwi_RunRig.prefab", "Jigegwi_Run.controller" })
                if (AssetDatabase.LoadMainAssetAtPath(_folder + "/" + name) != null)
                    throw new InvalidOperationException("기존 달리기 자산은 덮어쓰지 않습니다. 수정은 Animation 창에서 진행해 주세요: " + name);
            if (!AssetDatabase.IsValidFolder(_folder)) AssetDatabase.CreateFolder("Assets/5.Animation", "Jigegwi_Run");
            GameObject gameplay = PrefabUtility.LoadPrefabContents(_gameplay);
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                PreviewMonsterSprite display = gameplay.GetComponentInChildren<PreviewMonsterSprite>(true);
                var displayData = new SerializedObject(display);
                Sprite source = displayData.FindProperty("_fallback").objectReferenceValue as Sprite;
                SpriteRenderer normal = displayData.FindProperty("_renderer").objectReferenceValue as SpriteRenderer;
                if (source == null || normal == null) throw new InvalidOperationException("지게귀 원본 표시를 찾을 수 없습니다.");
                root = new GameObject("Jigegwi_RunRig"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root,previewScene);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = normal.sharedMaterial; renderer.sortingLayerID = normal.sortingLayerID; renderer.sortingOrder = normal.sortingOrder;
                var transforms = new Transform[_names.Length];
                var positions = new Vector2[_names.Length];
                for (int i=0;i<positions.Length;i++) positions[i] = i == 0 ? Vector2.zero : Local(source,_joints[i]);
                for (int i=0;i<transforms.Length;i++)
                {
                    transforms[i] = new GameObject(_names[i]).transform;
                    transforms[i].SetParent(_parents[i] < 0 ? root.transform : transforms[_parents[i]],false);
                    transforms[i].localPosition = i == 0 ? Vector3.zero : (Vector3)(positions[i] - positions[_parents[i]]);
                }
                Sprite sprite = MakeSprite(source, transforms, root.transform);
                AssetDatabase.CreateAsset(sprite, _folder + "/Jigegwi_RunSprite.asset"); renderer.sprite = sprite;
                SpriteSkin skin = root.AddComponent<SpriteSkin>(); skin.autoRebind = false; skin.alwaysUpdate = true;
                skin.SetRootBone(transforms[0]); skin.SetBoneTransforms(transforms);
                var skinData = new SerializedObject(skin);
                skinData.FindProperty("m_Bounds").boundsValue = new Bounds(sprite.bounds.center,sprite.bounds.size * 2f);
                skinData.ApplyModifiedPropertiesWithoutUndo();
                AnimationClip clip = MakeClip(source,transforms,positions);
                AssetDatabase.CreateAsset(clip, _folder + "/Jigegwi_Run.anim");
                var controller = AnimatorController.CreateAnimatorControllerAtPath(_folder + "/Jigegwi_Run.controller"); controller.AddMotion(clip);
                Animator animator = root.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.enabled = false;
                // 샘플 자세 대신 초기 바인드 자세를 프리팹에 보관합니다.
                for (int i=0;i<transforms.Length;i++)
                { transforms[i].localRotation = Quaternion.identity; transforms[i].localPosition = i == 0 ? Vector3.zero : (Vector3)(positions[i] - positions[_parents[i]]); }
                root.SetActive(true);
                GameObject rigPrefab = PrefabUtility.SaveAsPrefabAsset(root,_folder + "/Jigegwi_RunRig.prefab");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab,gameplay.transform);
                instance.name = "RunRig"; instance.transform.localPosition = normal.transform.localPosition; instance.transform.localScale = normal.transform.localScale;
                var view = gameplay.AddComponent<JigegwiRunView>(); var settings = new SerializedObject(view);
                settings.FindProperty("_monster").objectReferenceValue = gameplay.GetComponent<MonsterRuntime>();
                settings.FindProperty("_source").objectReferenceValue = normal;
                settings.FindProperty("_ground").objectReferenceValue = displayData.FindProperty("_executionGround").objectReferenceValue;
                settings.FindProperty("_rig").objectReferenceValue = instance;
                settings.FindProperty("_skin").objectReferenceValue = instance.GetComponent<SpriteRenderer>();
                settings.FindProperty("_run").objectReferenceValue = clip;
                float bottom = float.PositiveInfinity; foreach(Vector2 vertex in source.vertices) bottom = Mathf.Min(bottom,vertex.y);
                settings.FindProperty("_sourceFeet").floatValue = bottom; settings.ApplyModifiedPropertiesWithoutUndo();
                instance.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(gameplay,_gameplay);
                AssetDatabase.SaveAssetIfDirty(sprite); AssetDatabase.SaveAssetIfDirty(clip); AssetDatabase.SaveAssetIfDirty(controller);
                Debug.Log("지게귀 달리기 리그 생성 완료: 8뼈·관절 가중치·접지 IK를 구운 30fps 클립. 원본과 전투 수치는 보존했습니다.");
            }
            finally { if(root != null) UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(previewScene); PrefabUtility.UnloadPrefabContents(gameplay); }
        }

        private static Vector2 Local(Sprite source, Vector2 point)
        {
            float cell = source.texture.width / 3f;
            Vector2 cellOrigin = new Vector2(Mathf.Floor(source.rect.x / cell)*cell,Mathf.Floor(source.rect.y / cell)*cell);
            return (point + cellOrigin - source.rect.position - source.pivot) / source.pixelsPerUnit;
        }
        private static Sprite MakeSprite(Sprite source, Transform[] bones, Transform root)
        {
            Sprite sprite = Sprite.Create(source.texture,source.rect,new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height),source.pixelsPerUnit,0,SpriteMeshType.FullRect);
            sprite.name = "Jigegwi_RunSprite";
            int columns = Mathf.CeilToInt(source.rect.width/12f), rows = Mathf.CeilToInt(source.rect.height/12f);
            var vertices = new Vector2[(columns+1)*(rows+1)]; var triangles = new List<ushort>(); var weights = new BoneWeight[vertices.Length];
            float cell = source.texture.width/3f;
            Vector2 offset = source.rect.position - new Vector2(Mathf.Floor(source.rect.x/cell)*cell,Mathf.Floor(source.rect.y/cell)*cell);
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            {
                int index = y*(columns+1)+x;
                Vector2 pixel = new Vector2((float)x/columns*source.rect.width,(float)y/rows*source.rect.height);
                vertices[index]=(pixel-source.pivot)/source.pixelsPerUnit;
                Vector2 p = pixel+offset;
                weights[index] = Weights(p);
                if(x<columns&&y<rows){ushort a=(ushort)index,b=(ushort)(index+1),c=(ushort)(index+columns+1),d=(ushort)(index+columns+2);triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);}
            }
            // FullRect의 OverrideGeometry 제한을 피하고 스키닝용 스트림을 명시적으로 작성합니다.
            sprite.SetVertexCount(vertices.Length);
            var positions = new Vector3[vertices.Length]; var uvs = new Vector2[vertices.Length];
            for (int i=0;i<vertices.Length;i++)
            {
                positions[i] = vertices[i];
                Vector2 pixel = vertices[i]*source.pixelsPerUnit + source.pivot + source.rect.position;
                uvs[i] = new Vector2(pixel.x/source.texture.width,pixel.y/source.texture.height);
            }
            using(var values=new NativeArray<Vector3>(positions,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.Position,values);
            using(var values=new NativeArray<Vector2>(uvs,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.TexCoord0,values);
            using(var values=new NativeArray<ushort>(triangles.ToArray(),Allocator.Temp))sprite.SetIndices(values);
            var definitions=new SpriteBone[bones.Length]; var bindPoses=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++){definitions[i]=new SpriteBone{name=bones[i].name,parentId=_parents[i],position=bones[i].localPosition,rotation=Quaternion.identity,length=.1f};bindPoses[i]=bones[i].worldToLocalMatrix*root.localToWorldMatrix;}
            sprite.SetBones(definitions);
            using(var poses=new NativeArray<Matrix4x4>(bindPoses,Allocator.Temp))sprite.SetBindPoses(poses);
            using(var values=new NativeArray<BoneWeight>(weights,Allocator.Temp))sprite.SetVertexAttribute(VertexAttribute.BlendWeight,values);
            return sprite;
        }
        private static BoneWeight Weights(Vector2 point)
        {
            // 겹치는 관절의 영향도 연속적으로 섞어 영역 경계에서 메시가 뒤집히지 않게 합니다.
            var scores = new float[8]; scores[1] = .1f;
            for (int bone=2;bone<8;bone++)
            {
                Vector2 a = _joints[bone];
                Vector2 b = bone == 3 ? new Vector2(70,36) : bone == 5 ? new Vector2(103,37) : bone == 7 ? new Vector2(209,37) : _joints[bone+1];
                Vector2 segment = b-a;
                float projection = Mathf.Clamp01(Vector2.Dot(point-a,segment)/segment.sqrMagnitude);
                float distance = (point-a-segment*projection).sqrMagnitude;
                scores[bone] = Mathf.Exp(-distance/1800f)*(1f-Smooth(130f,170f,point.y));
            }
            var indices = new[] { 1,2,3,4,5,6,7 };
            Array.Sort(indices,(a,b)=>scores[b].CompareTo(scores[a]));
            float total = scores[indices[0]]+scores[indices[1]]+scores[indices[2]]+scores[indices[3]];
            return new BoneWeight { boneIndex0=indices[0],weight0=scores[indices[0]]/total,
                boneIndex1=indices[1],weight1=scores[indices[1]]/total,boneIndex2=indices[2],weight2=scores[indices[2]]/total,
                boneIndex3=indices[3],weight3=scores[indices[3]]/total };
        }

        private static float Smooth(float min,float max,float value) => Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(min,max,value));

        private static AnimationClip MakeClip(Sprite source,Transform[] bones,Vector2[] positions)
        {
            var clip=new AnimationClip{name="Jigegwi_Run",frameRate=30f};
            var rotations=new AnimationCurve[bones.Length,4]; var bodyY=new AnimationCurve();
            for(int i=0;i<bones.Length;i++)for(int j=0;j<4;j++)rotations[i,j]=new AnimationCurve();
            for(int frame=0;frame<=24;frame++)
            {
                float time=frame/30f, phase=(frame==24?0f:frame/24f)*Mathf.PI*2f;
                for(int i=0;i<bones.Length;i++){bones[i].localPosition=i==0?Vector3.zero:(Vector3)(positions[i]-positions[_parents[i]]);bones[i].localRotation=Quaternion.identity;}
                Vector3 body=bones[1].localPosition;body.y+=-.025f+.015f*Mathf.Cos(phase*2f);bones[1].localPosition=body;
                bones[1].localRotation=Quaternion.Euler(0,0,Mathf.Sin(phase)*1.5f);
                Solve(bones[4],bones[5],positions[5]-positions[4],Local(source,new Vector2(103,37))-positions[5],Foot(Local(source,new Vector2(103,37)),phase,.08f,.12f),-1);
                Solve(bones[6],bones[7],positions[7]-positions[6],Local(source,new Vector2(209,37))-positions[7],Foot(Local(source,new Vector2(209,37)),phase+Mathf.PI,.08f,.12f),-1);
                Solve(bones[2],bones[3],positions[3]-positions[2],Local(source,new Vector2(70,36))-positions[3],Foot(Local(source,new Vector2(70,36)),phase+Mathf.PI*.5f,.06f,.07f),-1);
                for(int i=0;i<bones.Length;i++){Quaternion q=bones[i].localRotation;rotations[i,0].AddKey(time,q.x);rotations[i,1].AddKey(time,q.y);rotations[i,2].AddKey(time,q.z);rotations[i,3].AddKey(time,q.w);}
                bodyY.AddKey(time,bones[1].localPosition.y);
            }
            for(int i=0;i<bones.Length;i++)for(int j=0;j<4;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],bones[0].parent),typeof(Transform),"m_LocalRotation."+"xyzw"[j]),rotations[i,j]);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Root/Body",typeof(Transform),"m_LocalPosition.y"),bodyY);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();return clip;
        }
        private static Vector2 Foot(Vector2 rest,float phase,float stride,float lift) => rest+new Vector2(Mathf.Cos(phase)*stride,Mathf.Max(0f,-Mathf.Sin(phase))*lift);
        private static void Solve(Transform upper,Transform lower,Vector2 upperRest,Vector2 lowerRest,Vector2 target,float pole)
        {
            Vector2 origin=upper.position, delta=target-origin;float a=upperRest.magnitude,b=lowerRest.magnitude,d=Mathf.Clamp(delta.magnitude,.001f,a+b-.0001f);
            Vector2 direction=delta.normalized;float along=(a*a+d*d-b*b)/(2f*d),height=Mathf.Sqrt(Mathf.Max(0f,a*a-along*along));
            Vector2 knee=origin+direction*along+new Vector2(-direction.y,direction.x)*height*pole;
            upper.rotation=Quaternion.Euler(0,0,(Mathf.Atan2(knee.y-origin.y,knee.x-origin.x)-Mathf.Atan2(upperRest.y,upperRest.x))*Mathf.Rad2Deg);
            Vector2 actualKnee=lower.position;
            lower.rotation=Quaternion.Euler(0,0,(Mathf.Atan2(target.y-actualKnee.y,target.x-actualKnee.x)-Mathf.Atan2(lowerRest.y,lowerRest.x))*Mathf.Rad2Deg);
        }
    }
}
