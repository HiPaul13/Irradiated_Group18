/// <summary>
/// Optional interface for scene objects that want to participate in the save system.
/// CollectableItem uses saveID directly instead, but this interface can be useful
/// for interactable stations (repair stations, crafting stations) that carry runtime state.
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// A stable, unique string identifying this object across sessions.
    /// Used by SaveGameManager to map saved data back to the correct scene object.
    /// </summary>
    string GetSaveID();
}
