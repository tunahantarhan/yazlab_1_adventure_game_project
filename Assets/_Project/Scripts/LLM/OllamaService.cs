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
        [SerializeField] private string modelName = "llama3.2:3b";
        [SerializeField] private float timeoutSeconds = 30f;

        public async Task<(bool isSuccess, NPCActionResponse action, string errorMessage)> SendChatPromptAsync(string systemPrompt, string playerMessage)
        {
            // İstek Gövdesi Hazırlığı
            var requestBody = new OllamaChatRequest
            {
                model = modelName,
                stream = false,
                format = "json",
                messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage("system", systemPrompt),
                    new OllamaChatMessage("user", playerMessage)
                }
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
                    // Asenkron istek - FPS dondurmayacak
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    // Ağ ve Bağlantı Hata Kontrolü
                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        string err = $"Ollama Sunucu Hatası: {request.error}. Ollama'nın çalıştığından emin olun (localhost:11434).";
                        Debug.LogError($"[OllamaService] {err}");
                        return (false, null, err);
                    }

                    // Ollama Yanıtını Çözümleme
                    string responseText = request.downloadHandler.text;
                    OllamaChatResponse chatResponse = JsonUtility.FromJson<OllamaChatResponse>(responseText);

                    if (chatResponse == null || chatResponse.message == null || string.IsNullOrEmpty(chatResponse.message.content))
                    {
                        return (false, null, "Modelden boş yanıt döndü.");
                    }

                    // Modelin Ürettiği JSON'ı NPC Davranışına Çözümleme
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
                    // Herhangi bir error veya beklenmeyen istisnada oyunun çökmesini engelle
                    Debug.LogError($"[OllamaService] Beklenmeyen Hata: {ex.Message}");
                    return (false, null, $"Sistem Hatası: {ex.Message}");
                }
            }
        }
    }
}