using System;
using UnityEngine;

namespace AdventureGame.LLM
{
    // Modelin ürettiği metni NPCActionResponse'a çevirir, alanları doğrular.
    // Serbest metin eşleştirmesi yapılmaz, geçerli JSON ve şemaya uyan değerler dışındaki her şey reddedilir.
    public class NPCResponseParser
    {
        public const string DecisionAllow = "ALLOW";
        public const string DecisionDeny = "DENY";
        public const int MinPersuasionDelta = -25;
        public const int MaxPersuasionDelta = 20;

        public const string UnknownIntent = "UNKNOWN";
        public const string DefaultAttitude = "NEUTRAL";
        public const string NoTopic = "NONE";

        public static readonly string[] Intents =
        {
            "GREETING", "QUESTION", "PERSUADE", "EMPATHY", "LIE",
            "BRIBE", "THREATEN", "INSULT", "OFF_TOPIC", "MANIPULATION"
        };

        public static readonly string[] Topics = { "CAPTAIN", "SUPPLIES", "FAMILY", "DUTY", NoTopic };

        public static readonly string[] Attitudes = { "ANGRY", "SUSPICIOUS", "NEUTRAL", "SYMPATHETIC", "CONVINCED" };

        public bool TryParse(string content, out NPCActionResponse response, out string error)
        {
            response = null;

            string json = ExtractJsonObject(content);
            if (json == null)
            {
                error = "Model yanıtında JSON nesnesi bulunamadı.";
                return false;
            }

            try
            {
                response = JsonUtility.FromJson<NPCActionResponse>(json);
            }
            catch (ArgumentException)
            {
                error = "Model geçersiz JSON üretti.";
                return false;
            }

            if (response == null)
            {
                error = "Model yanıtı ayrıştırılamadı.";
                return false;
            }

            // JsonUtility eksik alanları sessizce boş bıraktığı için zorunlu alanlar elle doğrulanır
            if (string.IsNullOrWhiteSpace(response.reply))
            {
                error = "Model yanıtında 'reply' alanı eksik.";
                return false;
            }

            // Bazı çok dilli modeller cümlenin ortasında başka bir dile kayabiliyor, bu tarz yanıtlar tekrar denenir
            if (ContainsForeignScript(response.reply))
            {
                error = "Model yanıtı Türkçe dışı karakterler içeriyor.";
                return false;
            }

            string decision = Normalize(response.decision);
            if (decision != DecisionAllow && decision != DecisionDeny)
            {
                error = $"Model geçersiz bir karar üretti: '{response.decision}'.";
                return false;
            }

            response.reply = response.reply.Trim();
            response.decision = decision;
            response.persuasion_delta = Mathf.Clamp(response.persuasion_delta, MinPersuasionDelta, MaxPersuasionDelta);

            // Tanımsız niyet/tavır yanıtı geçersiz kılmaz, nötr değerlere çekilir ve karar politikası buna göre davranır
            response.intent = OneOf(Normalize(response.intent), Intents, UnknownIntent);
            response.attitude = OneOf(Normalize(response.attitude), Attitudes, DefaultAttitude);
            response.topic = OneOf(Normalize(response.topic), Topics, NoTopic);

            error = null;
            return true;
        }

        private static string Normalize(string value)
        {
            return value?.Trim().ToUpperInvariant();
        }

        // Kiril, Japonca, Çince vb. alfabelerden harf içeriyor mu (Latin dışı harfler)
        private static bool ContainsForeignScript(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c) && c > 'ɏ') return true;
            }
            return false;
        }

        private static string OneOf(string value, string[] allowed, string fallback)
        {
            return Array.IndexOf(allowed, value) >= 0 ? value : fallback;
        }

        // Model JSON'u ```json bloğu veya açıklama metniyle sarmalarsa yalnızca nesne kısmı alınır
        private static string ExtractJsonObject(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;

            return content.Substring(start, end - start + 1);
        }
    }
}
