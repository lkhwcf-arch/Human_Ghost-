#if UNITY_EDITOR
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Preview;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 실제 주입된 읽기 전용 행과 발 위치만 표시합니다. 인식 규칙이나 게임 상태는 변경하지 않습니다.
    internal static class MonsterAggroGizmos
    {
        private const string _menu = "인귀/테스트/몬스터 인식 범위 Gizmos";
        [MenuItem(_menu)] private static void Toggle() => GameplayPreview.ShowAggroGizmos = !GameplayPreview.ShowAggroGizmos;
        [MenuItem(_menu, true)] private static bool Validate() { Menu.SetChecked(_menu, GameplayPreview.ShowAggroGizmos); return true; }

        [DrawGizmo(GizmoType.Selected)]
        private static void Draw(MonsterRuntime monster, GizmoType type)
        {
            if (!GameplayPreview.ShowAggroGizmos || !monster.isActiveAndEnabled || monster.Aggro == null || monster.Actor == null || monster.Actor.IsDead) return;
            var data = monster.Aggro;
            var body = monster.GetComponentInChildren<SpriteRenderer>();
            if (body == null) return;
            Vector3 feet = new Vector3(monster.transform.position.x, body.bounds.min.y, monster.transform.position.z);
            float unit = monster.ToWorld(IntegerPosition.FromData(100));
            float level = data.SameLevelHeight * unit / 100f, strip = unit / 2f;
            int facing = monster.Actor.Facing;
            var front = new Color(.2f,1f,.3f,.12f); var rear = new Color(1f,.7f,.15f,.12f);
            var above = new Color(.2f,.75f,1f,.12f); var below = new Color(.8f,.3f,1f,.12f);
            RectZone(feet,0,data.FrontRange*unit/100f,-level,level,facing,front);
            RectZone(feet,-data.BackRange*unit/100f,0,-level,level,facing,rear);
            if (data.UpRange > data.SameLevelHeight) RectZone(feet,-strip,strip,level,data.UpRange*unit/100f,1,above);
            if (data.DownRange > data.SameLevelHeight) RectZone(feet,-strip,strip,-data.DownRange*unit/100f,-level,1,below);
            Diagonal(feet,data.FrontUpRange,strip,level,unit,facing,1,above);
            Diagonal(feet,data.BackUpRange,strip,level,unit,-facing,1,above);
            Diagonal(feet,data.FrontDownRange,strip,level,unit,facing,-1,below);
            Diagonal(feet,data.BackDownRange,strip,level,unit,-facing,-1,below);
            Color previous = Handles.color;
            try
            {
                Handles.color = Color.red;
                float x=data.LoseRange*unit/100f, y=data.LoseHeight*unit/100f;
                Handles.DrawWireCube(feet,new Vector3(x*2,y*2,0));
                if ((type & GizmoType.Selected) != 0) Handles.Label(feet+Vector3.up*(level+.1f), "인식 " + data.FrontRange + "/" + data.BackRange + " · 추적해제 " + data.LoseTime + "ms" + (monster.Actor.HasAggro ? " · 추적 중" : ""));
            }
            finally { Handles.color = previous; }
        }
        private static void Diagonal(Vector3 feet,int value,float strip,float level,float unit,int facing,int vertical,Color color)
        {
            float range=value*unit/100f;
            if(range<=strip || range<=level)return;
            RectZone(feet,strip,range,vertical>0?level:-range,vertical>0?range:-level,facing,color);
        }
        private static void RectZone(Vector3 feet,float left,float right,float bottom,float top,int facing,Color fill)
        {
            if(right<=left || top<bottom)return;
            var outline=fill;outline.a=.85f;
            Handles.DrawSolidRectangleWithOutline(new[]{feet+new Vector3(left*facing,bottom,0),feet+new Vector3(right*facing,bottom,0),feet+new Vector3(right*facing,top,0),feet+new Vector3(left*facing,top,0)},fill,outline);
        }
    }
}
#endif
