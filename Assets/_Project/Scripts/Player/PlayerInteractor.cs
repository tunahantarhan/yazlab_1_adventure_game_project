using UnityEngine;
using AdventureGame.UI;

namespace AdventureGame.Player
{
    // Etkileşim tuşu yalnızca ekranda görünen bir ipucu varsa çalışır;
    // birden fazla ipucu görünüyorsa en yakını seçilir
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private void Update()
        {
            if (DialogueUIController.IsInputFocused || !Input.GetKeyDown(interactKey)) return;

            WorldPromptIndicator nearest = WorldPromptIndicator.FindNearestShown(transform.position);
            if (nearest != null) nearest.Interact();
        }
    }
}
