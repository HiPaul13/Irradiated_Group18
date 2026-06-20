using System.Collections.Generic;
using UnityEngine;

public class CauldronCraftingStation : MonoBehaviour, IInteractable
{
    [Header("Recipes")]
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    public void Interact(PlayerInteraction playerInteraction)
    {
        HotbarInventory inventory = playerInteraction.GetInventory();

        if (inventory == null)
        {
            Debug.LogWarning("No inventory found.");
            return;
        }

        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe == null) continue;

            if (CanCraft(recipe, inventory))
            {
                Craft(recipe, inventory);
                return;
            }
        }

        Debug.Log("No valid recipe found.");
    }

    private bool CanCraft(CraftingRecipe recipe, HotbarInventory inventory)
    {
        if (recipe.resultItem == null)
        {
            Debug.LogWarning("Recipe has no result item.");
            return false;
        }

        if (!inventory.HasAllItems(recipe.requiredItems))
        {
            return false;
        }

        int freeSlotsAfterRemovingIngredients =
        inventory.GetItems().Count - recipe.requiredItems.Count + 1;

        if (freeSlotsAfterRemovingIngredients > 5)
        {
            Debug.Log("No free slot for crafted item.");
            return false;
        }

        return true;
    }

    private void Craft(CraftingRecipe recipe, HotbarInventory inventory)
    {
        inventory.RemoveItems(recipe.requiredItems);
        inventory.AddItem(recipe.resultItem);

        Debug.Log("Crafted: " + recipe.resultItem.itemName);
    }

    public string GetInteractionText()
    {
        return "Press F to use cauldron";
    }
}