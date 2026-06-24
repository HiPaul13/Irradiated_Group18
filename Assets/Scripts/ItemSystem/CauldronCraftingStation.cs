using System.Collections.Generic;
using UnityEngine;

public class CauldronCraftingStation : MonoBehaviour, IInteractable
{
    [Header("Recipes")]
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    private void Start()
    {
        // If the potion was already brewed in a previous session, nothing to do.
        // Deposited state is stored in GameProgressManager and persists automatically.
    }

    public void Interact(PlayerInteraction playerInteraction)
    {
        HotbarInventory inventory = playerInteraction.GetInventory();

        if (inventory == null)
        {
            Debug.LogWarning("[Cauldron] No inventory found.");
            return;
        }

        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe == null) continue;

            // If all ingredients are already deposited, craft the result.
            if (AllDeposited(recipe))
            {
                Craft(recipe, inventory);
                return;
            }

            // Try to deposit one ingredient the player is currently carrying.
            if (TryDepositOne(recipe, inventory))
                return;
        }

        // Nothing could be deposited — tell the player what is still needed.
        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe == null) continue;
            PrintMissing(recipe, inventory);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if every required ingredient has been deposited into the cauldron.</summary>
    private bool AllDeposited(CraftingRecipe recipe)
    {
        if (GameProgressManager.Instance == null) return false;

        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;
            if (!GameProgressManager.Instance.DepositedIngredients.Contains(req.itemID))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Looks for the first required ingredient that is in the player's inventory
    /// but not yet deposited. Removes it from inventory and registers it as deposited.
    /// Returns true if something was deposited.
    /// </summary>
    private bool TryDepositOne(CraftingRecipe recipe, HotbarInventory inventory)
    {
        if (GameProgressManager.Instance == null) return false;

        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;

            bool alreadyDeposited = GameProgressManager.Instance.DepositedIngredients.Contains(req.itemID);
            if (alreadyDeposited) continue;

            if (!inventory.HasItem(req.itemID)) continue;

            // Found one — take it from the player and register it.
            inventory.RemoveItem(req.itemID);
            GameProgressManager.Instance.NotifyIngredientDeposited(req.itemID);

            int deposited = GameProgressManager.Instance.DepositedIngredients.Count;
            int needed    = recipe.requiredItems.Count;

            Debug.Log($"[Cauldron] Deposited '{req.itemName}'. " +
                      $"({deposited}/{needed} ingredients in cauldron)");

            // If that was the last one, craft immediately.
            if (AllDeposited(recipe))
            {
                Craft(recipe, inventory);
            }
            else
            {
                Debug.Log($"[Cauldron] {needed - deposited} more ingredient(s) needed.");
            }

            return true;
        }

        return false;
    }

    private void Craft(CraftingRecipe recipe, HotbarInventory inventory)
    {
        if (!inventory.HasFreeSlot())
        {
            Debug.Log("[Cauldron] Inventory full — no room for the crafted item.");
            return;
        }

        inventory.AddItem(recipe.resultItem);
        Debug.Log($"[Cauldron] Crafted: {recipe.resultItem.itemName}!");

        if (GameProgressManager.Instance != null && recipe.resultItem != null)
            GameProgressManager.Instance.NotifyPotionBrewed(recipe.resultItem.itemID);
    }

    private void PrintMissing(CraftingRecipe recipe, HotbarInventory inventory)
    {
        List<string> notDeposited = new List<string>();
        List<string> notCarried   = new List<string>();

        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;
            if (GameProgressManager.Instance != null &&
                GameProgressManager.Instance.DepositedIngredients.Contains(req.itemID))
                continue; // already in cauldron

            if (inventory.HasItem(req.itemID))
                notDeposited.Add(req.itemName); // has it but something went wrong
            else
                notCarried.Add(req.itemName);   // doesn't have it yet
        }

        if (notCarried.Count > 0)
            Debug.Log($"[Cauldron] Still need to find: {string.Join(", ", notCarried)}");
        if (notDeposited.Count > 0)
            Debug.Log($"[Cauldron] In inventory but not yet deposited: {string.Join(", ", notDeposited)}");
    }

    public string GetInteractionText() => "Press F to use cauldron";
}
