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

    // Modelin değerlendirmesini oyun durumuna çevirir. Kapıyı açma kararı modelde değil, buradadır:
    // model tek mesajda "ALLOW" dese bile güven puanı yeterli değilse kapı açılmaz.
    // Muhafızın birden fazla derdi vardı, oyuncu bunlardan herhangi birkaçına çözüm sunarak ikna edebilir,
    // böylece tek bir doğru konuşma yolu yerine farklı yaklaşımlar sağlanır.
    [Serializable]
    public class GuardDecisionPolicy
    {
        [SerializeField] private int startingTrust = 30;
        [SerializeField] private int passThreshold = 70;
        [Tooltip("Model ALLOW dediğinde veya yeterli dert çözüldüğünde kapının açılması için gereken minimum güven.")]
        [SerializeField] private int allowDecisionFloor = 50;
        [Tooltip("Artarda bu kadar düşmanca mesajdan sonra muhafız konuşmayı keser.")]
        [SerializeField] private int hostileMessageLimit = 3;
        [Tooltip("Muhafız sakinleştikten sonra güvenin döneceği minimum değer.")]
        [SerializeField] private int trustAfterCooldown = 10;

        [Header("Dertler")]
        [Tooltip("Muhafızın bir derdine ilk kez samimiyetle değinildiğinde verilen ek güven.")]
        [SerializeField] private int concernBonus = 10;
        [Tooltip("Kapının açılması için çözüm sunulması gereken farklı dert sayısı.")]
        [SerializeField] private int concernsToPass = 2;
        [Tooltip("Aynı derde tekrar değinildiğinde alınabilecek en fazla güven (aynı konuyu tekrarlamak ilerletmez).")]
        [SerializeField] private int repeatedConcernMaxDelta = 4;

        private int trust;
        private int consecutiveHostileMessages;
        private HashSet<string> addressedConcerns = new HashSet<string>();

        public int Trust => trust;
        public int PassThreshold => passThreshold;
        public int ConcernsToPass => concernsToPass;
        public IReadOnlyCollection<string> AddressedConcerns => addressedConcerns;

        public void Reset()
        {
            trust = startingTrust;
            consecutiveHostileMessages = 0;
            addressedConcerns = new HashSet<string>();
        }

        public GuardTurnOutcome Evaluate(NPCActionResponse response)
        {
            int delta = ClampDeltaForIntent(response.intent, response.persuasion_delta);

            // Model niyeti yanlış sınıflandırsa bile tavrı öfkeli/şüpheliyse derde değinilmiş sayılmaz
            bool addressesConcern = IsSincere(response.intent) && !IsUnfriendly(response.attitude)
                                    && response.topic != NPCResponseParser.NoTopic && delta > 0;
            bool newConcern = addressesConcern && addressedConcerns.Add(response.topic);
            if (newConcern)
            {
                delta += concernBonus;
            }
            else if (addressesConcern)
            {
                delta = Mathf.Min(delta, repeatedConcernMaxDelta);
            }

            trust = Mathf.Clamp(trust + delta, 0, 100);
            consecutiveHostileMessages = IsHostile(response.intent) ? consecutiveHostileMessages + 1 : 0;

            bool aboveFloor = trust >= allowDecisionFloor;
            bool modelAllows = response.decision == NPCResponseParser.DecisionAllow && aboveFloor;
            bool concernsResolved = addressedConcerns.Count >= concernsToPass && aboveFloor;
            bool opensGate = trust >= passThreshold || modelAllows || concernsResolved;

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

        private static bool IsUnfriendly(string attitude)
        {
            return attitude == "ANGRY" || attitude == "SUSPICIOUS";
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
