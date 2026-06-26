using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles two teleport conditions:
///
///   1. Trigger-zone teleportation
///      EnemyTeleportTriggerZone calls OnPlayerEnteredTriggerZone().
///      Two spawn strategies are chosen per zone (see EnemyTeleportTriggerZone.useSafeAreaLineSpawn):
///        A) Safe-area line spawn — spawn direction = safe-area centre → player → beyond.
///           Works correctly even if the player entered the zone backwards.
///        B) Behind-camera spawn — original behaviour; best for lake/dumpyard/forest zones.
///
///   2. Forest-timer teleportation
///      If the player roams freely for too long, the monster spawns behind them.
///      Uses behind-camera spawn with FOV protection. Timer is frozen inside safe zones.
///
/// Inspector setup — REQUIRED:
///   • monster            — the Monster GameObject (or auto-found)
///   • difficultyController — on the Monster GameObject
///   • player             — the Player root (or auto-found by tag)
///   • playerCamera       — Camera child of the Player (for FOV direction)
///   • forbiddenZones     — drag the CABIN SafeZone collider here
///
/// Optional:
///   • Tune minSpawnDistance, maxSpawnDistance, spawnSideSpread, navMeshSampleRadius, maxSpawnAttempts.
///   • Each EnemyTeleportTriggerZone controls its own safe-area min/max/spread independently.
/// </summary>
public class EnemyTeleportManager : MonoBehaviour
{
    public static EnemyTeleportManager Instance { get; private set; }

    // ── Inspector references ─────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private Monster_Movement          monster;
    [SerializeField] private EnemyDifficultyController difficultyController;
    [SerializeField] private Transform                 player;
    [Tooltip("Camera child of the Player — used for FOV direction and behind-camera spawns.")]
    [SerializeField] private Transform                 playerCamera;

    [Header("Behind-Camera Spawn (forest timer + non-safe-area zones)")]
    [Tooltip("Minimum distance behind the player for the spawn point")]
    [SerializeField] private float minSpawnDistance    = 10f;
    [Tooltip("Maximum distance behind the player for the spawn point")]
    [SerializeField] private float maxSpawnDistance    = 20f;
    [Tooltip("Random side offset applied to each attempt (metres left/right)")]
    [SerializeField] private float spawnSideSpread     = 5f;
    [Tooltip("NavMesh.SamplePosition search radius around the candidate point")]
    [SerializeField] private float navMeshSampleRadius = 5f;
    [Tooltip("How many positions to try before giving up")]
    [SerializeField] private int   maxSpawnAttempts    = 12;

    [Header("FOV Protection (behind-camera spawn only)")]
    [Tooltip("Prevent the monster from spawning in the player's camera FOV during forest-timer teleports.\n" +
             "Safe-area zones override this per-zone via ignoreFOVForThisZone.")]
    [SerializeField] private bool  avoidPlayerFOV     = true;
    [Tooltip("Half-angle of the protected FOV cone in degrees (e.g. 80 = 80° left and right of forward)")]
    [SerializeField] private float playerFOVHalfAngle = 80f;

    [Header("Forbidden Zones — always excluded from all spawns")]
    [Tooltip("Positions inside these colliders are rejected for every teleport.\n" +
             "Drag the CABIN SafeZone collider here. Do NOT add castle/house here — " +
             "those are handled per-zone via linkedSafeAreaCollider.")]
    [SerializeField] private List<Collider> forbiddenZones = new List<Collider>();

    [Header("Forest Timer Fallbacks (overridden by EnemyDifficultyController)")]
    [SerializeField] private float forestTeleportTime = 90f;
    [SerializeField] private float teleportCooldown   = 45f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private bool  playerInSafeZone       = false;
    private bool  playerInMajorZone      = false;
    private bool  forestTeleportDisabled = false;

    private float forestTimer      = 0f;
    private float lastTeleportTime = -9999f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        FindSceneReferences();

        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.OnStageChanged += OnStageChanged;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.OnStageChanged -= OnStageChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-acquire all references to scene objects that were destroyed on reload.
        FindSceneReferences();

        // Reset runtime state so respawn starts fresh.
        playerInSafeZone  = false;
        playerInMajorZone = false;
        forestTimer       = 0f;
        lastTeleportTime  = -9999f;

        // GameProgressManager persists across scene loads — re-check the disable flag.
        if (GameProgressManager.Instance != null)
            forestTeleportDisabled = GameProgressManager.Instance.CurrentStage >= GameProgressStage.CollectingCarParts;
    }

    private void FindSceneReferences()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Camera cam = playerObj.GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
        }

        if (monster == null || !monster)
            monster = FindObjectOfType<Monster_Movement>();

        if (difficultyController == null || !difficultyController)
            difficultyController = FindObjectOfType<EnemyDifficultyController>();
    }

    private void Update()
    {
        TickForestTimer();
    }

    // ── Forest timer ─────────────────────────────────────────────────────────

    private void TickForestTimer()
    {
        if (player == null || !player) return;
        if (forestTeleportDisabled) return;

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
            Debug.Log($"[Teleport] Forest timer expired ({forestTimer:F1}s >= {threshold}s). Attempting teleport.");
            TryForestTeleport();
        }
    }

    private void TryForestTeleport()
    {
        if (player == null || monster == null) return;

        // Forest timer always uses behind-camera spawn with FOV protection.
        if (!TrySpawnBehindPlayer(out Vector3 spawnPos, excludeZoneCollider: null, ignoreFOV: false))
        {
            forestTimer = 0f;
            Debug.LogWarning("[Teleport] Forest teleport: no valid behind-player position found. Timer reset.");
            return;
        }

        // Pursue toward the player's current position — they're in the forest, no safe area
        bool ok = monster.TeleportAndPursue(spawnPos, player.position);
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

        float cooldown  = GetCurrentCooldown();
        float remaining = cooldown - (Time.time - lastTeleportTime);

        if (Time.time < lastTeleportTime + cooldown)
        {
            Debug.Log($"[Teleport] Zone '{zone.zoneName}' entered but on cooldown " +
                      $"({remaining:F1}s remaining). Skipping.");
            return;
        }

        // ── Choose spawn strategy ────────────────────────────────────────────
        bool foundSpawn;
        Vector3 spawnPos = Vector3.zero;

        if (zone.useSafeAreaLineSpawn && zone.linkedSafeAreaCollider != null)
        {
            // Strategy A: safe-area line spawn
            foundSpawn = TrySpawnOnSafeAreaLine(zone, out spawnPos);

            if (!foundSpawn)
            {
                // Fallback: behind-camera spawn, but respect ignoreFOVForThisZone
                Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': safe-area line spawn failed. " +
                                 "Falling back to behind-camera spawn.");
                foundSpawn = TrySpawnBehindPlayer(out spawnPos,
                                                  zone.ZoneCollider,
                                                  ignoreFOV: zone.ignoreFOVForThisZone);
            }
        }
        else
        {
            // Strategy B: behind-camera spawn (lake, dumpyard, forest zones)
            foundSpawn = TrySpawnBehindPlayer(out spawnPos,
                                              zone.ZoneCollider,
                                              ignoreFOV: zone.ignoreFOVForThisZone);
        }

        if (!foundSpawn)
        {
            Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': all spawn strategies failed. " +
                             "No teleport this entry.");
            return;
        }

        // ── Warp monster and apply post-teleport behaviour ───────────────────
        bool ok;

        switch (zone.PostTeleportBehavior)
        {
            case Monster_Movement.PostTeleportBehavior.PursueEntryDirection:
                // Monster pursues the direction the player was moving into the zone.
                // EntryChaseTarget was calculated from movement velocity/delta — not camera.
                ok = monster.TeleportAndPursue(spawnPos, zone.EntryChaseTarget);
                break;

            case Monster_Movement.PostTeleportBehavior.Chase:
                ok = monster.TeleportTo(spawnPos, Monster_Movement.PostTeleportBehavior.Chase);
                break;

            case Monster_Movement.PostTeleportBehavior.ReturnToPatrol:
                ok = monster.TeleportTo(spawnPos, Monster_Movement.PostTeleportBehavior.ReturnToPatrol);
                break;

            default:
                ok = monster.TeleportAndPursue(spawnPos, zone.EntryChaseTarget);
                break;
        }

        if (ok)
        {
            lastTeleportTime = Time.time;
            Debug.Log($"[Teleport] Zone '{zone.zoneName}' teleport succeeded. " +
                      $"Spawn: {spawnPos:F1}  ChaseTarget: {zone.EntryChaseTarget:F1}");
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
        Debug.Log($"[Teleport] Player in cabin safe zone: {inSafe}. Forest timer reset.");
    }

    /// <summary>Called by SaveGameManager after loading so the timer starts clean.</summary>
    public void ResetForestTimer()
    {
        forestTimer = 0f;
        Debug.Log("[Teleport] Forest timer reset.");
    }

    // ── Spawn strategy A: safe-area line spawn ────────────────────────────────

    /// <summary>
    /// Computes the spawn direction as: safe-area-centre → player → beyond.
    ///
    /// This guarantees that regardless of which way the player was facing when
    /// they entered the zone, the monster always appears on the far side of the
    /// player from the safe area.
    ///
    /// Does NOT use FOV rejection — the spatial rule matters more than camera direction.
    /// </summary>
    private bool TrySpawnOnSafeAreaLine(EnemyTeleportTriggerZone zone, out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;

        // 1. Resolve safe area centre ─────────────────────────────────────────
        Vector3 safeCenter;

        if (zone.safeAreaCenterOverride != null)
        {
            safeCenter = zone.safeAreaCenterOverride.position;
            Debug.Log($"[Teleport] Safe-area centre from override: {safeCenter:F1}");
        }
        else if (zone.linkedSafeAreaCollider != null)
        {
            safeCenter = zone.linkedSafeAreaCollider.bounds.center;
            Debug.Log($"[Teleport] Safe-area centre from collider bounds: {safeCenter:F1}");
        }
        else
        {
            Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': TrySpawnOnSafeAreaLine called " +
                             "but no safe area centre available.");
            return false;
        }

        // 2. Direction: from safe centre outward through the player ────────────
        Vector3 awayDir = player.position - safeCenter;
        awayDir.y = 0f;

        if (awayDir.sqrMagnitude < 0.01f)
        {
            Debug.LogWarning($"[Teleport] Zone '{zone.zoneName}': player is at safe-area centre — " +
                             "cannot compute direction. Falling back.");
            return false;
        }

        awayDir.Normalize();

        // Perpendicular horizontal right of awayDir
        Vector3 rightDir = new Vector3(awayDir.z, 0f, -awayDir.x);

        Debug.Log($"[Teleport] Using safe-area line spawn for zone '{zone.zoneName}'. " +
                  $"SafeCenter: {safeCenter:F1}  Player: {player.position:F1}  " +
                  $"AwayDir: {awayDir:F2}");

        // 3. Try spawn candidates ──────────────────────────────────────────────
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float distance = Random.Range(zone.minSafeAreaSpawnDistance, zone.maxSafeAreaSpawnDistance);
            float side     = Random.Range(-zone.safeAreaSideSpread, zone.safeAreaSideSpread);

            Vector3 candidate = player.position
                              + awayDir  * distance
                              + rightDir * side;
            candidate.y = player.position.y;

            // NavMesh sample ──────────────────────────────────────────────────
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Debug.Log($"[Teleport] SA attempt {attempt + 1}: {candidate:F1} — no NavMesh.");
                continue;
            }

            Vector3 sampledPos = hit.position;

            // Must NOT be inside the linked safe area ─────────────────────────
            if (IsInsideCollider(zone.linkedSafeAreaCollider, sampledPos))
            {
                Debug.Log($"[Teleport] SA attempt {attempt + 1}: {sampledPos:F1} rejected — " +
                          "inside linked safe area.");
                continue;
            }

            // Must NOT be inside any global forbidden zone (cabin).
            // linkedSafeAreaCollider is excluded here — it is already checked above,
            // and users sometimes accidentally add it to forbiddenZones as well.
            if (IsInsideAnyForbiddenZone(sampledPos, exclude: zone.linkedSafeAreaCollider))
            {
                Debug.Log($"[Teleport] SA attempt {attempt + 1}: {sampledPos:F1} rejected — " +
                          "inside forbidden zone (see previous log for which one).");
                continue;
            }

            // Must NOT be inside this trigger zone itself ──────────────────────
            if (zone.ZoneCollider != null && IsInsideCollider(zone.ZoneCollider, sampledPos))
            {
                Debug.Log($"[Teleport] SA attempt {attempt + 1}: {sampledPos:F1} rejected — " +
                          "inside trigger zone.");
                continue;
            }

            // Dot product: player must be between safe area and monster ────────
            // safeToPlayer and playerToSpawn should point in the same general direction.
            Vector3 safeToPlayer  = player.position - safeCenter;
            safeToPlayer.y = 0f;

            Vector3 playerToSpawn = sampledPos - player.position;
            playerToSpawn.y = 0f;

            if (safeToPlayer.sqrMagnitude > 0.01f && playerToSpawn.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(safeToPlayer.normalized, playerToSpawn.normalized);

                if (dot < 0.3f) // lenient: allows ~72° spread from the away direction
                {
                    Debug.Log($"[Teleport] SA attempt {attempt + 1}: {sampledPos:F1} rejected — " +
                              $"player not between safe area and monster (dot={dot:F2}, need ≥ 0.30).");
                    continue;
                }
            }

            // All checks passed ────────────────────────────────────────────────
            spawnPos = sampledPos;
            float dist = Vector3.Distance(sampledPos, player.position);
            Debug.Log($"[Teleport] Safe-area line spawn succeeded on attempt {attempt + 1}. " +
                      $"Spawn: {sampledPos:F1}  (dist from player: {dist:F1}m)");
            return true;
        }

        Debug.LogWarning($"[Teleport] TrySpawnOnSafeAreaLine: all {maxSpawnAttempts} attempts failed " +
                         $"for zone '{zone.zoneName}'.");
        return false;
    }

    // ── Spawn strategy B: behind-camera spawn ────────────────────────────────

    /// <summary>
    /// Tries up to maxSpawnAttempts times to find a valid spawn position that is:
    ///   • behind the player's camera direction
    ///   • not inside the player's FOV (unless ignoreFOV is true)
    ///   • on valid NavMesh
    ///   • not inside any forbidden zone
    ///   • not inside excludeZoneCollider
    ///
    /// ignoreFOV should be true for safe-area zones where the player may have entered backwards.
    /// </summary>
    private bool TrySpawnBehindPlayer(out Vector3 spawnPos, Collider excludeZoneCollider,
                                      bool ignoreFOV = false)
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

            Vector3 candidate = player.position
                              + behind * distance
                              + right  * side;
            candidate.y = player.position.y;

            // FOV check — skipped when ignoreFOV is true ──────────────────────
            if (!ignoreFOV && avoidPlayerFOV && IsInPlayerFOV(candidate))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {candidate:F1} rejected — in player FOV.");
                continue;
            }

            // NavMesh sample ──────────────────────────────────────────────────
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {candidate:F1} rejected — " +
                          $"no NavMesh within {navMeshSampleRadius}m.");
                continue;
            }

            Vector3 sampledPos = hit.position;

            // Forbidden zone check (cabin) ────────────────────────────────────
            if (IsInsideAnyForbiddenZone(sampledPos))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {sampledPos:F1} rejected — inside forbidden zone.");
                continue;
            }

            // Trigger zone self-exclusion ──────────────────────────────────────
            if (excludeZoneCollider != null && IsInsideCollider(excludeZoneCollider, sampledPos))
            {
                Debug.Log($"[Teleport] Attempt {attempt + 1}: {sampledPos:F1} rejected — inside trigger zone.");
                continue;
            }

            // All checks passed ────────────────────────────────────────────────
            spawnPos = sampledPos;
            float dist = Vector3.Distance(sampledPos, player.position);
            Debug.Log($"[Teleport] Behind-camera spawn found on attempt {attempt + 1} at {sampledPos:F1} " +
                      $"(dist from player: {dist:F1}m  FOV ignored: {ignoreFOV})");
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

        if (dir.sqrMagnitude < 0.01f) return true;

        return Vector3.Angle(GetPlayerForward(), dir.normalized) < playerFOVHalfAngle;
    }

    /// <summary>
    /// Returns true if point is inside any forbidden zone.
    /// Pass 'exclude' to skip one collider (e.g. linkedSafeAreaCollider in safe-area line mode,
    /// so adding it to forbiddenZones by mistake doesn't break the spawn).
    /// </summary>
    private bool IsInsideAnyForbiddenZone(Vector3 point, Collider exclude = null)
    {
        foreach (Collider zone in forbiddenZones)
        {
            if (zone == exclude) continue;
            if (IsInsideCollider(zone, point))
            {
                Debug.Log($"[Teleport] Forbidden zone hit: '{zone.gameObject.name}' rejected {point:F1}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when 'point' is inside 'col'.
    /// Uses ClosestPoint: if the result equals the original point, the point is inside.
    /// Works with Box, Sphere, Capsule, and convex Mesh colliders.
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
        forestTeleportDisabled = stage >= GameProgressStage.CollectingCarParts;
        Debug.Log($"[Teleport] Stage → {stage}. Forest teleport disabled: {forestTeleportDisabled}.");
    }
}
