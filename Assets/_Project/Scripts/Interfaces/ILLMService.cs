using System.Collections.Generic;
using System.Threading.Tasks;
using AdventureGame.LLM;

namespace AdventureGame.Interfaces
{
    public interface ILLMService
    {
        // Model cevabı verilen şemaya göre ayrıştırılıp doğrulanır
        Task<LLMResult> SendChatHistoryAsync(List<OllamaChatMessage> conversationHistory, NPCResponseSchema schema);

        // Sunucunun çalıştığını ve modelin yüklü olduğunu kontrol eder (Action alanı boş döner)
        Task<LLMResult> CheckAvailabilityAsync();
    }
}
