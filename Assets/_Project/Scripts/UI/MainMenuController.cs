using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AdventureGame.Animation;
using AdventureGame.Core;
using AdventureGame.Player;

namespace AdventureGame.UI
{
    // Ana menü oyun sahnesinin içindedir. Arka planda canlı zindan görünür, oyuncu ve companion sahnede yoktur.
    // "Oyuna Başla" ile kamera takip açısına kayar, karakterler Spawn animasyonuyla iner ve kontroller açılır.
    public class MainMenuController : MonoBehaviour
    {
        // Bitiş ekranındaki "Yeniden Oyna" sahneyi menüyü atlayarak yeniden yükler
        public static bool SkipMenuOnNextLoad;

        [Header("Menü")]
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [Tooltip("Görev yazısı ve tuş ipuçlarını içeren HUD; menüde gizlenir.")]
        [SerializeField] private CanvasGroup hudGroup;

        [Header("Kamera")]
        [SerializeField] private CameraFollow cameraFollow;
        [Tooltip("Menüdeki kamera açısı.")]
        [SerializeField] private Transform menuCameraPoint;
        [SerializeField] private float cameraTransitionDuration = 1.6f;

        [Header("Karakterler")]
        [SerializeField] private PlayerController player;
        [Tooltip("Menüde gizlenen görseller (oyuncunun ve companion'ın Model objeleri).")]
        [SerializeField] private GameObject[] hiddenModels;
        [Tooltip("Spawn animasyonu oynatılacak karakterler.")]
        [SerializeField] private CharacterAnimator[] spawnAnimators;
        [Tooltip("İkinci karakterin ilkinden ne kadar sonra ineceği (saniye).")]
        [SerializeField] private float spawnStagger = 0.35f;
        [Tooltip("Spawn animasyonundan sonra kontrollerin açılması için beklenen süre.")]
        [SerializeField] private float spawnDuration = 1.3f;

        [Header("Geçiş")]
        [SerializeField] private float fadeDuration = 0.5f;

        private Transform cameraTransform;
        private Quaternion gameplayCameraRotation;
        private bool hasStarted;

        private void Awake()
        {
            if (cameraFollow != null)
            {
                cameraTransform = cameraFollow.transform;
                gameplayCameraRotation = cameraTransform.rotation; // Sahnedeki kamera açısı oyun içi takip açısıdır
            }

            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void Start()
        {
            if (SkipMenuOnNextLoad)
            {
                SkipMenuOnNextLoad = false;
                hasStarted = true;
                SetGroup(menuGroup, 0f, false);
                SetGroup(hudGroup, 1f, false);
                return;
            }

            SetGroup(menuGroup, 1f, true);
            SetGroup(hudGroup, 0f, false);
            SetModelsVisible(false);
            if (player != null) player.SetControlEnabled(false);

            if (cameraFollow != null) cameraFollow.enabled = false;
            if (cameraTransform != null && menuCameraPoint != null)
            {
                cameraTransform.SetPositionAndRotation(menuCameraPoint.position, menuCameraPoint.rotation);
            }

            Cursor.visible = true;
            if (startButton != null) startButton.Select();
        }

        public void StartGame()
        {
            if (hasStarted) return;
            hasStarted = true;
            StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            SetGroup(menuGroup, menuGroup != null ? menuGroup.alpha : 0f, false);
            StartCoroutine(Fade(menuGroup, 0f, fadeDuration));

            yield return MoveCameraToGameplay();
            if (cameraFollow != null) cameraFollow.enabled = true;

            // Karakterler sırayla havadan iner
            SetModelsVisible(true);
            for (int i = 0; i < spawnAnimators.Length; i++)
            {
                if (spawnAnimators[i] != null) spawnAnimators[i].PlaySpawn();
                if (i < spawnAnimators.Length - 1) yield return new WaitForSeconds(spawnStagger);
            }

            yield return new WaitForSeconds(spawnDuration);
            if (player != null) player.SetControlEnabled(true);
            yield return Fade(hudGroup, 1f, fadeDuration);
        }

        private IEnumerator MoveCameraToGameplay()
        {
            if (cameraTransform == null || cameraFollow == null) yield break;

            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            Vector3 endPosition = cameraFollow.DesiredPosition;

            for (float t = 0f; t < cameraTransitionDuration; t += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, t / cameraTransitionDuration);
                cameraTransform.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, progress),
                    Quaternion.Slerp(startRotation, gameplayCameraRotation, progress));
                yield return null;
            }
            cameraTransform.SetPositionAndRotation(endPosition, gameplayCameraRotation);
        }

        private void SetModelsVisible(bool isVisible)
        {
            if (hiddenModels == null) return;
            foreach (var model in hiddenModels)
            {
                if (model != null) model.SetActive(isVisible);
            }
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
