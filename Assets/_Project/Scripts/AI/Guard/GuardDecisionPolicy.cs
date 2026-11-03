using System;
using System.Collections.Generic;
using UnityEngine;
using AdventureGame.LLM;

namespace AdventureGame.AI.Guard
{
    public struct GuardTurnOutcome
    {
        public int AppliedDelta;
        public int Trust;
        public bool NewConcernAddressed;
        public int ConcernsAddressed;
        public bool OpensGate;
        public bool RefusesToTalk;
    }

    public struct GuardPolicyState
    {
        public int Trust;
        public int ConsecutiveHostileMessages;
        public int SmallTalkCount;
        public int MessagesWithoutProgress;
        public HashSet<string> AddressedConcerns;
    }

    // Modelin değerlendirmesini (intent, topic, persuasion_delta) oyun durumuna çevirir. Kapıyı açma kararı
    // modelde değil, buradadır: kapı ancak güven eşiği aşılınca veya yeterli dert anlaşılınca açılır.
    // Muhafızın birden fazla derdi vardır, oyuncu bunlardan herhangi birkaçına değinerek ikna edebilir,
    // böylece tek bir doğru konuşma yolu yerine farklı yaklaşımlar sağlanır.
    [Serializable]
    public class GuardDecisionPolicy
    {
        [Header("Güven")]

        [Tooltip("Oyuncu ile muhafız arasındaki güvenin başlangıç değeri.")]
        [SerializeField] private int startingTrust = 25;

        [Tooltip("Kapının açılması için gereken minimum güven.")]
        [SerializeField] private int passThreshold = 90;

        [Tooltip("Artarda bu kadar düşmanca mesajdan sonra muhafız konuşmayı keser.")]
        [SerializeField] private int hostileMessageLimit = 3;

        [Tooltip("Muhafız sakinleştikten sonra güvenin döneceği minimum değer.")]
        [SerializeField] private int trustAfterCooldown = 10;

        [Header("Dertler")]

        [Tooltip("Muhafızın bir derdine ilk kez samimiyetle değinildiğinde verilen ek güven.")]
        [SerializeField] private int concernBonus = 10;

        [Tooltip("Kapının açılması için değinilmesi gereken farklı dert sayısı.")]
        [SerializeField] private int concernsToPass = 3;

        [Tooltip("Yeterli dert anlaşıldığında kapının açılması için gereken minimum güven.")]
        [SerializeField] private int concernTrustFloor = 60;

        [Tooltip("Aynı derde tekrar değinildiğinde alınabilecek en fazla güven (aynı konuyu tekrarlamak ilerletmez).")]
        [SerializeField] private int repeatedConcernMaxDelta = 4;

        [Tooltip("Selamlaşma ve sorulardan en fazla bu kadar mesaj güven kazandırır; sohbet tek başına kapıyı açtırmaz.")]
        [SerializeField] private int rewardedSmallTalkLimit = 3;

        [Tooltip("Oyuncu bu kadar mesaj boyunca yeni bir derde ulaşamazsa muhafız açık bir ipucu verir.")]
        [SerializeField] private int stuckMessageLimit = 2;

        private int trust;
        private int consecutiveHostileMessages;
        private int smallTalkCount;
        private int messagesWithoutProgress;
        private HashSet<string> addressedConcerns = new HashSet<string>();

        public int Trust => trust;
        public int PassThreshold => passThreshold;
        public int ConcernsToPass => concernsToPass;
        public IReadOnlyCollection<string> AddressedConcerns => addressedConcerns;
        public bool IsPlayerStuck => messagesWithoutProgress >= stuckMessageLimit;

        public void Reset()
        {
            trust = startingTrust;
            consecutiveHostileMessages = 0;
            smallTalkCount = 0;
            messagesWithoutProgress = 0;
            addressedConcerns = new HashSet<string>();
        }

        public GuardPolicyState CaptureState()
        {
            return new GuardPolicyState
            {
                Trust = trust,
                ConsecutiveHostileMessages = consecutiveHostileMessages,
                SmallTalkCount = smallTalkCount,
                MessagesWithoutProgress = messagesWithoutProgress,
                AddressedConcerns = new HashSet<string>(addressedConcerns)
            };
        }

        public void RestoreState(GuardPolicyState state)
        {
            trust = state.Trust;
            consecutiveHostileMessages = state.ConsecutiveHostileMessages;
            smallTalkCount = state.SmallTalkCount;
            messagesWithoutProgress = state.MessagesWithoutProgress;
            addressedConcerns = new HashSet<string>(state.AddressedConcerns);
        }

        public GuardTurnOutcome Evaluate(NPCActionResponse evaluation)
        {
            int delta = ClampDeltaForIntent(evaluation.intent, evaluation.persuasion_delta);
            if (IsSmallTalk(evaluation.intent) && ++smallTalkCount > rewardedSmallTalkLimit)
            {
                delta = Mathf.Min(delta, 0);
            }

            // Konu yalnızca empati veya somut yardım teklifinde sayılır; soru sormak derdi sadece ortaya çıkarır
            bool addressesConcern = IsSincere(evaluation.intent) && evaluation.topic != NPCResponseParser.NoTopic && delta > 0;
            bool newConcern = addressesConcern && addressedConcerns.Add(evaluation.topic);
            if (newConcern)
            {
                delta += concernBonus;
            }
            else if (addressesConcern)
            {
                delta = Mathf.Min(delta, repeatedConcernMaxDelta);
            }

            trust = Mathf.Clamp(trust + delta, 0, 100);
            messagesWithoutProgress = newConcern ? 0 : messagesWithoutProgress + 1;
            consecutiveHostileMessages = IsHostile(evaluation.intent) ? consecutiveHostileMessages + 1 : 0;

            bool concernsResolved = addressedConcerns.Count >= concernsToPass && trust >= concernTrustFloor;
            bool opensGate = trust >= passThreshold || concernsResolved;

            return new GuardTurnOutcome
            {
                AppliedDelta = delta,
                Trust = trust,
                NewConcernAddressed = newConcern,
                ConcernsAddressed = addressedConcerns.Count,
                OpensGate = opensGate,
                RefusesToTalk = !opensGate && (trust <= 0 || consecutiveHostileMessages >= hostileMessageLimit)
            };
        }

        public void CalmDown()
        {
            consecutiveHostileMessages = 0;
            trust = Mathf.Max(trust, trustAfterCooldown);
        }

        private static bool IsHostile(string intent)
        {
            return intent == "THREATEN" || intent == "INSULT";
        }

        private static bool IsSmallTalk(string intent)
        {
            return intent == "GREETING" || intent == "QUESTION";
        }

        // Soru sormak derdi yalnızca ortaya çıkarır; derde karşılık vermek (empati veya çözüm) ilerletir
        private static bool IsSincere(string intent)
        {
            return intent == "PERSUADE" || intent == "EMPATHY";
        }

        // Model niyeti doğru sınıflandırıp puanı yanlış verirse (ör. tehdide +10) puan niyete uygun aralığa çekilir
        private static int ClampDeltaForIntent(string intent, int delta)
        {
            switch (intent)
            {
                case "THREATEN":
                case "INSULT":
                    return Mathf.Clamp(delta, NPCResponseParser.MinPersuasionDelta, -10);
                case "MANIPULATION":
                    return Mathf.Clamp(delta, -15, -5);
                case "BRIBE":
                case "LIE":
                    return Mathf.Clamp(delta, -8, -2);
                case "OFF_TOPIC":
                    return Mathf.Clamp(delta, -10, 0);
                case "REQUEST":
                    return Mathf.Clamp(delta, -2, 2);
                case "GREETING":
                case "QUESTION":
                    return Mathf.Clamp(delta, 1, 4);
                case "PERSUADE":
                case "EMPATHY":
                    return Mathf.Clamp(delta, 2, NPCResponseParser.MaxPersuasionDelta);
                default:
                    return Mathf.Clamp(delta, -5, 5);
            }
        }
    }
}
