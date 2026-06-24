using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Central save / load system.
/// Attach to a persistent GameObject (DontDestroyOnLoad).
///
/// Manual save: press F5 or interact with a SaveStation.
/// Manual load: call SaveGameManager.Instance.LoadGame() from a menu button.
///
/// Inspector setup:
///   • playerTransform  — the root Transform of the player GameObject
///   • radiationManager — on the player
///   • hotbarInventory  — on the player
///   • carRepairStation — the single CarRepairStation in the scene
///   • allItemDataAssets — drag every ItemData ScriptableObject here so
///                         items can be restored from their saved IDs
/// </summary>
public class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    private const string SAVE_FILE = "savegame.json";

    [Header("Scene References")]
    [SerializeField] private Transform      playerTransform;
    [SerializeField] private RadiationManager radiationManager;
    [SerializeField] private HotbarInventory  hotbarInventory;
    [SerializeField] private CarRepairStation carRepairStation;

    [Header("Item Registry")]
    [Tooltip("Drag every ItemData ScriptableObject into this list. " +
             "Required so the inventory can be restored by item ID after loading.")]
    [SerializeField] private List<ItemData> allItemDataAssets = new List<ItemData>();

    // ── Runtime ──────────────────────────────────────────────────────────────

    private HashSet<string> collectedObjectIDs = new HashSet<string>();

    // Holds item IDs to restore after a scene transition. Null means no restore pending.
    private List<string> _transitionInventory = null;

    private string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILE);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        FindSceneReferences();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-find the player in the newly loaded scene.
        FindSceneReferences();

        // If a scene transition snapshot is waiting, restore it into the new inventory.
        if (_transitionInventory != null)
        {
            RestoreInventory(_transitionInventory);
            Debug.Log($"[Save] Inventory restored after scene transition: {_transitionInventory.Count} item(s).");
            _transitionInventory = null;
        }
    }

    /// <summary>
    /// Finds the Player and other scene-specific references.
    /// Called on Start and after every scene load.
    /// </summary>
    private void FindSceneReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform  = player.transform;
            radiationManager = player.GetComponent<RadiationManager>();
            hotbarInventory  = player.GetComponent<HotbarInventory>();
        }

        if (carRepairStation == null)
            carRepairStation = FindObjectOfType<CarRepairStation>();
    }

    private void Update()
    {
        // Quick-save with F5 (keyboard shortcut — remove or disable for final build).
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            SaveGame();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SaveGame()
    {
        GameSaveData data = new GameSaveData();

        // Player
        if (playerTransform != null)
        {
            data.playerPosX = playerTransform.position.x;
            data.playerPosY = playerTransform.position.y;
            data.playerPosZ = playerTransform.position.z;
            data.playerRotY = playerTransform.eulerAngles.y;
        }

        if (radiationManager != null)
            data.currentRadiation = radiationManager.currentRadiation;

        // Inventory
        if (hotbarInventory != null)
        {
            data.inventoryItemIDs = new List<string>();
            foreach (ItemData item in hotbarInventory.GetItems())
                if (item != null) data.inventoryItemIDs.Add(item.itemID);
        }

        // Progress
        if (GameProgressManager.Instance != null)
        {
            GameProgressManager gpm = GameProgressManager.Instance;
            data.progressStage          = (int)gpm.CurrentStage;
            data.collectedIngredientIDs  = new List<string>(gpm.CollectedIngredients);
            data.depositedIngredientIDs  = new List<string>(gpm.DepositedIngredients);
            data.potionBrewed           = gpm.IsPotionBrewed;
            data.collectedCarPartIDs   = new List<string>(gpm.CollectedCarParts);
            data.insertedCarPartIDs    = new List<string>(gpm.InsertedCarParts);
            data.carRepaired           = gpm.IsCarRepaired;
        }

        // World — which objects have been picked up
        data.collectedObjectSaveIDs = new List<string>(collectedObjectIDs);

        // Enemy (optional)
        Monster_Movement monster = FindObjectOfType<Monster_Movement>();
        if (monster != null)
        {
            data.enemyPosX  = monster.transform.position.x;
            data.enemyPosY  = monster.transform.position.y;
            data.enemyPosZ  = monster.transform.position.z;
            data.enemyState = (int)monster.CurrentState;
        }

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);

        Debug.Log($"[Save] Saved to: {SavePath}");
        Debug.Log($"[Save] Stage: {(GameProgressStage)data.progressStage} | " +
                  $"Radiation: {data.currentRadiation:F1} | " +
                  $"Inventory: {data.inventoryItemIDs.Count} item(s) | " +
                  $"Collected objects: {data.collectedObjectSaveIDs.Count}");
    }

    public void LoadGame()
    {
        if (!HasSave())
        {
            Debug.LogWarning("[Save] No save file found at: " + SavePath);
            return;
        }

        string json = File.ReadAllText(SavePath);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

        Debug.Log($"[Save] Loading from: {SavePath}");

        // Restore collected object set FIRST so CollectableItem.Start() checks work
        // if a scene reload happens after this call.
        collectedObjectIDs = new HashSet<string>(data.collectedObjectSaveIDs ?? new List<string>());

        // Destroy any still-alive collectables that were already picked up
        CollectableItem[] allCollectables = FindObjectsOfType<CollectableItem>();
        foreach (CollectableItem c in allCollectables)
        {
            if (!string.IsNullOrEmpty(c.saveID) && collectedObjectIDs.Contains(c.saveID))
                Destroy(c.gameObject);
        }

        // Player position and rotation
        if (playerTransform != null)
        {
            playerTransform.position    = new Vector3(data.playerPosX, data.playerPosY, data.playerPosZ);
            playerTransform.eulerAngles = new Vector3(0f, data.playerRotY, 0f);
        }

        // Radiation
        if (radiationManager != null)
            radiationManager.currentRadiation = data.currentRadiation;

        // Inventory
        RestoreInventory(data.inventoryItemIDs);

        // Progress
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.LoadState(data);

        // Car repair station
        if (carRepairStation != null)
            carRepairStation.LoadState(data.insertedCarPartIDs, data.carRepaired);

        // Enemy teleport timer
        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.ResetForestTimer();

        // Reset trigger zones so they can fire again after loading
        EnemyTeleportTriggerZone[] zones = FindObjectsOfType<EnemyTeleportTriggerZone>();
        foreach (EnemyTeleportTriggerZone zone in zones)
            zone.ResetFired();

        Debug.Log($"[Save] Load complete. Stage: {(GameProgressStage)data.progressStage}");
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public void DeleteSave()
    {
        if (!File.Exists(SavePath)) return;
        File.Delete(SavePath);
        Debug.Log("[Save] Save file deleted.");
    }

    // ── Scene-transition inventory persistence ────────────────────────────────

    /// <summary>
    /// Call this just before loading a new scene.
    /// Snapshots the current inventory so it can be restored in the new scene.
    /// </summary>
    public void SaveInventoryForTransition()
    {
        _transitionInventory = new List<string>();

        if (hotbarInventory != null)
        {
            foreach (ItemData item in hotbarInventory.GetItems())
                if (item != null) _transitionInventory.Add(item.itemID);
        }

        Debug.Log($"[Save] Inventory snapshot for transition: {_transitionInventory.Count} item(s) → " +
                  string.Join(", ", _transitionInventory));
    }

    // ── Collected-object tracking (called by CollectableItem) ─────────────────

    public void RegisterCollectedObject(string saveID)
    {
        collectedObjectIDs.Add(saveID);
    }

    public bool IsObjectCollected(string saveID)
    {
        return collectedObjectIDs.Contains(saveID);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void RestoreInventory(List<string> itemIDs)
    {
        if (hotbarInventory == null || itemIDs == null) return;

        // Build an ID → ItemData lookup from the inspector-assigned registry.
        Dictionary<string, ItemData> lookup = new Dictionary<string, ItemData>();
        foreach (ItemData asset in allItemDataAssets)
            if (asset != null && !string.IsNullOrEmpty(asset.itemID))
                lookup[asset.itemID] = asset;

        // Clear the current inventory.
        List<ItemData> existing = hotbarInventory.GetItems();
        foreach (ItemData item in existing)
            if (item != null) hotbarInventory.RemoveItem(item.itemID);

        // Re-add saved items.
        foreach (string id in itemIDs)
        {
            if (lookup.TryGetValue(id, out ItemData itemData))
                hotbarInventory.AddItem(itemData);
            else
                Debug.LogWarning($"[Save] Could not find ItemData for saved item ID: '{id}'. " +
                                 "Make sure it is added to SaveGameManager.allItemDataAssets.");
        }
    }
}
