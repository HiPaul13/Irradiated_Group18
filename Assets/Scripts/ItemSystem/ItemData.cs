using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemID;
    public string itemName;
    public Sprite icon;
}