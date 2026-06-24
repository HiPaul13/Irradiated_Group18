using System.Collections.Generic;
using UnityEngine;

public class CauldronCraftingStation : MonoBehaviour, IInteractable
{
    [Header("Recipes")]
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();

    // Used when GameProgressManager is not in the scene (e.g. playing HouseInterior directly).
    private readonly HashSet<string> localDepositedIngredients = new HashSet<string>();

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

            // Always try to deposit first — one plant per press.
            if (TryDepositOne(recipe, inventory))
                return;

            // Craft only when everything is in the cauldron and not still in the bag.
            if (AllDeposited(recipe) && !PlayerCarriesRecipeIngredient(recipe, inventory))
            {
                Craft(recipe, inventory);
                return;
            }
        }

        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe == null) continue;
            PrintMissing(recipe, inventory);
        }
    }

    private bool AllDeposited(CraftingRecipe recipe)
    {
        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;
            if (!IsIngredientDeposited(req.itemID))
                return false;
        }

        return true;
    }

    private bool TryDepositOne(CraftingRecipe recipe, HotbarInventory inventory)
    {
        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;

            if (!inventory.HasItem(req.itemID))
                continue;

            // Stale progress: marked deposited but player still carries the plant.
            if (IsIngredientDeposited(req.itemID))
                ClearIngredientDeposited(req.itemID);

            inventory.RemoveItem(req.itemID);
            NotifyIngredientDeposited(req.itemID);

            int deposited = GetDepositedCount();
            int needed = recipe.requiredItems.Count;

            Debug.Log($"[Cauldron] Deposited '{req.itemName}'. ({deposited}/{needed} ingredients in cauldron)");

            if (AllDeposited(recipe) && !PlayerCarriesRecipeIngredient(recipe, inventory))
                Craft(recipe, inventory);
            else
                Debug.Log($"[Cauldron] {needed - deposited} more ingredient(s) needed.");

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

        if (recipe.resultItem != null && inventory.HasItem(recipe.resultItem.itemID))
        {
            Debug.Log("[Cauldron] You already have the crafted potion.");
            return;
        }

        inventory.AddItem(recipe.resultItem);
        Debug.Log($"[Cauldron] Crafted: {recipe.resultItem.itemName}!");

        if (recipe.resultItem != null)
            NotifyPotionBrewed(recipe.resultItem.itemID);

        CauldronIngredientDisplay display = GetComponent<CauldronIngredientDisplay>();
        if (display != null)
            display.ShowPotionReadyMessage();
    }

    private static bool PlayerCarriesRecipeIngredient(CraftingRecipe recipe, HotbarInventory inventory)
    {
        foreach (ItemData req in recipe.requiredItems)
        {
            if (req != null && inventory.HasItem(req.itemID))
                return true;
        }

        return false;
    }

    private void PrintMissing(CraftingRecipe recipe, HotbarInventory inventory)
    {
        List<string> notCarried = new List<string>();

        foreach (ItemData req in recipe.requiredItems)
        {
            if (req == null) continue;
            if (IsIngredientDeposited(req.itemID))
                continue;

            if (!inventory.HasItem(req.itemID))
                notCarried.Add(req.itemName);
        }

        if (notCarried.Count > 0)
            Debug.Log($"[Cauldron] Still need to find: {string.Join(", ", notCarried)}");
    }

    private bool IsIngredientDeposited(string itemID)
    {
        if (GameProgressManager.Instance != null)
            return GameProgressManager.Instance.DepositedIngredients.Contains(itemID);

        return localDepositedIngredients.Contains(itemID);
    }

    private void NotifyIngredientDeposited(string itemID)
    {
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.NotifyIngredientDeposited(itemID);
        else
            localDepositedIngredients.Add(itemID);
    }

    private void ClearIngredientDeposited(string itemID)
    {
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.ClearIngredientDeposited(itemID);
        else
            localDepositedIngredients.Remove(itemID);
    }

    private void NotifyPotionBrewed(string resultItemID)
    {
        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.NotifyPotionBrewed(resultItemID);
    }

    private int GetDepositedCount()
    {
        if (GameProgressManager.Instance != null)
            return GameProgressManager.Instance.DepositedIngredients.Count;

        return localDepositedIngredients.Count;
    }

    public string GetInteractionText() => "Press F to use cauldron";

    public CraftingRecipe GetPrimaryRecipe()
    {
        foreach (CraftingRecipe recipe in recipes)
        {
            if (recipe != null)
                return recipe;
        }

        return null;
    }
}
