using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AdventureGame.Interfaces;

namespace AdventureGame.LLM
{
    // Geçersiz yanıt senaryolarını test etmek için modelin gerçek cevabına uygulanan bozulma türleri
    public enum InvalidResponseSimulation
    {
        None,
        TruncatedJson,      // JSON yarıda kesilir
        PlainText,          // JSON yerine düz metin
        MissingReply,       // Zorunlu 'reply' alanı yok
        InvalidDecision     // decision ALLOW/DENY dışında bir değer
    }

    // Ollama /api/chat uç noktasına istek atar. Hiçbir durumda exception fırlatmaz;
    // her hata LLMResult.Failure olarak, kullanıcıya gösterilebilecek bir mesajla döner.
    public class OllamaService : MonoBehaviour, ILLMService
    {
        [Header("Ollama Ayarları")]
        [SerializeField] private string ollamaUrl = "http://localhost:11434/api/chat";
        [SerializeField] private string modelName = "gemma3:4b";
        [SerializeField] private float timeoutSeconds = 30f;

        [Header("Dayanıklılık")]
        [Tooltip("Model geçersiz JSON üretirse aynı istek değerdeki rakam kadar yeniden denenir.")]
        [SerializeField] private int invalidResponseRetries = 1;
        [SerializeField] private int maxResponseTokens = 300;
        [Tooltip("Oyun başında model belleğe yüklenir, böylece ilk yanıtın gecikmesi azalır.")]
        [SerializeField] private bool warmUpOnStart = true;
        [SerializeField] private float warmUpTimeoutSeconds = 120f;

        [Header("Test (Hata Enjeksiyonu)")]
        [Tooltip("Geçersiz yanıt testi için modelin gerçek cevabı ayrıştırılmadan önce seçilen şekilde bozulur. Normal oyunda None olmalı.")]
        [SerializeField] private InvalidResponseSimulation simulateInvalidResponse = InvalidResponseSimulation.None;

        private readonly NPCResponseParser parser = new NPCResponseParser();

        private struct RawResponse
        {
            public LLMErrorType ErrorType;
            public string ErrorMessage;
            public string Text;
        }

        private async void Start()
        {
            if (!warmUpOnStart) return;

            LLMResult availability = await CheckAvailabilityAsync();
            if (!availability.IsSuccess)
            {
                Debug.LogWarning($"[OllamaService] {availability.ErrorMessage}");
                return;
            }

            // Boş mesaj listesiyle atılan istek modeli belleğe yükler
            string payload = JsonUtility.ToJson(new OllamaChatRequest { model = modelName, messages = new List<OllamaChatMessage>() });
            RawResponse warmUp = await SendAsync(ollamaUrl, "POST", payload, warmUpTimeoutSeconds);
            Debug.Log(warmUp.ErrorType == LLMErrorType.None
                ? $"[OllamaService] '{modelName}' modeli belleğe yüklendi."
                : $"[OllamaService] Model ön yüklemesi başarısız: {warmUp.ErrorMessage}");
        }

        public async Task<LLMResult> CheckAvailabilityAsync()
        {
            try
            {
                string tagsUrl = new Uri(new Uri(ollamaUrl), "/api/tags").ToString();
                RawResponse raw = await SendAsync(tagsUrl, "GET", null, 5f);
                if (raw.ErrorType != LLMErrorType.None)
                {
                    return LLMResult.Failure(raw.ErrorType, raw.ErrorMessage);
                }

                var tags = JsonUtility.FromJson<OllamaTagsResponse>(raw.Text);
                if (tags?.models != null)
                {
                    foreach (var model in tags.models)
                    {
                        if (model.name == modelName || model.name == modelName + ":latest")
                        {
                            return LLMResult.Success();
                        }
                    }
                }

                return LLMResult.Failure(LLMErrorType.ModelNotFound, ModelNotFoundMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OllamaService] Erişilebilirlik kontrolünde istisna: {ex.Message}");
                return LLMResult.Failure(LLMErrorType.Unknown, "Ollama durumu kontrol edilemedi.");
            }
        }

        public async Task<LLMResult> SendChatHistoryAsync(List<OllamaChatMessage> conversationHistory)
        {
            try
            {
                var requestBody = new OllamaChatRequest
                {
                    model = modelName,
                    messages = conversationHistory,
                    options = new OllamaOptions { temperature = 0.2f, num_predict = maxResponseTokens }
                };
                string payload = JsonUtility.ToJson(requestBody);

                LLMResult result = null;
                for (int attempt = 0; attempt <= invalidResponseRetries; attempt++)
                {
                    RawResponse raw = await SendAsync(ollamaUrl, "POST", payload, timeoutSeconds);
                    if (raw.ErrorType != LLMErrorType.None)
                    {
                        // Beklenen hata durumları (sunucu kapalı vb.) uyarı olarak loglanır, LogError yalnızca beklenmeyen istisnalar içindir
                        Debug.LogWarning($"[OllamaService] {raw.ErrorType}: {raw.ErrorMessage}");
                        return LLMResult.Failure(raw.ErrorType, raw.ErrorMessage);
                    }

                    result = ParseChatResponse(raw.Text);
                    if (result.IsSuccess) return result;

                    // Bozuk çıktı genelde tekrar denemede düzelir; bağlantı hataları tekrar denenmez
                    Debug.LogWarning($"[OllamaService] Geçersiz model yanıtı ({attempt + 1}. deneme): {result.ErrorMessage}\nHam yanıt: {raw.Text}");
                }

                // Tekrar denemeler de geçersizse oyuncuya teknik ayrıntı yerine anlaşılır bir mesaj gösterilir
                return LLMResult.Failure(LLMErrorType.InvalidResponse,
                    $"Muhafızın cevabı anlaşılamadı, model geçersiz bir yanıt üretti ({result.ErrorMessage}).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OllamaService] İstisna: {ex}");
                return LLMResult.Failure(LLMErrorType.Unknown, $"Beklenmeyen bir hata oluştu: {ex.Message}");
            }
        }

        private LLMResult ParseChatResponse(string responseText)
        {
            OllamaChatResponse chatResponse;
            try
            {
                chatResponse = JsonUtility.FromJson<OllamaChatResponse>(responseText);
            }
            catch (ArgumentException)
            {
                return LLMResult.Failure(LLMErrorType.InvalidResponse, "Ollama'dan okunamayan bir yanıt geldi.");
            }

            string content = chatResponse?.message?.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return LLMResult.Failure(LLMErrorType.InvalidResponse, "Model boş yanıt döndürdü.");
            }

            content = ApplyInvalidResponseSimulation(content);

            return parser.TryParse(content, out NPCActionResponse action, out string error)
                ? LLMResult.Success(action)
                : LLMResult.Failure(LLMErrorType.InvalidResponse, error);
        }

        private string ApplyInvalidResponseSimulation(string content)
        {
            if (simulateInvalidResponse == InvalidResponseSimulation.None) return content;

            string corrupted;
            switch (simulateInvalidResponse)
            {
                case InvalidResponseSimulation.TruncatedJson:
                    corrupted = content.Substring(0, content.Length / 2);
                    break;
                case InvalidResponseSimulation.PlainText:
                    corrupted = content.Replace("{", "").Replace("}", "");
                    break;
                case InvalidResponseSimulation.MissingReply:
                    corrupted = content.Replace("\"reply\"", "\"cevap\"");
                    break;
                case InvalidResponseSimulation.InvalidDecision:
                    corrupted = content.Replace("\"DENY\"", "\"MAYBE\"").Replace("\"ALLOW\"", "\"MAYBE\"");
                    break;
                default:
                    return content;
            }

            Debug.LogWarning($"[OllamaService] TEST: Model yanıtı kasıtlı olarak bozuldu ({simulateInvalidResponse}).");
            return corrupted;
        }

        // İstek ana thread'de, frame'i bloklamadan (Task.Yield) beklenir
        private async Task<RawResponse> SendAsync(string url, string method, string jsonBody, float timeout)
        {
            using var request = new UnityWebRequest(url, method);
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(timeout);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                // Sahne kapanır veya obje yok edilirse istek iptal edilir
                if (this == null)
                {
                    request.Abort();
                    return Fail(LLMErrorType.Unknown, "İstek iptal edildi.");
                }
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return new RawResponse { ErrorType = LLMErrorType.None, Text = request.downloadHandler.text };
            }

            return ClassifyError(request, timeout);
        }

        private RawResponse ClassifyError(UnityWebRequest request, float timeout)
        {
            string error = request.error ?? "";

            if (error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Fail(LLMErrorType.Timeout,
                    $"Muhafız {timeout:0} saniye içinde cevap veremedi (zaman aşımı). Model ilk kez yükleniyor olabilir, tekrar dene.");
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return Fail(LLMErrorType.ServerUnreachable,
                    "Ollama sunucusuna ulaşılamıyor. Ollama uygulamasını aç (veya terminalden çalıştır) ve tekrar dene.");
            }

            if (request.result == UnityWebRequest.Result.ProtocolError)
            {
                // 404: model indirilmemiş, 400: model adı geçersiz
                string serverError = TryReadServerError(request.downloadHandler?.text);
                bool isModelError = serverError != null && serverError.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0;
                if (request.responseCode == 404 || (request.responseCode == 400 && isModelError))
                {
                    return Fail(LLMErrorType.ModelNotFound, ModelNotFoundMessage);
                }

                return Fail(LLMErrorType.Unknown, $"Ollama hata döndürdü ({request.responseCode}): {serverError ?? error}");
            }

            return Fail(LLMErrorType.InvalidResponse, $"Ollama yanıtı işlenemedi: {error}");
        }

        private string ModelNotFoundMessage =>
            $"'{modelName}' modeli yüklü değil. Terminalde 'ollama pull {modelName}' komutunu çalıştır.";

        private static string TryReadServerError(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                return JsonUtility.FromJson<OllamaErrorResponse>(body)?.error;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static RawResponse Fail(LLMErrorType type, string message)
        {
            return new RawResponse { ErrorType = type, ErrorMessage = message };
        }
    }
}
