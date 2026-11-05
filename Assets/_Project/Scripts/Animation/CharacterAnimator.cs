using UnityEngine;

namespace AdventureGame.Animation
{
    // Karakterin gerçek hareket hızını ölçüp Animator'a aktarır. Hız konum farkından hesaplandığı için
    // CharacterController (oyuncu), NavMeshAgent (companion) veya hareketsiz NPC'de aynı şekilde çalışır.
    public class CharacterAnimator : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int InteractParam = Animator.StringToHash("Interact");
        private static readonly int SpawnParam = Animator.StringToHash("Spawn");

        [Tooltip("Boş bırakılırsa alt objelerdeki Animator kullanılır.")]
        [SerializeField] private Animator animator;
        [Tooltip("Hız değişimlerinin yumuşatılma süresi; animasyon geçişlerindeki titremeyi önler.")]
        [SerializeField] private float speedDampTime = 0.1f;

        private Vector3 lastPosition;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f; // Yerçekimi ve merdiven inişi yürüme hızına sayılmaz
            lastPosition = transform.position;

            animator.SetFloat(SpeedParam, delta.magnitude / Time.deltaTime, speedDampTime, Time.deltaTime);
        }

        public void PlayInteract()
        {
            if (animator != null) animator.SetTrigger(InteractParam);
        }

        public void PlaySpawn()
        {
            if (animator != null) animator.SetTrigger(SpawnParam);
        }
    }
}
