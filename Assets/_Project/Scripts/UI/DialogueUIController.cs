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

        [Header("Space İndikatörü")]
        [SerializeField] private CanvasGroup spaceIndicatorGroup;
        [SerializeField] private float blinkSpeed = 2.2f;

        [Header("Portreler")]
        [SerializeField] private Sprite guardNormalSprite;
        [SerializeField] private Sprite guardThinkingSprite;
        [SerializeField] private Sprite playerSprite;

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
            onSendMessageAction = onSendMessage;
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);

            HideSpaceIndicator();
            SetSpeaker(speakerName, guardNormalSprite);
            ShowInputArea(true);
            StartTyping(greetingText);
        }

        public void ShowSimpleMessage(string speakerName, string message)
        {
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);
            ShowInputArea(false);
            HideSpaceIndicator();
            SetSpeaker(speakerName, guardNormalSprite);

            StartTyping(message, onComplete: () =>
            {
                PromptSpaceAction(() => CloseDialogue());
            });
        }

        public void CloseDialogue()
        {
            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
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
            ShowInputArea(false);
            HideSpaceIndicator();

            SetSpeaker("Sen", playerSprite);
            StartTyping(message, onComplete: () =>
            {
                PromptSpaceAction(() =>
                {
                    SetThinkingState(true);
                    onSendMessageAction?.Invoke(message);
                });
            });
        }

        public void SetThinkingState(bool isThinking)
        {
            HideSpaceIndicator();
            if (isThinking)
            {
                SetSpeaker("Kraliyet Muhafızı", guardThinkingSprite != null ? guardThinkingSprite : guardNormalSprite);
                dialogueBodyText.text = "";
                statusIndicatorText.text = "<i>Muhafız düşünüyor...</i>";
            }
            else
            {
                statusIndicatorText.text = "";
            }
        }

        public void ShowNPCResponse(string reply, bool isConvinced, Action onConvincedAction)
        {
            SetThinkingState(false);
            SetSpeaker("Kraliyet Muhafızı", guardNormalSprite);

            StartTyping(reply, onComplete: () =>
            {
                if (isConvinced)
                {
                    onConvincedAction?.Invoke();
                }
                else
                {
                    ShowInputArea(true);
                }
            });
        }

        public void ShowError(string errorMessage)
        {
            SetThinkingState(false);
            statusIndicatorText.text = $"<color=red>Hata: {errorMessage}</color>";
            ShowInputArea(true);
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
            speakerNameText.text = name;
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