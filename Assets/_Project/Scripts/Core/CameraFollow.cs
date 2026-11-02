using UnityEngine;

namespace AdventureGame.Core
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Takip Hedefi")]
        [SerializeField] private Transform target;

        [Header("Mesafe ve Açı")]
        [Tooltip("Açıkken offset, sahnede kameraya verilen konumdan hesaplanır ve aşağıdaki değer yok sayılır.")]
        [SerializeField] private bool useSceneOffset = true;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
        [SerializeField] private float smoothSpeed = 5f;

        [Tooltip("Kapalıyken kamera sahnedeki rotasyonunu korur (izometrik görünüm).")]
        [SerializeField] private bool lookAtTarget = false;

        private void Start()
        {
            if (target != null && useSceneOffset)
            {
                // Sahnede ayarlanan kamera-oyuncu mesafesi korunur
                offset = transform.position - target.position;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Kameranın gitmesi gereken hedef pozisyon
            Vector3 desiredPosition = target.position + offset;

            // Yumuşak geçiş ile hedefe ilerler
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            if (lookAtTarget)
            {
                transform.LookAt(target.position + Vector3.up * 1.5f);
            }
        }
    }
}
