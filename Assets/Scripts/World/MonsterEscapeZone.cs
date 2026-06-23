using UnityEngine;

/// <summary>
/// Attach to a trigger collider to create a soft "escape zone".
/// When the player is inside and the monster is far enough away,
/// the monster loses the player and investigates the last known position.
/// Unlike SafeZone, this does NOT grant full immunity — if the monster
/// is closer than minDistanceStillDangerous it keeps chasing normally.
/// </summary>
public class MonsterEscapeZone : MonoBehaviour
{
    [Header("Escape Zone Tuning")]
    [Tooltip("Effective lose-player range while the player is inside this zone. " +
             "Should be much smaller than the normal losePlayerRange (e.g. 35–50).")]
    [SerializeField] private float reducedLosePlayerRange = 40f;

    [Tooltip("If the monster is closer than this distance the zone offers no protection " +
             "and the monster continues chasing normally.")]
    [SerializeField] private float minDistanceStillDangerous = 15f;

    [Header("Forest Teleport Reset (optional)")]
    [Tooltip("If true, entering this zone resets the forest-teleport timer, " +
             "giving the player a brief window before the next teleport.")]
    [SerializeField] private bool resetForestTeleportTimerOnEnter = false;
    [SerializeField] private EnemyTeleportManager teleportManager;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Try to auto-find the teleport manager if not assigned
        if (teleportManager == null)
            teleportManager = EnemyTeleportManager.Instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (showDebugLogs)
            Debug.Log($"[EscapeZone] Player entered escape zone '{gameObject.name}'. " +
                      $"Reduced lose range = {reducedLosePlayerRange}, " +
                      $"min danger distance = {minDistanceStillDangerous}");

        Monster_Movement monster = FindObjectOfType<Monster_Movement>();
        if (monster != null)
            monster.SetPlayerInEscapeZone(true, reducedLosePlayerRange, minDistanceStillDangerous);

        if (resetForestTeleportTimerOnEnter)
        {
            if (teleportManager == null)
                teleportManager = EnemyTeleportManager.Instance;

            if (teleportManager != null)
            {
                teleportManager.ResetForestTimer();
                if (showDebugLogs)
                    Debug.Log("[EscapeZone] Forest teleport timer reset on zone enter.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (showDebugLogs)
            Debug.Log($"[EscapeZone] Player exited escape zone '{gameObject.name}'.");

        Monster_Movement monster = FindObjectOfType<Monster_Movement>();
        if (monster != null)
            monster.SetPlayerInEscapeZone(false, 0f, 0f);
    }

    // ── Editor gizmo — shows the collider range ──────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(transform.position, reducedLosePlayerRange);

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawSphere(transform.position, minDistanceStillDangerous);
    }
}
