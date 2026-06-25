using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the automatic checkpoint system.
///
/// A checkpoint is saved automatically after every meaningful progress event
/// (ingredient deposited into cauldron, car part installed). It stores only
/// real progress — NOT player position or temporary inventory.
///
/// On death PlayerDeath calls RespawnPlayer(), which reloads the gameplay scene
/// from the last checkpoint. Items that were only picked up (never deposited)
/// are NOT in the checkpoint, so they respawn naturally in the world.
///
/// Setup:
///   1. Add this component to the same persistent GameObject as GameProgressManager
///      (DontDestroyOnLoad).
///   2. In Forest_Environment, create an empty GameObject near the cabin, add a
///      PlayerSpawnPoint component, set its SpawnPointId to match cabinRespawnPointId
///      (default: "cabin_respawn").
///   3. Set gameplaySceneName and endingCutsceneSceneName to match Build Settings.
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    /// <summary>Set to true before loading the gameplay scene to signal a fresh run.
    /// CheckpointManager clears any existing checkpoint so death respawn doesn't
    /// restore progress from a previous session.</summary>
    public static bool IsNewGame { get; set; }

    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "Forest_Environment";

    [Header("Respawn")]
    [SerializeField] private string cabinRespawnPointId   = "cabin_respawn";
    [SerializeField] private float  respawnRadiationLevel = 30f;

    private const string CHECKPOINT_FILE = "checkpoint.json";
    private string CheckpointPath => Path.Combine(Application.persistentDataPath, CHECKPOINT_FILE);

    private CheckpointData currentCheckpoint = new CheckpointData();
    private bool           pendingRespawn    = false;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // ── Checkpoint save ──────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot current progress to disk. Called automatically by
    /// CauldronCraftingStation and CarRepairStation after a successful action.
    /// </summary>
    public void SaveCheckpoint()
    {
        if (GameProgressManager.Instance != null)
        {
            GameProgressManager gpm = GameProgressManager.Instance;
            currentCheckpoint.progressStage         = (int)gpm.CurrentStage;
            currentCheckpoint.depositedIngredientIDs = new List<string>(gpm.DepositedIngredients);
            currentCheckpoint.potionBrewed           = gpm.IsPotionBrewed;
            currentCheckpoint.insertedCarPartIDs     = new List<string>(gpm.InsertedCarParts);
            currentCheckpoint.carRepaired            = gpm.IsCarRepaired;
        }

        if (SaveGameManager.Instance != null)
        {
            currentCheckpoint.permanentCollectedSaveIDs =
                new List<string>(SaveGameManager.Instance.PermanentCollectedSaveIDs);
        }

        File.WriteAllText(CheckpointPath, JsonUtility.ToJson(currentCheckpoint, prettyPrint: true));

        Debug.Log($"[Checkpoint] Saved. Stage: {(GameProgressStage)currentCheckpoint.progressStage} | " +
                  $"Ingredients: {currentCheckpoint.depositedIngredientIDs.Count} | " +
                  $"Car parts: {currentCheckpoint.insertedCarPartIDs.Count}");
    }

    /// <summary>Erases the on-disk checkpoint and resets in-memory state (called on New Game).</summary>
    public void DeleteCheckpoint()
    {
        currentCheckpoint = new CheckpointData();
        if (File.Exists(CheckpointPath)) File.Delete(CheckpointPath);
        Debug.Log("[Checkpoint] Checkpoint cleared for new game.");
    }

    // ── Death respawn ────────────────────────────────────────────────────────

    /// <summary>Called by PlayerDeath. Reloads the gameplay scene and restores checkpoint state.</summary>
    public void RespawnPlayer()
    {
        // Discard session-only state that should not survive death.
        PotionProtectionState.Clear();
        SessionCollectableTracker.Clear();

        // Load the on-disk checkpoint (in case this session saved progress more recently
        // than the in-memory copy, e.g. after manual F5 save).
        if (File.Exists(CheckpointPath))
        {
            string json = File.ReadAllText(CheckpointPath);
            currentCheckpoint = JsonUtility.FromJson<CheckpointData>(json) ?? new CheckpointData();
        }

        pendingRespawn = true;
        SceneManager.LoadScene(gameplaySceneName);
    }

    // ── Scene-load hook ──────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameplaySceneName) return;

        // On a fresh new game, wipe any leftover checkpoint from a previous session.
        if (IsNewGame)
        {
            IsNewGame = false;
            DeleteCheckpoint();
            return;
        }

        if (!pendingRespawn) return;
        pendingRespawn = false;

        // Step 1 — Restore collected-object IDs BEFORE CollectableItem.Start() runs.
        //          Items that were only picked up (never deposited) are absent from
        //          permanentCollectedSaveIDs, so they will respawn in the world.
        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.RestoreCollectedObjectIDs(
                currentCheckpoint.permanentCollectedSaveIDs);

        // Step 2 — Restore game-progress state.
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.LoadState(BuildSaveDataFromCheckpoint());

        // Step 3 — Restore car repair station (no Start() dependency).
        CarRepairStation car = FindFirstObjectByType<CarRepairStation>();
        if (car != null)
            car.LoadState(currentCheckpoint.insertedCarPartIDs, currentCheckpoint.carRepaired);

        // Step 4 — Reset enemy systems.
        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.ResetForestTimer();

        EnemyTeleportTriggerZone[] zones =
            FindObjectsByType<EnemyTeleportTriggerZone>(FindObjectsSortMode.None);
        foreach (EnemyTeleportTriggerZone z in zones) z.ResetFired();

        // Step 5 — Teleport, radiation reset, inventory clear — after Start() has run.
        StartCoroutine(FinishRespawnAfterStart());
    }

    private IEnumerator FinishRespawnAfterStart()
    {
        yield return null; // wait one frame so all Start() methods finish

        TeleportPlayerToCabin();

        // Reset radiation (runs after RadiationManager.Start() → PotionProtectionState was already cleared).
        RadiationManager radiation = FindFirstObjectByType<RadiationManager>();
        if (radiation != null)
            radiation.currentRadiation = respawnRadiationLevel;

        // Clear inventory — items not deposited/installed are lost on death.
        HotbarInventory inventory = FindFirstObjectByType<HotbarInventory>();
        if (inventory != null)
        {
            foreach (ItemData item in inventory.GetItems())
                if (item != null) inventory.RemoveItem(item.itemID);
        }

        Debug.Log("[Checkpoint] Player respawned at cabin.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void TeleportPlayerToCabin()
    {
        PlayerSpawnPoint[] spawnPoints =
            FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

        PlayerSpawnPoint cabin = null;
        foreach (PlayerSpawnPoint sp in spawnPoints)
        {
            if (sp.SpawnPointId == cabinRespawnPointId) { cabin = sp; break; }
        }

        if (cabin == null)
        {
            Debug.LogWarning($"[Checkpoint] No PlayerSpawnPoint with id='{cabinRespawnPointId}' found. " +
                             "Create one near the cabin in Forest_Environment.");
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        playerObj.transform.SetPositionAndRotation(
            cabin.transform.position, cabin.transform.rotation);

        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = cabin.transform.position;
            rb.rotation        = cabin.transform.rotation;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        FirstPersonController fpc = playerObj.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.SyncRotationFromTransform();
    }

    private GameSaveData BuildSaveDataFromCheckpoint()
    {
        // collectedIngredients = depositedIngredients  (deposited ⇒ definitely collected)
        // collectedCarParts    = insertedCarParts       (installed ⇒ definitely collected)
        return new GameSaveData
        {
            progressStage          = currentCheckpoint.progressStage,
            collectedIngredientIDs = currentCheckpoint.depositedIngredientIDs,
            depositedIngredientIDs = currentCheckpoint.depositedIngredientIDs,
            potionBrewed           = currentCheckpoint.potionBrewed,
            collectedCarPartIDs    = currentCheckpoint.insertedCarPartIDs,
            insertedCarPartIDs     = currentCheckpoint.insertedCarPartIDs,
            carRepaired            = currentCheckpoint.carRepaired,
            inventoryItemIDs       = new List<string>(),
            collectedObjectSaveIDs = currentCheckpoint.permanentCollectedSaveIDs,
        };
    }
}
