using System;
using System.Collections.Generic;

/// <summary>
/// Pure data container written to / read from JSON by SaveGameManager.
/// All fields must be serializable by JsonUtility (primitives and Lists of primitives).
/// </summary>
[Serializable]
public class GameSaveData
{
    // ── Player ───────────────────────────────────────────────────────────────

    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;
    public float playerRotY;       // Y-axis euler angle only (FPS camera)
    public float currentRadiation;

    // ── Inventory ────────────────────────────────────────────────────────────

    /// <summary>itemID of each item currently in the hotbar.</summary>
    public List<string> inventoryItemIDs = new List<string>();

    // ── Game progress ────────────────────────────────────────────────────────

    /// <summary>Serialized as int so JsonUtility handles it correctly.</summary>
    public int progressStage;

    public List<string> collectedIngredientIDs  = new List<string>();
    public List<string> depositedIngredientIDs  = new List<string>(); // ingredients put into cauldron
    public bool         potionBrewed;
    public List<string> collectedCarPartIDs    = new List<string>();
    public List<string> insertedCarPartIDs     = new List<string>();
    public bool         carRepaired;

    // ── World — which pickups have been destroyed ────────────────────────────

    /// <summary>
    /// saveID values from every CollectableItem that has been picked up.
    /// Objects whose saveID appears here will not respawn after loading.
    /// </summary>
    public List<string> collectedObjectSaveIDs = new List<string>();

    // ── Enemy (optional) ─────────────────────────────────────────────────────

    public float enemyPosX;
    public float enemyPosY;
    public float enemyPosZ;

    /// <summary>Serialized value of Monster_Movement.MonsterState.</summary>
    public int enemyState;
}
