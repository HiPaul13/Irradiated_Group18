using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles two teleport conditions:
///
///   1. Trigger-zone teleportation
///      EnemyTeleportTriggerZone calls OnPlayerEnteredTriggerZone().
///      The monster spawns BEHIND the player at that moment, outside the zone.
///
///   2. Forest-timer teleportation
///      If the player roams freely for too long, the monster spawns behind them.
///      Timer is frozen inside safe zones and major trigger zones.
///
/// Inspector setup — REQUIRED:
///   • monster            — the Monster GameObject (or auto-found)
///   • difficultyController — on the Monster GameObject
///   • player             — the Player root (or auto-found by tag)
///   • playerCamera       — the Camera child of the Player (for FOV direction)
///   • forbiddenZones     — drag the cabin SafeZone collider here
///
/// Inspector setup — optional but recommended:
///   • Tune spawn distance, side spread, FOV angle, and max attempts.
/// </summary>
public class EnemyTeleportManager : MonoBehaviour
{
    public static EnemyTeleportManager Instance { get; private set; }

    // ── Inspector references ─────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Monster_Movement          monster;
    [SerializeField] private EnemyDifficultyController difficultyController;
    [SerializeField] private Transform                 player;
    [Tooltip("Camera child of the Player — used for FOV direction. Leave empty to use Player.forward.")]
    [SerializeField] private Transform                 playerCamera;

    [Header("Behind-Player Spawn Settings")]
    [Tooltip("Minimum distance behind the player for the spawn point")]
    [SerializeField] private float minSpawnDistance = 10f;
    [Tooltip("Maximum distance behind the player for the spawn point")]
    [SerializeField] private float maxSpawnDistance = 20f;
    [Tooltip("Random side offset applied to each attempt (meters left/right)")]
    [SerializeField] private float spawnSideSpread  = 5f;
    [Tooltip("NavMesh.SamplePosition search radius around the candidate point")]
    [SerializeField] private float navMeshSampleRadius = 5f;
    [Tooltip("How many positions to try before giving up")]
    [SerializeField] private int   maxSpawnAttempts = 12;

    [Header("FOV Protection")]
    [Tooltip("Prevent the monster from spawning in the player's current field of view")]
    [SerializeField] private bool  avoidPlayerFOV    = true;
    [Tooltip("Half-angle of the FOV cone to protect (90° means nothing within 90° of forward)")]
    [SerializeField] private float playerFOVHalfAngle = 80f;

    [Header("Forbidden Zones — drag colliders here")]
    [Tooltip("Positions inside these colliders are always rejected. " +
             "Add the cabin SafeZone collider here.")]
    [SerializeField] private List<Collider> forbiddenZones = new List<Collider>();

    [Header("Forest Timer Fallbacks (overridden by EnemyDifficultyController)")]
    [SerializeField] private float forestTeleportTime = 90f;
    [SerializeField] private float teleportCooldown   = 45f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private bool  playerInSafeZone  = false;
    private bool  playerInMajorZone = false;

    private float forestTimer      = 0f;
    private float lastTeleportTime = -9999f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) player = obj.transform;
        }

        if (playerCamera == null && player != null)
        {
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
        }

        if (monster == null)
            monster = FindObjectOfType<Monster_Movement>();

        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.OnStageChanged += OnStageChanged;
    }

    private void OnDestroy()
    {
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.OnStageChanged -= OnStageChanged;
    }

    private void Update()
    {
        TickForestTimer();
    }

    // ── Forest timer ─────────────────────────────────────────────────────────

    private void TickForestTimer()
    {
        if (playerInSafeZone || playerInMajorZone)
        {
            forestTimer = 0f;
            return;
        }

        forestTimer += Time.deltaTime;

        float threshold = GetCurrentThreshold();
        float cooldown  = GetCurrentCooldown();

        if (forestTimer >= threshold && Time.time >= lastTeleportTime + cooldown)
        {
            Debug.Log($"[Teleport] Forest timer expired ({forestTimer:F1}s ≥ {threshold}s). Attempting teleport.");
            TryForestTeleport();
        }
    }

    private void TryForestTeleport()
    {
        if (player == null || monster == null) return;

        if (!TrySpawnBehindPlayer(out Vector3 spawnPos, excludeZoneCollider: null))
        {
            forestTimer = 0f;
            Debug.LogWarning("[Teleport] Forest teleport: no valid behind-player position found. Timer reset.");
            return;
        }

        bool ok = monster.TeleportTo(spawnPos,
                                     Monster_Movement.PostTeleportBehavior.Investigate,
                                     player.position);
        if (ok)
        {
            lastTeleportTime = Time.time;
            forestTimer      = 0f;
            Debug.Log($"[Teleport] Forest teleport succeeded → {spawnPos:F1}");
        }
        else
        {
            forestTimer = 0f;
            Debug.LogWarning("[Teleport] Forest teleport: warp failed. Timer reset.");
        }
    }

    // ── Trigger-zone API (called by EnemyTeleportTriggerZone) ────────────────

    public void OnPlayerEnteredTriggerZone(EnemyTeleportTriggerZone zone)
    {
        playerInMajorZone = true;
        forestTimer       = 0f;

        float cooldown = GetCurrentCooldown();
        float remaining = cooldown - (Time.time - lastTeleportTime);

        if (Time.time < lastTeleportTime + cooldown)
        {
            Debug.Log($"[Teleport] Zone '{zone.zoneName}' entered but on cooldown " +
                      $"({remaining:F1}s remaining). Skipping.");
            return;
        }

        if (!TrySpawnBehindPlayer(out Vector3 spawnPos, zone.ZoneCollider))
        {
            Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': no valid behind-player spawn found.");
            return;
        }

        // Calculate investigate target:
        //   If an override is set, use it.
        //   Otherwise: entry position + player-forward * distance → points into the area.
        Vector3 investigateTarget;
        if (zone.investigatePointOverride != null)
        {
            investigateTarget = zone.investigatePointOverride.position;
        }
        else
        {
            Vector3 forward = GetPlayerForward();
            investigateTarget = zone.EntryPlayerPosition + forward * zone.investigateForwardDistance;
        }

        bool ok = monster.TeleportTo(spawnPos, zone.PostTeleportBehavior, investigateTarget);

        if (ok)
        {
            lastTeleportTime = Time.time;
            Debug.Log($"[Teleport] Zone '{zone.zoneName}' teleport succeeded. " +
                      $"Spawn: {spawnPos:F1}  Investigate: {investigateTarget:F1}");

            // If this trigger zone has a linked AreaSafeZone and the post-behavior is Investigate,
            // override with area roaming so the monster circles the zone instead of walking
            // to a single fixed point.
            if (zone.linkedAreaSafeZone != null &&
                zone.PostTeleportBehavior == Monster_Movement.PostTeleportBehavior.Investigate)
            {
                zone.linkedAreaSafeZone.StartMonsterAreaInvestigation();
                Debug.Log($"[Teleport] Zone '{zone.zoneName}': starting area investigation " +
                          $"via linked AreaSafeZone '{zone.linkedAreaSafeZone.gameObject.name}'.");
            }
        }
        else
        {
            Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': warp to {spawnPos:F1} failed (NavMesh).");
        }
    }

    public void OnPlayerExitedTriggerZone(EnemyTeleportTriggerZone zone)
    {
        playerInMajorZone = false;
    }

    // ── SafeZone API (called by SafeZone) ────────────────────────────────────

    public void SetPlayerInSafeZone(bool inSafe)
    {
        playerInSafeZone = inSafe;
        if (inSafe) forestTimer = 0f;
        Debug.Log($"[Teleport] Player in safe zone: {inSafe}. Forest timer reset.");
    }

    /// <summary>Called by SaveGameManager after loading so the timer starts clean.</summary>
    public void ResetForestTimer()
    {
        forestTimer = 0f;
        Debug.Log("[Teleport] Forest timer reset.");
    }

    // ── Core: behind-player spawn algorithm ──────────────────────────────────

    /// <summary>
    /// Tries up to maxSpawnAttempts times to find a valid world position that is:
    ///   • behind the player (opposite of facing direction)
    ///   • within [minSpawnDistance, maxSpawnDistance] of the player
    ///   • not inside the player's current FOV (if avoidPlayerFOV is true)
    ///   • on a valid NavMesh surface
    ///   • not inside any forbidden zone (cabin)
    ///   • not inside excludeZoneCollider (the trigger zone just entered)
    ///
    /// Returns true and sets spawnPos on success.
    /// </summary>
    private bool TrySpawnBehindPlayer(out Vector3 spawnPos, Collider excludeZoneCollider)
    {
        spawnPos = Vector3.zero;

        if (player == null)
        {
            Debug.LogWarning("[Teleport] TrySpawnBehindPlayer: player reference is null.");
            return false;
        }

        Vector3 forward = GetPlayerForward();
        Vector3 right   = GetPlayerRight();
        Vector3 behind  = -forward;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            float side     = Random.Range(-spawnSideSpread, spawnSideSpread);

            // Candidate: behind + random side offset, at player height
            Vector3 candidate = player.position
                                + behind * distance
                                + right  * side;
            candidate.y = player.position.y;

            // ── Reject: inside player FOV ────────────────────────────────────
            if (avoidPlayerFOV && IsInPlayerFOV(candidate))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {candidate:F1} rejected — in player FOV.");
                continue;
            }

            // ── Reject: not on NavMesh ───────────────────────────────────────
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {candidate:F1} rejected — no NavMesh within {navMeshSampleRadius}m.");
                continue;
            }

            Vector3 sampledPos = hit.position;

            // ── Reject: inside a forbidden zone (cabin) ──────────────────────
            if (IsInsideAnyForbiddenZone(sampledPos))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {sampledPos:F1} rejected — inside forbidden zone.");
                continue;
            }

            // ── Reject: still inside the trigger zone the player just entered ─
            if (excludeZoneCollider != null && IsInsideCollider(excludeZoneCollider, sampledPos))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {sampledPos:F1} rejected — inside trigger zone.");
                continue;
            }

            // ── All checks passed ────────────────────────────────────────────
            spawnPos = sampledPos;
            Debug.Log($"[Teleport] Valid spawn found on attempt {attempt + 1} at {sampledPos:F1} " +
                      $"(dist from player: {Vector3.Distance(sampledPos, player.position):F1}m)");
            return true;
        }

        Debug.LogWarning($"[Teleport] TrySpawnBehindPlayer: all {maxSpawnAttempts} attempts failed.");
        return false;
    }

    // ── Direction helpers ────────────────────────────────────────────────────

    private Vector3 GetPlayerForward()
    {
        Transform reference = playerCamera != null ? playerCamera : player;

        if (reference == null) return Vector3.forward;

        Vector3 fwd = reference.forward;
        fwd.y = 0f;

        return fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
    }

    private Vector3 GetPlayerRight()
    {
        Transform reference = playerCamera != null ? playerCamera : player;

        if (reference == null) return Vector3.right;

        Vector3 r = reference.right;
        r.y = 0f;

        return r.sqrMagnitude > 0.01f ? r.normalized : Vector3.right;
    }

    // ── Validation helpers ───────────────────────────────────────────────────

    private bool IsInPlayerFOV(Vector3 worldPosition)
    {
        if (player == null) return false;

        Vector3 dir = worldPosition - player.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return true; // too close → treat as in FOV

        float angle = Vector3.Angle(GetPlayerForward(), dir.normalized);
        return angle < playerFOVHalfAngle;
    }

    private bool IsInsideAnyForbiddenZone(Vector3 point)
    {
        foreach (Collider zone in forbiddenZones)
        {
            if (IsInsideCollider(zone, point)) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true when 'point' is inside 'col'.
    /// Uses ClosestPoint: if the closest surface point equals the original point,
    /// the point is inside (or exactly on the surface).
    /// Works with Box, Sphere, Capsule, and convex Mesh Colliders.
    /// </summary>
    private bool IsInsideCollider(Collider col, Vector3 point)
    {
        if (col == null || !col.enabled) return false;

        Vector3 closest = col.ClosestPoint(point);
        return (closest - point).sqrMagnitude < 0.01f;
    }

    // ── Difficulty helpers ───────────────────────────────────────────────────

    private float GetCurrentThreshold()
    {
        if (difficultyController != null && difficultyController.CurrentSettings != null)
            return difficultyController.CurrentSettings.forestTeleportTime;
        return forestTeleportTime;
    }

    private float GetCurrentCooldown()
    {
        if (difficultyController != null && difficultyController.CurrentSettings != null)
            return difficultyController.CurrentSettings.teleportCooldown;
        return teleportCooldown;
    }

    // ── Progress listener ────────────────────────────────────────────────────

    private void OnStageChanged(GameProgressStage stage)
    {
        Debug.Log($"[Teleport] Stage → {stage}. Timer thresholds update automatically via difficulty controller.");
    }
}
