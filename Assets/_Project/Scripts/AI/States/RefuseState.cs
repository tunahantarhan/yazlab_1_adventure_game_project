using UnityEngine;

namespace AdventureGame.AI.States
{
    // Emir yerine getirilemiyorsa (şalter zaten çekilmiş, hedefe yol yok) kafa sallayıp oyuncuyu takibe döner
    public class RefuseState : CompanionStateBase
    {
        private const float Duration = 0.9f;
        private const float ShakeAngle = 25f;
        private const float ShakeFrequency = 3f;

        private Quaternion baseRotation;
        private float timer;

        public RefuseState(CompanionController companion) : base(companion) { }

        public override CompanionStateType Type => CompanionStateType.Refusing;

        public override void Enter()
        {
            timer = 0f;
            companion.StopMoving();
            companion.Agent.updateRotation = false;
            baseRotation = companion.transform.rotation;
        }

        public override void UpdateState()
        {
            timer += Time.deltaTime;

            float yaw = Mathf.Sin(timer * ShakeFrequency * 2f * Mathf.PI) * ShakeAngle;
            companion.transform.rotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);

            if (timer >= Duration)
            {
                companion.ChangeState(CompanionStateType.FollowPlayer);
            }
        }

        public override void Exit()
        {
            companion.transform.rotation = baseRotation;
            companion.Agent.updateRotation = true;
        }
    }
}
