namespace AdventureGame.AI
{
    public interface ICompanionState
    {
        void Enter();
        void UpdateState();
        void Exit();
    }
}