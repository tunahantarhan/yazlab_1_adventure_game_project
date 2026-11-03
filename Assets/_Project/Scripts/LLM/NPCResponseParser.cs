using System;
using UnityEngine;

namespace AdventureGame.LLM
{
    // Muhafız iki ayrı JSON şeması üretir: değerlendirme (intent, topic, persuasion_delta, reason)
    // ve cevap (attitude, decision, reply). Her şemada farklı alanlar zorunludur.
    public enum NPCResponseSchema
    {
        Evaluation,
        Reply
    }

    // Modelin ürettiği metni NPCActionResponse'a çevirir, alanları seçilen şemaya göre doğrular.
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
            "GREETING", "QUESTION", "REQUEST", "PERSUADE", "EMPATHY", "LIE",
            "BRIBE", "THREATEN", "INSULT", "OFF_TOPIC", "MANIPULATION"
        };

        public static readonly string[] Topics = { "CAPTAIN", "SUPPLIES", "FAMILY", "DUTY", NoTopic };

        public static readonly string[] Attitudes = { "ANGRY", "SUSPICIOUS", "NEUTRAL", "SYMPATHETIC", "CONVINCED" };

        public bool TryParse(string content, NPCResponseSchema schema, out NPCActionResponse response, out string error)
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
            error = schema == NPCResponseSchema.Evaluation ? ValidateEvaluation(response) : ValidateReply(response);
            return error == null;
        }

        private static string ValidateEvaluation(NPCActionResponse response)
        {
            if (string.IsNullOrWhiteSpace(response.intent))
            {
                return "Değerlendirmede 'intent' alanı eksik.";
            }

            // Tanımsız niyet/konu yanıtı geçersiz kılmaz, nötr değerlere çekilir ve karar politikası buna göre davranır
            response.intent = OneOf(Normalize(response.intent), Intents, UnknownIntent);
            response.topic = OneOf(Normalize(response.topic), Topics, NoTopic);
            response.persuasion_delta = Mathf.Clamp(response.persuasion_delta, MinPersuasionDelta, MaxPersuasionDelta);
            response.reason = response.reason?.Trim() ?? "";
            return null;
        }

        private static string ValidateReply(NPCActionResponse response)
        {
            if (string.IsNullOrWhiteSpace(response.reply))
            {
                return "Model yanıtında 'reply' alanı eksik.";
            }

            // Bazı çok dilli modeller cümlenin ortasında başka bir dile kayabiliyor, bu tarz yanıtlar tekrar denenir
            if (ContainsForeignScript(response.reply))
            {
                return "Model yanıtı Türkçe dışı karakterler içeriyor.";
            }

            string decision = Normalize(response.decision);
            if (decision != DecisionAllow && decision != DecisionDeny)
            {
                return $"Model geçersiz bir karar üretti: '{response.decision}'.";
            }

            response.reply = response.reply.Trim();
            response.decision = decision;
            response.attitude = OneOf(Normalize(response.attitude), Attitudes, DefaultAttitude);
            return null;
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
