namespace AdventureGame.AI.States
{
    // Oyuncu diyalogdayken durur ve oyuncuya döner
    public class InDialogueState : CompanionStateBase
    {
        public InDialogueState(CompanionController companion) : base(companion) { }

        public override CompanionStateType Type => CompanionStateType.InDialogue;

        public override void Enter()
        {
            companion.StopMoving();
            companion.Agent.updateRotation = false;
        }

        public override void UpdateState()
        {
            if (companion.PlayerTarget != null)
            {
                companion.RotateTowards(companion.PlayerTarget.position);
            }
        }

        public override void Exit()
        {
            companion.Agent.updateRotation = true;
        }
    }
}
