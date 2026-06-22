using UnityEngine;

/// <summary>
/// An interactable world object the player can walk up to and press F to save.
///
/// Setup:
///   1. Create a GameObject in the scene (e.g. a lantern, a chest, a notebook).
///   2. Add a Collider and make sure it is reachable by the PlayerInteraction raycast.
///   3. Attach this script.
///   4. Optionally set stationName for the interaction prompt text.
/// </summary>
public class SaveStation : MonoBehaviour, IInteractable
{
    [Header("Save Station")]
    public string stationName = "Save Point";

    public void Interact(PlayerInteraction playerInteraction)
    {
        if (SaveGameManager.Instance == null)
        {
            Debug.LogWarning("[SaveStation] No SaveGameManager found in scene.");
            return;
        }

        SaveGameManager.Instance.SaveGame();
        Debug.Log($"[SaveStation] Game saved at '{stationName}'.");
    }

    public string GetInteractionText()
    {
        return $"Press F to save game ({stationName})";
    }
}
