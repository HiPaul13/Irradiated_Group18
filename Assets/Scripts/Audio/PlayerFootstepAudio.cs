using UnityEngine;

public class PlayerFootstepAudio : MonoBehaviour
{
    public AK.Wwise.Event forestFootsteps;
    public AK.Wwise.Event lakeFootsteps;

    public Rigidbody playerRigidbody;

    public float minSpeed = 0.2f;

    public float forestStepInterval = 0.45f;
    public float lakeStepInterval = 0.8f;

    private AK.Wwise.Event currentFootstepEvent;
    private float stepTimer;

    private void Awake()
    {
        currentFootstepEvent = forestFootsteps;
    }

    private void Update()
    {
        if (playerRigidbody == null || currentFootstepEvent == null)
            return;

        Vector3 horizontalVelocity = playerRigidbody.linearVelocity;
        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude < minSpeed)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer += Time.deltaTime;

        float currentInterval =
            currentFootstepEvent == lakeFootsteps
            ? lakeStepInterval
            : forestStepInterval;

        if (stepTimer >= currentInterval)
        {
            currentFootstepEvent.Post(gameObject);
            stepTimer = 0f;
        }
    }

    public void SetForestFootsteps()
    {
        Debug.Log("Set footsteps: FOREST");
        currentFootstepEvent = forestFootsteps;
    }

    public void SetLakeFootsteps()
    {
        Debug.Log("Set footsteps: LAKE");
        currentFootstepEvent = lakeFootsteps;
    }
}