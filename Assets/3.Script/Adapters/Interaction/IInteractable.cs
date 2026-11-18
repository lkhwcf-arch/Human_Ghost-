using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // K15 상호작용 대상입니다. 종류는 Talk·Door·Ledger·Shrine 네 가지입니다.
    public interface IInteractable
    {
        event System.Action Changed;
        bool CanInteract { get; }
        // [정수화 미적용 이유] 안내 표시·Transform 경계의 월드 위치입니다. 상호작용 후보 선택·거리 판정은 정수 좌표(InteractionCandidate)로 합니다.
        Vector2 Position { get; }
        // 표시 글자 Language ID(30063~30066)입니다.
        int PromptTextId { get; }
        void Interact(InteractionContext context);
    }
}
