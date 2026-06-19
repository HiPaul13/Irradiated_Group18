using UnityEngine;

public class RadiationZone : MonoBehaviour
{
    [Header("Radiation Zone Settings")]
    public float radiationMultiplier = 3f;

    private void OnTriggerEnter(Collider other)
    {
        RadiationManager radiationManager = other.GetComponent<RadiationManager>();

        if (radiationManager != null)
        {
            radiationManager.SetZoneMultiplier(radiationMultiplier);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        RadiationManager radiationManager = other.GetComponent<RadiationManager>();

        if (radiationManager != null)
        {
            radiationManager.ResetZoneMultiplier();
        }
    }
}