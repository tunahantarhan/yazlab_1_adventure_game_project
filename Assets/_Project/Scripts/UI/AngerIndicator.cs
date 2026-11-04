using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureGame.UI
{
    // Muhafızın başının üstünde, öfkeli olduğu süre boyunca görünen ikon. Dairesel dolgu kalan bekleme süresini gösterir.
    public class AngerIndicator : MonoBehaviour
    {
        [Header("Bileşenler")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Image Type = Filled, Fill Method = Radial 360")]
        [SerializeField] private Image cooldownFill;
        [Tooltip("Kalan saniyeyi gösterir (opsiyonel).")]
        [SerializeField] private TextMeshProUGUI remainingText;

        [Header("Animasyon")]
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseAmount = 0.08f;
        [SerializeField] private float popInDuration = 0.25f;

        private Camera mainCamera;
        private float duration;
        private float endTime;
        private float shownTime;
        private Vector3 baseScale;

        public bool IsVisible => Time.time < endTime;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            baseScale = transform.localScale;
            SetVisible(false);
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        public void Show(float cooldownSeconds)
        {
            duration = Mathf.Max(cooldownSeconds, 0.01f);
            endTime = Time.time + duration;
            shownTime = Time.time;
            SetVisible(true);
        }

        public void Hide()
        {
            endTime = 0f;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (canvasGroup == null || canvasGroup.alpha <= 0f) return;

            float remaining = endTime - Time.time;
            if (remaining <= 0f)
            {
                SetVisible(false);
                return;
            }

            if (cooldownFill != null) cooldownFill.fillAmount = remaining / duration;
            if (remainingText != null) remainingText.text = Mathf.CeilToInt(remaining).ToString();

            // Belirirken büyüyerek gelir, sonra hafifçe nabız gibi atar
            float popIn = Mathf.Clamp01((Time.time - shownTime) / popInDuration);
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * (Mathf.SmoothStep(0f, 1f, popIn) * pulse);

            if (mainCamera != null)
            {
                transform.rotation = mainCamera.transform.rotation;
            }
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup != null) canvasGroup.alpha = isVisible ? 1f : 0f;
        }
    }
}
