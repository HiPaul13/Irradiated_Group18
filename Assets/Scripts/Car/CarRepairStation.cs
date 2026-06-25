using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CarRepairStation : MonoBehaviour, IInteractable
{
    [Header("Required Car Parts")]
    public List<ItemData> requiredItems = new List<ItemData>();

    [Header("Ending")]
    [SerializeField] private string endingCutsceneSceneName = "End_Cutscene";

    private List<string> insertedItemIDs = new List<string>();
    private bool         isRepaired      = false;

    // Read by SaveGameManager during save.
    public List<string> InsertedItemIDs => insertedItemIDs;
    public bool         IsRepaired      => isRepaired;

    public void Interact(PlayerInteraction playerInteraction)
    {
        if (isRepaired)
        {
            Debug.Log("Car is already repaired.");
            return;
        }

        HotbarInventory inventory = playerInteraction.GetInventory();

        if (inventory == null)
        {
            Debug.LogWarning("No inventory found.");
            return;
        }

        bool insertedSomething = false;

        foreach (ItemData requiredItem in requiredItems)
        {
            if (requiredItem == null) continue;

            bool alreadyInserted = insertedItemIDs.Contains(requiredItem.itemID);

            if (!alreadyInserted && inventory.HasItem(requiredItem.itemID))
            {
                inventory.RemoveItem(requiredItem.itemID);
                insertedItemIDs.Add(requiredItem.itemID);

                Debug.Log("Inserted car part: " + requiredItem.itemName);
                insertedSomething = true;

                if (GameProgressManager.Instance != null)
                    GameProgressManager.Instance.NotifyCarPartInserted(requiredItem.itemID);

                // Permanently register the world object.
                string saveID = SessionCollectableTracker.GetSaveID(requiredItem.itemID);
                if (saveID != null)
                {
                    if (SaveGameManager.Instance != null)
                        SaveGameManager.Instance.RegisterCollectedObject(saveID);
                    SessionCollectableTracker.RemoveTrack(requiredItem.itemID);
                }

                // Auto-save checkpoint so death won't lose this install.
                if (CheckpointManager.Instance != null)
                    CheckpointManager.Instance.SaveCheckpoint();

                break; // insert one part per interaction
            }
        }

        if (!insertedSomething)
            PrintMissingItems(inventory);

        CheckIfRepaired();
    }

    private void CheckIfRepaired()
    {
        if (insertedItemIDs.Count < requiredItems.Count) return;

        isRepaired = true;
        Debug.Log("Car repaired! You can escape now.");

        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.NotifyCarRepaired();

        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.SaveCheckpoint();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;

        if (!string.IsNullOrEmpty(endingCutsceneSceneName))
            SceneManager.LoadScene(endingCutsceneSceneName);
    }

    private void PrintMissingItems(HotbarInventory inventory)
    {
        Debug.Log("Car still needs:");

        foreach (ItemData requiredItem in requiredItems)
        {
            if (requiredItem == null) continue;
            if (!insertedItemIDs.Contains(requiredItem.itemID))
                Debug.Log("  - " + requiredItem.itemName);
        }
    }

    public string GetInteractionText()
    {
        return isRepaired ? "Car is repaired — drive away!" : "Press F to repair car";
    }

    /// <summary>Called by SaveGameManager when loading a saved game.</summary>
    public void LoadState(List<string> savedInsertedIDs, bool savedRepaired)
    {
        insertedItemIDs = new List<string>(savedInsertedIDs ?? new List<string>());
        isRepaired      = savedRepaired;
        Debug.Log($"[CarRepair] State loaded. Inserted parts: {insertedItemIDs.Count}  Repaired: {isRepaired}");
    }
}
