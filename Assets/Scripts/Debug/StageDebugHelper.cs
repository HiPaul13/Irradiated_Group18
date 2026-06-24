using UnityEngine;

/// <summary>
/// Drop on any GameObject to force game progress stages during testing.
///
/// KEYBOARD SHORTCUTS (play mode):
///   1 → EarlyGame
///   2 → OneIngredient
///   3 → TwoIngredients
///   4 → AllIngredients
///   5 → PotionBrewed
///   6 → CollectingCarParts
///   7 → CarRepaired
///
/// Or right-click this component in the Inspector → "Set Stage: …"
/// </summary>
public class StageDebugHelper : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Hold this modifier key while pressing 1-7 to change stage. None = no modifier needed.")]
    public KeyCode modifier = KeyCode.None;

    [Tooltip("Show on-screen stage display while in play mode.")]
    public bool showGUI = true;

    private void Update()
    {
        bool modHeld = modifier == KeyCode.None || Input.GetKey(modifier);
        if (!modHeld) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) { Debug.Log("[StageDebugHelper] Key 1 pressed"); SetStage(GameProgressStage.EarlyGame); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { Debug.Log("[StageDebugHelper] Key 2 pressed"); SetStage(GameProgressStage.OneIngredient); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { Debug.Log("[StageDebugHelper] Key 3 pressed"); SetStage(GameProgressStage.TwoIngredients); }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { Debug.Log("[StageDebugHelper] Key 4 pressed"); SetStage(GameProgressStage.AllIngredients); }
        if (Input.GetKeyDown(KeyCode.Alpha5)) { Debug.Log("[StageDebugHelper] Key 5 pressed"); SetStage(GameProgressStage.PotionBrewed); }
        if (Input.GetKeyDown(KeyCode.Alpha6)) { Debug.Log("[StageDebugHelper] Key 6 pressed"); SetStage(GameProgressStage.CollectingCarParts); }
        if (Input.GetKeyDown(KeyCode.Alpha7)) { Debug.Log("[StageDebugHelper] Key 7 pressed"); SetStage(GameProgressStage.CarRepaired); }
    }

    private void OnGUI()
    {
        if (!showGUI || GameProgressManager.Instance == null) return;

        string label = $"Stage: {GameProgressManager.Instance.CurrentStage} " +
                       $"(press {(modifier != KeyCode.None ? modifier + "+" : "")}1-7 to change)";

        GUI.color = Color.black;
        GUI.Label(new Rect(11, 11, 500, 24), label);
        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, 10, 500, 24), label);
    }

    // ── Context-menu shortcuts ────────────────────────────────────────────────

    [ContextMenu("Set Stage: EarlyGame")]
    private void SetEarlyGame()            => SetStage(GameProgressStage.EarlyGame);

    [ContextMenu("Set Stage: OneIngredient")]
    private void SetOneIngredient()        => SetStage(GameProgressStage.OneIngredient);

    [ContextMenu("Set Stage: TwoIngredients")]
    private void SetTwoIngredients()       => SetStage(GameProgressStage.TwoIngredients);

    [ContextMenu("Set Stage: AllIngredients")]
    private void SetAllIngredients()       => SetStage(GameProgressStage.AllIngredients);

    [ContextMenu("Set Stage: PotionBrewed")]
    private void SetPotionBrewed()         => SetStage(GameProgressStage.PotionBrewed);

    [ContextMenu("Set Stage: CollectingCarParts")]
    private void SetCollectingCarParts()   => SetStage(GameProgressStage.CollectingCarParts);

    [ContextMenu("Set Stage: CarRepaired")]
    private void SetCarRepaired()          => SetStage(GameProgressStage.CarRepaired);

    // ── Core ──────────────────────────────────────────────────────────────────

    private void SetStage(GameProgressStage stage)
    {
        if (GameProgressManager.Instance == null)
        {
            Debug.LogWarning("[StageDebugHelper] GameProgressManager not found in scene.");
            return;
        }

        GameProgressManager.Instance.ForceSetStage(stage);

        EnemyDifficultyController diff = FindObjectOfType<EnemyDifficultyController>();
        if (diff != null && diff.CurrentSettings != null)
        {
            EnemyDifficultySettings s = diff.CurrentSettings;
            Debug.Log(
                $"[StageDebugHelper] ── Stage: {stage} ──\n" +
                $"  Speeds      patrol={s.patrolSpeed}  investigate={s.investigateSpeed}  chase={s.chaseSpeed}\n" +
                $"  Detection   patrol={s.patrolDetectionRange}  investigate={s.investigateDetectionRange}  chase={s.chaseRange}  lose={s.losePlayerRange}\n" +
                $"  Teleport    forestTimer={s.forestTeleportTime}s  cooldown={s.teleportCooldown}s"
            );
        }
        else
        {
            Debug.Log($"[StageDebugHelper] Forced stage → {stage}  (no EnemyDifficultyController found for value readout)");
        }
    }
}
