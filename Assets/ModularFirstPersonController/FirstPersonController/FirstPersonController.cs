using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Look")]
    public float mouseSensitivity = 0.12f;
    public float maxLookAngle = 80f;
    public bool lockCursor = true;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float maxVelocityChange = 10f;

    [Header("Jump")]
    public bool enableJump = true;
    public float jumpPower = 5f;
    public float groundCheckDistance = 0.85f;
    public LayerMask groundMask = ~0;

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
    }

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        yaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        HandleLook();

        if (jumpPressed && enableJump && IsGrounded())
        {
            Jump();
        }

        jumpPressed = false;
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleLook()
    {
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector3 targetVelocity = new Vector3(moveInput.x, 0f, moveInput.y);
        targetVelocity = Vector3.ClampMagnitude(targetVelocity, 1f);

        float currentSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        targetVelocity = transform.TransformDirection(targetVelocity) * currentSpeed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 velocityChange = targetVelocity - velocity;

        velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
        velocityChange.y = 0f;

        rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
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