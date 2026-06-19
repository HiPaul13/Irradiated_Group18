using UnityEngine;

public class SafeZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        RadiationManager radiationManager = other.GetComponent<RadiationManager>();

        if (radiationManager != null)
        {
            radiationManager.EnterSafeZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        RadiationManager radiationManager = other.GetComponent<RadiationManager>();

        if (radiationManager != null)
        {
            radiationManager.ExitSafeZone();
        }
    }
}