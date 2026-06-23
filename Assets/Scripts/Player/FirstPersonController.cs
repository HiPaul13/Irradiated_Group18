using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraHolder;
    public Camera playerCamera;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 80f;
    public bool lockCursor = true;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float maxVelocityChange = 10f;

    [Header("Radiation Effect")]
    public bool showInvertedDebug = false;
    private bool isMovementInverted = false;

    [Header("Step Climbing")]
    public float stepHeight     = 0.35f;  // max step the player can climb (metres)
    public float stepCheckDist  = 0.45f;  // how far ahead to probe for a step
    public float stepSmooth     = 0.12f;  // how much to lift per FixedUpdate tick

    [Header("Jump")]
    public bool enableJump = true;
    public float jumpPower = 5f;
    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    [Header("Radiation Sprint Block")]
    public bool canSprint = true;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintHeld;
    private bool jumpPressed;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (cameraHolder == null && playerCamera != null)
        {
            cameraHolder = playerCamera.transform.parent;
        }
    }

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        yaw = transform.eulerAngles.y;
        pitch = 0f;
    }

    private void Update()
    {
        ApplyLookInput();
        ApplyCameraPitch();

        if (jumpPressed && enableJump && IsGrounded())
        {
            Jump();
        }

        jumpPressed = false;
    }

    private void FixedUpdate()
    {
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));
        HandleMovement();
        ClimbStep();
    }

    private void ApplyLookInput()
    {
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        lookInput = Vector2.zero;
    }

    private void ApplyCameraPitch()
    {
        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    public void SyncRotationFromTransform()
    {
        yaw = transform.eulerAngles.y;

        if (cameraHolder != null)
        {
            pitch = cameraHolder.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;
        }
        else
        {
            pitch = 0f;
        }

        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
    }

    private void HandleMovement()
    {
        Vector2 finalMoveInput = moveInput;

        if (isMovementInverted)
        {
            finalMoveInput *= -1f;
        }

        Vector3 inputDirection = new Vector3(finalMoveInput.x, 0f, finalMoveInput.y);
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 moveDirection = flatRotation * inputDirection;
        moveDirection.y = 0f;
        moveDirection.Normalize();

        float currentSpeed = (sprintHeld && canSprint) ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = moveDirection * currentSpeed;

        Vector3 currentVelocity = rb.linearVelocity;

        Vector3 velocityChange = targetVelocity - currentVelocity;

        velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
        velocityChange.y = 0f;

        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void ClimbStep()
    {
        // Only try to step-up when the player is actually moving
        if (moveInput.sqrMagnitude < 0.01f) return;

        Vector3 moveDir = (Quaternion.Euler(0f, yaw, 0f) *
                           new Vector3(moveInput.x, 0f, moveInput.y)).normalized;

        // Lower ray — is there a low obstacle directly ahead?
        bool blockedLow = Physics.Raycast(
            transform.position + Vector3.up * 0.05f,
            moveDir, stepCheckDist, groundMask, QueryTriggerInteraction.Ignore);

        if (!blockedLow) return;

        // Upper ray — is the space above the step clear?
        bool blockedHigh = Physics.Raycast(
            transform.position + Vector3.up * (stepHeight + 0.05f),
            moveDir, stepCheckDist, groundMask, QueryTriggerInteraction.Ignore);

        if (blockedHigh) return;

        // Lift the rigidbody smoothly over the step
        rb.position += Vector3.up * stepSmooth;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }

    public void SetCanSprint(bool value)
    {
        canSprint = value;
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
    }

    public void SetMovementInverted(bool inverted)
    {
        isMovementInverted = inverted;

        if (showInvertedDebug)
        {
            Debug.Log("Movement inverted: " + inverted);
        }
    }

    public bool IsMovementInverted()
    {
        return isMovementInverted;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.ReadValueAsButton();
    }
}