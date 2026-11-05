using System.Collections;
using TMPro;
using UnityEngine;
using AdventureGame.Interactions;

namespace AdventureGame.UI
{
    // Sol üstteki görev yazısı: kapıların durumuna göre oyuncunun sıradaki hedefini gösterir.
    // Durum her frame kapılardan okunur, böylece test aracı kapıları sıfırlarsa görev de geri döner.
    public class QuestTracker : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [Tooltip("Companion'ın şalterle açtığı kapı.")]
        [SerializeField] private DoorController companionDoor;
        [Tooltip("Muhafızın koruduğu çıkış kapısı.")]
        [SerializeField] private DoorController finalDoor;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [Tooltip("Diyalog sırasında görev yazısını gizlemek için.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Görevler")]
        [TextArea][SerializeField] private string findWayObjective = "Kapalı kapının ardına geçmenin bir yolunu bul. İpucu: Yoldaşın sana yardım edebilir.";
        [TextArea][SerializeField] private string talkToGuardObjective = "Zindanın kapısını koruyan muhafızla konuş ve onu ikna et.";
        [TextArea][SerializeField] private string escapeObjective = "Kapıdan geç ve zindana giriş yap.";

        [Header("Animasyon")]
        [SerializeField] private float switchDuration = 0.35f;
        [SerializeField] private float visibilityFadeSpeed = 6f;

        private int currentStep = -1;
        private float targetAlpha = 1f;
        private Coroutine switchRoutine;

        private void OnEnable()
        {
            DialogueUIController.OnDialogueStateChanged += HandleDialogueStateChanged;
        }

        private void OnDisable()
        {
            DialogueUIController.OnDialogueStateChanged -= HandleDialogueStateChanged;
        }

        private void HandleDialogueStateChanged(bool isOpen)
        {
            targetAlpha = isOpen ? 0f : 1f;
        }

        private void Update()
        {
            int step = CurrentStep();
            if (step != currentStep)
            {
                bool isFirst = currentStep < 0;
                currentStep = step;
                ShowObjective(ObjectiveFor(step), animate: !isFirst);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, visibilityFadeSpeed * Time.unscaledDeltaTime);
            }
        }

        private int CurrentStep()
        {
            if (finalDoor != null && finalDoor.IsOpen) return 2;
            if (companionDoor != null && companionDoor.IsOpen) return 1;
            return 0;
        }

        private string ObjectiveFor(int step)
        {
            switch (step)
            {
                case 1: return talkToGuardObjective;
                case 2: return escapeObjective;
                default: return findWayObjective;
            }
        }

        private void ShowObjective(string text, bool animate)
        {
            if (objectiveText == null) return;

            if (switchRoutine != null) StopCoroutine(switchRoutine);
            if (!animate)
            {
                objectiveText.text = text;
                objectiveText.alpha = 1f;
                return;
            }
            switchRoutine = StartCoroutine(SwitchRoutine(text));
        }

        // Eski görev solar, yenisi hafifçe büyüyerek belirir
        private IEnumerator SwitchRoutine(string text)
        {
            float half = switchDuration * 0.5f;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                objectiveText.alpha = 1f - t / half;
                yield return null;
            }

            objectiveText.text = text;
            Transform textTransform = objectiveText.transform;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float progress = t / half;
                objectiveText.alpha = progress;
                textTransform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, progress);
                yield return null;
            }

            objectiveText.alpha = 1f;
            textTransform.localScale = Vector3.one;
            switchRoutine = null;
        }
    }
}
