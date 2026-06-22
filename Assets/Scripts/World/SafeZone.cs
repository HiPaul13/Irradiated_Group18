using UnityEngine;

/// <summary>
/// Attach to a trigger collider covering the cabin interior.
/// Notifies RadiationManager, Monster_Movement, and EnemyTeleportManager
/// whenever the player enters or exits the safe zone.
/// </summary>
public class SafeZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        RadiationManager radiation = other.GetComponent<RadiationManager>();
        if (radiation != null) radiation.EnterSafeZone();

        Monster_Movement monster = FindObjectOfType<Monster_Movement>();
        if (monster != null) monster.SetPlayerSafe(true);

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.SetPlayerInSafeZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        RadiationManager radiation = other.GetComponent<RadiationManager>();
        if (radiation != null) radiation.ExitSafeZone();

        Monster_Movement monster = FindObjectOfType<Monster_Movement>();
        if (monster != null) monster.SetPlayerSafe(false);

        if (EnemyTeleportManager.Instance != null)
            EnemyTeleportManager.Instance.SetPlayerInSafeZone(false);
    }
}
