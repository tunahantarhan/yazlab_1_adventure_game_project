using UnityEngine;

namespace AdventureGame.AI.States
{
    // Önce durup sensörün ölçtüğü açıya göre hedefe döner ve hizalanınca etkileşimi gerçekleştirir
    public class InteractState : CompanionStateBase
    {
        private const float MaxAlignTime = 2f;

        private float timer;

        public InteractState(CompanionController companion) : base(companion) { }

        public override CompanionStateType Type => CompanionStateType.Interacting;

        public override void Enter()
        {
            timer = 0f;
            companion.StopMoving();
            companion.Agent.updateRotation = false;
        }

        public override void UpdateState()
        {
            timer += Time.deltaTime;

            Vector3 point = companion.ObjectivePoint;
            TargetReading reading = companion.Sensor.SenseTarget(point, companion.ObjectiveRoot);
            companion.RotateTowards(point);

            if (Mathf.Abs(reading.Angle) > companion.FacingTolerance && timer < MaxAlignTime) return;

            var objective = companion.TargetInteractable;
            bool canInteract = objective != null && objective.CanInteract;
            if (canInteract)
            {
                companion.PlayInteractAnimation();
                objective.Interact();
            }

            companion.ReportObjectiveFinished(canInteract);
            companion.ChangeState(CompanionStateType.FollowPlayer);
        }

        public override void Exit()
        {
            companion.Agent.updateRotation = true;
        }
    }
}
