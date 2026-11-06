using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AdventureGame.Audio;

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
        [Tooltip("Blip sesi kaç harfte bir çalsın ayarı. (her harfte çalınca ses çok sıklaşır)")]
        [SerializeField] private int blipEveryNChars = 2;
        [Tooltip("Muhafızın sesi daha kalın, oyuncununki daha ince bir aralıkta duyulur.")]
        [SerializeField] private float guardBlipPitch = 0.8f;
        [SerializeField] private float playerBlipPitch = 1.15f;
        [SerializeField] private SoundCue openSound = new SoundCue { volume = 0.6f };
        [SerializeField] private SoundCue closeSound = new SoundCue { volume = 0.6f };
        [SerializeField] private SoundCue sendSound = new SoundCue { volume = 0.5f };

        private Action<string> onSendMessageAction;
        private Action onSpacePressedAction;
        private Action currentTypingCompleteCallback;
        private Coroutine activeTypingCoroutine;
        private bool isTyping = false;
        private float currentBlipPitch = 1f;
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
        }

        private void Update()
        {
            IsInputFocused = playerInputField != null && playerInputField.isFocused;

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
            ShowPanel();

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
            ShowPanel();
            ShowInputArea(false);
            if (attitude != null) SetGuardAttitude(attitude);
            SetSpeaker(speakerName, GuardPortrait);
            if (attitude == null && guardStatusPanel != null) guardStatusPanel.SetAttitudeVisible(false);
            if (statusMessage != null) statusIndicatorText.text = $"<color=red>{statusMessage}</color>";

            StartTyping(message, onComplete: () =>
            {
                PromptSpaceAction(() => CloseDialogue());
            });
        }

        private void ShowPanel()
        {
            if (!dialoguePanel.activeSelf) openSound.Play(audioSource);
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);
        }

        public void CloseDialogue()
        {
            if (dialoguePanel.activeSelf) closeSound.Play(audioSource);
            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
            StopThinkingTimer();
            isTyping = false;
            currentTypingCompleteCallback = null;
            dialoguePanel.SetActive(false);
            onSendMessageAction = null;
            onSpacePressedAction = null;
            OnDialogueStateChanged?.Invoke(false);
        }

        private void OnSendButtonClicked()
        {
            string message = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(message) || isTyping) return;

            playerInputField.text = "";
            sendSound.Play(audioSource);
            ClearStatus();
            ShowInputArea(false);

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
        }

        private void SetSpeaker(string name, Sprite portrait)
        {
            SetSpeaker(name, portrait, currentGuardColor, isGuard: true);
        }

        // Tavır etiketi portrenin altında durduğu için yalnızca muhafız konuşurken gösterilir
        private void SetSpeaker(string name, Sprite portrait, Color frameColor, bool isGuard)
        {
            speakerNameText.text = name;
            currentBlipPitch = isGuard ? guardBlipPitch : playerBlipPitch;
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
            if (inputContainer != null) inputContainer.SetActive(isVisible);
            if (isVisible)
            {
                playerInputField.interactable = true;
                playerInputField.ActivateInputField();
            }
        }

        private void StartTyping(string textToType, Action onComplete = null)
        {
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

                if (char.IsLetterOrDigit(c) && i % Mathf.Max(1, blipEveryNChars) == 0 && audioSource != null && voiceBlipClip != null)
                {
                    audioSource.pitch = currentBlipPitch + UnityEngine.Random.Range(-0.08f, 0.08f);
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