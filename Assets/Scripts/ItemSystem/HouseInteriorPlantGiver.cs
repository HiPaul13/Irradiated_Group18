using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives all cauldron plants to the player when the house interior scene loads.
/// </summary>
[RequireComponent(typeof(HotbarInventory))]
public class HouseInteriorPlantGiver : MonoBehaviour
{
    [SerializeField] private List<ItemData> plantItems = new List<ItemData>();

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "HouseInterior")
            return;

        GiveAllPlants();
    }

    private void GiveAllPlants()
    {
        HotbarInventory inventory = GetComponent<HotbarInventory>();
        if (inventory == null)
            return;

        if (GameProgressManager.Instance != null)
            GameProgressManager.Instance.ClearAllIngredientDeposits();

        foreach (ItemData plant in plantItems)
        {
            if (plant == null)
                continue;

            if (!inventory.AddItem(plant))
                continue;

            if (GameProgressManager.Instance != null)
                GameProgressManager.Instance.NotifyItemCollected(plant.itemID);
        }
    }
}
