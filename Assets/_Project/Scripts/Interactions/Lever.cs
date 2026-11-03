using UnityEngine;
using AdventureGame.Interfaces;

namespace AdventureGame.Interactions
{
    public class Lever : MonoBehaviour, IInteractable, IResettable
    {
        [Header("Etkilesim Ayarlari")]
        [SerializeField] private DoorController targetDoor;
        [SerializeField] private Transform interactionPoint;

        private bool isActivated = false;
        private Quaternion initialLocalRotation;

        public string InteractionPrompt => isActivated ? "Kullanildi" : "Salteri Indir";
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
        public bool CanInteract => !isActivated;

        private void Awake()
        {
            initialLocalRotation = transform.localRotation;
        }

        public void Interact()
        {
            if (isActivated) return;

            isActivated = true;
            Debug.Log("[Lever] Salter cekildi, kapi aciliyor!");

            // Şalterin görseli 90 derece döndürülür, açıldığı izlenimi verilir
            transform.Rotate(45f, 0f, 0f);

            if (targetDoor != null)
            {
                targetDoor.OpenDoor();
            }
        }

        // Şalter ve bağlı kapı defaault haline döner
        public void ResetState()
        {
            isActivated = false;
            transform.localRotation = initialLocalRotation;
            if (targetDoor != null) targetDoor.ResetState();
        }
    }
}
