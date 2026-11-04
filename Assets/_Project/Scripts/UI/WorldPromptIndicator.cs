using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AdventureGame.Interfaces;
using AdventureGame.Player;

namespace AdventureGame.UI
{
    // Etkileşim hedefinin üstünde görünen tuş ipucu. İpucu yalnızca oyuncu yakınken, hedef kullanılabilirken
    // ve diyalog kapalıyken görünür; PlayerInteractor da yalnızca görünen ipuçlarını tetikler.
    public class WorldPromptIndicator : MonoBehaviour
    {
        private static readonly List<WorldPromptIndicator> ActiveIndicators = new List<WorldPromptIndicator>();

        [Header("Mesafe Ayarları")]
        [SerializeField] private float activationDistance = 6.5f;

        [Header("Etkileşim")]
        [Tooltip("IPlayerInteractable uygulayan bileşen (örnek NPCGuardController, CompanionCommandTrigger). Boş bırakılırsa üst objelerde aranır.")]
        [SerializeField] private MonoBehaviour interactionTarget;
        [SerializeField] private string keyLabel = "E";

        [Header("Bileşenler")]
        [SerializeField] private GameObject visualContainer;
        [SerializeField] private TextMeshProUGUI promptKeyText;

        [Header("Animasyon (Breathing)")]
        [SerializeField] private float breathSpeed = 2.5f;
        [SerializeField] private float floatOffset = 0.08f;

        private CanvasGroup canvasGroup;
        private Transform playerTransform;
        private Camera mainCamera;
        private Vector3 initialLocalPos;
        private bool isAvailable = true;
        private bool isDialogueOpen = false;
        private IPlayerInteractable target;

        public bool IsShown { get; private set; }

        private void Awake()
        {
            // Unity'de CanvasGroup yoksa hata vermek yerine otomatik ekler
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (visualContainer != null)
            {
                initialLocalPos = visualContainer.transform.localPosition;
            }

            target = interactionTarget != null
                ? interactionTarget.GetComponent<IPlayerInteractable>()
                : GetComponentInParent<IPlayerInteractable>();
            if (target == null)
            {
                Debug.LogWarning($"[WorldPromptIndicator] {name} için IPlayerInteractable bulunamadı, ipucu tuşa bağlı değil.", this);
            }
        }

        private void OnEnable()
        {
            ActiveIndicators.Add(this);
            DialogueUIController.OnDialogueStateChanged += HandleDialogueStateChanged;
        }

        private void OnDisable()
        {
            ActiveIndicators.Remove(this);
            DialogueUIController.OnDialogueStateChanged -= HandleDialogueStateChanged;
            IsShown = false;
        }

        private void HandleDialogueStateChanged(bool isOpen)
        {
            isDialogueOpen = isOpen;
        }

        public static WorldPromptIndicator FindNearestShown(Vector3 position)
        {
            WorldPromptIndicator nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var indicator in ActiveIndicators)
            {
                if (!indicator.IsShown || indicator.target == null) continue;

                float distance = Vector3.Distance(position, indicator.transform.position);
                if (distance < nearestDistance)
                {
                    nearest = indicator;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        public void Interact()
        {
            if (IsShown && target != null && target.CanPlayerInteract) target.PlayerInteract();
        }

        private void Start()
        {
            mainCamera = Camera.main;

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Başlangıçta diyalog kutusu gizlenir
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (visualContainer != null) visualContainer.SetActive(false);
            SetKey(keyLabel);
        }

        private void LateUpdate()
        {
            bool targetAvailable = target == null || target.CanPlayerInteract;
            if (!isAvailable || isDialogueOpen || !targetAvailable || playerTransform == null || mainCamera == null)
            {
                IsShown = false;
                HideIndicator();
                return;
            }

            // Parent nesne ile interaksiyon göstergesi ölçekleme ayarlaması
            CompensateParentScale();

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool shouldShow = distance <= activationDistance;
            IsShown = shouldShow;

            float targetAlpha = shouldShow ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 4f);

            if (canvasGroup.alpha > 0.05f)
            {
                if (!visualContainer.activeSelf) visualContainer.SetActive(true);

                // Kameraya dönük olur
                visualContainer.transform.rotation = Quaternion.LookRotation(visualContainer.transform.position - mainCamera.transform.position);

                // Smooth breathing animasyonu
                float sine = Mathf.Sin(Time.time * breathSpeed);
                float pulse = Mathf.Lerp(0.92f, 1.08f, (sine + 1f) * 0.5f);
                visualContainer.transform.localScale = Vector3.one * pulse;
                visualContainer.transform.localPosition = initialLocalPos + new Vector3(0f, sine * floatOffset, 0f);
            }
            else
            {
                if (visualContainer.activeSelf) visualContainer.SetActive(false);
            }
        }

        private void CompensateParentScale()
        {
            if (transform.parent != null)
            {
                Vector3 parentScale = transform.parent.lossyScale;
                // Ebeveynin küçüklüğünü ters oranla dengeleyerek, göstergenin dünya ölçeğinde sabit kalmasını sağlar
                transform.localScale = new Vector3(
                    parentScale.x != 0 ? 0.015f / parentScale.x : 0.015f,
                    parentScale.y != 0 ? 0.015f / parentScale.y : 0.015f,
                    parentScale.z != 0 ? 0.015f / parentScale.z : 0.015f
                );
            }
        }

        public void SetKey(string key)
        {
            if (promptKeyText != null) promptKeyText.text = key;
        }

        public void SetAvailable(bool available)
        {
            isAvailable = available;
            if (!available) HideIndicator();
        }

        private void HideIndicator()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (visualContainer != null) visualContainer.SetActive(false);
        }
    }
}