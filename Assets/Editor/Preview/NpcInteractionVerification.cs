using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HumanGhost.Gameplay.Adapters;
using UnityEditor;
using UnityEngine;

namespace HumanGhost.EditorTools
{
    // 실행 상태를 읽기만 합니다. 배치·저장·입력을 변경하지 않습니다.
    public static class NpcInteractionVerification
    {
        [MenuItem("인귀/검사/상호작용 등록 수명 검사")]
        public static void InspectLifetime()
        {
            var root = new GameObject("InteractionLifetimeCheck");
            root.SetActive(false);
            try
            {
                InteractionRegistry registry = root.AddComponent<InteractionRegistry>();
                var child = new GameObject("InteractionTarget");
                child.transform.SetParent(root.transform, false);
                NpcTalk npc = child.AddComponent<NpcTalk>();
                registry.Prepare();
                Require(registry.Targets.Count == 0, "비활성 생성 중 대상을 등록했습니다.");
                root.SetActive(true);
                Require(registry.Targets.Count == 1 && registry.Targets.Contains(npc), "맵 활성화 후 NPC 등록이 누락됐습니다.");
                registry.Prepare();
                Require(registry.Targets.Count == 1, "반복 준비에서 대상이 중복 등록됐습니다.");
                child.SetActive(false);
                Require(registry.Targets.Count == 0, "NPC 비활성 후 등록이 남았습니다.");
                child.SetActive(true);
                Require(registry.Targets.Count == 1, "NPC 재활성 등록이 누락됐습니다.");
                root.SetActive(false);
                Require(registry.Targets.Count == 0, "맵 비활성 후 등록이 남았습니다.");
                root.SetActive(true);
                Require(registry.Targets.Count == 1, "맵 재활성 등록이 누락됐습니다.");
                Debug.Log("상호작용 등록 수명 검사 통과: 비활성 생성·활성 등록·중복 방지·개별 해제·맵 재활성");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
        }

        [MenuItem("인귀/검사/NPC 상호작용 연결 검사", true)]
        private static bool CanInspect() => EditorApplication.isPlaying && !EditorApplication.isCompiling;

        [MenuItem("인귀/검사/NPC 상호작용 연결 검사")]
        public static void Inspect()
        {
            if (!CanInspect()) throw new InvalidOperationException("컴파일 완료 후 Play에서 NPC 앞에 서서 실행해 주세요.");
            var report = new StringBuilder("NPC 상호작용 연결 검사\n");
            foreach (PlayerInteractor interactor in UnityEngine.Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
            {
                InteractionRegistry registry = Read<InteractionRegistry>(interactor, "_registry");
                InteractionContext context = Read<InteractionContext>(interactor, "_context");
                float rangeX = Read<float>(interactor, "_rangeX"), rangeY = Read<float>(interactor, "_rangeY");
                report.AppendLine($"플레이어: {interactor.name}, 활성={interactor.isActiveAndEnabled}, 등록 대상 수={registry?.Targets.Count ?? 0}, 화면 열림={context?.Textbox?.IsOpen}");
                if (context?.Player?.Actor != null)
                    report.AppendLine($"접지={context.Player.Actor.Locomotion.IsGrounded}, 이동 가능={context.Player.Actor.Locomotion.CanMove}, 상태={context.Player.Actor.CurrentState}, 위치={context.Player.transform.position}, 범위=({rangeX}, {rangeY})");
                if (registry == null) { report.AppendLine("문제: 상호작용 등록 묶음이 연결되지 않았습니다."); continue; }
                foreach (NpcTalk npc in registry.GetComponentsInChildren<NpcTalk>(true))
                {
                    bool registered = registry.Targets.Contains(npc);
                    Vector2 delta = context?.Player != null ? npc.Position - (Vector2)context.Player.transform.position : Vector2.positiveInfinity;
                    bool nearby = Mathf.Abs(delta.x) <= rangeX && Mathf.Abs(delta.y) <= rangeY;
                    DialogueRunner runner = Read<DialogueRunner>(npc, "_dialogues");
                    report.AppendLine($"NPC={npc.name}, ID={npc.NpcId}, 활성={npc.isActiveAndEnabled}, 등록={registered}, 실행 가능={npc.CanInteract}, 범위 안={nearby}, 위치={npc.Position}, 거리차={delta}, 대화 연결={runner != null}, 대화 중={runner?.IsRunning}");
                }
            }
            Debug.Log(report.ToString());
        }

        private static T Read<T>(object owner, string field)
        {
            FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new InvalidOperationException("검사 대상 필드가 변경됐습니다: " + field);
            return (T)info.GetValue(owner);
        }
    }
}
