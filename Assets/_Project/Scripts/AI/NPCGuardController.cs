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
    public class NPCGuardController : MonoBehaviour, IPlayerInteractable
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
        private const string FarewellMessage = "*Meçhul başıyla açık kapıyı gösteriyor, sonra sessizce nöbet yerine dönüyor.*";
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
        private int dialogueSession = 0; // Diyalog kapanıp açılınca eski servis uyarıları yok sayılır
        private string currentAttitude = NPCResponseParser.DefaultAttitude;
        private bool isTurnInProgress = false;
        private bool isDoorOpenPending = false; // İkna edildikten sonra kapı anlatım satırında açılır
        private bool isAngerPending = false; // Öfke sayacı muhafızın son sözü bitip diyalog kapanınca başlar
        private System.Action deferredTurnPresentation; // Diyalog kapalıyken gelen cevap, tekrar açılınca gösterilir

        private bool IsAngry => isAngerPending || Time.time < angryUntilTime;

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
            if (!isOpen) OnDialogueClosed();
        }

        // Kapanış ESC, mesafe veya otomatik kapanmadan gelse de tek yerden işlenir
        private void OnDialogueClosed()
        {
            if (!isDialogueActive) return;

            isDialogueActive = false;
            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }

            // Oyuncu kapı açılış anlatımını beklemeden çıktıysa kapı yine de açılır
            if (isDoorOpenPending) OpenFinalDoor();

            if (isAngerPending)
            {
                isAngerPending = false;
                angryUntilTime = Time.time + angerCooldownSeconds;
                if (angerIndicator != null) angerIndicator.Show(angerCooldownSeconds);
            }
        }

        private void OpenFinalDoor()
        {
            isDoorOpenPending = false;
            if (finalDoor != null) finalDoor.OpenDoor();
        }

        private void Update()
        {
            if (playerTransform == null || !isDialogueActive) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);

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
        }

        // Etkileşim tuşu (E) PlayerInteractor üzerinden, ipucu görünürken gelir
        public bool CanPlayerInteract => !isDialogueActive && playerTransform != null;

        public void PlayerInteract()
        {
            if (!CanPlayerInteract) return;

            if (isTurnInProgress || deferredTurnPresentation != null)
            {
                ResumeDialogue();
            }
            else if (isGateOpened)
            {
                ShowNarration(FarewellMessage, "CONVINCED");
            }
            else if (IsAngry)
            {
                int remainingSeconds = Mathf.CeilToInt(angryUntilTime - Time.time);
                ShowNarration(SilentTreatmentMessage, "ANGRY", $"Meçhul hâlâ öfkeli. {remainingSeconds} saniye sonra tekrar dene.");
            }
            else
            {
                StartDialogue();
            }
        }

        private void StartDialogue()
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = false;
            dialogueSession++;

            // Konuşma daha önce başladıysa karşılama yerine muhafızın son sözü tekrar gösterilir
            string openingLine = conversationHistory.Count > 0 ? lastGuardReply : Greeting;
            // Tavır panelden önce güncellenir ki portre çerçevesi açılışta doğru renkte olsun
            UpdateMoodIndicator(currentAttitude, immediate: true);
            dialogueUI.OpenDialogue(GuardPromptBuilder.GuardName, openingLine, HandlePlayerMessage);

            WarnIfServiceUnavailable(dialogueSession);
        }

        // Muhafız düşünürken diyalog kapatıldıysa istek arka planda sürer,
        // geri dönünce bekleme ya da gelen cevap gösterilir
        private void ResumeDialogue()
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = false;
            dialogueSession++;
            dialogueUI.ResumeDialogue(GuardPromptBuilder.GuardName, HandlePlayerMessage);

            if (deferredTurnPresentation != null)
            {
                var present = deferredTurnPresentation;
                deferredTurnPresentation = null;
                present.Invoke();
            }
            else
            {
                dialogueUI.SetThinkingState(true, resetTimer: false);
            }
        }

        // Sessizlik yazısı ile kapı açıldıktan sonraki veda yazısı LLM'e gitmeyen sabit anlatım satırlarıdır
        private void ShowNarration(string message, string attitude, string statusMessage = null)
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = true;
            playerConvincedPosition = playerTransform.position;
            dialogueUI.ShowSimpleMessage(GuardPromptBuilder.GuardName, message, attitude, statusMessage);
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
            dialogueUI.CloseDialogue();
            OnDialogueClosed();
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

            isTurnInProgress = true;
            GuardPolicyState stateBeforeTurn = decisionPolicy.CaptureState();

            // 1) Değerlendirme
            LLMResult evaluationResult = await llmService.SendChatHistoryAsync(BuildEvaluationMessages(playerMessage), NPCResponseSchema.Evaluation);
            if (!IsTurnStillValid(evaluationResult, stateBeforeTurn)) return;

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
            if (!IsTurnStillValid(replyResult, stateBeforeTurn)) return;

            isTurnInProgress = false;
            NPCActionResponse reply = replyResult.Action;
            Debug.Log($"[NPCGuard] Cevap → Karar: {reply.decision} | Tavır: {reply.attitude}");

            lastGuardReply = reply.reply;

            // Modelin kendi cevabı geçerli JSON olarak geçmişe eklenir (tırnak vb. karakterler doğru kaçırılır)
            conversationHistory.Add(userMessage);
            conversationHistory.Add(new OllamaChatMessage("assistant",
                JsonUtility.ToJson(new GuardReplyMessage { attitude = reply.attitude, decision = reply.decision, reply = reply.reply })));

            // Kapıyı kod açtığı için tavır göstergesi de kararla tutarlı tutulur
            string attitude = outcome.OpensGate ? "CONVINCED" : reply.attitude;
            PresentTurn(() =>
            {
                currentAttitude = attitude;
                UpdateMoodIndicator(attitude, outcome.AppliedDelta);
                PresentReply(reply, outcome);
            });
        }

        // Diyalog açıksa cevap hemen gösterilir, kapalıysa oyuncu muhafıza geri döndüğünde gösterilir
        private void PresentTurn(System.Action present)
        {
            if (isDialogueActive) present.Invoke();
            else deferredTurnPresentation = present;
        }

        private void PresentReply(NPCActionResponse reply, GuardTurnOutcome outcome)
        {
            if (outcome.OpensGate)
            {
                isGateOpened = true;
                isDoorOpenPending = true;

                // Muhafızın (LLM) cevabı bittikten sonra SPACE ile kapının açıldığını anlatan sahne satırı gelir ve kapı açılır
                dialogueUI.ShowNPCResponse(reply.reply, () =>
                {
                    dialogueUI.ShowNarrationAfterSpace(GateOpeningNarration, () =>
                    {
                        OpenFinalDoor();

                        playerConvincedPosition = playerTransform.position;
                        isConvincedSequenceCompleted = true;
                        RestartAutoClose(10f);
                    });
                });
            }
            else if (outcome.RefusesToTalk)
            {
                // Muhafız öfkelenir: bir süre konuşmaz, sonra sakinleşir
                isAngerPending = true;
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

        // İstek başarısızsa tur geri alınır, oyuncu aynı mesajı tekrar gönderebilir
        private bool IsTurnStillValid(LLMResult result, GuardPolicyState stateBeforeTurn)
        {
            if (this == null) return false;
            if (result.IsSuccess) return true;

            isTurnInProgress = false;
            decisionPolicy.RestoreState(stateBeforeTurn);
            if (isDialogueActive)
            {
                dialogueUI.ShowError($"{result.ErrorMessage} Mesajını tekrar gönderebilirsin.");
            }
            else
            {
                Debug.Log($"[NPCGuard] Diyalog kapalıyken istek başarısız oldu: {result.ErrorMessage}");
            }
            return false;
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
