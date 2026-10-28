using System.Collections.Generic;
using System.Threading.Tasks;
using AdventureGame.LLM;

namespace AdventureGame.Interfaces
{
    public interface ILLMService
    {
        Task<(bool isSuccess, NPCActionResponse action, string errorMessage)> SendChatHistoryAsync(List<OllamaChatMessage> conversationHistory);
    }
}