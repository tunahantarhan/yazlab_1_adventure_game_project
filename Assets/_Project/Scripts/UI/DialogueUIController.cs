using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureGame.UI
{
    public class DialogueUIController : MonoBehaviour
    {
        public static event Action<bool> OnDialogueStateChanged;
        public static bool IsInputFocused { get; private set; }

        [Header("Paneller")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private GameObject inputContainer;

        [Header("Metin ve Görsel Bileşenleri")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueBodyText;
        [SerializeField] private TMP_InputField playerInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI statusIndicatorText;
        [SerializeField] private Image portraitImage;
        [Tooltip("Muhafızın tavrı ve ikna ilerlemesi.")]
        [SerializeField] private TextMeshProUGUI moodIndicatorText;
        [Tooltip("Tavır, güven barı ve dertleri gösteren panel.")]
        [SerializeField] private GuardStatusPanel guardStatusPanel;
        [Tooltip("Portrenin çerçevesi, muhafızın tavrının rengine boyanır.")]
        [SerializeField] private Image portraitFrame;
        [Tooltip("Oyuncu konuşurken portre çerçevesinin rengi.")]
        [SerializeField] private Color playerFrameColor = new Color(0.784f, 0.643f, 0.361f);

        [Header("Space İndikatörü")]
        [SerializeField] private CanvasGroup spaceIndicatorGroup;
        [SerializeField] private float blinkSpeed = 2.2f;

        [Header("Portreler")]
        [SerializeField] private Sprite guardNormalSprite;
        [SerializeField] private Sprite guardThinkingSprite;
        [SerializeField] private Sprite playerSprite;
        [Tooltip("Tavra göre muhafız portresi. Atanmayan tavırlarda normal portre kullanılır.")]
        [SerializeField] private AttitudePortrait[] attitudePortraits;

        [Header("Daktilo & Ses")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip voiceBlipClip;
        [SerializeField] private float typingSpeed = 0.03f;

        private Action<string> onSendMessageAction;
        private Action onSpacePressedAction;
        private Action currentTypingCompleteCallback;
        private Coroutine activeTypingCoroutine;
        private bool isTyping = false;
        private string currentFullText = "";
        private string npcSpeakerName = "";
        private Coroutine thinkingTimerCoroutine;
        private float thinkingStartTime;
        private Sprite currentGuardSprite;
        private Color currentGuardColor = GuardStatusPanel.AttitudeColor(null);

        [Serializable]
        private class AttitudePortrait
        {
            public string attitude;
            public Sprite sprite;
        }

        private void Awake()
        {
            if (dialoguePanel == null)
            {
                var found = transform.Find("DialoguePanel");
                if (found != null) dialoguePanel = found.gameObject;
            }

            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (sendButton != null) sendButton.onClick.AddListener(OnSendButtonClicked);
            HideSpaceIndicator();
        }

        private void Update()
        {
            IsInputFocused = playerInputField != null && playerInputField.isFocused;

            if (spaceIndicatorGroup != null && spaceIndicatorGroup.gameObject.activeSelf)
            {
                float sine = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
                spaceIndicatorGroup.alpha = Mathf.Lerp(0.25f, 1f, sine);
                float scale = Mathf.Lerp(0.97f, 1.03f, sine);
                spaceIndicatorGroup.transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (!dialoguePanel.activeSelf) return;

            // Hareket diyalog sırasında kilitli olduğu için diyalogdan çıkış ESC ile yapılır
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseDialogue();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space) && !IsInputFocused)
            {
                if (isTyping)
                {
                    CompleteTypingInstantly();
                }
                else if (onSpacePressedAction != null)
                {
                    HideSpaceIndicator();
                    var action = onSpacePressedAction;
                    onSpacePressedAction = null;
                    action.Invoke();
                }
            }

            if (inputContainer.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (!string.IsNullOrEmpty(playerInputField.text.Trim()) && playerInputField.interactable && !isTyping)
                {
                    OnSendButtonClicked();
                }
            }
        }

        public void OpenDialogue(string speakerName, string greetingText, Action<string> onSendMessage)
        {
            ResumeDialogue(speakerName, onSendMessage);
            ShowInputArea(true);
            StartTyping(greetingText);
        }

        // Panel açılır ama yazı yazılmaz; bekleyen cevap veya düşünme durumu çağıran tarafından gösterilir
        public void ResumeDialogue(string speakerName, Action<string> onSendMessage)
        {
            onSendMessageAction = onSendMessage;
            npcSpeakerName = speakerName;
            ClearStatus();
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);

            HideSpaceIndicator();
            SetGuardStatusVisible(true);
            SetSpeaker(speakerName, GuardPortrait);
            ShowInputArea(false);
            dialogueBodyText.text = "";
        }

        // attitude verilirse portre altındaki tavır etiketi ve çerçeve o tavra boyanır, statusMessage durum satırında gösterilir
        public void ShowSimpleMessage(string speakerName, string message, string attitude = null, string statusMessage = null)
        {
            ClearStatus();
            SetGuardStatusVisible(false);
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);
            ShowInputArea(false);
            HideSpaceIndicator();
            if (attitude != null) SetGuardAttitude(attitude);
            SetSpeaker(speakerName, GuardPortrait);
            if (attitude == null && guardStatusPanel != null) guardStatusPanel.SetAttitudeVisible(false);
            if (statusMessage != null) statusIndicatorText.text = $"<color=red>{statusMessage}</color>";

            StartTyping(message, onComplete: () =>
            {
                PromptSpaceAction(() => CloseDialogue());
            });
        }

        public void CloseDialogue()
        {
            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
            StopThinkingTimer();
            isTyping = false;
            currentTypingCompleteCallback = null;
            dialoguePanel.SetActive(false);
            onSendMessageAction = null;
            onSpacePressedAction = null;
            HideSpaceIndicator();
            OnDialogueStateChanged?.Invoke(false);
        }

        private void OnSendButtonClicked()
        {
            string message = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(message) || isTyping) return;

            playerInputField.text = "";
            ClearStatus();
            ShowInputArea(false);
            HideSpaceIndicator();

            SetSpeaker("Sen", playerSprite, playerFrameColor, isGuard: false);
            StartTyping(message, onComplete: () =>
            {
                PromptSpaceAction(() =>
                {
                    SetThinkingState(true);
                    onSendMessageAction?.Invoke(message);
                });
            });
        }

        // resetTimer = false -> diyaloğa geri dönüldüğünde süre kaldığı yerden devam eder
        public void SetThinkingState(bool isThinking, bool resetTimer = true)
        {
            HideSpaceIndicator();
            StopThinkingTimer();
            if (isThinking)
            {
                if (resetTimer) thinkingStartTime = Time.unscaledTime;
                SetSpeaker(npcSpeakerName, guardThinkingSprite != null ? guardThinkingSprite : GuardPortrait);
                dialogueBodyText.text = "";
                thinkingTimerCoroutine = StartCoroutine(ThinkingTimerRoutine());
            }
            else
            {
                statusIndicatorText.text = "";
            }
        }

        // LLM yanıtı beklenirken geçen süre gösterilir; oyun bu sırada akmaya devam eder
        private IEnumerator ThinkingTimerRoutine()
        {
            while (true)
            {
                int elapsed = Mathf.FloorToInt(Time.unscaledTime - thinkingStartTime);
                statusIndicatorText.text = $"<i>{npcSpeakerName} düşünüyor... ({elapsed} sn)</i>";
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        private void StopThinkingTimer()
        {
            if (thinkingTimerCoroutine == null) return;
            StopCoroutine(thinkingTimerCoroutine);
            thinkingTimerCoroutine = null;
        }

        // onFinished verilmezse yanıt bittikten sonra oyuncunun yeni mesaj yazması beklenir
        public void ShowNPCResponse(string reply, Action onFinished = null)
        {
            SetThinkingState(false);
            SetSpeaker(npcSpeakerName, GuardPortrait);

            StartTyping(reply, onComplete: () =>
            {
                if (onFinished != null)
                {
                    onFinished.Invoke();
                }
                else
                {
                    ShowInputArea(true);
                }
            });
        }

        // Konuşmanın ardından SPACE ile bir sahne anlatımı gösterilir; anlatım başlarken onNarrationStarted çağrılır
        public void ShowNarrationAfterSpace(string narration, Action onNarrationStarted)
        {
            PromptSpaceAction(() =>
            {
                onNarrationStarted?.Invoke();
                StartTyping(narration, onComplete: () => PromptSpaceAction(CloseDialogue));
            });
        }

        public void ShowError(string errorMessage, bool allowRetry = true)
        {
            SetThinkingState(false);
            statusIndicatorText.text = $"<color=red>{errorMessage}</color>";
            ShowInputArea(allowRetry);
        }

        // Muhafızın tavrı portreye, çerçeve rengine ve durum paneline yansıtılır
        public void SetGuardStatus(GuardStatus status, bool immediate = false)
        {
            currentGuardSprite = FindAttitudePortrait(status.Attitude);
            currentGuardColor = GuardStatusPanel.AttitudeColor(status.Attitude);

            if (guardStatusPanel != null)
            {
                if (immediate) guardStatusPanel.ShowImmediate(status);
                else guardStatusPanel.Show(status);
            }
            else if (moodIndicatorText != null)
            {
                moodIndicatorText.text = $"{GuardStatusPanel.AttitudeLabel(status.Attitude)}  ·  Güven {status.Trust}/{status.PassThreshold}  ·  " +
                                         $"Anlaşılan dert {status.ConcernsAddressed}/{status.ConcernsToPass}";
            }
        }

        // Durum paneli gizliyken yalnızca tavır (portre, çerçeve ve etiket) güncellenir
        private void SetGuardAttitude(string attitude)
        {
            currentGuardSprite = FindAttitudePortrait(attitude);
            currentGuardColor = GuardStatusPanel.AttitudeColor(attitude);
            if (guardStatusPanel != null) guardStatusPanel.ShowAttitude(attitude);
        }

        private void SetGuardStatusVisible(bool isVisible)
        {
            if (guardStatusPanel != null) guardStatusPanel.gameObject.SetActive(isVisible);
            if (moodIndicatorText != null) moodIndicatorText.gameObject.SetActive(isVisible);
        }

        private Sprite GuardPortrait => currentGuardSprite != null ? currentGuardSprite : guardNormalSprite;

        private Sprite FindAttitudePortrait(string attitude)
        {
            if (attitudePortraits == null) return null;
            foreach (var portrait in attitudePortraits)
            {
                if (portrait != null && portrait.attitude == attitude) return portrait.sprite;
            }
            return null;
        }

        private void ClearStatus()
        {
            if (statusIndicatorText != null) statusIndicatorText.text = "";
        }

        private void PromptSpaceAction(Action nextAction)
        {
            onSpacePressedAction = nextAction;
            ShowSpaceIndicator();
        }

        private void ShowSpaceIndicator()
        {
            if (spaceIndicatorGroup != null)
            {
                spaceIndicatorGroup.gameObject.SetActive(true);
            }
        }

        private void HideSpaceIndicator()
        {
            if (spaceIndicatorGroup != null)
            {
                spaceIndicatorGroup.gameObject.SetActive(false);
            }
        }

        private void SetSpeaker(string name, Sprite portrait)
        {
            SetSpeaker(name, portrait, currentGuardColor, isGuard: true);
        }

        // Tavır etiketi portrenin altında durduğu için yalnızca muhafız konuşurken gösterilir
        private void SetSpeaker(string name, Sprite portrait, Color frameColor, bool isGuard)
        {
            speakerNameText.text = name;
            if (guardStatusPanel != null) guardStatusPanel.SetAttitudeVisible(isGuard);
            if (portraitFrame != null) portraitFrame.color = frameColor;
            if (portraitImage != null && portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.color = Color.white;
            }
        }

        private void ShowInputArea(bool isVisible)
        {
            HideSpaceIndicator();
            if (inputContainer != null) inputContainer.SetActive(isVisible);
            if (isVisible)
            {
                playerInputField.interactable = true;
                playerInputField.ActivateInputField();
            }
        }

        private void StartTyping(string textToType, Action onComplete = null)
        {
            HideSpaceIndicator();
            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
            currentFullText = textToType;
            currentTypingCompleteCallback = onComplete;
            activeTypingCoroutine = StartCoroutine(TypeWriterRoutine(textToType));
        }

        private void CompleteTypingInstantly()
        {
            if (!isTyping) return;

            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
            dialogueBodyText.text = currentFullText;
            isTyping = false;
            activeTypingCoroutine = null;

            var callback = currentTypingCompleteCallback;
            currentTypingCompleteCallback = null;
            callback?.Invoke();
        }

        private IEnumerator TypeWriterRoutine(string textToType)
        {
            isTyping = true;
            dialogueBodyText.text = "";

            for (int i = 0; i < textToType.Length; i++)
            {
                char c = textToType[i];
                dialogueBodyText.text += c;

                if (char.IsLetterOrDigit(c) && audioSource != null && voiceBlipClip != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                    audioSource.PlayOneShot(voiceBlipClip, 0.35f);
                }

                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            activeTypingCoroutine = null;

            var callback = currentTypingCompleteCallback;
            currentTypingCompleteCallback = null;
            callback?.Invoke();
        }
    }
}