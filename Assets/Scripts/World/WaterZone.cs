using UnityEngine;

/// <summary>
/// Attach to the water plane. Add a Box Collider set to Is Trigger.
/// When the player enters the water their Y position is locked to the
/// surface level and movement speed is reduced (swimming feel).
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterZone : MonoBehaviour
{
    [Header("Water Surface")]
    [Tooltip("The Y position the player floats at. Set this to your water plane Y.")]
    public float surfaceY = 4f;

    [Header("Swimming")]
    [Tooltip("How much to slow the player while in water (0.5 = half speed).")]
    public float swimSpeedMultiplier = 0.5f;

    private Transform      playerTransform;
    private Rigidbody      playerRb;
    private FirstPersonController playerController;
    private bool           playerInWater = false;
    private float          originalWalkSpeed;
    private float          originalSprintSpeed;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (!playerInWater || playerRb == null) return;

        // Lock Y position to water surface.
        Vector3 pos = playerRb.position;
        if (pos.y != surfaceY)
        {
            playerRb.position = new Vector3(pos.x, surfaceY, pos.z);

            // Kill any vertical velocity so the player doesn't bounce.
            Vector3 vel = playerRb.linearVelocity;
            playerRb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerTransform  = other.transform;
        playerRb         = other.GetComponent<Rigidbody>();
        playerController = other.GetComponent<FirstPersonController>();

        if (playerController != null)
        {
            originalWalkSpeed   = playerController.walkSpeed;
            originalSprintSpeed = playerController.sprintSpeed;
            playerController.walkSpeed   *= swimSpeedMultiplier;
            playerController.sprintSpeed *= swimSpeedMultiplier;
        }

        playerInWater = true;
        Debug.Log("[Water] Player entered water — locked to Y " + surfaceY);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (playerController != null)
        {
            playerController.walkSpeed   = originalWalkSpeed;
            playerController.sprintSpeed = originalSprintSpeed;
        }

        playerInWater    = false;
        playerRb         = null;
        playerTransform  = null;
        playerController = null;
        Debug.Log("[Water] Player left water — movement restored.");
    }
}