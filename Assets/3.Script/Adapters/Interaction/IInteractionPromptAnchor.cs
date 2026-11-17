using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 공용 말걸기 안내를 대상 위의 특정 위치에 붙이고 싶은 상호작용 대상이 선택적으로 구현합니다(2026-11-17 NPC 이름표).
    // 상호작용 거리 기준인 IInteractable.Position과는 별개이며, 구현하지 않은 대상(서낭당·문·명부)은 기존 배치를 그대로 씁니다.
    // [정수화 미적용 이유] 화면 표시(Transform) 경계의 월드 위치라 Unity Vector2를 씁니다. 계산은 구현 측에서 정수로 합니다.
    public interface IInteractionPromptAnchor
    {
        // 안내 묶음(배경·테두리 포함)의 아랫변 가운데가 놓일 월드 위치입니다. 표시 준비가 안 됐으면 false를 돌려 기존 배치를 씁니다.
        bool TryGetPromptBase(out Vector2 position);
    }
}
