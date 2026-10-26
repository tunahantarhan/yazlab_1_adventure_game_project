using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AdventureGame.Interfaces;

namespace AdventureGame.AI
{
    public enum CompanionStateType
    {
        FollowPlayer,
        MoveToObjective,
        Interacting
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private MonoBehaviour objectiveInteractable; // Şalter veya başka bir etkileşimli nesne referansı

        [Header("Mesafe Ayarlari")]
        [SerializeField] private float followDistance = 3.5f;
        [SerializeField] private float interactDistance = 1.8f;

        [Header("Sensor (Raycast) Ayarlari")]
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
        }

        private void Start()
        {
            SetState(CompanionStateType.FollowPlayer);
        }

        private void Update()
        {
            // Raycast sensörü ile önündeki engelleri algılar
            PerformRaycastSensor();

            // Oyuncu 'E' tuşuna basarsa şalteri açma görevi verilir
            if (Input.GetKeyDown(KeyCode.E) && targetInteractable != null)
            {
                Debug.Log("[Companion] Oyuncudan emir alindi: Saltere gidiliyor!");
                SetState(CompanionStateType.MoveToObjective);
            }

            // Durum mantığını günceller
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
                    // İşlem bittikten sonra tekrar oyuncuyu takibe dön
                    SetState(CompanionStateType.FollowPlayer);
                    break;
            }
        }

        // Sensör tabanlı gözlem mekanizması (Raycast)
        private void PerformRaycastSensor()
        {
            Vector3 startPos = transform.position + Vector3.up * 0.5f;
            Vector3 forwardDir = transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(startPos, forwardDir, out hit, sensorRange))
            {
                // Engel tespit edildiyse kırmızı çizgi
                Debug.DrawRay(startPos, forwardDir * hit.distance, Color.red);
            }
            else
            {
                // Önü açıksa yeşil çizgi
                Debug.DrawRay(startPos, forwardDir * sensorRange, Color.green);
            }
        }
    }
}