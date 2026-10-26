using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace AdventureGame.Interactions
{
    [RequireComponent(typeof(NavMeshObstacle))]
    public class DoorController : MonoBehaviour
    {
        [Header("Kapi Ayarlari")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
        [SerializeField] private float openSpeed = 2f;

        private Vector3 closedPosition;
        private Vector3 targetPosition;
        private NavMeshObstacle navObstacle;
        private bool isOpen = false;

        private void Awake()
        {
            closedPosition = transform.position;
            targetPosition = closedPosition + openOffset;
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
            while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, openSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPosition;
        }
    }
}