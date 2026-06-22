using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameProgressStage
{
    EarlyGame             = 0,
    OneIngredient         = 1,
    TwoIngredients        = 2,
    AllIngredients        = 3,
    PotionBrewed          = 4,
    CollectingCarParts    = 5,
    CarRepaired           = 6
}

/// <summary>
/// Central tracker for all meaningful game-progress events.
/// Attach to a persistent GameObject (DontDestroyOnLoad).
/// Other systems subscribe to OnStageChanged to react to progress.
/// </summary>
public class GameProgressManager : MonoBehaviour
{
    public static GameProgressManager Instance { get; private set; }

    public event Action<GameProgressStage> OnStageChanged;

    [Header("Ingredient Item IDs — must match ItemData.itemID")]
    public string castleMushroomID = "castle_mushroom";
    public string cabinFlowerID    = "cabin_flower";
    public string lakeFlowerID     = "lake_flower";

    [Header("Potion Item ID")]
    public string antiRadiationPotionID = "anti_radiation_potion";

    [Header("Car Part Item IDs")]
    public List<string> carPartIDs = new List<string>();

    // ── Runtime state ────────────────────────────────────────────────────────

    private HashSet<string> collectedIngredientIDs = new HashSet<string>();
    private bool            potionBrewed            = false;
    private HashSet<string> collectedCarPartIDs     = new HashSet<string>();
    private HashSet<string> insertedCarPartIDs      = new HashSet<string>();
    private bool            carRepaired             = false;

    private GameProgressStage currentStage = GameProgressStage.EarlyGame;

    // ── Public read-only accessors (used by SaveGameManager) ─────────────────

    public GameProgressStage CurrentStage         => currentStage;
    public bool              IsPotionBrewed       => potionBrewed;
    public bool              IsCarRepaired        => carRepaired;
    public HashSet<string>   CollectedIngredients => collectedIngredientIDs;
    public HashSet<string>   CollectedCarParts    => collectedCarPartIDs;
    public HashSet<string>   InsertedCarParts     => insertedCarPartIDs;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Notification methods (called by gameplay scripts) ────────────────────

    /// <summary>Called by CollectableItem when any item is picked up.</summary>
    public void NotifyItemCollected(string itemID)
    {
        if (itemID == castleMushroomID || itemID == cabinFlowerID || itemID == lakeFlowerID)
        {
            collectedIngredientIDs.Add(itemID);
            Debug.Log($"[Progress] Ingredient collected: {itemID}  ({collectedIngredientIDs.Count}/3)");
        }
        else if (carPartIDs.Contains(itemID))
        {
            collectedCarPartIDs.Add(itemID);
            Debug.Log($"[Progress] Car part picked up: {itemID}");
        }

        RecalculateStage();
    }

    /// <summary>Called by CauldronCraftingStation after a successful craft.</summary>
    public void NotifyPotionBrewed(string resultItemID)
    {
        if (resultItemID != antiRadiationPotionID) return;

        potionBrewed = true;
        Debug.Log("[Progress] Anti-radiation potion brewed!");
        RecalculateStage();
    }

    /// <summary>Called by CarRepairStation when a part is inserted into the car.</summary>
    public void NotifyCarPartInserted(string itemID)
    {
        insertedCarPartIDs.Add(itemID);
        Debug.Log($"[Progress] Car part inserted: {itemID}  ({insertedCarPartIDs.Count} total)");
        RecalculateStage();
    }

    /// <summary>Called by CarRepairStation when all parts are inserted and the car is repaired.</summary>
    public void NotifyCarRepaired()
    {
        carRepaired = true;
        Debug.Log("[Progress] Car repaired! Player can now escape.");
        RecalculateStage();
    }

    // ── Save / Load integration ──────────────────────────────────────────────

    /// <summary>Restores state from a GameSaveData object loaded by SaveGameManager.</summary>
    public void LoadState(GameSaveData data)
    {
        collectedIngredientIDs = new HashSet<string>(data.collectedIngredientIDs ?? new List<string>());
        potionBrewed           = data.potionBrewed;
        collectedCarPartIDs    = new HashSet<string>(data.collectedCarPartIDs ?? new List<string>());
        insertedCarPartIDs     = new HashSet<string>(data.insertedCarPartIDs  ?? new List<string>());
        carRepaired            = data.carRepaired;
        currentStage           = (GameProgressStage)data.progressStage;

        Debug.Log($"[Progress] State loaded. Stage: {currentStage}");
        OnStageChanged?.Invoke(currentStage);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void RecalculateStage()
    {
        GameProgressStage newStage;

        if (carRepaired)
            newStage = GameProgressStage.CarRepaired;
        else if (insertedCarPartIDs.Count > 0 || collectedCarPartIDs.Count > 0)
            newStage = GameProgressStage.CollectingCarParts;
        else if (potionBrewed)
            newStage = GameProgressStage.PotionBrewed;
        else if (collectedIngredientIDs.Count >= 3)
            newStage = GameProgressStage.AllIngredients;
        else if (collectedIngredientIDs.Count == 2)
            newStage = GameProgressStage.TwoIngredients;
        else if (collectedIngredientIDs.Count == 1)
            newStage = GameProgressStage.OneIngredient;
        else
            newStage = GameProgressStage.EarlyGame;

        if (newStage == currentStage) return;

        currentStage = newStage;
        Debug.Log($"[Progress] Stage advanced to: {currentStage}");
        OnStageChanged?.Invoke(currentStage);
    }
}
