namespace AdventureGame.LLM
{
    public enum LLMErrorType
    {
        None,
        ServerUnreachable, // Ollama çalışmıyor / bağlantı reddedildi
        ModelNotFound,     // Model indirilmemiş (HTTP 404)
        Timeout,           // Model süre içinde yanıt vermedi
        InvalidResponse,   // Bozuk JSON, eksik veya geçersiz alanlar
        Unknown
    }

    // LLM isteğinin sonucu -> ya ayrıştırılmış bir aksiyon ya da kullanıcıya döndürülen hata
    public class LLMResult
    {
        public NPCActionResponse Action { get; }
        public LLMErrorType ErrorType { get; }
        public string ErrorMessage { get; }
        public bool IsSuccess => ErrorType == LLMErrorType.None;

        private LLMResult(NPCActionResponse action, LLMErrorType errorType, string errorMessage)
        {
            Action = action;
            ErrorType = errorType;
            ErrorMessage = errorMessage;
        }

        public static LLMResult Success(NPCActionResponse action = null)
        {
            return new LLMResult(action, LLMErrorType.None, null);
        }

        public static LLMResult Failure(LLMErrorType errorType, string errorMessage)
        {
            return new LLMResult(null, errorType, errorMessage);
        }
    }
}
