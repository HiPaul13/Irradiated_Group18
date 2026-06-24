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

    // ── Editor helper ─────────────────────────────────────────────────────────

    [ContextMenu("Populate Recommended Values")]
    private void PopulateRecommendedValues()
    {
        stageSettings = new List<EnemyDifficultySettings>
        {
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.EarlyGame,
                patrolSpeed = 3.2f, investigateSpeed = 4.8f, chaseSpeed = 9.2f,
                patrolDetectionRange = 55f, investigateDetectionRange = 80f,
                chaseRange = 105f, losePlayerRange = 150f,
                forestTeleportTime = 180f, teleportCooldown = 60f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.OneIngredient,
                patrolSpeed = 3.6f, investigateSpeed = 5.3f, chaseSpeed = 9.8f,
                patrolDetectionRange = 65f, investigateDetectionRange = 95f,
                chaseRange = 125f, losePlayerRange = 175f,
                forestTeleportTime = 150f, teleportCooldown = 60f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.TwoIngredients,
                patrolSpeed = 4.0f, investigateSpeed = 5.8f, chaseSpeed = 10.3f,
                patrolDetectionRange = 75f, investigateDetectionRange = 110f,
                chaseRange = 145f, losePlayerRange = 200f,
                forestTeleportTime = 120f, teleportCooldown = 55f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.AllIngredients,
                patrolSpeed = 4.4f, investigateSpeed = 6.3f, chaseSpeed = 10.8f,
                patrolDetectionRange = 85f, investigateDetectionRange = 120f,
                chaseRange = 160f, losePlayerRange = 220f,
                forestTeleportTime = 95f, teleportCooldown = 50f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.PotionBrewed,
                patrolSpeed = 4.7f, investigateSpeed = 6.7f, chaseSpeed = 11.2f,
                patrolDetectionRange = 95f, investigateDetectionRange = 130f,
                chaseRange = 175f, losePlayerRange = 235f,
                forestTeleportTime = 75f, teleportCooldown = 45f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.CollectingCarParts,
                patrolSpeed = 5.0f, investigateSpeed = 7.1f, chaseSpeed = 11.5f,
                patrolDetectionRange = 105f, investigateDetectionRange = 140f,
                chaseRange = 185f, losePlayerRange = 245f,
                forestTeleportTime = 60f, teleportCooldown = 40f
            },
            new EnemyDifficultySettings
            {
                stage = GameProgressStage.CarRepaired,
                patrolSpeed = 5.3f, investigateSpeed = 7.5f, chaseSpeed = 11.8f,
                patrolDetectionRange = 115f, investigateDetectionRange = 150f,
                chaseRange = 195f, losePlayerRange = 255f,
                forestTeleportTime = 50f, teleportCooldown = 35f
            },
        };

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[Difficulty] Populated all 7 stages with recommended values.");
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
