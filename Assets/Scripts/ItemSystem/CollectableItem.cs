using UnityEngine;

public class CollectableItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    public ItemData itemData;

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

        if (added)
        {
            Destroy(gameObject);
        }
    }

    public string GetInteractionText()
    {
        if (itemData == null)
        {
            return "Press F to pick up item";
        }

        return "Press F to pick up " + itemData.itemName;
    }
}