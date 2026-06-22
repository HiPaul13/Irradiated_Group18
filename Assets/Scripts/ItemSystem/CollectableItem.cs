using UnityEngine;

/// <summary>
/// Place on any pickup object in the world.
///
/// saveID MUST be unique across every scene.
/// Recommended format: "scene_objecttype_location"
/// Examples: "forest_mushroom_castle", "world_flower_lake", "dumpyard_enginepart"
///
/// If the saveID matches an already-collected entry in SaveGameManager,
/// the object will self-destruct on Start so it doesn't respawn after loading.
/// </summary>
public class CollectableItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    public ItemData itemData;

    [Header("Save System")]
    [Tooltip("Unique persistent ID for this pickup. Never leave two items with the same saveID.")]
    public string saveID = "";

    private void Start()
    {
        // Self-destruct if already collected in a previous session or after a load.
        if (SaveGameManager.Instance != null
            && !string.IsNullOrEmpty(saveID)
            && SaveGameManager.Instance.IsObjectCollected(saveID))
        {
            Destroy(gameObject);
        }
    }

    public void Interact(PlayerInteraction playerInteraction)
    {
        if (itemData == null)
        {
            Debug.LogWarning("Collectable has no ItemData assigned.");
            return;
        }

        HotbarInventory inventory = playerInteraction.GetInventory();

        if (inventory == null)
        {
            Debug.LogWarning("Player has no HotbarInventory.");
            return;
        }

        bool added = inventory.AddItem(itemData);

        if (!added) return;

        // Tell the progress tracker about this item.
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.NotifyItemCollected(itemData.itemID);

        // Mark this specific world object as permanently collected.
        if (SaveGameManager.Instance != null && !string.IsNullOrEmpty(saveID))
            SaveGameManager.Instance.RegisterCollectedObject(saveID);

        Destroy(gameObject);
    }

    public string GetInteractionText()
    {
        if (itemData == null) return "Press F to pick up item";
        return "Press F to pick up " + itemData.itemName;
    }
}
