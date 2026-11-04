namespace AdventureGame.Interfaces
{
    // Oyuncunun etkileşim tuşuyla (E) tetiklediği hedefler. Companion'ın görev hedefi olan 
    // IInteractable'dan ayrıdır.
    public interface IPlayerInteractable
    {
        bool CanPlayerInteract { get; }
        void PlayerInteract();
    }
}
