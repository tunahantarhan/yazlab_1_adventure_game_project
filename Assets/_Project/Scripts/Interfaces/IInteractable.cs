using UnityEngine;

namespace AdventureGame.Interfaces
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionPoint { get; }
        bool CanInteract { get; }
        void Interact();
    }
}