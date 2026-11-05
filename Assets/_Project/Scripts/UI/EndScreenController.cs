using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdventureGame.UI
{
    // Oyun sonu ekranı -> ekranı karartır, ardından yeniden oyna / ana menü / çıkış seçeneklerini gösterir
    public class EndScreenController : MonoBehaviour
    {
        [Header("Paneller")]
        [Tooltip("Tüm ekranı kaplayan siyah Image'ın CanvasGroup'u.")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [Tooltip("Başlık ve butonları içeren panelin CanvasGroup'u.")]
        [SerializeField] private CanvasGroup menuGroup;

        [Header("Özet")]
        [Tooltip("Oyun süresi ve muhafızla konuşma sayısını gösteren yazı.")]
        [SerializeField] private TMPro.TextMeshProUGUI summaryText;

        [Header("Butonlar")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Ayarlar")]
        [Tooltip("Boş bırakılırsa ana menü bu sahnenin içinde kabul edilir ve sahne menüyle yeniden açılır.")]
        [SerializeField] private string mainMenuSceneName = "";
        [SerializeField] private float menuFadeDuration = 0.8f;

        private void Awake()
        {
            SetGroup(fadeOverlay, 0f, false);
            SetGroup(menuGroup, 0f, false);

            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenu);
        }

        public void SetSummary(float playSeconds, int guardMessages)
        {
            if (summaryText == null) return;

            int minutes = Mathf.FloorToInt(playSeconds / 60f);
            int seconds = Mathf.FloorToInt(playSeconds % 60f);
            summaryText.text = $"Süre {minutes:00}:{seconds:00}   ·   Meçhul'la {guardMessages} mesajda anlaştın";
        }

        public IEnumerator FadeToBlack(float duration)
        {
            // Kararma sırasında arkadaki UI'a tıklanamaz
            SetGroup(fadeOverlay, fadeOverlay != null ? fadeOverlay.alpha : 0f, true);
            yield return Fade(fadeOverlay, 1f, duration);
        }

        public void ShowMenu()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(ShowMenuRoutine());
        }

        private IEnumerator ShowMenuRoutine()
        {
            yield return Fade(menuGroup, 1f, menuFadeDuration);
            SetGroup(menuGroup, 1f, true);
            if (restartButton != null) restartButton.Select();
        }

        // Yeniden oyna ana menüyü atlar, doğrudan oyun başlar
        private void Restart()
        {
            MainMenuController.SkipMenuOnNextLoad = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void LoadMainMenu()
        {
            if (string.IsNullOrEmpty(mainMenuSceneName)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            else SceneManager.LoadScene(mainMenuSceneName);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static IEnumerator Fade(CanvasGroup group, float targetAlpha, float duration)
        {
            if (group == null) yield break;

            float startAlpha = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
                yield return null;
            }
            group.alpha = targetAlpha;
        }

        private static void SetGroup(CanvasGroup group, float alpha, bool isInteractive)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = isInteractive;
            group.blocksRaycasts = isInteractive;
        }
    }
}
