using System;

namespace AdventureGame.LLM
{
    [Serializable]
    public class NPCActionResponse
    {
        public string reply;       // NPC'nin oyuncuya vereceği diyalog cevabı
        public string decision;    // "ALLOW" veya "DENY"
        public string reason;      // Kararın gerekçesi (Analiz ve raporlama için)
    }
}