using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureGame.UI
{
    // Muhafızın diyalog kutusunda gösterilen anlık durumu
    public struct GuardStatus
    {
        public string Attitude;
        public int Trust;
        public int PassThreshold;
        public int ConcernsAddressed;
        public int ConcernsToPass;
        public int TrustDelta;
    }

    // Muhafızın tavrını, güven barını ve anlaşılan dertleri görsel olarak gösterir
    public class GuardStatusPanel : MonoBehaviour
    {
        [Header("Tavır")]
        [SerializeField] private TextMeshProUGUI attitudeText;
        [Tooltip("Tavrın rengine boyanan ikon veya çerçeve.")]
        [SerializeField] private Image attitudeTint;

        [Header("Güven Barı")]
        [Tooltip("Image Type = Filled, Fill Method = Horizontal")]
        [SerializeField] private Image trustFill;
        [SerializeField] private TextMeshProUGUI trustText;
        [SerializeField] private Gradient trustGradient = DefaultTrustGradient();
        [SerializeField] private float fillAnimationSpeed = 2.5f;

        [Header("Dertler")]
        [Tooltip("Her biri bir derdi temsil eden küçük ikonlar, soldan sağa doldurulur.")]
        [SerializeField] private Image[] concernPips;
        [SerializeField] private Color pipFilledColor = new Color(0.95f, 0.78f, 0.35f);
        [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.2f);

        [Header("Güven Değişimi Yazısı")]
        [SerializeField] private TextMeshProUGUI deltaPopupText;
        [SerializeField] private float popupDuration = 1.6f;
        [SerializeField] private float popupRise = 30f;

        private float displayedFill;
        private float targetFill;
        private Vector2 popupStartPosition;
        private Coroutine popupCoroutine;

        private void Awake()
        {
            if (deltaPopupText != null)
            {
                popupStartPosition = deltaPopupText.rectTransform.anchoredPosition;
                deltaPopupText.alpha = 0f;
            }
        }

        private void Update()
        {
            if (trustFill == null || Mathf.Approximately(displayedFill, targetFill)) return;

            displayedFill = Mathf.MoveTowards(displayedFill, targetFill, fillAnimationSpeed * Time.unscaledDeltaTime);
            ApplyFill(displayedFill);
        }

        public void Show(GuardStatus status)
        {
            ShowAttitude(status.Attitude);

            if (trustText != null) trustText.text = $"Güven {status.Trust}/{status.PassThreshold}";
            targetFill = status.PassThreshold > 0 ? Mathf.Clamp01((float)status.Trust / status.PassThreshold) : 0f;

            UpdateConcernPips(status.ConcernsAddressed, status.ConcernsToPass);

            if (status.TrustDelta != 0) ShowDeltaPopup(status.TrustDelta);
        }

        // Diyalog yeniden açıldığında bar animasyonsuz olarak mevcut değere oturtulur
        public void ShowImmediate(GuardStatus status)
        {
            Show(status);
            displayedFill = targetFill;
            ApplyFill(displayedFill);
        }

        public void ShowAttitude(string attitude)
        {
            Color attitudeColor = AttitudeColor(attitude);
            if (attitudeText != null)
            {
                attitudeText.text = AttitudeLabel(attitude);
                attitudeText.color = attitudeColor;
            }
            if (attitudeTint != null) attitudeTint.color = attitudeColor;
        }

        public void SetAttitudeVisible(bool isVisible)
        {
            if (attitudeText != null) attitudeText.gameObject.SetActive(isVisible);
        }

        public static string AttitudeLabel(string attitude)
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

        public static Color AttitudeColor(string attitude)
        {
            switch (attitude)
            {
                case "ANGRY": return new Color(0.9f, 0.3f, 0.25f);
                case "SUSPICIOUS": return new Color(0.95f, 0.65f, 0.3f);
                case "SYMPATHETIC": return new Color(0.55f, 0.8f, 0.5f);
                case "CONVINCED": return new Color(1f, 0.88f, 0.5f);
                default: return new Color(0.784f, 0.643f, 0.361f); // Temkinli: çerçevenin varsayılan altın rengi
            }
        }

        private void ApplyFill(float fill)
        {
            trustFill.fillAmount = fill;
            trustFill.color = trustGradient.Evaluate(fill);
        }

        private void UpdateConcernPips(int addressed, int required)
        {
            if (concernPips == null) return;

            for (int i = 0; i < concernPips.Length; i++)
            {
                if (concernPips[i] == null) continue;
                concernPips[i].gameObject.SetActive(i < required);
                concernPips[i].color = i < addressed ? pipFilledColor : pipEmptyColor;
            }
        }

        private void ShowDeltaPopup(int delta)
        {
            if (deltaPopupText == null) return;

            deltaPopupText.text = delta > 0 ? $"+{delta} Güven" : $"{delta} Güven";
            deltaPopupText.color = delta > 0 ? new Color(0.55f, 0.85f, 0.5f) : new Color(0.9f, 0.35f, 0.3f);

            if (popupCoroutine != null) StopCoroutine(popupCoroutine);
            popupCoroutine = StartCoroutine(DeltaPopupRoutine());
        }

        private IEnumerator DeltaPopupRoutine()
        {
            RectTransform rect = deltaPopupText.rectTransform;
            for (float t = 0f; t < popupDuration; t += Time.unscaledDeltaTime)
            {
                float progress = t / popupDuration;
                rect.anchoredPosition = popupStartPosition + Vector2.up * (popupRise * progress);
                // Hızlıca belirir, sonra yavaşça kaybolur
                deltaPopupText.alpha = progress < 0.15f ? progress / 0.15f : 1f - (progress - 0.15f) / 0.85f;
                yield return null;
            }

            deltaPopupText.alpha = 0f;
            rect.anchoredPosition = popupStartPosition;
            popupCoroutine = null;
        }

        private static Gradient DefaultTrustGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.8f, 0.25f, 0.2f), 0f),
                    new GradientColorKey(new Color(0.9f, 0.7f, 0.3f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.8f, 0.45f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
