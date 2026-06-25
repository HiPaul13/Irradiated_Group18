using System.Collections;
using UnityEngine;

public class IndoorFootstepAudio : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event indoorFootsteps;
    [SerializeField] private Rigidbody      playerRigidbody;
    [SerializeField] private float          minSpeed     = 0.2f;
    [SerializeField] private float          stepInterval = 0.45f;

    private Coroutine footstepCoroutine;

    private void Awake()
    {
        if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        footstepCoroutine = StartCoroutine(FootstepLoop());
    }

    private void OnDisable()
    {
        if (footstepCoroutine != null) StopCoroutine(footstepCoroutine);
    }

    private IEnumerator FootstepLoop()
    {
        var wait = new WaitForSeconds(stepInterval);

        while (true)
        {
            yield return wait;

            if (playerRigidbody == null || indoorFootsteps == null) continue;

            Vector3 hVel = playerRigidbody.linearVelocity;
            hVel.y = 0f;

            if (hVel.magnitude >= minSpeed)
                indoorFootsteps.Post(gameObject);
        }
    }
}
