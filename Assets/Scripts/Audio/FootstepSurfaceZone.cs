using UnityEngine;

public class FootstepSurfaceZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerFootstepAudio footsteps = other.GetComponentInParent<PlayerFootstepAudio>();

        if (footsteps == null) return;

        Debug.Log("Set footsteps: LAKE");
        footsteps.SetLakeFootsteps();
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerFootstepAudio footsteps = other.GetComponentInParent<PlayerFootstepAudio>();

        if (footsteps == null) return;

        Debug.Log("Set footsteps: FOREST");
        footsteps.SetForestFootsteps();
    }
}