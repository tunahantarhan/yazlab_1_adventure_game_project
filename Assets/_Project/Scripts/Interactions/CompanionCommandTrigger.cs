using UnityEngine;
using AdventureGame.AI;
using AdventureGame.Interfaces;

namespace AdventureGame.Interactions
{
    // Oyuncu bu noktada E'ye basınca companion görev hedefine gönderilir. Hedef kullanıldıktan sonra
    // veya companion zaten görevdeyken etkileşim kapanır, böylece ipucu da görünmez.
    public class CompanionCommandTrigger : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private CompanionController companion;

        public bool CanPlayerInteract =>
            companion != null &&
            companion.TargetInteractable != null &&
            companion.TargetInteractable.CanInteract &&
            companion.CurrentStateType == CompanionStateType.FollowPlayer;

        public void PlayerInteract()
        {
            if (CanPlayerInteract) companion.IssueObjectiveCommand();
        }
    }
}
