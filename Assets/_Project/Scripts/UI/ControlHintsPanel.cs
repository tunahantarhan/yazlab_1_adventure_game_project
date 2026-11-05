using UnityEngine;

namespace AdventureGame.UI
{
    // Köşedeki tuş ipuçları: dolaşırken hareket tuşları, diyalogda konuşma tuşları gösterilir
    public class ControlHintsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject explorationHints;
        [SerializeField] private GameObject dialogueHints;

        private void OnEnable()
        {
            DialogueUIController.OnDialogueStateChanged += ApplyContext;
        }

        private void OnDisable()
        {
            DialogueUIController.OnDialogueStateChanged -= ApplyContext;
        }

        private void Start()
        {
            ApplyContext(false);
        }

        private void ApplyContext(bool isInDialogue)
        {
            if (explorationHints != null) explorationHints.SetActive(!isInDialogue);
            if (dialogueHints != null) dialogueHints.SetActive(isInDialogue);
        }
    }
}
