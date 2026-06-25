using System.Collections.Generic;

public static class SceneTransitionState
{
    public static string NextSpawnPointId { get; set; }
    public static bool IsTransitioning { get; set; }
    public static List<string> TransitionInventory { get; private set; }

    public static void SetTransitionInventory(List<string> itemIDs)
    {
        TransitionInventory = itemIDs != null ? new List<string>(itemIDs) : null;
    }

    public static void Clear()
    {
        NextSpawnPointId = null;
        IsTransitioning = false;
    }

    public static void ClearTransitionInventory()
    {
        TransitionInventory = null;
    }
}
