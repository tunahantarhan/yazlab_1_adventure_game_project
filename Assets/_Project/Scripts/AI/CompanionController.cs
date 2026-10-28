using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AdventureGame.Interfaces;
using AdventureGame.UI; // CS0103 hatasını çözen kütüphane

namespace AdventureGame.AI
{
    public enum CompanionStateType
    {
        FollowPlayer,
        MoveToObjective,
        Interacting,
        InDialogue
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private MonoBehaviour objectiveInteractable;

        [Header("Mesafe Ayarları")]
        [SerializeField] private float followDistance = 3.5f;
        [SerializeField] private float interactDistance = 1.8f;

        [Header("Sensör (Raycast) Ayarları")]
        [SerializeField] private float sensorRange = 5f;
        [SerializeField] private LayerMask obstacleLayer;

        private NavMeshAgent navAgent;
        private IInteractable targetInteractable;
        private CompanionStateType currentState;

        public NavMeshAgent Agent => navAgent;
        public Transform PlayerTarget => playerTarget;
        public IInteractable TargetInteractable => targetInteractable;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();

            if (objectiveInteractable is IInteractable interactable)
            {
                targetInteractable = interactable;
            }

            if (playerTarget == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTarget = player.transform;
            }
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
            SetState(CompanionStateType.FollowPlayer);
        }

        private void Update()
        {
            PerformRaycastSensor();

            // Input kutusuna yazı yazılırken 'E' tuşunu tamamen yok say
            if (DialogueUIController.IsInputFocused) return;

            // Oyuncu serbestken 'E' tuşuna basarsa şaltere yönlendir
            if (Input.GetKeyDown(KeyCode.E) && targetInteractable != null && currentState != CompanionStateType.InDialogue)
            {
                Debug.Log("[Companion] Oyuncudan emir alındı: Şaltere gidiliyor!");
                SetState(CompanionStateType.MoveToObjective);
            }

            ExecuteCurrentState();
        }

        public void SetState(CompanionStateType newState)
        {
            currentState = newState;
        }

        private void ExecuteCurrentState()
        {
            switch (currentState)
            {
                case CompanionStateType.FollowPlayer:
                    if (playerTarget != null)
                    {
                        float dist = Vector3.Distance(transform.position, playerTarget.position);
                        if (dist > followDistance)
                        {
                            navAgent.SetDestination(playerTarget.position);
                        }
                        else
                        {
                            navAgent.ResetPath();
                        }
                    }
                    break;

                case CompanionStateType.MoveToObjective:
                    if (targetInteractable != null)
                    {
                        Vector3 dest = targetInteractable.InteractionPoint.position;
                        navAgent.SetDestination(dest);

                        float distToGoal = Vector3.Distance(transform.position, dest);
                        if (distToGoal <= interactDistance)
                        {
                            SetState(CompanionStateType.Interacting);
                        }
                    }
                    break;

                case CompanionStateType.Interacting:
                    navAgent.ResetPath();
                    if (targetInteractable != null)
                    {
                        targetInteractable.Interact();
                    }
                    SetState(CompanionStateType.FollowPlayer);
                    break;

                case CompanionStateType.InDialogue:
                    navAgent.ResetPath();
                    if (playerTarget != null)
                    {
                        Vector3 lookDir = (playerTarget.position - transform.position).normalized;
                        lookDir.y = 0;
                        if (lookDir.sqrMagnitude > 0.01f)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 3f * Time.deltaTime);
                        }
                    }
                    break;
            }
        }

        private void HandleDialogueState(bool isInDialogue)
        {
            if (isInDialogue)
            {
                SetState(CompanionStateType.InDialogue);
            }
            else
            {
                if (currentState != CompanionStateType.MoveToObjective && currentState != CompanionStateType.Interacting)
                {
                    SetState(CompanionStateType.FollowPlayer);
                }
            }
        }

        private void PerformRaycastSensor()
        {
            Vector3 startPos = transform.position + Vector3.up * 0.5f;
            Vector3 forwardDir = transform.forward;

            RaycastHit hit;
            bool isBlocked = obstacleLayer.value != 0
                ? Physics.Raycast(startPos, forwardDir, out hit, sensorRange, obstacleLayer)
                : Physics.Raycast(startPos, forwardDir, out hit, sensorRange);

            if (isBlocked)
            {
                Debug.DrawRay(startPos, forwardDir * hit.distance, Color.red);
            }
            else
            {
                Debug.DrawRay(startPos, forwardDir * sensorRange, Color.green);
            }
        }
    }
}