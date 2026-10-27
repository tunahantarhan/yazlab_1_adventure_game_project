using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdventureGame.UI
{
    public class DialogueUIController : MonoBehaviour
    {
        [Header("Arayüz Elemanları")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueHistoryText;
        [SerializeField] private TMP_InputField playerInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private TextMeshProUGUI statusIndicatorText;

        private Action<string> onSendMessageAction;

        private void Awake()
        {
            if (dialoguePanel != null)
                dialoguePanel.SetActive(false);

            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        public void OpenDialogue(string speakerName, Action<string> onSendMessage)
        {
            onSendMessageAction = onSendMessage;
            speakerNameText.text = speakerName;
            dialogueHistoryText.text = "Muhafız: 'Buradan geçemezsin yabancı! Beni ikna etmen gerek.'";
            statusIndicatorText.text = "";
            playerInputField.text = "";
            playerInputField.interactable = true;
            sendButton.interactable = true;
            dialoguePanel.SetActive(true);
        }

        public void CloseDialogue()
        {
            dialoguePanel.SetActive(false);
            onSendMessageAction = null;
        }

        private void OnSendButtonClicked()
        {
            string message = playerInputField.text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            // Oyuncunun mesajını geçmişe ekle
            dialogueHistoryText.text += $"\n\n<b>Sen:</b> {message}";
            playerInputField.text = "";

            // Asenkron istek süresince butonları kilitle ve düşünüyor uyarısı göster
            SetThinkingState(true);

            onSendMessageAction?.Invoke(message);
        }

        public void SetThinkingState(bool isThinking)
        {
            playerInputField.interactable = !isThinking;
            sendButton.interactable = !isThinking;
            statusIndicatorText.text = isThinking ? "<i>Muhafız düşünüyor...</i>" : "";
        }

        public void ShowNPCResponse(string reply)
        {
            SetThinkingState(false);
            dialogueHistoryText.text += $"\n\n<b>Muhafız:</b> {reply}";
        }

        public void ShowError(string errorMessage)
        {
            SetThinkingState(false);
            statusIndicatorText.text = $"<color=red>Hata: {errorMessage}</color>";
        }
    }
}