using System;

namespace AdventureGame.LLM
{
    // Modelden beklenen JSON alanları. Değerlendirme aşaması intent/topic/persuasion_delta/reason, cevap aşaması attitude/decision/reply alanlarını doldurur.
    [Serializable]
    public class NPCActionResponse
    {
        public string reply;            // NPC'nin oyuncuya söylediği Türkçe cümle
        public string intent;           // Oyuncunun son mesajının niyeti (GuardIntent değerleri)
        public string topic;            // Mesajın değindiği muhafız derdi (CAPTAIN, SUPPLIES, FAMILY, DUTY, NONE)
        public int persuasion_delta;    // Bu mesajın güvene etkisi (-25 ile +20 arası)
        public string decision;         // "ALLOW" veya "DENY" (modelin önerisi; son kararı kod verir)
        public string attitude;         // NPC'nin tavrı (ANGRY, SUSPICIOUS, NEUTRAL, SYMPATHETIC, CONVINCED)
        public string reason;           // Kararın kısa gerekçesi (analiz ve raporlama için)
    }
}
