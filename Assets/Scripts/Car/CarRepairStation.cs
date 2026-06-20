using System.Collections.Generic;
using UnityEngine;

public class CarRepairStation : MonoBehaviour, IInteractable
{
    [Header("Required Car Parts")]
    public List<ItemData> requiredItems = new List<ItemData>();

    private List<string> insertedItemIDs = new List<string>();
    private bool isRepaired = false;

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
            }
        }

        if (!insertedSomething)
        {
            PrintMissingItems(inventory);
        }

        CheckIfRepaired();
    }

    private void CheckIfRepaired()
    {
        if (insertedItemIDs.Count >= requiredItems.Count)
        {
            isRepaired = true;
            Debug.Log("Car repaired! You can escape now.");
        }
    }

    private void PrintMissingItems(HotbarInventory inventory)
    {
        Debug.Log("Car still needs:");

        foreach (ItemData requiredItem in requiredItems)
        {
            if (requiredItem == null) continue;

            if (!insertedItemIDs.Contains(requiredItem.itemID))
            {
                Debug.Log("- " + requiredItem.itemName);
            }
        }
    }

    public string GetInteractionText()
    {
        return "Press F to repair car";
    }
}