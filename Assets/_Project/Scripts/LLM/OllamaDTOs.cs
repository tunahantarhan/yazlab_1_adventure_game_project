using System;
using System.Collections.Generic;

namespace AdventureGame.LLM
{
    [Serializable]
    public class OllamaOptions
    {
        public float temperature = 0.2f; // Ciddiyet ayarı
        public int num_ctx = 4096; // Context boyutu
        public int num_predict = 300; // Yanıt uzunluğu sınırı (gecikmeyi sınırlar)
    }

    [Serializable]
    public class OllamaChatRequest
    {
        public string model;
        public List<OllamaChatMessage> messages;
        public bool stream = false;
        public string format = "json";
        public string keep_alive = "15m"; // Model bellekte tutulur, sonraki yanıtlar hızlanır
        public OllamaOptions options = new OllamaOptions();
    }

    [Serializable]
    public class OllamaChatMessage
    {
        public string role; // "system", "user", "assistant"
        public string content;

        public OllamaChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    public class OllamaChatResponse
    {
        public string model;
        public OllamaChatMessage message;
        public bool done;
    }

    [Serializable]
    public class OllamaErrorResponse
    {
        public string error;
    }

    [Serializable]
    public class OllamaModelInfo
    {
        public string name;
    }

    // GET /api/tags yanıtı: yüklü modellerin listesi
    [Serializable]
    public class OllamaTagsResponse
    {
        public List<OllamaModelInfo> models;
    }
}
