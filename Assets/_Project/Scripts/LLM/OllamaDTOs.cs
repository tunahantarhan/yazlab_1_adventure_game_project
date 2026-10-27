using System;
using System.Collections.Generic;

namespace AdventureGame.LLM
{
    [Serializable]
    public class OllamaChatRequest
    {
        public string model;
        public List<OllamaChatMessage> messages;
        public bool stream = false;
        public string format = "json"; // zorunlu json formatı
    }

    [Serializable]
    public class OllamaChatMessage
    {
        public string role; // "system" veya "user"
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