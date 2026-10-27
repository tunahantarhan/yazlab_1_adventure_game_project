using System.Threading.Tasks;
using AdventureGame.LLM;

namespace AdventureGame.Interfaces
{
    public interface ILLMService
    {
        Task<(bool isSuccess, NPCActionResponse action, string errorMessage)> SendChatPromptAsync(string systemPrompt, string playerMessage);
    }
}