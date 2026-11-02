using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace AdventureGame.Interactions
{
    public enum DoorOpenMode
    {
        Swing,
        Slide
    }

    [RequireComponent(typeof(NavMeshObstacle))]
    public class DoorController : MonoBehaviour
    {
        [Header("Kapi Ayarlari")]
        [SerializeField] private DoorOpenMode openMode = DoorOpenMode.Swing;
        [Tooltip("Açılma süresi (saniye).")]
        [SerializeField] private float openDuration = 1.2f;

        [Header("Swing (Menteşe)")]
        [Tooltip("Menteşenin kapının pivotuna göre yerel konumu. Sahnede sarı küre ile gösterilir.")]
        [SerializeField] private Vector3 hingeLocalOffset = new Vector3(-1f, 0f, 0f);
        [Tooltip("Pozitif/negatif değer kapının hangi yöne (öne/arkaya) açılacağını belirler.")]
        [SerializeField] private float swingAngle = 90f;

        [Header("Slide")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);

        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private Vector3 hingeWorldPoint;
        private NavMeshObstacle navObstacle;
        private bool isOpen = false;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            closedPosition = transform.position;
            closedRotation = transform.rotation;
            hingeWorldPoint = transform.TransformPoint(hingeLocalOffset);
            navObstacle = GetComponent<NavMeshObstacle>();
        }

        public void OpenDoor()
        {
            if (isOpen) return;
            isOpen = true;

            // NavMesh üzerindeki engel kaldırılır, böylece ajan ve oyuncu geçebilir
            if (navObstacle != null)
            {
                navObstacle.enabled = false;
            }

            StartCoroutine(AnimateDoorOpen());
        }

        private IEnumerator AnimateDoorOpen()
        {
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                ApplyOpenAmount(Mathf.SmoothStep(0f, 1f, elapsed / openDuration));
                yield return null;
            }
            ApplyOpenAmount(1f);
        }

        // 0 = kapalı, 1 = tamamen açık
        private void ApplyOpenAmount(float t)
        {
            if (openMode == DoorOpenMode.Slide)
            {
                transform.position = closedPosition + openOffset * t;
                return;
            }

            // Kapı, menteşe noktası etrafında dikey eksende döndürülür
            Quaternion swing = Quaternion.AngleAxis(swingAngle * t, Vector3.up);
            transform.SetPositionAndRotation(
                hingeWorldPoint + swing * (closedPosition - hingeWorldPoint),
                swing * closedRotation);
        }

        private void OnDrawGizmosSelected()
        {
            if (openMode != DoorOpenMode.Swing) return;

            Vector3 hinge = Application.isPlaying ? hingeWorldPoint : transform.TransformPoint(hingeLocalOffset);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(hinge, 0.15f);
            Gizmos.DrawLine(hinge, hinge + Vector3.up * 3f);
        }
    }
}
