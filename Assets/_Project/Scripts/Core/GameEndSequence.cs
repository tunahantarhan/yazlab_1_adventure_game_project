using System.Collections;
using UnityEngine;
using AdventureGame.AI;
using AdventureGame.Player;
using AdventureGame.UI;

namespace AdventureGame.Core
{
    // Oyuncu son merdivene gelince kontrolü alır, onu merdivenden aşağı yürütür,
    // ekranı karartır ve bitiş menüsünü açar
    [RequireComponent(typeof(Collider))]
    public class GameEndSequence : MonoBehaviour
    {
        [Header("Bağlantılar")]
        [SerializeField] private PlayerController player;
        [SerializeField] private EndScreenController endScreen;
        [Tooltip("Bitiş özetindeki mesaj sayısı için.")]
        [SerializeField] private NPCGuardController guard;
        [Tooltip("Oyuncunun merdivenden inerken sırayla yürüyeceği noktalar (yukarıdan aşağıya).")]
        [SerializeField] private Transform[] descentPath;

        [Header("İniş Ayarları")]
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private float turnSpeed = 6f;
        [Tooltip("Yürüyüş başladıktan kaç saniye sonra ekran kararmaya başlar.")]
        [SerializeField] private float fadeDelay = 1.5f;
        [SerializeField] private float fadeDuration = 2.5f;

        private bool hasStarted = false;

        public bool HasStarted => hasStarted;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasStarted || !other.CompareTag("Player")) return;

            if (player == null) player = other.GetComponent<PlayerController>();
            if (player == null) return;

            hasStarted = true;
            StartCoroutine(EndingRoutine());
        }

        private IEnumerator EndingRoutine()
        {
            player.SetControlEnabled(false);
            if (endScreen != null)
            {
                endScreen.SetSummary(Time.time - MainMenuController.GameplayStartTime, guard != null ? guard.PlayerMessageCount : 0);
            }

            Coroutine fade = endScreen != null ? StartCoroutine(FadeAfterDelay()) : null;
            yield return WalkDescentPath();
            if (fade != null) yield return fade;

            if (endScreen != null) endScreen.ShowMenu();
        }

        private IEnumerator FadeAfterDelay()
        {
            yield return new WaitForSeconds(fadeDelay);
            yield return endScreen.FadeToBlack(fadeDuration);
        }

        private IEnumerator WalkDescentPath()
        {
            if (descentPath == null) yield break;

            Transform playerTransform = player.transform;
            foreach (Transform point in descentPath)
            {
                if (point == null) continue;

                while (Vector3.Distance(playerTransform.position, point.position) > 0.05f)
                {
                    Vector3 toPoint = point.position - playerTransform.position;
                    Vector3 flatDirection = new Vector3(toPoint.x, 0f, toPoint.z);
                    if (flatDirection.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
                        playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                    }

                    playerTransform.position = Vector3.MoveTowards(playerTransform.position, point.position, walkSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (descentPath == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < descentPath.Length; i++)
            {
                if (descentPath[i] == null) continue;
                Gizmos.DrawSphere(descentPath[i].position, 0.15f);
                if (i > 0 && descentPath[i - 1] != null)
                {
                    Gizmos.DrawLine(descentPath[i - 1].position, descentPath[i].position);
                }
            }
        }
    }
}
