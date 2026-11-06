using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdventureGame.Audio
{
    // Butonun üzerine gelince ve tıklayınca ses çalar. Butonlara eklenir, sesler sahnedeki tek bir 2D kaynaktan çalınır.
    [RequireComponent(typeof(Button))]
    public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private SoundCue hoverSound = new SoundCue { volume = 0.4f, pitchVariation = 0.05f };
        [SerializeField] private SoundCue clickSound = new SoundCue { volume = 0.6f, pitchVariation = 0.03f };

        private static AudioSource sharedSource;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => clickSound.Play(GetSource()));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable) hoverSound.Play(GetSource());
        }

        // Duraklatmada timeScale 0 olsa da UI sesleri çalmaya devam eder
        private static AudioSource GetSource()
        {
            if (sharedSource == null)
            {
                sharedSource = SoundCue.EnsureSource(new GameObject("UIAudio"), spatial: false);
                sharedSource.ignoreListenerPause = true;
            }
            return sharedSource;
        }
    }
}
