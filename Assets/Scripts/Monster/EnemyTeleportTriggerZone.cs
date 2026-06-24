using UnityEngine;

/// <summary>
/// Place a trigger collider on any key area (castle entrance, lake, dumpyard, barn, etc.).
///
/// On trigger entry the zone records the direction the player was MOVING INTO the zone,
/// then tells EnemyTeleportManager to spawn the monster and pursue along that direction.
/// This works correctly even if the player walked backwards into the zone.
///
/// ── Entry direction priority ────────────────────────────────────────────────
///   1. entryDirectionOverride.forward        (if assigned)
///   2. Player Rigidbody.velocity             (if Rigidbody present and moving)
///   3. Player position delta (prev → now)    (most reliable for CharacterController)
///   4. Zone centre → player position         (geometric fallback)
///   5. Player transform.forward              (last resort — NOT camera forward)
///
/// ── Spawn mode ──────────────────────────────────────────────────────────────
///   A) Safe-area line spawn  (useSafeAreaLineSpawn = true)
///      Spawn direction = safe-area centre → player → beyond.
///      Correct even when the player entered backwards. Use for castle/house/barn.
///
///   B) Behind-camera spawn  (useSafeAreaLineSpawn = false)
///      Standard spawn behind the player's camera. Use for forest/lake/dumpyard.
///
/// ── Inspector setup ─────────────────────────────────────────────────────────
///   1. Add a Box or Sphere Collider → Is Trigger = true.
///   2. Set zoneName for readable logs.
///   3. Tune chaseIntoZoneDistance (how far the monster pursues in the entry direction).
///   4. For safe-area zones: enable useSafeAreaLineSpawn and assign linkedSafeAreaCollider.
///   5. Enable onlyFireOnce to prevent re-triggering on re-entry (recommended).
/// </summary>
public class EnemyTeleportTriggerZone : MonoBehaviour
{
    // ── Zone identity ─────────────────────────────────────────────────────────

    [Header("Zone Identity")]
    [Tooltip("Readable name shown in all debug logs for this zone.")]
    public string zoneName = "Zone";

    // ── Post-teleport pursuit ─────────────────────────────────────────────────

    [Header("Post-Teleport Pursuit")]
    [Tooltip("How far beyond the player's entry position the monster pursues (metres).\n" +
             "Larger = monster moves deeper into the zone before stopping.")]
    public float chaseIntoZoneDistance = 18f;

    [Tooltip("Minimum horizontal speed (m/s) the player must have for Rigidbody velocity\n" +
             "to be used as the entry direction. Below this threshold the zone falls back\n" +
             "to the position-delta method.")]
    public float minimumEntryVelocity = 0.2f;

    [Tooltip("Optional fixed direction source. If assigned, its forward vector is used as\n" +
             "the entry direction regardless of how the player entered.\n" +
             "Leave blank to use the automatic movement-based direction.")]
    public Transform entryDirectionOverride;

    // ── Post-teleport behaviour ───────────────────────────────────────────────

    [Header("Post-Teleport Behaviour")]
    [Tooltip("PursueEntryDirection — monster moves toward EntryChaseTarget (recommended).\n" +
             "Chase — monster immediately chases the player.\n" +
             "ReturnToPatrol — monster warps and goes back to patrol.")]
    public Monster_Movement.PostTeleportBehavior PostTeleportBehavior
        = Monster_Movement.PostTeleportBehavior.PursueEntryDirection;

    // ── Safe-area line spawn ──────────────────────────────────────────────────

    [Header("Safe-Area Spawn (castle / house / barn)")]
    [Tooltip("Enable to use safe-area-line spawn instead of behind-camera spawn.\n\n" +
             "Spawn direction = safe-area centre → player → beyond.\n" +
             "Works correctly even when the player enters the zone facing away.\n" +
             "Recommended for all zones linked to a safe area (castle, house, barn).")]
    public bool useSafeAreaLineSpawn = false;

    [Tooltip("Collider of the linked safe area building.\n" +
             "Monster is rejected from spawning inside it.\n" +
             "Also defines the spawn direction. Required when useSafeAreaLineSpawn is true.")]
    public Collider linkedSafeAreaCollider;

    [Tooltip("Optional override for the safe-area centre point used in spawn direction.\n" +
             "If empty, linkedSafeAreaCollider.bounds.center is used.\n" +
             "Place a Transform near the main entrance for better accuracy.")]
    public Transform safeAreaCenterOverride;

    [Tooltip("Minimum spawn distance from the player in safe-area-line mode.")]
    public float minSafeAreaSpawnDistance = 12f;

    [Tooltip("Maximum spawn distance from the player in safe-area-line mode.")]
    public float maxSafeAreaSpawnDistance = 24f;

    [Tooltip("Random side spread around the spawn direction in safe-area-line mode.")]
    public float safeAreaSideSpread = 4f;

    [Tooltip("When true, FOV rejection is disabled for this zone.\n" +
             "Recommended for safe-area zones where the player may have entered backwards.")]
    public bool ignoreFOVForThisZone = true;

    // ── General settings ──────────────────────────────────────────────────────

    [Header("Settings")]
    [Tooltip("Only fire once per session. Disable for repeated teleports on every entry.")]
    public bool onlyFireOnce = true;

    // ── Runtime properties (read by EnemyTeleportManager) ────────────────────

    /// <summary>Player world position at the moment of trigger entry.</summary>
    public Vector3 EntryPlayerPosition { get; private set; }

    /// <summary>
    /// Horizontal direction the player was moving when they entered.
    /// Computed from movement velocity / delta — NOT camera forward.
    /// </summary>
    public Vector3 EntryMoveDirection { get; private set; }

    /// <summary>
    /// EntryPlayerPosition + EntryMoveDirection * chaseIntoZoneDistance.
    /// The monster pursues toward this point after teleporting.
    /// </summary>
    public Vector3 EntryChaseTarget { get; private set; }

    /// <summary>Collider on this GameObject — passed to EnemyTeleportManager for zone self-exclusion.</summary>
    public Collider ZoneCollider { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────────

    private bool      hasFired = false;
    private Transform playerTransform;
    private Vector3   prevPlayerPosition;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        ZoneCollider = GetComponent<Collider>();

        if (ZoneCollider == null)
            Debug.LogWarning($"[TriggerZone] '{zoneName}': no Collider on this GameObject.");

        if (useSafeAreaLineSpawn && linkedSafeAreaCollider == null)
            Debug.LogWarning($"[TriggerZone] '{zoneName}': useSafeAreaLineSpawn is enabled " +
                             "but linkedSafeAreaCollider is not assigned.");
    }

    private void Start()
    {
        // Cache the player transform so Update can track position each frame
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform    = playerObj.transform;
            prevPlayerPosition = playerTransform.position;
        }
        else
        {
            Debug.LogWarning($"[TriggerZone] '{zoneName}': no GameObject with tag 'Player' found. " +
                             "Entry-direction tracking will use fallback methods.");
        }
    }

    private void Update()
    {
        // Track player position every frame so we have a reliable one-frame-ago delta on entry.
        if (playerTransform != null)
            prevPlayerPosition = playerTransform.position;
    }

    // ── Trigger callbacks ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (onlyFireOnce && hasFired)    return;

        hasFired = true;

        // Snapshot position, direction and pursue target
        EntryPlayerPosition = other.transform.position;
        EntryMoveDirection  = ComputeEntryDirection(other);
        EntryChaseTarget    = EntryPlayerPosition + EntryMoveDirection * chaseIntoZoneDistance;

        Debug.Log($"[TriggerZone] '{zoneName}' entered. " +
                  $"EntryPos={EntryPlayerPosition:F1}  " +
                  $"EntryDir={EntryMoveDirection:F2}  " +
                  $"ChaseTarget={EntryChaseTarget:F1}");

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.OnPlayerEnteredTriggerZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.OnPlayerExitedTriggerZone(this);
    }

    // ── Entry direction computation ──────────────────────────────────────────

    /// <summary>
    /// Determines the horizontal direction the player was moving when they entered the zone.
    /// Uses a priority chain — never uses camera forward.
    /// </summary>
    private Vector3 ComputeEntryDirection(Collider playerCollider)
    {
        Transform t = playerCollider.transform;

        // Priority 1: explicit direction override
        if (entryDirectionOverride != null)
        {
            Vector3 overrideDir = entryDirectionOverride.forward;
            overrideDir.y = 0f;
            if (overrideDir.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[TriggerZone] '{zoneName}': using entryDirectionOverride.");
                return overrideDir.normalized;
            }
        }

        // Priority 2: Rigidbody velocity (works for physics-based players)
        // Note: Unity 6+ uses rb.linearVelocity instead of rb.velocity
        Rigidbody rb = playerCollider.attachedRigidbody ?? playerCollider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 vel = rb.linearVelocity;  // change to rb.linearVelocity if on Unity 6+
            vel.y = 0f;
            if (vel.magnitude >= minimumEntryVelocity)
            {
                Debug.Log($"[TriggerZone] '{zoneName}': using Rigidbody velocity {vel:F2}.");
                return vel.normalized;
            }
        }

        // Priority 3: one-frame position delta (reliable for CharacterController players)
        Vector3 delta = t.position - prevPlayerPosition;
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.0001f)
        {
            Debug.Log($"[TriggerZone] '{zoneName}': using position delta {delta:F2}.");
            return delta.normalized;
        }

        // Priority 4: geometric direction — zone centre → player
        Vector3 fromCenter = t.position - transform.position;
        fromCenter.y = 0f;
        if (fromCenter.sqrMagnitude > 0.01f)
        {
            Debug.Log($"[TriggerZone] '{zoneName}': using zone-centre → player direction (fallback).");
            return fromCenter.normalized;
        }

        // Priority 5: player transform.forward — NOT camera forward
        Vector3 playerFwd = t.forward;
        playerFwd.y = 0f;
        Debug.Log($"[TriggerZone] '{zoneName}': using player transform.forward (last resort).");
        return playerFwd.sqrMagnitude > 0.01f ? playerFwd.normalized : Vector3.forward;
    }

    // ── Public helpers ───────────────────────────────────────────────────────

    /// <summary>Called by SaveGameManager after loading so the zone can fire again.</summary>
    public void ResetFired()
    {
        hasFired = false;
    }
}
