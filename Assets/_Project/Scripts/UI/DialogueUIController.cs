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

        [Header("Panel ve Kapsayicilar")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private GameObject inputContainer; // InputField ve Gönder butonunu tutan alt grup

        [Header("Metin ve Resim Bilesenleri")]
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueBodyText;
        [SerializeField] private TMP_InputField playerInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI statusIndicatorText;
        [SerializeField] private Image portraitImage; // Karakter avatarı

        [Header("Gorsel Varliklar (Portreler)")]
        [SerializeField] private Sprite guardNormalSprite;
        [SerializeField] private Sprite guardThinkingSprite;
        [SerializeField] private Sprite playerSprite;

        [Header("Ses Efekti (Mumble / Typewriter)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip voiceBlipClip;
        [SerializeField] private float typingSpeed = 0.035f; // Harf geciş hızı

        private Action<string> onSendMessageAction;
        private Coroutine activeTypingCoroutine;
        private bool isTyping = false;

        private void Awake()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (sendButton != null) sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        private void Update()
        {
            if (!dialoguePanel.activeSelf) return;

            // Enter tuşuna basıldığında input gönderilir
            if (inputContainer.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (!string.IsNullOrEmpty(playerInputField.text.Trim()) && playerInputField.interactable)
                {
                    OnSendButtonClicked();
                }
            }
        }

        public void OpenDialogue(string speakerName, Action<string> onSendMessage)
        {
            onSendMessageAction = onSendMessage;
            dialoguePanel.SetActive(true);
            OnDialogueStateChanged?.Invoke(true);

            // İlk muhafız repliği ile başlanır
            SetSpeaker(speakerName, guardNormalSprite);
            string initialLine = "Buradan gecemezsin yabanci! Beni ikna etmen gerek.";
            StartTyping(initialLine, onComplete: () =>
            {
                // Muhafız konuşmayı bitirince oyuncunun input kutusu açılır
                ShowInputArea(true);
            });
        }

        public void CloseDialogue()
        {
            if (activeTypingCoroutine != null) StopCoroutine(activeTypingCoroutine);
            dialoguePanel.SetActive(false);
            onSendMessageAction = null;
            OnDialogueStateChanged?.Invoke(false);
        }

        private void OnSendButtonClicked()
        {
            string message = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(message) || isTyping) return;

            playerInputField.text = "";
            ShowInputArea(false); // Yazı gönderilince input kutusu gizlenir

            // Oyuncu konuşuyor moduna geçilir
            SetSpeaker("Sen", playerSprite);
            StartTyping(message, onComplete: () =>
            {
                // Oyuncu repliğini söyledi, şimdi sıra muhafızın düşünmesinde
                SetThinkingState(true);
                onSendMessageAction?.Invoke(message);
            });
        }

        public void SetThinkingState(bool isThinking)
        {
            if (isThinking)
            {
                SetSpeaker("Kraliyet Muhafizi", guardThinkingSprite != null ? guardThinkingSprite : guardNormalSprite);
                dialogueBodyText.text = "";
                statusIndicatorText.text = "<i>Dusunuyor...</i>";
            }
            else
            {
                statusIndicatorText.text = "";
            }
        }

        public void ShowNPCResponse(string reply, Action onComplete = null)
        {
            SetThinkingState(false);
            SetSpeaker("Kraliyet Muhafizi", guardNormalSprite);

            StartTyping(reply, onComplete: () =>
            {
                if (onComplete != null)
                {
                    onComplete.Invoke();
                }
                else
                {
                    // İkna olunmadıysa oyuncuya tekrar yazma hakkı verir
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
            activeTypingCoroutine = StartCoroutine(TypeWriterRoutine(textToType, onComplete));
        }

        private IEnumerator TypeWriterRoutine(string textToType, Action onComplete)
        {
            isTyping = true;
            dialogueBodyText.text = "";

            for (int i = 0; i < textToType.Length; i++)
            {
                char c = textToType[i];
                dialogueBodyText.text += c;

                // konuşma ses efekti
                if (char.IsLetterOrDigit(c) && audioSource != null && voiceBlipClip != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
                    audioSource.PlayOneShot(voiceBlipClip, 0.4f);
                }

                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            activeTypingCoroutine = null;
            onComplete?.Invoke();
        }
    }
}