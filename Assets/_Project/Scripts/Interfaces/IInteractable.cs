using UnityEngine;

namespace AdventureGame.Interfaces
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionPoint { get; }
        void Interact();
    }
}