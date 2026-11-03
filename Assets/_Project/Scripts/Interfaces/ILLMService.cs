using System.Collections.Generic;
using System.Threading.Tasks;
using AdventureGame.LLM;

namespace AdventureGame.Interfaces
{
    public interface ILLMService
    {
        Task<LLMResult> SendChatHistoryAsync(List<OllamaChatMessage> conversationHistory);

        // Sunucunun çalıştığını ve modelin yüklü olduğunu kontrol eder (Action alanı boş döner)
        Task<LLMResult> CheckAvailabilityAsync();
    }
}
