namespace AdventureGame.Interfaces
{
    // Başlangıç durumuna döndürülebilen nesneler (ör. tekrarlanan companion testleri için şalter ve kapı)
    public interface IResettable
    {
        void ResetState();
    }
}
