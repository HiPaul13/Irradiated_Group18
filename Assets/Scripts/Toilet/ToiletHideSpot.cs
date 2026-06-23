using UnityEngine;

public class ToiletHideSpot : MonoBehaviour, IInteractable
{
    [Header("Toilet Positions")]
    [Tooltip("Position und Rotation des Players innerhalb der Toilette.")]
    public Transform hidePoint;

    [Tooltip("Position und Rotation des Players nach dem Verlassen.")]
    public Transform exitPoint;

    [Header("Inside Interaction")]
    [Tooltip("Unsichtbarer Collider vor der Kamera, der nur während des Versteckens aktiv ist.")]
    public GameObject insideExitTarget;

    [Header("Debug")]
    public bool showDebugMessages = true;

    private PlayerInteraction hiddenPlayer;
    private FirstPersonController playerController;
    private Rigidbody playerRigidbody;
    private CapsuleCollider playerCapsule;
    private PlayerHideState playerHideState;

    private bool previousControllerEnabled;
    private bool previousCapsuleEnabled;
    private bool previousIsKinematic;
    private bool previousUseGravity;

    public bool IsOccupied => hiddenPlayer != null;

    private void Awake()
    {
        // Dieser Collider soll außen nicht im Weg sein.
        if (insideExitTarget != null)
        {
            insideExitTarget.SetActive(false);
        }
    }

    public void Interact(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null)
        {
            return;
        }

        // Toilette ist noch frei: Player hineinsetzen.
        if (hiddenPlayer == null)
        {
            EnterToilet(playerInteraction);
            return;
        }

        // Derselbe Player sitzt bereits darin: Toilette verlassen.
        if (hiddenPlayer == playerInteraction)
        {
            ExitToilet();
            return;
        }

        // Nur relevant, falls später mehrere Player möglich wären.
        if (showDebugMessages)
        {
            Debug.Log("Diese Toilette ist bereits besetzt.");
        }
    }

    private void EnterToilet(PlayerInteraction playerInteraction)
    {
        if (hidePoint == null)
        {
            Debug.LogError(
                "ToiletHideSpot: HidePoint wurde nicht zugewiesen.",
                this
            );
            return;
        }

        if (exitPoint == null)
        {
            Debug.LogError(
                "ToiletHideSpot: ExitPoint wurde nicht zugewiesen.",
                this
            );
            return;
        }

        if (insideExitTarget == null)
        {
            Debug.LogError(
                "ToiletHideSpot: InsideExitTarget wurde nicht zugewiesen.",
                this
            );
            return;
        }

        hiddenPlayer = playerInteraction;

        playerController =
            hiddenPlayer.GetComponent<FirstPersonController>();

        playerRigidbody =
            hiddenPlayer.GetComponent<Rigidbody>();

        playerCapsule =
            hiddenPlayer.GetComponent<CapsuleCollider>();

        playerHideState =
            hiddenPlayer.GetComponent<PlayerHideState>();

        /*
         * Die bisherigen Zustände werden gespeichert,
         * damit sie beim Verlassen korrekt wiederhergestellt werden.
         */
        if (playerController != null)
        {
            previousControllerEnabled = playerController.enabled;

            // Movement und Kamerabewegung vollständig stoppen.
            playerController.enabled = false;
        }

        if (playerCapsule != null)
        {
            previousCapsuleEnabled = playerCapsule.enabled;

            /*
             * Innerhalb der Toilette deaktivieren wir den Player-Collider.
             * Dadurch wird der Player nicht aus dem Toilettenmodell
             * herausgedrückt.
             */
            playerCapsule.enabled = false;
        }

        if (playerRigidbody != null)
        {
            previousIsKinematic = playerRigidbody.isKinematic;
            previousUseGravity = playerRigidbody.useGravity;

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;

            /*
             * Kinematic verhindert Bewegung durch Gravitation,
             * Kollisionen oder andere Kräfte.
             */
            playerRigidbody.useGravity = false;
            playerRigidbody.isKinematic = true;
        }

        SetPlayerPose(hidePoint);
        ResetCameraPitch();

        if (playerHideState != null)
        {
            playerHideState.SetHidden(true, this);
        }

        /*
         * Aktiviert einen Collider direkt vor der versteckten Kamera.
         * Der PlayerInteraction-Raycast kann ihn treffen und dadurch
         * dieselbe Toilette erneut mit F ansprechen.
         */
        insideExitTarget.SetActive(true);

        if (showDebugMessages)
        {
            Debug.Log("Player versteckt sich in: " + gameObject.name);
        }
    }

    private void ExitToilet()
    {
        if (hiddenPlayer == null)
        {
            return;
        }

        /*
         * Zuerst den inneren Interaktions-Collider deaktivieren,
         * damit er draußen nicht getroffen wird.
         */
        if (insideExitTarget != null)
        {
            insideExitTarget.SetActive(false);
        }

        SetPlayerPose(exitPoint);
        ResetCameraPitch();

        if (playerCapsule != null)
        {
            playerCapsule.enabled = previousCapsuleEnabled;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = previousIsKinematic;
            playerRigidbody.useGravity = previousUseGravity;

            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (playerHideState != null)
        {
            playerHideState.SetHidden(false, null);
        }

        if (playerController != null)
        {
            /*
             * Synchronisiert den internen Yaw/Pitch-Wert deines Controllers
             * mit der Rotation des ExitPoints.
             */
            playerController.SyncRotationFromTransform();
            playerController.enabled = previousControllerEnabled;
        }

        if (showDebugMessages)
        {
            Debug.Log("Player verlässt: " + gameObject.name);
        }

        hiddenPlayer = null;
        playerController = null;
        playerRigidbody = null;
        playerCapsule = null;
        playerHideState = null;
    }

    private void SetPlayerPose(Transform targetPoint)
    {
        if (targetPoint == null || hiddenPlayer == null)
        {
            return;
        }

        if (playerRigidbody != null)
        {
            playerRigidbody.position = targetPoint.position;
            playerRigidbody.rotation = targetPoint.rotation;
        }

        hiddenPlayer.transform.SetPositionAndRotation(
            targetPoint.position,
            targetPoint.rotation
        );

        Physics.SyncTransforms();
    }

    private void ResetCameraPitch()
    {
        if (playerController != null)
        {
            /*
             * Dadurch schaut der Player exakt in die Vorwärtsrichtung
             * des HidePoints beziehungsweise ExitPoints.
             */
            if (playerController.cameraHolder != null)
            {
                playerController.cameraHolder.localRotation =
                    Quaternion.identity;
            }
            else if (playerController.playerCamera != null)
            {
                playerController.playerCamera.transform.localRotation =
                    Quaternion.identity;
            }

            return;
        }

        if (hiddenPlayer != null &&
            hiddenPlayer.playerCamera != null)
        {
            hiddenPlayer.playerCamera.transform.localRotation =
                Quaternion.identity;
        }
    }

    public string GetInteractionText()
    {
        if (hiddenPlayer != null)
        {
            return "Press F to leave toilet";
        }

        return "Press F to hide in toilet";
    }

    public bool IsPlayerInside(PlayerInteraction player)
    {
        return hiddenPlayer == player;
    }

    private void OnDrawGizmosSelected()
    {
        if (hidePoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hidePoint.position, 0.12f);
            Gizmos.DrawLine(
                hidePoint.position,
                hidePoint.position + hidePoint.forward
            );
        }

        if (exitPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(exitPoint.position, 0.12f);
            Gizmos.DrawLine(
                exitPoint.position,
                exitPoint.position + exitPoint.forward
            );
        }
    }
}