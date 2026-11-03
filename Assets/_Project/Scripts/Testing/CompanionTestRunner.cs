using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using AdventureGame.AI;
using AdventureGame.Interfaces;

namespace AdventureGame.Testing
{
    // Companion'ı farklı başlangıç noktalarından tekrar tekrar şaltere gönderir; başarı oranını ve
    // ortalama tamamlama süresini ölçer. Sonuçlar konsola ve Markdown dosyasına (README raporu için) yazılır.
    public class CompanionTestRunner : MonoBehaviour
    {
        private struct TrialResult
        {
            public string StartName;
            public int Run;
            public bool Success;
            public bool Reachable;      // "Kapı kapalıyken bu noktadan şaltere NavMesh yolu var mı?"
            public float Duration;
            public float PathLength;
            public float TravelledDistance;
        }

        [Header("Bağlantılar")]
        [SerializeField] private CompanionController companion;
        [Tooltip("Her denemeden önce ilk haline döndürülecek nesneler (örneğin şalter -> dolayısıyla şaltere bağlı kapı).")]
        [SerializeField] private MonoBehaviour[] resettableObjects;
        [Tooltip("Companion'ın denemelere başlayacağı noktalar.")]
        [SerializeField] private Transform[] startPoints;

        [Header("Test Ayarları")]
        [SerializeField] private KeyCode startKey = KeyCode.Tab; // Tab tuşu ile test işlemi başlatılır
        [SerializeField] private int runsPerStartPoint = 5;
        [Tooltip("Bu süre içinde görev bitmezse deneme başarısız sayılır (saniye).")]
        [SerializeField] private float trialTimeout = 30f;
        [Tooltip("Her denemede başlangıç noktasının etrafında rastgele seçilen konumun yarıçapı (m). Yön de rastgele seçilir.")]
        [SerializeField] private float startJitterRadius = 1f;
        [Tooltip("Sıfırlamadan sonra NavMesh engelinin yeniden oyulması için beklenen süre.")]
        [SerializeField] private float settleTime = 0.5f;

        private readonly List<TrialResult> results = new List<TrialResult>();
        private bool isRunning = false;
        private bool? lastOutcome;
        private string progressText = "";

        private void Update()
        {
            if (!isRunning && Input.GetKeyDown(startKey))
            {
                StartCoroutine(RunAllTrials());
            }
        }

        private void OnEnable()
        {
            if (companion != null) companion.ObjectiveFinished += HandleObjectiveFinished;
        }

        private void OnDisable()
        {
            if (companion != null) companion.ObjectiveFinished -= HandleObjectiveFinished;
        }

        private void HandleObjectiveFinished(bool success)
        {
            lastOutcome = success;
        }

        private IEnumerator RunAllTrials()
        {
            if (companion == null || startPoints == null || startPoints.Length == 0)
            {
                Debug.LogWarning("[CompanionTest] Companion veya başlangıç noktaları atanmamış.");
                yield break;
            }

            isRunning = true;
            results.Clear();
            Vector3 originalPosition = companion.transform.position;
            int total = startPoints.Count(p => p != null) * runsPerStartPoint;

            foreach (Transform start in startPoints)
            {
                if (start == null) continue;

                for (int run = 1; run <= runsPerStartPoint; run++)
                {
                    progressText = $"Companion testi {results.Count + 1}/{total} | Başlangıç: {start.name} | Deneme {run}";
                    yield return RunTrial(start, run);
                }
            }

            // Test bitince companion default başlangıç yerine döner, oyun kaldığı yerden devam edebilir
            ResetScene();
            companion.Agent.Warp(originalPosition);
            companion.ChangeState(CompanionStateType.FollowPlayer);
            progressText = "";
            isRunning = false;
            Report();
        }

        private IEnumerator RunTrial(Transform start, int run)
        {
            ResetScene();
            NavMeshAgent agent = companion.Agent;

            // Denemeler birebir aynı olmasın diye başlangıç konumu ve yönü her seferinde biraz değişir
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * startJitterRadius;
            Vector3 startPosition = start.position + new Vector3(jitter.x, 0f, jitter.y);

            if (!NavMesh.SamplePosition(startPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[CompanionTest] '{start.name}' NavMesh üzerinde değil, deneme başarısız sayıldı.");
                results.Add(new TrialResult { StartName = start.name, Run = run });
                yield break;
            }

            agent.Warp(hit.position);
            agent.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            companion.ChangeState(CompanionStateType.FollowPlayer);
            yield return new WaitForSeconds(settleTime);

            bool reachable = TryCalculatePath(agent.transform.position, companion.ObjectivePoint, out float pathLength);
            lastOutcome = null;
            float startTime = Time.time;
            float travelled = 0f;
            Vector3 lastPosition = agent.transform.position;

            bool accepted = companion.IssueObjectiveCommand();
            while (accepted && lastOutcome == null && Time.time - startTime < trialTimeout)
            {
                yield return null;
                travelled += Vector3.Distance(lastPosition, agent.transform.position);
                lastPosition = agent.transform.position;
            }

            var result = new TrialResult
            {
                StartName = start.name,
                Run = run,
                Success = lastOutcome == true,
                Reachable = reachable,
                Duration = Time.time - startTime,
                PathLength = pathLength,
                TravelledDistance = travelled
            };
            results.Add(result);

            string outcomeText = !reachable ? "YOL YOK (companion reddetti)" : result.Success ? "BAŞARILI" : "BAŞARISIZ";
            Debug.Log($"[CompanionTest] {start.name} #{run}: {outcomeText} " +
                      $"| {result.Duration:0.00} sn | yol {result.PathLength:0.0} m | kat edilen {result.TravelledDistance:0.0} m");
        }

        private void ResetScene()
        {
            if (resettableObjects == null) return;
            foreach (MonoBehaviour mono in resettableObjects)
            {
                if (mono is IResettable resettable) resettable.ResetState();
            }
        }

        // Şalterin etkileşim noktası NavMesh'in hemen dışında olabilir; ajan gibi en yakın NavMesh noktası hedeflenir
        private static bool TryCalculatePath(Vector3 from, Vector3 to, out float length)
        {
            length = 0f;
            if (NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas)) to = hit.position;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)) return false;

            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return path.status == NavMeshPathStatus.PathComplete;
        }

        private void Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Companion test sonuçları ({DateTime.Now:yyyy-MM-dd HH:mm})");
            sb.AppendLine();
            sb.AppendLine("| Başlangıç noktası | Deneme | Başarı oranı | Ort. süre (sn) | Min / Maks (sn) | NavMesh yol uzunluğu (m) | Yol yok (doğru red) |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            foreach (var group in results.GroupBy(r => r.StartName))
            {
                AppendSummaryRow(sb, group.Key, group.ToList());
            }
            AppendSummaryRow(sb, "**Toplam**", results);

            sb.AppendLine();
            sb.AppendLine("| Başlangıç | # | Sonuç | Süre (sn) | Yol (m) | Kat edilen (m) |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (TrialResult r in results)
            {
                sb.AppendLine($"| {r.StartName} | {r.Run} | {(!r.Reachable ? "Yol yok, reddetti" : r.Success ? "Başarılı" : "Başarısız")} | {r.Duration:0.00} | {r.PathLength:0.0} | {r.TravelledDistance:0.0} |");
            }

            string report = sb.ToString();
            Debug.Log("[CompanionTest] Test tamamlandı:\n" + report);

            try
            {
                string directory = Application.isEditor
                    ? Path.Combine(Application.dataPath, "..", "TestResults")
                    : Application.persistentDataPath;
                Directory.CreateDirectory(directory);
                string filePath = Path.Combine(directory, $"companion_test_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                File.WriteAllText(filePath, report);
                Debug.Log($"[CompanionTest] Rapor kaydedildi: {Path.GetFullPath(filePath)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CompanionTest] Rapor dosyası yazılamadı: {e.Message}");
            }
        }

        private static void AppendSummaryRow(StringBuilder sb, string name, List<TrialResult> trials)
        {
            // Yolu olmayan noktalarda reddetmek doğru davranıştır; başarı oranı yalnızca ulaşılabilir denemelerden hesaplanır
            List<TrialResult> reachable = trials.Where(t => t.Reachable).ToList();
            List<TrialResult> successful = reachable.Where(t => t.Success).ToList();
            float rate = reachable.Count > 0 ? 100f * successful.Count / reachable.Count : 0f;
            string average = successful.Count > 0 ? successful.Average(t => t.Duration).ToString("0.00") : "-";
            string range = successful.Count > 0
                ? $"{successful.Min(t => t.Duration):0.00} / {successful.Max(t => t.Duration):0.00}"
                : "-";
            string pathLength = reachable.Count > 0 ? reachable.Average(t => t.PathLength).ToString("0.0") : "-";
            string ratio = reachable.Count > 0 ? $"%{rate:0} ({successful.Count}/{reachable.Count})" : "-";
            int unreachable = trials.Count - reachable.Count;

            sb.AppendLine($"| {name} | {trials.Count} | {ratio} | {average} | {range} | {pathLength} | {unreachable} |");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(progressText)) return;
            GUI.Label(new Rect(10, 10, 600, 25), progressText);
        }
    }
}
