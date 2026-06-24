using System.Collections.Generic;
using UnityEngine;

public class HotbarInventory : MonoBehaviour
{
    [Header("Settings")]
    public int maxSlots = 5;

    [Header("UI")]
    public ItemSlotUI[] slotUIs;

    private List<ItemData> items = new List<ItemData>();

    private void Start()
    {
        RefreshUI();
    }

    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        if (items.Count >= maxSlots)
        {
            Debug.Log("Inventory full.");
            return false;
        }

        if (HasItem(item.itemID))
        {
            Debug.Log("Item already collected: " + item.itemName);
            return false;
        }

        items.Add(item);
        RefreshUI();

        Debug.Log("Picked up item: " + item.itemName);
        return true;
    }

    public bool HasItem(string itemID)
    {
        foreach (ItemData item in items)
        {
            if (item != null && item.itemID == itemID)
                return true;
        }

        return false;
    }

    public bool RemoveItem(string itemID)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemID == itemID)
            {
                Debug.Log("Removed item: " + items[i].itemName);
                items.RemoveAt(i);
                RefreshUI();
                return true;
            }
        }

        return false;
    }

    public bool HasAllItems(List<ItemData> requiredItems)
    {
        foreach (ItemData requiredItem in requiredItems)
        {
            if (requiredItem == null) continue;

            if (!HasItem(requiredItem.itemID))
                return false;
        }

        return true;
    }

    public bool RemoveItems(List<ItemData> requiredItems)
    {
        if (!HasAllItems(requiredItems))
            return false;

        foreach (ItemData item in requiredItems)
        {
            if (item != null)
                RemoveItem(item.itemID);
        }

        return true;
    }

    public bool HasFreeSlot()
    {
        return items.Count < maxSlots;
    }

    public List<ItemData> GetItems()
    {
        return new List<ItemData>(items);
    }

    private void RefreshUI()
    {
        if (slotUIs == null)
            return;

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] == null)
                continue;

            if (i < items.Count)
                slotUIs[i].SetItem(items[i]);
            else
                slotUIs[i].ClearSlot();
        }
    }
}