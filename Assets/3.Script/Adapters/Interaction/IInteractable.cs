using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // K15 상호작용 대상입니다. 종류는 Talk·Door·Ledger·Shrine 네 가지입니다.
    public interface IInteractable
    {
        bool CanInteract { get; }
        Vector2 Position { get; }
        // 표시 글자 Language ID(30063~30066)입니다.
        int PromptTextId { get; }
        void Interact(InteractionContext context);
    }
}
