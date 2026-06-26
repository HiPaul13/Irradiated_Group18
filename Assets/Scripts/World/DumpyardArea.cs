using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Box Collider Trigger über dem gesamten Dumpyard.
///
/// Beim Betreten:
///   - Chase-Range des Monsters wird reduziert.
///   - Patrol-Points werden auf Dumpyard-Points umgestellt (nächster zuerst).
///
/// Beim Verlassen:
///   - Chase-Range und Patrol-Points werden wiederhergestellt.
///   - Falls das Monster NICHT gerade chased, spawnt es nach <exitSpawnDelay>
///     hinter dem Player und verfolgt ihn.
/// </summary>
public class DumpyardArea : MonoBehaviour
{
    [Header("Chase Range")]
    [Tooltip("Chase-Range des Monsters innerhalb des Dumpyards.")]
    [SerializeField] private float reducedChaseRange = 10f;

    [Header("Dumpyard Patrol Points")]
    [Tooltip("Patrol-Points die das Monster innerhalb des Dumpyards abläuft.")]
    [SerializeField] private Transform[] dumpyardPatrolPoints;

    [Header("Exit Spawn")]
    [Tooltip("Sekunden nach dem Verlassen bevor das Monster hinter dem Player spawnt " +
             "(nur wenn das Monster nicht gerade chased).")]
    [SerializeField] private float exitSpawnDelay = 5f;
    [Tooltip("Minimale Distanz hinter dem Player für den Spawn-Punkt.")]
    [SerializeField] private float minSpawnDistance = 8f;
    [Tooltip("Maximale Distanz hinter dem Player für den Spawn-Punkt.")]
    [SerializeField] private float maxSpawnDistance = 16f;
    [Tooltip("Seitlicher Zufalls-Offset des Spawn-Punkts (Meter links/rechts).")]
    [SerializeField] private float spawnSideSpread = 4f;
    [Tooltip("NavMesh.SamplePosition Suchradius um den Kandidaten-Punkt.")]
    [SerializeField] private float navMeshSampleRadius = 5f;
    [Tooltip("Wie viele Positionen versucht werden bevor aufgegeben wird.")]
    [SerializeField] private int   maxSpawnAttempts = 10;

    [Header("Radiation")]
    [Tooltip("Multiplikator für den Radiation-Anstieg im Dumpyard. " +
             "z.B. 5 = 5x schneller als normal.")]
    [SerializeField] private float radiationMultiplier = 5f;

    [Header("References")]
    [Tooltip("Kamera-Transform des Players für die 'hinter dem Player' Richtung. " +
             "Wird automatisch gesucht wenn leer.")]
    [SerializeField] private Transform playerCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Monster_Movement monster;
    private Transform        player;
    private RadiationManager radiationManager;
    private Coroutine        exitSpawnCoroutine;

    // Gespeicherte Originalwerte
    private float       originalChaseRange;
    private Transform[] originalPatrolPoints;
    private bool        isActive;

    private void Awake()
    {
        monster = FindObjectOfType<Monster_Movement>();
        if (monster == null)
            Debug.LogWarning("[DumpyardArea] Kein Monster_Movement in der Szene gefunden.");
    }

    private void Start()
    {
        if (monster != null) player = monster.Player;

        if (player != null)
            radiationManager = player.GetComponent<RadiationManager>();

        if (playerCamera == null && player != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (monster == null || isActive) return;

        // Laufenden Exit-Spawn abbrechen falls der Player schnell zurückkommt
        if (exitSpawnCoroutine != null)
        {
            StopCoroutine(exitSpawnCoroutine);
            exitSpawnCoroutine = null;
            if (showDebugLogs)
                Debug.Log("[DumpyardArea] Player zurückgekehrt — Exit-Spawn abgebrochen.");
        }

        if (radiationManager == null)
            radiationManager = other.GetComponent<RadiationManager>();

        originalChaseRange   = monster.ChaseRange;
        originalPatrolPoints = monster.PatrolPoints;

        monster.SetChaseRange(reducedChaseRange);
        radiationManager?.SetZoneMultiplier(radiationMultiplier);

        if (dumpyardPatrolPoints != null && dumpyardPatrolPoints.Length > 0)
            monster.SetPatrolPoints(dumpyardPatrolPoints,
                FindNearestPatrolIndex(monster.transform.position));

        isActive = true;

        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] Player betreten — Chase-Range → {reducedChaseRange}, " +
                      $"{dumpyardPatrolPoints?.Length ?? 0} Dumpyard-Patrol-Points aktiviert.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] OnTriggerExit — isActive={isActive}  " +
                      $"Collider: {other.name} (Tag: {other.tag})\n{System.Environment.StackTrace}");

        if (monster == null || !isActive) return;

        // Chase-Range und Patrol-Points wiederherstellen
        monster.SetChaseRange(originalChaseRange);
        radiationManager?.ResetZoneMultiplier();

        if (originalPatrolPoints != null)
            monster.SetPatrolPoints(originalPatrolPoints);

        isActive = false;

        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] Player verlassen — Chase-Range auf {originalChaseRange} " +
                      "und originale Patrol-Points wiederhergestellt.");

        // Nur spawnen wenn das Monster nicht gerade chased
        if (monster.CurrentState != Monster_Movement.MonsterState.Chase)
        {
            exitSpawnCoroutine = StartCoroutine(SpawnBehindPlayerDelayed());
        }
        else if (showDebugLogs)
        {
            Debug.Log("[DumpyardArea] Monster chased bereits — kein Exit-Spawn.");
        }
    }

    private IEnumerator SpawnBehindPlayerDelayed()
    {
        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] Exit-Spawn in {exitSpawnDelay}s...");

        yield return new WaitForSeconds(exitSpawnDelay);

        exitSpawnCoroutine = null;

        // Nochmal prüfen: Monster könnte jetzt schon chasen (z.B. weil es den Player selbst entdeckt hat)
        if (monster.CurrentState == Monster_Movement.MonsterState.Chase)
        {
            if (showDebugLogs)
                Debug.Log("[DumpyardArea] Exit-Spawn abgebrochen — Monster chased inzwischen selbst.");
            yield break;
        }

        if (player == null)
        {
            Debug.LogWarning("[DumpyardArea] Exit-Spawn: Player-Referenz fehlt.");
            yield break;
        }

        if (TryGetSpawnBehindPlayer(out Vector3 spawnPos))
        {
            bool ok = monster.TeleportAndPursue(spawnPos, player.position);
            if (showDebugLogs)
                Debug.Log(ok
                    ? $"[DumpyardArea] Monster hinter Player gespawnt bei {spawnPos:F1} → verfolgt {player.position:F1}"
                    : $"[DumpyardArea] TeleportAndPursue fehlgeschlagen für {spawnPos:F1}");
        }
        else
        {
            Debug.LogWarning("[DumpyardArea] Exit-Spawn: kein gültiger NavMesh-Punkt hinter dem Player gefunden.");
        }
    }

    private bool TryGetSpawnBehindPlayer(out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;

        Vector3 forward = playerCamera != null
            ? Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up).normalized
            : player.forward;

        Vector3 behind = -forward;
        Vector3 right  = new Vector3(behind.z, 0f, -behind.x); // perpendicular horizontal

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
            float side = Random.Range(-spawnSideSpread, spawnSideSpread);

            Vector3 candidate = player.position + behind * dist + right * side;
            candidate.y = player.position.y;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                if (showDebugLogs)
                    Debug.Log($"[DumpyardArea] Spawn-Versuch {i + 1}: {candidate:F1} — kein NavMesh.");
                continue;
            }

            spawnPos = hit.position;
            if (showDebugLogs)
                Debug.Log($"[DumpyardArea] Spawn-Versuch {i + 1}: Position gefunden bei {spawnPos:F1}");
            return true;
        }

        return false;
    }

    private int FindNearestPatrolIndex(Vector3 from)
    {
        int   nearest = 0;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < dumpyardPatrolPoints.Length; i++)
        {
            if (dumpyardPatrolPoints[i] == null) continue;
            float sqr = (dumpyardPatrolPoints[i].position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; nearest = i; }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        if (dumpyardPatrolPoints != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            for (int i = 0; i < dumpyardPatrolPoints.Length; i++)
            {
                if (dumpyardPatrolPoints[i] == null) continue;
                Gizmos.DrawSphere(dumpyardPatrolPoints[i].position, 0.4f);
                int next = (i + 1) % dumpyardPatrolPoints.Length;
                if (dumpyardPatrolPoints[next] != null)
                    Gizmos.DrawLine(dumpyardPatrolPoints[i].position, dumpyardPatrolPoints[next].position);
            }
        }

        // Visualisiert den möglichen Spawn-Bereich (hinter dem Player, nur im Editor sichtbar)
        if (Application.isPlaying) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, maxSpawnDistance);
    }
}
