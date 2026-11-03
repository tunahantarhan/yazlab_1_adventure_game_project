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
        private const string SilentTreatmentMessage = "*Meçhul öfkeyle gözlerini dikmiş sana bakıyor, afalladığın için konuşamıyorsun.*";

        private ILLMService llmService;
        private readonly GuardPromptBuilder promptBuilder = new GuardPromptBuilder();
        private readonly List<OllamaChatMessage> conversationHistory = new List<OllamaChatMessage>(); // system prompt hariç

        private bool isDialogueActive = false;
        private bool isGateOpened = false;
        private bool isConvincedSequenceCompleted = false;
        private float angryUntilTime = 0f;
        private Vector3 playerConvincedPosition;
        private Coroutine autoCloseCoroutine;
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
            dialogueUI.OpenDialogue(GuardPromptBuilder.GuardName, Greeting, HandlePlayerMessage);
            UpdateMoodIndicator(NPCResponseParser.DefaultAttitude);

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

        private async void HandlePlayerMessage(string playerMessage)
        {
            if (llmService == null)
            {
                dialogueUI.ShowError("LLM Servisi bulunamadı!");
                return;
            }

            int session = dialogueSession;
            var userMessage = new OllamaChatMessage("user", playerMessage);
            conversationHistory.Add(userMessage);

            LLMResult result = await llmService.SendChatHistoryAsync(BuildRequestMessages());
            if (this == null) return;

            // Yanıt beklenirken oyuncu bir şekilde diyaloğu kapattıysa yanıt yok sayılır
            if (session != dialogueSession || !isDialogueActive)
            {
                conversationHistory.Remove(userMessage);
                Debug.Log("[NPCGuard] Diyalog kapandığı için gelen yanıt yok sayıldı.");
                return;
            }

            if (!result.IsSuccess)
            {
                // Cevapsız kalan mesaj geçmişten çıkarılır, oyuncu aynı mesajı tekrar gönderebilir
                conversationHistory.Remove(userMessage);
                dialogueUI.ShowError($"{result.ErrorMessage} Mesajını tekrar gönderebilirsin.");
                return;
            }

            NPCActionResponse action = result.Action;
            GuardTurnOutcome outcome = decisionPolicy.Evaluate(action);

            Debug.Log($"[NPCGuard] Niyet: {action.intent} | Dert: {action.topic}{(outcome.NewConcernAddressed ? " (YENİ)" : "")} " +
                      $"({outcome.ConcernsAddressed}/{decisionPolicy.ConcernsToPass}) | Güven: {outcome.Trust}/100 ({outcome.AppliedDelta:+0;-0;0}, model: {action.persuasion_delta:+0;-0;0}) " +
                      $"| Model kararı: {action.decision} | Tavır: {action.attitude} | Kapı: {(outcome.OpensGate ? "AÇILIYOR" : "kapalı")} | Sebep: {action.reason}");

            // Güven eşiği bu turda aşıldıysa model cevabını eski puanla ("açamam") üretmiş olabilir;
            // kapıyı açtığını söyleyen yeni bir cevap yine modelden istenir
            if (outcome.OpensGate && action.decision != NPCResponseParser.DecisionAllow)
            {
                LLMResult concession = await llmService.SendChatHistoryAsync(BuildRequestMessages(true));
                if (this == null) return;

                if (concession.IsSuccess)
                {
                    action = concession.Action;
                    Debug.Log($"[NPCGuard] Güven eşiği aşıldı, kapı kodla açılıyor. Açılış cevabı → Model kararı: {action.decision} | Tavır: {action.attitude}");
                }
                else
                {
                    Debug.LogWarning($"[NPCGuard] Kapı açılış cevabı alınamadı, ilk cevap kullanılıyor: {concession.ErrorMessage}");
                }

                if (session != dialogueSession || !isDialogueActive)
                {
                    // Oyuncu bu sırada uzaklaştıysa ikna yine geçerlidir, kapı doğrudan açılır
                    isGateOpened = true;
                    if (finalDoor != null) finalDoor.OpenDoor();
                    return;
                }
            }

            UpdateMoodIndicator(action.attitude);

            // Modelin kendi yanıtı geçerli JSON olarak geçmişe eklenir (tırnak vb. karakterler doğru kaçırılır)
            conversationHistory.Add(new OllamaChatMessage("assistant", JsonUtility.ToJson(action)));

            if (outcome.OpensGate)
            {
                isGateOpened = true;

                // Yanıt tamamen ekranda yazılıp bitince kapı açılır
                dialogueUI.ShowNPCResponse(action.reply, () =>
                {
                    if (finalDoor != null) finalDoor.OpenDoor();

                    playerConvincedPosition = playerTransform.position;
                    isConvincedSequenceCompleted = true;
                    RestartAutoClose(10f);
                });
            }
            else if (outcome.RefusesToTalk)
            {
                // Muhafız öfkelenir: bir süre konuşmaz, sonra sakinleşir
                angryUntilTime = Time.time + angerCooldownSeconds;
                decisionPolicy.CalmDown();
                dialogueUI.ShowNPCResponse(action.reply, () =>
                {
                    dialogueUI.ShowError($"Meçhul öfkelendi ve konuşmayı kesti. {angerCooldownSeconds:0} saniye sonra tekrar dene.", false);
                    RestartAutoClose(3f);
                });
            }
            else
            {
                dialogueUI.ShowNPCResponse(action.reply);
            }
        }

        // Her istekte güncel güven puanıyla yeniden üretilen system prompt + son N mesaj gönderilir
        private List<OllamaChatMessage> BuildRequestMessages(bool isConvinced = false)
        {
            int start = Mathf.Max(0, conversationHistory.Count - maxHistoryMessages);
            var messages = new List<OllamaChatMessage>
            {
                new OllamaChatMessage("system", promptBuilder.Build(decisionPolicy.Trust, decisionPolicy.PassThreshold, decisionPolicy.AddressedConcerns))
            };
            messages.AddRange(conversationHistory.GetRange(start, conversationHistory.Count - start));

            if (isConvinced)
            {
                messages.Add(new OllamaChatMessage("system", GuardPromptBuilder.ConvincedInstruction));
            }
            return messages;
        }

        // Oyuncu muhafızın tavrını ve ilerlemesini görür; hangi yolun işe yaradığını tahmin etmek zorunda kalmaz
        private void UpdateMoodIndicator(string attitude)
        {
            dialogueUI.SetMoodIndicator(
                $"{AttitudeLabel(attitude)}  ·  Güven {decisionPolicy.Trust}/{decisionPolicy.PassThreshold}  ·  " +
                $"Anlaşılan dert {decisionPolicy.AddressedConcerns.Count}/{decisionPolicy.ConcernsToPass}");
        }

        private static string AttitudeLabel(string attitude)
        {
            switch (attitude)
            {
                case "ANGRY": return "Öfkeli";
                case "SUSPICIOUS": return "Şüpheli";
                case "SYMPATHETIC": return "Yumuşadı";
                case "CONVINCED": return "İkna oldu";
                default: return "Temkinli";
            }
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
