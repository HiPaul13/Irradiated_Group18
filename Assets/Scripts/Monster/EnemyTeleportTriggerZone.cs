using UnityEngine;

/// <summary>
/// Place a trigger collider on a key area (castle entrance, lake, dumpyard, etc.).
///
/// When the player enters, EnemyTeleportManager spawns the enemy BEHIND the player
/// using its behind-player spawn algorithm. No teleport points need to be placed by hand.
///
/// Inspector setup:
///   1. Add a Box or Sphere Collider → Is Trigger = true.
///   2. Set zoneName for readable debug logs.
///   3. Choose PostTeleportBehavior (Investigate is best for most zones).
///   4. Tune investigateForwardDistance so the monster walks toward the area interior.
///   5. Optionally assign investigatePointOverride to pin the investigate destination.
///   6. Enable onlyFireOnce to prevent re-triggering when the player exits and re-enters.
/// </summary>
public class EnemyTeleportTriggerZone : MonoBehaviour
{
    [Header("Zone Identity")]
    [Tooltip("Readable name used in debug logs")]
    public string zoneName = "Zone";

    [Header("Post-Teleport Behaviour")]
    [Tooltip("What the monster does immediately after spawning behind the player")]
    public Monster_Movement.PostTeleportBehavior PostTeleportBehavior
        = Monster_Movement.PostTeleportBehavior.Investigate;

    [Header("Investigate Direction")]
    [Tooltip("How far ahead of the player's entry position the investigate target is placed. " +
             "Positive = further into the area the player just entered.")]
    public float investigateForwardDistance = 8f;

    [Tooltip("Optional fixed investigate point. Overrides investigateForwardDistance if assigned.")]
    public Transform investigatePointOverride;

    [Header("Linked Area Safe Zone (optional)")]
    [Tooltip("If assigned and PostTeleportBehavior is Investigate, the monster will start " +
             "area-roaming around this zone's settings immediately after teleporting. " +
             "Use this for Castle / House / Barn zones.")]
    public AreaSafeZone linkedAreaSafeZone;

    [Header("Settings")]
    [Tooltip("Only fire on the first entry during this session. " +
             "Disable for repeated teleports each time the player enters.")]
    public bool onlyFireOnce = true;

    // ── Runtime data — set when the player enters ────────────────────────────

    /// <summary>Player world position at the moment of trigger entry.</summary>
    public Vector3 EntryPlayerPosition { get; private set; }

    /// <summary>Collider on this GameObject — passed to EnemyTeleportManager for zone exclusion.</summary>
    public Collider ZoneCollider { get; private set; }

    private bool hasFired = false;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        ZoneCollider = GetComponent<Collider>();

        if (ZoneCollider == null)
            Debug.LogWarning($"[TriggerZone] '{zoneName}' has no Collider on this GameObject.");
    }

    // ── Trigger callbacks ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (onlyFireOnce && hasFired)    return;

        hasFired = true;

        // Snapshot the player's position at the exact moment of entry.
        EntryPlayerPosition = other.transform.position;

        Debug.Log($"[TriggerZone] '{zoneName}' entered. Player at {EntryPlayerPosition:F1}");

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.OnPlayerEnteredTriggerZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.OnPlayerExitedTriggerZone(this);
    }

    // ── Public helpers ───────────────────────────────────────────────────────

    /// <summary>Called by SaveGameManager after loading so the zone can fire again.</summary>
    public void ResetFired()
    {
        hasFired = false;
    }
}
