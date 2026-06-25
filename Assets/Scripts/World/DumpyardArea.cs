using UnityEngine;

/// <summary>
/// Lege dieses Script auf ein GameObject mit einem Box Collider (Is Trigger = true),
/// das den gesamten Dumpyard abdeckt.
///
/// Effekte wenn der Player die Zone betritt:
///   - Die Chase-Range des Monsters wird auf <reducedChaseRange> gesenkt.
///   - Die Patrol-Points des Monsters werden auf die Dumpyard-spezifischen
///     <dumpyardPatrolPoints> umgestellt.
///
/// Beim Verlassen werden beide Werte wiederhergestellt.
/// </summary>
public class DumpyardArea : MonoBehaviour
{
    [Header("Chase Range")]
    [Tooltip("Chase-Range des Monsters, solange der Player im Dumpyard ist.")]
    [SerializeField] private float reducedChaseRange = 10f;

    [Header("Dumpyard Patrol Points")]
    [Tooltip("Patrol-Points die das Monster innerhalb des Dumpyards abläuft.")]
    [SerializeField] private Transform[] dumpyardPatrolPoints;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Monster_Movement monster;

    // Gespeicherte Originalwerte
    private float        originalChaseRange;
    private Transform[]  originalPatrolPoints;
    private bool         isActive;

    private void Awake()
    {
        monster = FindObjectOfType<Monster_Movement>();
        if (monster == null)
            Debug.LogWarning("[DumpyardArea] Kein Monster_Movement im Szene gefunden.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (monster == null || isActive) return;

        // Originalwerte sichern
        originalChaseRange   = monster.ChaseRange;
        originalPatrolPoints = monster.PatrolPoints;

        // Dumpyard-Werte setzen
        monster.SetChaseRange(reducedChaseRange);

        if (dumpyardPatrolPoints != null && dumpyardPatrolPoints.Length > 0)
            monster.SetPatrolPoints(dumpyardPatrolPoints, FindNearestPatrolIndex(monster.transform.position));

        isActive = true;

        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] Player betreten — Chase-Range reduziert auf {reducedChaseRange}, " +
                      $"{dumpyardPatrolPoints?.Length ?? 0} Dumpyard-Patrol-Points aktiviert.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log($"[DumpyardArea] OnTriggerExit gefeuert — isActive={isActive}. " +
                  $"Collider: {other.name} (Tag: {other.tag})\n{System.Environment.StackTrace}");
        if (monster == null || !isActive) return;

        // Originalwerte wiederherstellen
        monster.SetChaseRange(originalChaseRange);

        if (originalPatrolPoints != null)
            monster.SetPatrolPoints(originalPatrolPoints);

        isActive = false;

        if (showDebugLogs)
            Debug.Log($"[DumpyardArea] Player verlassen — Chase-Range ({originalChaseRange}) " +
                      "und originale Patrol-Points wiederhergestellt.");
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
        if (dumpyardPatrolPoints == null) return;

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
}
