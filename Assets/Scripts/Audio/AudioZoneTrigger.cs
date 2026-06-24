using UnityEngine;

public class AudioZoneTrigger : MonoBehaviour
{
    public AK.Wwise.Event playZoneEvent;
    public AK.Wwise.Event stopZoneEvent;

    [Header("Optional Global Events")]
    public AK.Wwise.Event onEnterExtraEvent;
    public AK.Wwise.Event onExitExtraEvent;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        onEnterExtraEvent?.Post(gameObject);
        playZoneEvent?.Post(gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        stopZoneEvent?.Post(gameObject);
        onExitExtraEvent?.Post(gameObject);
    }
}