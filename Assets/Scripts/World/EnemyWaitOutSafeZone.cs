using UnityEngine;

/// <summary>
/// Castle wait-out zone — a trigger the player can hide in to make the monster disengage.
///
/// Gameplay loop:
///   1. Player enters the castle area → monster teleports behind them and investigates.
///   2. Player runs up to the castle wait-out zone (e.g., rooftop, inner room).
///   3. Player stays there for requiredStayTime seconds.
///   4. Monster is forced back to patrol; forest teleport timer is reset.
///   5. Player can see the monster walk away, then safely go collect the mushroom.
///
/// IMPORTANT: This zone does NOT set playerInSafeArea (unlike the cabin SafeZone).
/// The monster can still see and chase the player in this zone — it just eventually
/// gives up if the player waits long enough. Keep requiredStayTime high enough
/// to be a challenge.
///
/// Inspector setup:
///   1. Add a Box/Sphere Collider → Is Trigger = true.
///   2. Place the GameObject inside the castle (rooftop or upper room works well).
///   3. Set requiredStayTime (15–25 s is a good starting value).
///   4. Leave showDebugLogs = true during development.
/// </summary>
public class EnemyWaitOutSafeZone : MonoBehaviour
{
    [Header("Wait-Out Settings")]
    [Tooltip("How many seconds the player must stay inside before the monster disengages")]
    public float requiredStayTime = 20f;

    [Header("Debug")]
    [Tooltip("Print stay-timer progress to the Console during play")]
    public bool  showDebugLogs     = true;
    [Tooltip("How often (seconds) to print the stay-timer progress")]
    public float debugLogInterval  = 5f;

    // ── Runtime ──────────────────────────────────────────────────────────────

    private bool  playerInside    = false;
    private float stayTimer       = 0f;
    private bool  hasTriggered    = false;
    private float nextDebugLogTime = 0f;

    // ── Trigger callbacks ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside      = true;
        stayTimer         = 0f;
        hasTriggered      = false;
        nextDebugLogTime  = Time.time + debugLogInterval;

        if (showDebugLogs)
            Debug.Log($"[WaitOutZone] Player entered. Stay {requiredStayTime}s for the monster to disengage.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        stayTimer    = 0f;

        if (showDebugLogs && !hasTriggered)
            Debug.Log("[WaitOutZone] Player left before timer completed. Timer reset.");
    }

    // ── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!playerInside || hasTriggered) return;

        stayTimer += Time.deltaTime;

        // Periodic progress log
        if (showDebugLogs && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + debugLogInterval;
            Debug.Log($"[WaitOutZone] Stay timer: {stayTimer:F1} / {requiredStayTime}s");
        }

        if (stayTimer >= requiredStayTime)
        {
            hasTriggered = true;
            DisengageMonster();
        }
    }

    // ── Core action ──────────────────────────────────────────────────────────

    private void DisengageMonster()
    {
        Monster_Movement monster = FindObjectOfType<Monster_Movement>();

        if (monster != null)
            monster.ForceReturnToPatrol();
        else
            Debug.LogWarning("[WaitOutZone] No Monster_Movement found in scene.");

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.ResetForestTimer();

        if (showDebugLogs)
            Debug.Log($"[WaitOutZone] {requiredStayTime}s elapsed. Monster forced to patrol and forest timer reset.");
    }

    // ── Public helpers ───────────────────────────────────────────────────────

    /// <summary>Call this after loading a save so the zone can trigger again.</summary>
    public void ResetZone()
    {
        stayTimer    = 0f;
        hasTriggered = false;
    }
}
