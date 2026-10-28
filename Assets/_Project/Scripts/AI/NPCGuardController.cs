using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AdventureGame.Interfaces;
using AdventureGame.Interactions;
using AdventureGame.LLM;
using AdventureGame.UI;

namespace AdventureGame.AI
{
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
        [SerializeField] private int persuasionScore = 20;
        [SerializeField] private int requiredScoreToPass = 70;

        private ILLMService llmService;
        private bool isDialogueActive = false;
        private bool isGateOpened = false;
        private bool isConvincedSequenceCompleted = false;
        private Vector3 playerConvincedPosition;
        private Coroutine autoCloseCoroutine;

        private List<OllamaChatMessage> conversationHistory = new List<OllamaChatMessage>();

        private const string SystemPrompt =
            "Sen bir krallık kalesinin ana kapısını koruyan kıdemli, şüpheci ve disiplinli bir Baş Muhafızsın.\n" +
            "GÖREVİN: Kapıdan kimseyi geçirmemek. Ancak oyuncunun diplomasi, mantık veya belgelerle seni ikna etmesine göre karar vereceksin.\n\n" +
            "KATI KURALLAR:\n" +
            "1. Sadece kusursuz ve doğal bir TÜRKÇE kullan. Asla İngilizce kelime kullanma.\n" +
            "2. Bir kale muhafızı gibi ciddi ol; ferman, mühür veya resmi görev sorgula.\n" +
            "3. Oyuncunun her mesajını değerlendirip 'persuasion_delta' (-25 ile +30 arası) ve 'decision' üret:\n" +
            "   - Hakaret, tehdit, saçmalık: persuasion_delta: -20, decision: 'DENY'\n" +
            "   - Sıradan iddialar, zayıf bahaneler: persuasion_delta: -5, decision: 'DENY'\n" +
            "   - Saygılı diplomasi, mantıklı kurgu: persuasion_delta: +15, decision: 'DENY'\n" +
            "   - Kraliyet mührü, resmi ferman, acil savaş haberi veya güçlü ikna: persuasion_delta: +30, decision: 'ALLOW'\n\n" +
            "ZORUNLU JSON ŞEMASI:\n" +
            "{\n" +
            "  \"reply\": \"Muhafızın oyuncuya söyleyeceği Türkçe diyalog cümlesi\",\n" +
            "  \"decision\": \"ALLOW veya DENY\",\n" +
            "  \"persuasion_delta\": 15,\n" +
            "  \"attitude\": \"Şüpheci veya Etkilenmiş veya İkna Oldu\",\n" +
            "  \"reason\": \"Kararın gerekçesi\"\n" +
            "}";

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

            conversationHistory.Add(new OllamaChatMessage("system", SystemPrompt));
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
                    StartVictoryDialogue();
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
            string greeting = "Dur yabancı! Bu kapı sadece krallık izni olanlara açılır. Ne hakla geçmek istersin?";
            dialogueUI.OpenDialogue("Kraliyet Muhafızı", greeting, HandlePlayerMessage);
        }

        private void StartVictoryDialogue()
        {
            isDialogueActive = true;
            isConvincedSequenceCompleted = true;
            playerConvincedPosition = playerTransform.position;
            dialogueUI.ShowSimpleMessage("Kraliyet Muhafızı", "Görevinde başarılar şanlı maceracı! Yolun açık olsun.");
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

            conversationHistory.Add(new OllamaChatMessage("user", playerMessage));

            var (isSuccess, actionResponse, errorMessage) = await llmService.SendChatHistoryAsync(conversationHistory);

            if (!isSuccess)
            {
                dialogueUI.ShowError(errorMessage);
                return;
            }

            persuasionScore = Mathf.Clamp(persuasionScore + actionResponse.persuasion_delta, 0, 100);

            string deltaSign = actionResponse.persuasion_delta > 0 ? "+" : "";
            Debug.Log($"[NPCGuard] Karar: {actionResponse.decision} | Puan: {persuasionScore}/100 ({deltaSign}{actionResponse.persuasion_delta}) | Tutum: {actionResponse.attitude} | Sebep: {actionResponse.reason}");

            conversationHistory.Add(new OllamaChatMessage("assistant",
                $"{{\"reply\":\"{actionResponse.reply}\",\"decision\":\"{actionResponse.decision}\",\"persuasion_delta\":{actionResponse.persuasion_delta}}}"));

            bool isConvinced = actionResponse.decision == "ALLOW" || persuasionScore >= requiredScoreToPass;

            if (isConvinced)
            {
                isGateOpened = true;

                // Yanıt tamamen ekranda yazılıp bitince kapı açılır
                dialogueUI.ShowNPCResponse(actionResponse.reply, true, () =>
                {
                    if (finalDoor != null) finalDoor.OpenDoor();

                    playerConvincedPosition = playerTransform.position;
                    isConvincedSequenceCompleted = true;

                    if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
                    autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(10f));
                });
            }
            else
            {
                dialogueUI.ShowNPCResponse(actionResponse.reply, false, null);
            }
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