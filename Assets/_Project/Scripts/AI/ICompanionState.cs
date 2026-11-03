namespace AdventureGame.AI
{
    public enum CompanionStateType
    {
        FollowPlayer,
        MoveToObjective,
        Interacting,
        Refusing,
        InDialogue
    }

    public interface ICompanionState
    {
        CompanionStateType Type { get; }
        void Enter();
        void UpdateState();
        void Exit();
    }
}
