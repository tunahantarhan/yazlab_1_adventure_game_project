using UnityEngine;

namespace AdventureGame.Audio
{
    // Karakter yatayda belirli bir mesafe aldıkça adım sesi çalar.
    // Hareketin kaynağına (CharacterController, NavMeshAgent, ara sahne) bakmadığı için oyuncu ve companion'da aynı şekilde çalışır.
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private SoundCue footsteps = new SoundCue { volume = 0.35f, pitchVariation = 0.1f };
        [Tooltip("İki adım arası mesafe (metre). Koşarken adımlar doğal olarak sıklaşır.")]
        [SerializeField] private float strideLength = 1.6f;
        [Tooltip("Bir karede bundan fazla yer değiştirme ışınlanma sayılır, adım çalınmaz.")]
        [SerializeField] private float teleportThreshold = 2f;

        private AudioSource audioSource;
        private Vector3 lastPosition;
        private float distanceSinceStep;

        private void Awake()
        {
            audioSource = SoundCue.EnsureSource(gameObject, spatial: true);
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            float distance = delta.magnitude;
            if (distance > teleportThreshold) return;

            distanceSinceStep += distance;
            if (distanceSinceStep < strideLength) return;

            distanceSinceStep = 0f;
            footsteps.Play(audioSource);
        }
    }
}
