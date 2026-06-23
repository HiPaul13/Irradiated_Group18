using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One row in the Inspector difficulty table.
/// Fill in one entry per GameProgressStage you want to tune.
/// Stages with no explicit entry inherit the nearest lower stage.
///
/// RECOMMENDED VALUES (map ~1000x1000, player sprint = 10):
///
/// Stage               | PatSpd | InvSpd | ChaSpd | PatDet | InvDet | ChaRng | LoseRng | ForestT | Cooldown
/// EarlyGame           |  3.2   |  4.8   |  9.2   |   55   |   80   |  105   |   150   |  180    |   60
/// OneIngredient       |  3.6   |  5.3   |  9.8   |   65   |   95   |  125   |   175   |  150    |   60
/// TwoIngredients      |  4.0   |  5.8   | 10.3   |   75   |  110   |  145   |   200   |  120    |   55
/// AllIngredients      |  4.4   |  6.3   | 10.8   |   85   |  120   |  160   |   220   |   95    |   50
/// PotionBrewed        |  4.7   |  6.7   | 11.2   |   95   |  130   |  175   |   235   |   75    |   45
/// CollectingCarParts  |  5.0   |  7.1   | 11.5   |  105   |  140   |  185   |   245   |   60    |   40
/// CarRepaired         |  5.3   |  7.5   | 11.8   |  115   |  150   |  195   |   255   |   50    |   35
/// </summary>
[Serializable]
public class EnemyDifficultySettings
{
    public GameProgressStage stage;

    [Header("Movement Speeds")]
    public float patrolSpeed      = 2.5f;
    public float investigateSpeed = 3.5f;
    public float chaseSpeed       = 5f;

    [Header("Detection Ranges")]
    [Tooltip("Detection while patrolling or returning to patrol")]
    public float patrolDetectionRange      = 10f;
    [Tooltip("Detection while investigating a noise/last-known position")]
    public float investigateDetectionRange = 14f;
    [Tooltip("Detection range while actively chasing — also entry range for starting a chase")]
    public float chaseRange                = 18f;
    [Tooltip("Chase is abandoned when the player exceeds this distance")]
    public float losePlayerRange           = 25f;

    [Header("Forest Teleport Timing")]
    [Tooltip("Seconds the player can roam before a forest-timer teleport fires")]
    public float forestTeleportTime = 90f;
    [Tooltip("Minimum seconds between any two teleports")]
    public float teleportCooldown   = 45f;
}

/// <summary>
/// Reads the current GameProgressStage and pushes the matching
/// EnemyDifficultySettings values to Monster_Movement.
///
/// Attach to the same GameObject as Monster_Movement.
/// </summary>
public class EnemyDifficultyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Monster_Movement monster;

    [Header("Difficulty Table — one row per stage")]
    public List<EnemyDifficultySettings> stageSettings = new List<EnemyDifficultySettings>();

    /// <summary>Current applied settings — read by EnemyTeleportManager for timer values.</summary>
    public EnemyDifficultySettings CurrentSettings { get; private set; }

    private void Awake()
    {
        if (monster == null) monster = GetComponent<Monster_Movement>();
    }

    private void Start()
    {
        if (GameProgressManager.Instance != null)
        {
            GameProgressManager.Instance.OnStageChanged += OnStageChanged;
            ApplyForStage(GameProgressManager.Instance.CurrentStage);
        }
        else
        {
            Debug.LogWarning("[Difficulty] GameProgressManager not found — applying first entry.");
            if (stageSettings.Count > 0) Apply(stageSettings[0]);
        }
    }

    private void OnDestroy()
    {
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.OnStageChanged -= OnStageChanged;
    }

    private void OnStageChanged(GameProgressStage newStage) => ApplyForStage(newStage);

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ApplyForStage(GameProgressStage stage)
    {
        EnemyDifficultySettings s = FindSettingsForStage(stage);

        if (s == null) { Debug.LogWarning($"[Difficulty] No settings for stage {stage}."); return; }

        Apply(s);
        Debug.Log($"[Difficulty] Stage {stage} → " +
                  $"patrol={s.patrolSpeed} inv={s.investigateSpeed} chase={s.chaseSpeed} | " +
                  $"patrolDetect={s.patrolDetectionRange} invDetect={s.investigateDetectionRange} " +
                  $"chaseRange={s.chaseRange} lose={s.losePlayerRange} | " +
                  $"forestTimer={s.forestTeleportTime}s cooldown={s.teleportCooldown}s");
    }

    private void Apply(EnemyDifficultySettings s)
    {
        CurrentSettings = s;
        if (monster == null) return;

        monster.SetPatrolSpeed(s.patrolSpeed);
        monster.SetInvestigateSpeed(s.investigateSpeed);
        monster.SetChaseSpeed(s.chaseSpeed);
        monster.SetPatrolDetectionRange(s.patrolDetectionRange);
        monster.SetInvestigateDetectionRange(s.investigateDetectionRange);
        monster.SetChaseRange(s.chaseRange);
        monster.SetLosePlayerRange(s.losePlayerRange);
    }

    private EnemyDifficultySettings FindSettingsForStage(GameProgressStage stage)
    {
        EnemyDifficultySettings best = null;

        foreach (EnemyDifficultySettings s in stageSettings)
        {
            if (s.stage == stage) return s;

            if (s.stage <= stage && (best == null || s.stage > best.stage))
                best = s;
        }

        return best ?? (stageSettings.Count > 0 ? stageSettings[0] : null);
    }
}
