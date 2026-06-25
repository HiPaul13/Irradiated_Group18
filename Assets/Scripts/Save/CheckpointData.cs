using System;
using System.Collections.Generic;

/// <summary>
/// Minimal data written to disk after each real progress event (ingredient deposited,
/// car part installed). Intentionally does NOT store player position or inventory —
/// those are discarded on death.
/// </summary>
[Serializable]
public class CheckpointData
{
    public int  progressStage;

    public List<string> depositedIngredientIDs   = new List<string>();
    public bool         potionBrewed;

    public List<string> insertedCarPartIDs        = new List<string>();
    public bool         carRepaired;

    /// <summary>
    /// saveIDs of world objects that are permanently gone (deposited into the cauldron
    /// or installed on the car). Items that were only picked up (and then lost to death)
    /// are NOT in this list, so they respawn when the scene reloads.
    /// </summary>
    public List<string> permanentCollectedSaveIDs = new List<string>();
}
