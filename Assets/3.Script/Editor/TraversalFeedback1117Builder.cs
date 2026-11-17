#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.EditorTools
{
    public static class TraversalFeedback1117Builder
    {
        [MenuItem("인귀/애니메이션/매달리기 손 기준 굽기")]
        public static void BakeHands()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Play 종료 후 실행해 주세요.");
            var view=UnityEngine.Object.FindFirstObjectByType<PreviewPlayerSprite>();
            if(view==null)throw new InvalidOperationException("도화골 게임 장면에서 실행해 주세요.");
            var so=new SerializedObject(view);var sprites=new HashSet<Sprite>();
            var hang=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/5.Animation/Characters/Player/Clips/Player_10010_Hang.anim");
            foreach(var clip in new[]{hang,(AnimationClip)so.FindProperty("_climb").objectReferenceValue})
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding))if(key.value is Sprite sprite)sprites.Add(sprite);
            var textures=new Dictionary<Texture2D,Color32[]>();var rows=so.FindProperty("_traversalAnchors");rows.arraySize=sprites.Count;int index=0;
            foreach(var sprite in sprites.OrderBy(s=>AssetDatabase.GetAssetPath(s)).ThenBy(s=>s.name))
            {
                var source=sprite.texture;
                if(!textures.TryGetValue(source,out Color32[] pixels))
                {
                    var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                    var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
                    try{Graphics.Blit(source,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();pixels=copy.GetPixels32();textures.Add(source,pixels);}
                    finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(copy);}
                }
                Rect rect=sprite.rect;int top=-1,sum=0,count=0;
                for(int y=(int)rect.height-1;y>=0 && top<0;y--)
                    for(int x=0;x<(int)rect.width;x++)if(pixels[((int)rect.y+y)*source.width+(int)rect.x+x].a>25){top=y;break;}
                if(top<0)throw new InvalidOperationException("손 기준의 불투명 픽셀이 없습니다: "+sprite.name);
                for(int y=Math.Max(0,top-1);y<=top;y++)for(int x=0;x<(int)rect.width;x++)
                    if(pixels[((int)rect.y+y)*source.width+(int)rect.x+x].a>25){sum+=x;count++;}
                var row=rows.GetArrayElementAtIndex(index++);row.FindPropertyRelative("_sprite").objectReferenceValue=sprite;
                row.FindPropertyRelative("_hand").vector2Value=new Vector2((sum/(float)count+.5f-sprite.pivot.x)/sprite.pixelsPerUnit,(top+1f-sprite.pivot.y)/sprite.pixelsPerUnit);
            }
            // 매달림 원화의 작은 머리·몸통 비율을 보정하고, 올라가기 원화는 원래 배율을 유지합니다.
            so.FindProperty("_hangScale").floatValue=1.25f;
            so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(view.gameObject.scene);EditorSceneManager.SaveScene(view.gameObject.scene);
        }

        [MenuItem("인귀/애니메이션/옹귀 투척 확인 씬 연결")]
        public static void SetupThrowPreview()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Play 종료 후 실행해 주세요.");
            const string path="Assets/5.Animation/Characters/CharacterAnimationPreview.unity";
            var original=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var group=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="OnggwiThrowPreview");
                if(group==null){group=new GameObject("OnggwiThrowPreview");SceneManager.MoveGameObjectToScene(group,scene);}
                for(int i=0;i<2;i++)
                {
                    string name=i==0?"OnggwiThrowLeft":"OnggwiThrowRight";
                    var rig=group.transform.Find(name)?.gameObject;
                    if(rig==null){rig=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/5.Animation/Characters/Onggwi/Throw/Onggwi_ThrowRig.prefab"),group.transform);rig.name=name;}
                    rig.transform.localPosition=new Vector3(-3.5f+i*4f,-3.6f,0);rig.transform.localScale=Vector3.one*.8f;
                    var showcase=rig.GetComponent<OnggwiAnimationShowcase>();if(showcase==null)showcase=rig.AddComponent<OnggwiAnimationShowcase>();
                    var so=new SerializedObject(showcase);so.FindProperty("_throw").objectReferenceValue=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/5.Animation/Characters/Onggwi/Throw/Onggwi_Throw.anim");
                    so.FindProperty("_heldStone").objectReferenceValue=rig.transform.Find("Root/Body/UpperArm/Forearm/Hand/HeldStone").gameObject;so.FindProperty("_faceRight").boolValue=i==1;so.ApplyModifiedPropertiesWithoutUndo();
                    var label=group.transform.Find(name+"Label")?.GetComponent<TextMesh>();
                    if(label==null){var go=new GameObject(name+"Label",typeof(TextMesh));go.transform.SetParent(group.transform,false);label=go.GetComponent<TextMesh>();}
                    label.transform.localPosition=new Vector3(-3.5f+i*4f,-4.85f,0);label.anchor=TextAnchor.MiddleCenter;label.characterSize=.08f;label.fontSize=48;label.text="Onggwi Throw "+(i==0?"Left":"Right");
                }
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
                camera.orthographicSize=5f;var position=camera.transform.position;position.y=-.5f;camera.transform.position=position;
                EditorSceneManager.SaveScene(scene);
            }
            finally{SceneManager.SetActiveScene(original);if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
#endif
