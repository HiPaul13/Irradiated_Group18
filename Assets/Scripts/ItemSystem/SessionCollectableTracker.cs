using System.Collections.Generic;

/// <summary>
/// Tracks items that are currently in the player's inventory but NOT yet deposited
/// into the cauldron or installed on the car.
///
/// These are "session-only" items. If the player dies before depositing/installing,
/// they are NOT registered as permanently collected, so they respawn in the world.
///
/// Flow:
///   CollectableItem.Interact  → TrackPickup(itemID, saveID)
///   CauldronCraftingStation   → GetSaveID → RegisterCollectedObject → RemoveTrack
///   CarRepairStation          → GetSaveID → RegisterCollectedObject → RemoveTrack
///   On death (CheckpointManager.RespawnPlayer) → Clear()
/// </summary>
public static class SessionCollectableTracker
{
    private static readonly Dictionary<string, string> sessionItems = new Dictionary<string, string>();

    /// <summary>Call when the player picks up an item (before depositing).</summary>
    public static void TrackPickup(string itemID, string saveID)
    {
        if (string.IsNullOrEmpty(itemID) || string.IsNullOrEmpty(saveID)) return;
        sessionItems[itemID] = saveID;
    }

    /// <summary>Returns the saveID for this itemID, or null if not tracked.</summary>
    public static string GetSaveID(string itemID)
    {
        return sessionItems.TryGetValue(itemID, out string id) ? id : null;
    }

    /// <summary>Call after the item has been permanently deposited/installed.</summary>
    public static void RemoveTrack(string itemID)
    {
        sessionItems.Remove(itemID);
    }

    /// <summary>Call on death — all session items are lost.</summary>
    public static void Clear()
    {
        sessionItems.Clear();
    }
}
