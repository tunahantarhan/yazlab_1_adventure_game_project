using UnityEngine;
using UnityEngine.Serialization;
using AdventureGame.UI;

namespace AdventureGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Hareket Ayarlari")]
        [FormerlySerializedAs("moveSpeed")]
        [SerializeField] private float walkSpeed = 3f;
        [Tooltip("Shift basılıyken kullanılan hız.")]
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;

        private CharacterController characterController;
        private Vector3 velocity;
        private bool isMovementLocked = false;
        private bool isControlEnabled = true;
        private Transform cameraTransform;

        public bool IsRunning { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
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

        // Ara sahnelerde (örneğin oyun sonu merdiven inişi) oyuncu dışarıdan hareket ettirilir;
        // CharacterController kapatılır ve böylece transform'a doğrudan verilen konum ezilmez
        public void SetControlEnabled(bool isEnabled)
        {
            isControlEnabled = isEnabled;
            characterController.enabled = isEnabled;
            velocity = Vector3.zero;
        }

        private void Update()
        {
            if (!isControlEnabled) return;
            HandleMovement();
        }

        private void HandleMovement()
        {
            // Diyalog açıkken veya input kutusuna yazılırken yön girdisi yok sayılır, yerçekimi uygulanmaya devam eder
            bool canMove = !isMovementLocked && !DialogueUIController.IsInputFocused;
            float horizontal = canMove ? Input.GetAxis("Horizontal") : 0f;
            float vertical = canMove ? Input.GetAxis("Vertical") : 0f;

            Vector3 direction = GetCameraRelativeDirection(horizontal, vertical);
            bool isMoving = direction.magnitude >= 0.1f;
            IsRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            if (isMoving)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                float speed = IsRunning ? runSpeed : walkSpeed;
                characterController.Move(direction * (speed * Time.deltaTime));
            }

            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        // W her zaman ekranın yukarısına gider; kamera açısı değişse de kontroller ekrana göre kalır
        private Vector3 GetCameraRelativeDirection(float horizontal, float vertical)
        {
            if (cameraTransform == null)
            {
                return new Vector3(horizontal, 0f, vertical).normalized;
            }

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;

            return (forward.normalized * vertical + right.normalized * horizontal).normalized;
        }
    }
}