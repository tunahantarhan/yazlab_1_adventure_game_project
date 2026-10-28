using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AdventureGame.Interfaces;

namespace AdventureGame.LLM
{
    public class OllamaService : MonoBehaviour, ILLMService
    {
        [Header("Ollama Ayarları")]
        [SerializeField] private string ollamaUrl = "http://localhost:11434/api/chat";
        [SerializeField] private string modelName = "qwen2.5:7b";
        [SerializeField] private float timeoutSeconds = 35f;

        public async Task<(bool isSuccess, NPCActionResponse action, string errorMessage)> SendChatHistoryAsync(List<OllamaChatMessage> conversationHistory)
        {
            var requestBody = new OllamaChatRequest
            {
                model = modelName,
                stream = false,
                format = "json",
                messages = conversationHistory,
                options = new OllamaOptions { temperature = 0.2f }
            };

            string jsonPayload = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = (int)timeoutSeconds;

                try
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        string err = $"Ollama Bağlantı Hatası: {request.error}. (Ollama ve qwen2.5:3b açık mı?)";
                        Debug.LogError($"[OllamaService] {err}");
                        return (false, null, err);
                    }

                    string responseText = request.downloadHandler.text;
                    OllamaChatResponse chatResponse = JsonUtility.FromJson<OllamaChatResponse>(responseText);

                    if (chatResponse == null || chatResponse.message == null || string.IsNullOrEmpty(chatResponse.message.content))
                    {
                        return (false, null, "Modelden boş yanıt döndü.");
                    }

                    string innerJson = chatResponse.message.content.Trim();
                    NPCActionResponse parsedAction = JsonUtility.FromJson<NPCActionResponse>(innerJson);

                    if (parsedAction == null || string.IsNullOrEmpty(parsedAction.decision))
                    {
                        return (false, null, "Model beklenen JSON formatında karar üretemedi.");
                    }

                    return (true, parsedAction, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[OllamaService] İstisna: {ex.Message}");
                    return (false, null, $"Sistem Hatası: {ex.Message}");
                }
            }
        }
    }
}