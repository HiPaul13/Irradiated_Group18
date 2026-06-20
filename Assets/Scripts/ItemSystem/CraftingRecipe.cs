using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Crafting/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Recipe")]
    public string recipeName;

    [Header("Required Items")]
    public List<ItemData> requiredItems = new List<ItemData>();

    [Header("Result")]
    public ItemData resultItem;
}