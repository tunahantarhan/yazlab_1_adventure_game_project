using UnityEngine;
using AdventureGame.UI;

namespace AdventureGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Hareket Ayarlari")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;

        private CharacterController characterController;
        private Vector3 velocity;
        private bool isMovementLocked = false;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            DialogueUIController.OnDialogueStateChanged += HandleDialogueState;
        }

        private void OnDisable()
        {
            DialogueUIController.OnDialogueStateChanged -= HandleDialogueState;
        }

        private void HandleDialogueState(bool isDialogueOpen)
        {
            isMovementLocked = isDialogueOpen;
            if (isMovementLocked)
            {
                velocity = Vector3.zero; // hareket durdurma
            }
        }

        private void Update()
        {
            // Diyalog açıkken WASD engellenir
            if (isMovementLocked) return;

            HandleMovement();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                characterController.Move(direction * (moveSpeed * Time.deltaTime));
            }

            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}