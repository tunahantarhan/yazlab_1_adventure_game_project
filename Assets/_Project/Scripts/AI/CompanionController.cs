using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AdventureGame.AI.States;
using AdventureGame.Animation;
using AdventureGame.Interfaces;
using AdventureGame.UI;

namespace AdventureGame.AI
{
    // Companion'ın bağlam sınıfı: durumları (State pattern) yönetir, durumların ortak kullandığı
    // NavMesh, sensör ve hedef bilgisini sağlar. Karar mantığı States klasöründeki sınıflardadır.
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private MonoBehaviour objectiveInteractable;

        [Header("Mesafe Ayarları")]
        [SerializeField] private float followDistance = 3.5f;
        [SerializeField] private float interactDistance = 1.8f;
        [Tooltip("Oyuncu bu mesafeden uzaklaşırsa companion koşarak yetişir (örneğin oyuncu koşarken).")]
        [SerializeField] private float runDistance = 6f;
        [SerializeField] private float runSpeed = 6f;

        [Header("Animasyon")]
        [Tooltip("Modelin animasyonlarını süren bileşen (opsiyonel).")]
        [SerializeField] private CharacterAnimator characterAnimator;

        [Header("Sensör (Raycast) Ayarları")]
        [SerializeField] private float sensorRange = 5f;
        [SerializeField] private LayerMask obstacleLayer;
        [Tooltip("Önündeki engel bu mesafeden yakınsa yavaşlar.")]
        [SerializeField] private float slowDownDistance = 1.2f;
        [Range(0.1f, 1f)][SerializeField] private float slowDownFactor = 0.5f;

        [Header("Görev Ayarları")]
        [Tooltip("Ajan value'daki kadar saniye hareketsiz kalırsa takılmış sayılır ve yol yeniden hesaplanır.")]
        [SerializeField] private float stuckTimeout = 1.5f;
        [SerializeField] private int maxRepathAttempts = 3;
        [Tooltip("Etkileşimden önce hedefe dönmesi gereken maksimum açı farkı (derece bakımından).")]
        [SerializeField] private float facingTolerance = 10f;
        [SerializeField] private float turnSpeed = 6f;

        private const float RepathDistanceThreshold = 0.3f;

        private NavMeshAgent navAgent;
        private IInteractable targetInteractable;
        private ICompanionSensor sensor;
        private Dictionary<CompanionStateType, ICompanionState> states;
        private ICompanionState currentState;
        private float baseSpeed;
        private Vector3 lastDestination;
        private bool hasWarnedOffNavMesh = false;
        private bool isRunning = false;

        public event Action<CompanionStateType> StateChanged;
        public event Action<bool> ObjectiveFinished; // true: görev başarılı

        public NavMeshAgent Agent => navAgent;
        public ICompanionSensor Sensor => sensor;
        public Transform PlayerTarget => playerTarget;
        public IInteractable TargetInteractable => targetInteractable;
        public Transform ObjectiveRoot => objectiveInteractable != null ? objectiveInteractable.transform : null;
        public Vector3 ObjectivePoint => targetInteractable.InteractionPoint.position;
        public CompanionStateType CurrentStateType => currentState != null ? currentState.Type : CompanionStateType.FollowPlayer;

        public float FollowDistance => followDistance;
        public float InteractDistance => interactDistance;
        public float RunDistance => runDistance;
        public float StuckTimeout => stuckTimeout;
        public int MaxRepathAttempts => maxRepathAttempts;
        public float FacingTolerance => facingTolerance;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            baseSpeed = navAgent.speed;

            if (objectiveInteractable is IInteractable interactable)
            {
                targetInteractable = interactable;
            }

            if (playerTarget == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTarget = player.transform;
            }

            sensor = new RaycastCompanionSensor(transform, sensorRange, obstacleLayer);

            states = new Dictionary<CompanionStateType, ICompanionState>
            {
                { CompanionStateType.FollowPlayer, new FollowPlayerState(this) },
                { CompanionStateType.MoveToObjective, new MoveToObjectiveState(this) },
                { CompanionStateType.Interacting, new InteractState(this) },
                { CompanionStateType.Refusing, new RefuseState(this) },
                { CompanionStateType.InDialogue, new InDialogueState(this) }
            };
        }

        private void OnEnable()
        {
            DialogueUIController.OnDialogueStateChanged += HandleDialogueState;
        }

        private void OnDisable()
        {
            DialogueUIController.OnDialogueStateChanged -= HandleDialogueState;
        }

        private void Start()
        {
            ChangeState(CompanionStateType.FollowPlayer);
        }

        private void Update()
        {
            // Ajan NavMesh üzerinde değilse (bake edilmemiş zemin vb.) her frame hata basmak yerine bekler
            if (!navAgent.isOnNavMesh)
            {
                if (!hasWarnedOffNavMesh)
                {
                    Debug.LogWarning("[Companion] Ajan NavMesh üzerinde değil, NavMesh'in bake edildiğinden emin olun.");
                    hasWarnedOffNavMesh = true;
                }
                return;
            }
            hasWarnedOffNavMesh = false;

            currentState?.UpdateState();
        }

        // "Şaltere git" emri. Oyuncu CompanionCommandTrigger ile (E), test araçları doğrudan çağırır.
        public bool IssueObjectiveCommand()
        {
            if (targetInteractable == null) return false;

            // Yeni emir yalnızca takip durumundayken kabul edilir
            if (CurrentStateType != CompanionStateType.FollowPlayer)
            {
                return false;
            }

            if (!targetInteractable.CanInteract)
            {
                Debug.Log("[Companion] Hedef zaten kullanılmış, emir reddedildi.");
                ChangeState(CompanionStateType.Refusing);
                return false;
            }

            Debug.Log("[Companion] Oyuncudan emir alındı: Şaltere gidiliyor!");
            ChangeState(CompanionStateType.MoveToObjective);
            return true;
        }

        public void ChangeState(CompanionStateType newState)
        {
            if (!states.TryGetValue(newState, out var next)) return;

            currentState?.Exit();
            isRunning = false;
            currentState = next;
            currentState.Enter();

            StateChanged?.Invoke(newState);
        }

        public void ReportObjectiveFinished(bool success)
        {
            ObjectiveFinished?.Invoke(success);
        }

        // Hedef belirgin şekilde değişmediyse yol her frame yeniden hesaplanmaz
        public void MoveTo(Vector3 destination, bool force = false)
        {
            if (!navAgent.isOnNavMesh) return;
            if (!force && navAgent.hasPath && (destination - lastDestination).sqrMagnitude < RepathDistanceThreshold * RepathDistanceThreshold) return;

            navAgent.SetDestination(destination);
            lastDestination = destination;
        }

        public void StopMoving()
        {
            if (navAgent.isOnNavMesh) navAgent.ResetPath();
        }

        public void RotateTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        public void SetRunning(bool shouldRun)
        {
            isRunning = shouldRun;
        }

        public void PlayInteractAnimation()
        {
            if (characterAnimator != null) characterAnimator.PlayInteract();
        }

        // Sensör önünde yakın bir engel görürse ajan yavaşlar
        public void AdjustSpeedForObstacles()
        {
            ObstacleReading reading = sensor.SenseAhead();
            bool isClose = reading.IsBlocked && reading.Distance < slowDownDistance;
            float speed = isRunning ? runSpeed : baseSpeed;
            navAgent.speed = isClose ? speed * slowDownFactor : speed;
        }

        private void HandleDialogueState(bool isInDialogue)
        {
            if (isInDialogue)
            {
                ChangeState(CompanionStateType.InDialogue);
            }
            else if (CurrentStateType == CompanionStateType.InDialogue)
            {
                ChangeState(CompanionStateType.FollowPlayer);
            }
        }
    }
}
