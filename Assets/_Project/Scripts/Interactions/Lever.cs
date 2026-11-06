using System.Collections;
using UnityEngine;
using AdventureGame.Audio;
using AdventureGame.Interfaces;

namespace AdventureGame.Interactions
{
    public class Lever : MonoBehaviour, IInteractable, IResettable
    {
        [Header("Etkilesim Ayarlari")]
        [SerializeField] private DoorController targetDoor;
        [SerializeField] private Transform interactionPoint;

        [Header("Görsel")]
        [Tooltip("Çekilince dönen parça (asa). Boş bırakılırsa objenin kendisi döner.")]
        [SerializeField] private Transform handle;
        [SerializeField] private Vector3 pulledRotation = new Vector3(45f, 0f, 0f);
        [SerializeField] private float pullDuration = 0.4f;

        [Header("Ses")]
        [SerializeField] private SoundCue pullSound = new SoundCue();

        private bool isActivated = false;
        private Quaternion initialLocalRotation;
        private Coroutine pullCoroutine;
        private AudioSource audioSource;

        private Transform Handle => handle != null ? handle : transform;

        public string InteractionPrompt => isActivated ? "Kullanildi" : "Salteri Indir";
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
        public bool CanInteract => !isActivated;

        private void Awake()
        {
            initialLocalRotation = Handle.localRotation;
            audioSource = SoundCue.EnsureSource(gameObject, spatial: true);
        }

        public void Interact()
        {
            if (isActivated) return;

            isActivated = true;
            Debug.Log("[Lever] Salter cekildi, kapi aciliyor!");

            // Şalterin görseli yumuşakça döndürülür, çekildiği izlenimi verilir
            pullCoroutine = StartCoroutine(PullRoutine(initialLocalRotation * Quaternion.Euler(pulledRotation)));
            pullSound.Play(audioSource);

            if (targetDoor != null)
            {
                targetDoor.OpenDoor();
            }
        }

        // Şalter ve bağlı kapı defaault haline döner
        public void ResetState()
        {
            isActivated = false;
            if (pullCoroutine != null) StopCoroutine(pullCoroutine);
            Handle.localRotation = initialLocalRotation;
            if (targetDoor != null) targetDoor.ResetState();
        }

        private IEnumerator PullRoutine(Quaternion targetRotation)
        {
            Quaternion startRotation = Handle.localRotation;
            for (float t = 0f; t < pullDuration; t += Time.deltaTime)
            {
                Handle.localRotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.SmoothStep(0f, 1f, t / pullDuration));
                yield return null;
            }
            Handle.localRotation = targetRotation;
            pullCoroutine = null;
        }
    }
}
