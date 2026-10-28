using System;
using System.Collections.Generic;

namespace AdventureGame.LLM
{
    [Serializable]
    public class OllamaOptions
    {
        public float temperature = 0.2f; // Ciddiyet ayarı
        public int num_ctx = 4096; // Context boyutu
    }

    [Serializable]
    public class OllamaChatRequest
    {
        public string model;
        public List<OllamaChatMessage> messages;
        public bool stream = false;
        public string format = "json";
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
}