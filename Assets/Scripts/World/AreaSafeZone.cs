using UnityEngine;

/// <summary>
/// Use this on Castle / House / Barn / any scattered safe area.
/// Unlike the cabin SafeZone, this does NOT send the monster straight back to patrol.
///
/// When the player enters:
///   • Player is protected — monster cannot attack or kill them.
///   • Monster randomly roams around this area for monsterStayTime seconds.
///   • Monster will NOT walk inside safeAreaCollider.
///   • After the timer expires the monster returns to cabin patrol on its own.
///
/// When the player exits before the timer:
///   • Protection is removed immediately.
///   • Monster can chase the player again if it detects them.
///   • Monster continues roaming until its timer runs out.
///
/// Inspector setup:
///   1. Add a Box / Sphere Collider → Is Trigger = true  (the safe volume the player hides in).
///   2. Assign that same collider to safeAreaCollider so the monster avoids it.
///   3. Place an empty Transform in the scene near the area and assign it to roamCenter.
///   4. Tune roamRadius, monsterStayTime, randomMoveInterval.
///   5. Optionally assign monster and teleportManager, or leave blank for auto-find.
/// </summary>
public class AreaSafeZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The monster. Leave blank to auto-find.")]
    [SerializeField] private Monster_Movement    monster;
    [Tooltip("Leave blank to auto-find via Instance.")]
    [SerializeField] private EnemyTeleportManager teleportManager;

    [Header("Safe Area")]
    [Tooltip("The trigger collider the player hides in. The monster will NOT walk inside it. " +
             "Assign the collider on this same GameObject, or drag a different one.")]
    [SerializeField] private Collider safeAreaCollider;

    [Header("Monster Roam Settings")]
    [Tooltip("Center of the zone the monster patrols around while the player is inside. " +
             "Typically a Transform placed in the middle of the exterior of the area.")]
    [SerializeField] private Transform roamCenter;
    [Tooltip("Radius (metres) around roamCenter the monster may wander.")]
    [SerializeField] private float roamRadius = 35f;
    [Tooltip("Total time (seconds) the monster stays near this area before returning to cabin patrol.")]
    [SerializeField] private float monsterStayTime = 30f;
    [Tooltip("How often (seconds) the monster picks a new random roam destination.")]
    [SerializeField] private float randomMoveInterval = 4f;
    [Tooltip("NavMesh.SamplePosition search radius when picking random roam points.")]
    [SerializeField] private float navMeshSampleRadius = 7f;

    [Header("Radiation")]
    [Tooltip("If true, radiation decreases while the player is inside this zone.")]
    [SerializeField] private bool reduceRadiation = true;
    [Tooltip("How fast radiation drains per second inside this zone. " +
             "Uses RadiationManager.safeZoneDecreasePerSecond if left at 0.")]
    [SerializeField] private float radiationDecreasePerSecond = 1.5f;

    [Header("Options")]
    [Tooltip("Reset the forest teleport timer when the player enters this zone.")]
    [SerializeField] private bool resetForestTimerOnEnter = true;
    [SerializeField] private bool showDebugLogs = true;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private bool             playerInside;
    private RadiationManager radiationManager;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (safeAreaCollider == null)
            safeAreaCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        if (monster == null)
            monster = FindObjectOfType<Monster_Movement>();

        if (teleportManager == null)
            teleportManager = EnemyTeleportManager.Instance;
    }

    private void Update()
    {
        if (!playerInside || !reduceRadiation || radiationManager == null) return;

        float rate = radiationDecreasePerSecond > 0f
                     ? radiationDecreasePerSecond
                     : radiationManager.safeZoneDecreasePerSecond;

        radiationManager.ReduceRadiation(rate * Time.deltaTime);
    }

    // ── Trigger callbacks ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = true;

        if (reduceRadiation && radiationManager == null)
            radiationManager = other.GetComponent<RadiationManager>();

        if (showDebugLogs)
            Debug.Log($"[AreaSafeZone] '{gameObject.name}' — player entered. Protection ON.");

        // Protect the player (no kill, no attack) — does NOT return monster to patrol
        if (monster != null)
            monster.SetPlayerProtected(true);

        // Optionally reset the forest teleport timer
        if (resetForestTimerOnEnter)
        {
            if (teleportManager == null) teleportManager = EnemyTeleportManager.Instance;
            if (teleportManager != null) teleportManager.ResetForestTimer();
        }

        // Tell the monster to roam around this area
        StartMonsterAreaInvestigation();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;

        if (showDebugLogs)
            Debug.Log($"[AreaSafeZone] '{gameObject.name}' — player exited. Protection OFF. " +
                      "Monster may still be roaming.");

        // Remove protection — monster can detect and chase the player again
        if (monster != null)
            monster.SetPlayerProtected(false);

        // Note: we do NOT stop area investigation here.
        // The monster keeps roaming until its own timer expires, which creates
        // the situation where the player must pick the right moment to leave.
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starts (or restarts) the monster's area roaming using this zone's settings.
    /// Can also be called externally — e.g. by EnemyTeleportTriggerZone when the monster
    /// has just teleported into this area and the post-teleport behavior is Investigate.
    /// </summary>
    public void StartMonsterAreaInvestigation()
    {
        if (monster == null)
        {
            Debug.LogWarning($"[AreaSafeZone] '{gameObject.name}': Monster_Movement not assigned or found.");
            return;
        }

        Vector3 center = roamCenter != null ? roamCenter.position : transform.position;

        monster.StartAreaInvestigation(
            center,
            roamRadius,
            safeAreaCollider,
            monsterStayTime,
            randomMoveInterval,
            navMeshSampleRadius);

        if (showDebugLogs)
            Debug.Log($"[AreaSafeZone] '{gameObject.name}' — monster roaming started. " +
                      $"Center={center:F1}  Radius={roamRadius}  Duration={monsterStayTime}s");
    }

    // ── Editor gizmo ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 center = roamCenter != null ? roamCenter.position : transform.position;

        // Roam boundary
        Gizmos.color = new Color(1f, 0.65f, 0f, 0.18f);
        Gizmos.DrawSphere(center, roamRadius);

        Gizmos.color = new Color(1f, 0.65f, 0f, 0.85f);
        DrawWireCircle(center, roamRadius, 40);

        // Inner "forbidden" area label — just show a line to the safe collider centre
        if (safeAreaCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
            Gizmos.DrawLine(center, safeAreaCollider.bounds.center);
        }
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float step  = 360f / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float   a    = i * step * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
