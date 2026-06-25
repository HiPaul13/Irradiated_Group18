using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Restores inventory after a scene transition when SaveGameManager is not present.
/// </summary>
[DefaultExecutionOrder(-50)]
public class TransitionInventoryRestorer : MonoBehaviour
{
    private void Start()
    {
        if (SceneTransitionState.TransitionInventory == null)
            return;

        if (SaveGameManager.Instance != null)
            return;

        HotbarInventory inventory = GetComponent<HotbarInventory>();
        if (inventory == null)
            return;

        RestoreInventory(inventory, SceneTransitionState.TransitionInventory);
        SceneTransitionState.ClearTransitionInventory();
    }

    private static void RestoreInventory(HotbarInventory inventory, List<string> itemIDs)
    {
        ItemData[] allItems = Resources.FindObjectsOfTypeAll<ItemData>();

        List<ItemData> existing = inventory.GetItems();
        foreach (ItemData item in existing)
        {
            if (item != null)
                inventory.RemoveItem(item.itemID);
        }

        foreach (string itemID in itemIDs)
        {
            ItemData itemData = FindItemData(allItems, itemID);
            if (itemData != null)
                inventory.AddItem(itemData);
            else
                Debug.LogWarning($"[TransitionInventory] Unknown item ID '{itemID}'.");
        }
    }

    private static ItemData FindItemData(ItemData[] allItems, string itemID)
    {
        foreach (ItemData item in allItems)
        {
            if (item != null && item.itemID == itemID)
                return item;
        }

        return null;
    }
}
