using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the Player GameObject alongside HotbarInventory and RadiationManager.
/// Press G to consume the anti-radiation potion and gain 60 seconds of protection.
/// </summary>
public class RadiationPotionUser : MonoBehaviour
{
    [Header("Potion Settings")]
    [Tooltip("Must match the itemID on the anti-radiation potion ItemData asset.")]
    public string potionItemID = "anti_radiation_potion";

    [Tooltip("How many seconds of radiation protection the potion gives.")]
    public float protectionDuration = 60f;

    [Header("Key")]
    public Key useKey = Key.G;

    private HotbarInventory  inventory;
    private RadiationManager radiationManager;

    private void Awake()
    {
        inventory        = GetComponent<HotbarInventory>();
        radiationManager = GetComponent<RadiationManager>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[useKey].wasPressedThisFrame) return;

        TryUsePotion();
    }

    private void TryUsePotion()
    {
        if (inventory == null)
        {
            Debug.LogWarning("[PotionUser] No HotbarInventory found on player.");
            return;
        }

        if (!inventory.HasItem(potionItemID))
        {
            Debug.Log("[PotionUser] No radiation potion in inventory.");
            return;
        }

        if (radiationManager == null)
        {
            Debug.LogWarning("[PotionUser] No RadiationManager found on player.");
            return;
        }

        if (radiationManager.IsProtected)
        {
            Debug.Log("[PotionUser] Radiation protection is already active.");
            return;
        }

        inventory.RemoveItem(potionItemID);
        radiationManager.ActivateProtection(protectionDuration);

        Debug.Log($"[PotionUser] Radiation potion used! Protected for {protectionDuration}s.");
    }
}
