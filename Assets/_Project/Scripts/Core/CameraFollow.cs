using UnityEngine;

namespace AdventureGame.Core
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Takip Hedefi")]
        [SerializeField] private Transform target;

        [Header("Mesafe ve Açı")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
        [SerializeField] private float smoothSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            // Kameranın gitmesi gereken hedef pozisyon
            Vector3 desiredPosition = target.position + offset;

            // Yumuşak geçiş ile hedefe ilerler
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            // Kameranın bakış açısını sabit tutarak hedefe bakmasını sağlar
            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }
}