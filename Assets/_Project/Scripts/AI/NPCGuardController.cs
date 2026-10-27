using UnityEngine;
using AdventureGame.Interfaces;
using AdventureGame.Interactions;
using AdventureGame.UI;

namespace AdventureGame.AI
{
    public class NPCGuardController : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [SerializeField] private MonoBehaviour llmServiceMono; // OllamaService
        [SerializeField] private DialogueUIController dialogueUI;
        [SerializeField] private DoorController finalDoor; // Geçişi engelleyen son kapı (varsa)

        [Header("Etkileşim Ayarları")]
        [SerializeField] private float interactionDistance = 4f;
        [SerializeField] private Transform playerTransform;

        private ILLMService llmService;
        private bool isDialogueActive = false;
        private bool isGateOpened = false;

        // Sistem promptu net biçimde tanımlı ve JSON üreten yapı
        private const string SystemPrompt =
            "Sen fantastik bir kalede kilitli kapıyı koruyan sadık ve zeki bir kraliyet muhafızısın. " +
            "Görevin kapıdan kimseyi geçirmemektir. Ancak oyuncu çok geçerli bir krallık emri sunarsa, zekice bir rüşvet/diplomasi teklif ederse " +
            "veya seni gerçekten mantıklı bir bahaneyle ikna ederse kapıyı açabilirsin. Boş laflara ve tehditlere pabuç bırakma.\n\n" +
            "ÇOK ÖNEMLİ KURAL: Yanıtını SADECE ve KESİNLİKLE aşağıdaki geçerli JSON formatında ver, önüne arkasına açıklama ekleme:\n" +
            "{\n" +
            "  \"reply\": \"Oyuncuya vereceğin Türkçe diyalog cümlesi\",\n" +
            "  \"decision\": \"ALLOW\" veya \"DENY\",\n" +
            "  \"reason\": \"Neden izin verdiğinin veya reddettiğinin kısa gerekçesi\"\n" +
            "}";

        private void Awake()
        {
            if (llmServiceMono is ILLMService service)
            {
                llmService = service;
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // Oyuncu yaklaştığında ve F tuşuna bastığında diyaloğu aç/kapat
            if (distance <= interactionDistance)
            {
                if (Input.GetKeyDown(KeyCode.F) && !isDialogueActive && !isGateOpened)
                {
                    StartDialogue();
                }
            }
            else if (isDialogueActive)
            {
                // Oyuncu uzaklaşırsa diyaloğu otomatik kapat
                CloseDialogue();
            }
        }

        private void StartDialogue()
        {
            isDialogueActive = true;
            dialogueUI.OpenDialogue("Kraliyet Muhafızı", HandlePlayerMessage);
        }

        private void CloseDialogue()
        {
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

            // Asenkron olarak Ollama'ya sor (hata yakalama mekanizması)
            var (isSuccess, actionResponse, errorMessage) = await llmService.SendChatPromptAsync(SystemPrompt, playerMessage);

            if (!isSuccess)
            {
                // Sunucu kapalıysa veya JSON bozulduysa oyun çökmez, arayüzde hata gösterilir
                dialogueUI.ShowError(errorMessage);
                return;
            }

            // NPC cevabını ekrana yazdır
            dialogueUI.ShowNPCResponse(actionResponse.reply);

            // Yapılandırılmış JSON çıktısındaki kararı işle
            if (actionResponse.decision == "ALLOW")
            {
                isGateOpened = true;
                Debug.Log($"[NPCGuard] İkna başarılı! Gerekçe: {actionResponse.reason}");

                if (finalDoor != null)
                {
                    finalDoor.OpenDoor();
                }

                // 2 saniye sonra arayüzü kapatıp galibiyet durumuna geçir
                Invoke(nameof(CloseDialogue), 2.5f);
            }
        }
    }
}