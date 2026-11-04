using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AdventureGame.AI.Guard;
using AdventureGame.Interfaces;
using AdventureGame.Interactions;
using AdventureGame.LLM;
using AdventureGame.UI;

namespace AdventureGame.AI
{
    // Muhafız NPC'si: diyalog akışını yönetir. Prompt üretimi GuardPromptBuilder'da,
    // modelin değerlendirmesini oyun durumuna çevirme GuardDecisionPolicy'de, LLM iletişimi ILLMService'tedir.
    public class NPCGuardController : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [SerializeField] private MonoBehaviour llmServiceMono;
        [SerializeField] private DialogueUIController dialogueUI;
        [SerializeField] private DoorController finalDoor;
        [Tooltip("Muhafız öfkelendiğinde başının üstünde beliren ikon.")]
        [SerializeField] private AngerIndicator angerIndicator;

        [Header("Etkileşim Ayarları")]
        [SerializeField] private float interactionDistance = 4.5f;
        [SerializeField] private float leaveDistanceAfterConvinced = 2.5f;
        [SerializeField] private Transform playerTransform;

        [Header("İkna Mekaniği")]
        [SerializeField] private GuardDecisionPolicy decisionPolicy = new GuardDecisionPolicy();
        [Tooltip("Muhafız öfkelenip konuşmayı kestikten sonra tekrar konuşmak için beklenecek süre (saniye).")]
        [SerializeField] private float angerCooldownSeconds = 20f;
        [Tooltip("Modele gönderilen en fazla geçmiş mesaj sayısı (system prompt hariç). Yanıt süresini sınırlar.")]
        [SerializeField] private int maxHistoryMessages = 10;

        private const string Greeting = "Dur! Kaptan Van Dijk'ın mührü olmadan bu kapıdan kimse geçemez. Gerçi kaptanı aylardır gören yok ya... Ne arıyorsun burada?";
        private const string FarewellMessage = "Yolun açık olsun. Dışarıda kaptanı görürsen, Meçhul hâlâ kapıda de.";
        private const string GateOpeningNarration = "*Meçhul paslı sürgüyü çekiyor, kapı gıcırdayarak açılıyor.*";
        private const string SilentTreatmentMessage = "*Meçhul öfkeyle gözlerini dikmiş sana bakıyor, afalladığın için konuşamıyorsun.*";

        private const int ConcessionHistoryMessages = 4;

        // Geçmişe eklenen muhafız cevabı, değerlendirme alanları olmadan yalnızca cevap şeması saklanır
        [System.Serializable]
        private class GuardReplyMessage
        {
            public string attitude;
            public string decision;
            public string reply;
        }

        private ILLMService llmService;
        private readonly GuardPromptBuilder promptBuilder = new GuardPromptBuilder();
        private readonly List<OllamaChatMessage> conversationHistory = new List<OllamaChatMessage>(); // system prompt hariç

        private bool isDialogueActive = false;
        private bool isGateOpened = false;
        private bool isConvincedSequenceCompleted = false;
        private float angryUntilTime = 0f;
        private Vector3 playerConvincedPosition;
        private Coroutine autoCloseCoroutine;
        private string lastGuardReply = Greeting; // Değerlendirmede oyuncunun neye cevap verdiğini anlamak için
        private int dialogueSession = 0; // Diyalog kapanıp açılınca eski isteklerin yanıtları yok sayılır

        private bool IsAngry => Time.time < angryUntilTime;

        private void Awake()
        {
            if (llmServiceMono is ILLMService service)
            {
                llmService = service;
            }

            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            decisionPolicy.Reset();
        }

        private void OnEnable()
        {
            DialogueUIController.OnDialogueStateChanged += HandleDialogueStateChanged;
        }

        private void OnDisable()
        {
            DialogueUIController.OnDialogueStateChanged -= HandleDialogueStateChanged;
        }

        // Diyalog UI tarafından (mesela ESC ile) kapatılırsa muhafızın durumu da senkronlanır
        private void HandleDialogueStateChanged(bool isOpen)
        {
            if (isOpen || !isDialogueActive) return;

            isDialogueActive = false;
            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);

            if (isDialogueActive)
            {
                // Yalnızca yazı tamamen bittiğinde ve kapı açıldığında mesafe kontrolü devreye girer
                if (isConvincedSequenceCompleted)
                {
                    float distanceFromConvincedPoint = Vector3.Distance(playerTransform.position, playerConvincedPosition);
                    if (distanceFromConvincedPoint >= leaveDistanceAfterConvinced || distance > interactionDistance)
                    {
                        CloseDialogue();
                    }
                    return;
                }

                if (distance > interactionDistance)
                {
                    CloseDialogue();
                }
                return;
            }

            // F tuşu interaksiyonu
            if (Input.GetKeyDown(KeyCode.F) && distance <= interactionDistance)
            {
                if (isGateOpened)
                {
                    ShowNarration(FarewellMessage);
                }
                else if (IsAngry)
                {
                    ShowNarration(SilentTreatmentMessage);
                }
                else
                {
                    StartDialogue();
                }
            }
        }

        private void StartDialogue()
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = false;
            dialogueSession++;
            lastGuardReply = Greeting;
            dialogueUI.OpenDialogue(GuardPromptBuilder.GuardName, Greeting, HandlePlayerMessage);
            UpdateMoodIndicator(NPCResponseParser.DefaultAttitude, immediate: true);

            WarnIfServiceUnavailable(dialogueSession);
        }

        // Sessizlik yazısı ile kapı açıldıktan sonraki veda yazısı LLM'e gitmeyen sabit anlatım satırlarıdır
        private void ShowNarration(string message)
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = true;
            playerConvincedPosition = playerTransform.position;
            dialogueUI.ShowSimpleMessage(GuardPromptBuilder.GuardName, message);
        }

        // Ollama kapalıysa veya model yoksa oyuncu mesaj yazmadan önce bilgilendirilir
        private async void WarnIfServiceUnavailable(int session)
        {
            if (llmService == null)
            {
                dialogueUI.ShowError("LLM servisi bağlanmamış (NPCGuardController > Llm Service Mono).");
                return;
            }

            LLMResult availability = await llmService.CheckAvailabilityAsync();
            if (this == null || availability.IsSuccess || session != dialogueSession || !isDialogueActive) return;

            dialogueUI.ShowError(availability.ErrorMessage);
        }

        private void CloseDialogue()
        {
            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }

            isDialogueActive = false;
            dialogueUI.CloseDialogue();
        }

        // Her tur iki LLM çağrısından oluşur: önce oyuncunun mesajı değerlendirilir (JSON: intent, topic, persuasion_delta),
        // kapı kararı bu değerlendirmeye göre kodda verilir, ardından muhafızın cevabı (JSON: attitude, decision, reply) istenir
        private async void HandlePlayerMessage(string playerMessage)
        {
            if (llmService == null)
            {
                dialogueUI.ShowError("LLM Servisi bulunamadı!");
                return;
            }

            int session = dialogueSession;
            GuardPolicyState stateBeforeTurn = decisionPolicy.CaptureState();

            // 1) Değerlendirme
            LLMResult evaluationResult = await llmService.SendChatHistoryAsync(BuildEvaluationMessages(playerMessage), NPCResponseSchema.Evaluation);
            if (!IsTurnStillValid(session, evaluationResult, stateBeforeTurn)) return;

            NPCActionResponse evaluation = evaluationResult.Action;
            GuardTurnOutcome outcome = decisionPolicy.Evaluate(evaluation);

            Debug.Log($"[NPCGuard] Değerlendirme → Niyet: {evaluation.intent} | Dert: {evaluation.topic}{(outcome.NewConcernAddressed ? " (YENİ)" : "")} " +
                      $"({outcome.ConcernsAddressed}/{decisionPolicy.ConcernsToPass}) | Güven: {outcome.Trust}/100 ({outcome.AppliedDelta:+0;-0;0}, model: {evaluation.persuasion_delta:+0;-0;0}) " +
                      $"| Kapı: {(outcome.OpensGate ? "AÇILIYOR" : "kapalı")} | Sebep: {evaluation.reason}");

            // 2) Cevap: kapı açılıyorsa muhafız kapıyı açtığını söyleyen bir cevap üretir.
            // Manipülasyon olarak değerlendirilen mesaj cevap modeline gösterilmez (prompt injection savunması)
            bool isManipulation = evaluation.intent == "MANIPULATION";
            var userMessage = new OllamaChatMessage("user", isManipulation ? GuardPromptBuilder.ManipulationPlaceholder : playerMessage);
            List<OllamaChatMessage> replyMessages = outcome.OpensGate
                ? BuildConcessionMessages(playerMessage)
                : BuildReplyMessages(evaluation, userMessage);

            LLMResult replyResult = await llmService.SendChatHistoryAsync(replyMessages, NPCResponseSchema.Reply);
            if (!IsTurnStillValid(session, replyResult, stateBeforeTurn)) return;

            NPCActionResponse reply = replyResult.Action;
            Debug.Log($"[NPCGuard] Cevap → Karar: {reply.decision} | Tavır: {reply.attitude}");

            // Kapıyı kod açtığı için tavır göstergesi de kararla tutarlı tutulur
            UpdateMoodIndicator(outcome.OpensGate ? "CONVINCED" : reply.attitude, outcome.AppliedDelta);
            lastGuardReply = reply.reply;

            // Modelin kendi cevabı geçerli JSON olarak geçmişe eklenir (tırnak vb. karakterler doğru kaçırılır)
            conversationHistory.Add(userMessage);
            conversationHistory.Add(new OllamaChatMessage("assistant",
                JsonUtility.ToJson(new GuardReplyMessage { attitude = reply.attitude, decision = reply.decision, reply = reply.reply })));

            if (outcome.OpensGate)
            {
                isGateOpened = true;

                // Muhafızın (LLM) cevabı bittikten sonra SPACE ile kapının açıldığını anlatan sahne satırı gelir ve kapı açılır
                dialogueUI.ShowNPCResponse(reply.reply, () =>
                {
                    dialogueUI.ShowNarrationAfterSpace(GateOpeningNarration, () =>
                    {
                        if (finalDoor != null) finalDoor.OpenDoor();

                        playerConvincedPosition = playerTransform.position;
                        isConvincedSequenceCompleted = true;
                        RestartAutoClose(10f);
                    });
                });
            }
            else if (outcome.RefusesToTalk)
            {
                // Muhafız öfkelenir: bir süre konuşmaz, sonra sakinleşir
                angryUntilTime = Time.time + angerCooldownSeconds;
                if (angerIndicator != null) angerIndicator.Show(angerCooldownSeconds);
                decisionPolicy.CalmDown();
                dialogueUI.ShowNPCResponse(reply.reply, () =>
                {
                    dialogueUI.ShowError($"Meçhul öfkelendi ve konuşmayı kesti. {angerCooldownSeconds:0} saniye sonra tekrar dene.", false);
                    RestartAutoClose(3f);
                });
            }
            else
            {
                dialogueUI.ShowNPCResponse(reply.reply);
            }
        }

        // Yanıt beklenirken diyalog kapandıysa veya istek başarısızsa tur geri alınır; oyuncu aynı mesajı tekrar gönderebilir
        private bool IsTurnStillValid(int session, LLMResult result, GuardPolicyState stateBeforeTurn)
        {
            if (this == null) return false;

            if (session != dialogueSession || !isDialogueActive)
            {
                decisionPolicy.RestoreState(stateBeforeTurn);
                Debug.Log("[NPCGuard] Diyalog kapandığı için gelen yanıt yok sayıldı.");
                return false;
            }

            if (!result.IsSuccess)
            {
                decisionPolicy.RestoreState(stateBeforeTurn);
                dialogueUI.ShowError($"{result.ErrorMessage} Mesajını tekrar gönderebilirsin.");
                return false;
            }

            return true;
        }

        // Değerlendirme rol yapmadan, yalnızca muhafızın son sözü ve oyuncunun son mesajıyla yapılır
        private List<OllamaChatMessage> BuildEvaluationMessages(string playerMessage)
        {
            return new List<OllamaChatMessage>
            {
                new OllamaChatMessage("system", promptBuilder.BuildEvaluation(lastGuardReply, playerMessage))
            };
        }

        // Cevap prompt'u güncel güven, değerlendirme ve ipucu durumuyla her turda yeniden üretilir; son N mesaj eklenir
        private List<OllamaChatMessage> BuildReplyMessages(NPCActionResponse evaluation, OllamaChatMessage userMessage)
        {
            int start = Mathf.Max(0, conversationHistory.Count - maxHistoryMessages);
            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage("system", promptBuilder.BuildReply(evaluation, decisionPolicy.Trust,
                    decisionPolicy.AddressedConcerns, decisionPolicy.IsPlayerStuck))
            };
            messages.AddRange(conversationHistory.GetRange(start, conversationHistory.Count - start));
            messages.Add(userMessage);
            return messages;
        }

        // Kapı açılış cevabı kısa bir prompt ve yalnızca son birkaç mesajla istenir
        private List<OllamaChatMessage> BuildConcessionMessages(string playerMessage)
        {
            int start = Mathf.Max(0, conversationHistory.Count - ConcessionHistoryMessages);
            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage("system", promptBuilder.BuildConcession(playerMessage))
            };
            messages.AddRange(conversationHistory.GetRange(start, conversationHistory.Count - start));
            messages.Add(new OllamaChatMessage("user", playerMessage));
            return messages;
        }

        // Oyuncu muhafızın tavrını ve ilerlemesini görür; hangi yolun işe yaradığını tahmin etmek zorunda kalmaz
        private void UpdateMoodIndicator(string attitude, int trustDelta = 0, bool immediate = false)
        {
            dialogueUI.SetGuardStatus(new GuardStatus
            {
                Attitude = attitude,
                Trust = decisionPolicy.Trust,
                PassThreshold = decisionPolicy.PassThreshold,
                ConcernsAddressed = decisionPolicy.AddressedConcerns.Count,
                ConcernsToPass = decisionPolicy.ConcernsToPass,
                TrustDelta = trustDelta
            }, immediate);
        }

        private void RestartAutoClose(float delay)
        {
            if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(delay));
        }

        private IEnumerator AutoCloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (isDialogueActive)
            {
                CloseDialogue();
            }
        }
    }
}
