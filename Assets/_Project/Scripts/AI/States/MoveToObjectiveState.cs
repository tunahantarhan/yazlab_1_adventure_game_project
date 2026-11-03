using UnityEngine;
using UnityEngine.AI;

namespace AdventureGame.AI.States
{
    // Hedefe NavMesh ile yol bulur. Hedef etkileşim mesafesinde ve sensör görüş hattını doğruladığında etkileşime geçer.
    // Yol geçersizse veya ajan engel yüzünden takılırsa yeniden yol hesaplar; denemeler biterse görevden vazgeçer.
    public class MoveToObjectiveState : CompanionStateBase
    {
        private const float StuckSpeedThreshold = 0.1f;

        private float stuckTimer;
        private int repathAttempts;

        public MoveToObjectiveState(CompanionController companion) : base(companion) { }

        public override CompanionStateType Type => CompanionStateType.MoveToObjective;

        public override void Enter()
        {
            stuckTimer = 0f;
            repathAttempts = 0;
            companion.MoveTo(companion.ObjectivePoint, true);
        }

        public override void UpdateState()
        {
            var objective = companion.TargetInteractable;
            if (objective == null || !objective.CanInteract)
            {
                companion.ChangeState(CompanionStateType.FollowPlayer);
                return;
            }

            NavMeshAgent agent = companion.Agent;
            Vector3 destination = companion.ObjectivePoint;

            if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Fail("Hedefe giden tam bir yol bulunamadı.");
                return;
            }

            companion.AdjustSpeedForObstacles();

            TargetReading reading = companion.Sensor.SenseTarget(destination, companion.ObjectiveRoot);
            if (reading.Distance <= companion.InteractDistance && reading.HasLineOfSight)
            {
                companion.ChangeState(CompanionStateType.Interacting);
                return;
            }

            companion.MoveTo(destination);
            CheckIfStuck(agent, destination);
        }

        private void CheckIfStuck(NavMeshAgent agent, Vector3 destination)
        {
            if (agent.pathPending || agent.velocity.magnitude > StuckSpeedThreshold)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer < companion.StuckTimeout) return;

            stuckTimer = 0f;
            repathAttempts++;
            if (repathAttempts > companion.MaxRepathAttempts)
            {
                Fail("Ajan takıldı, hedefe ulaşılamıyor.");
                return;
            }

            Debug.Log($"[Companion] Takıldı, yol yeniden hesaplanıyor ({repathAttempts}/{companion.MaxRepathAttempts}).");
            agent.ResetPath();
            companion.MoveTo(destination, true);
        }

        private void Fail(string reason)
        {
            Debug.LogWarning($"[Companion] Görev başarısız: {reason}");
            companion.ReportObjectiveFinished(false);
            companion.ChangeState(CompanionStateType.Refusing);
        }
    }
}
