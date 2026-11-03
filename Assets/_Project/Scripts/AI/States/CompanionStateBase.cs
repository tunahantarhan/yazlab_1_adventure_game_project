namespace AdventureGame.AI.States
{
    // Tüm companion durumlarının ortak tabanı; durumlar bağlam olarak CompanionController'ı kullanır
    public abstract class CompanionStateBase : ICompanionState
    {
        protected readonly CompanionController companion;

        protected CompanionStateBase(CompanionController companion)
        {
            this.companion = companion;
        }

        public abstract CompanionStateType Type { get; }

        public virtual void Enter() { }
        public abstract void UpdateState();
        public virtual void Exit() { }
    }
}
