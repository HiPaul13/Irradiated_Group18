using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCarController : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 18f;
    public float acceleration = 12f;
    public float brakePower = 20f;
    public float naturalDeceleration = 4f;

    [Header("Steering")]
    public float turnSpeed = 90f;
    public float minTurnSpeedFactor = 0.25f;

    private float currentSpeed = 0f;

    void Update()
    {
        float inputMove = 0f;
        float inputTurn = 0f;

        if (Keyboard.current.wKey.isPressed) inputMove = 1f;
        if (Keyboard.current.sKey.isPressed) inputMove = -1f;
        if (Keyboard.current.aKey.isPressed) inputTurn = -1f;
        if (Keyboard.current.dKey.isPressed) inputTurn = 1f;

        HandleMovement(inputMove);
        HandleSteering(inputTurn);

        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    void HandleMovement(float inputMove)
    {
        if (inputMove > 0)
        {
            currentSpeed += acceleration * Time.deltaTime;
        }
        else if (inputMove < 0)
        {
            if (currentSpeed > 0)
                currentSpeed -= brakePower * Time.deltaTime;
            else
                currentSpeed -= acceleration * Time.deltaTime;
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                naturalDeceleration * Time.deltaTime
            );
        }

        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed * 0.5f, maxSpeed);
    }

    void HandleSteering(float inputTurn)
    {
        float speedFactor = Mathf.Abs(currentSpeed) / maxSpeed;
        speedFactor = Mathf.Clamp(speedFactor, minTurnSpeedFactor, 1f);

        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            transform.Rotate(
                Vector3.up,
                inputTurn * turnSpeed * speedFactor * Time.deltaTime
            );
        }
    }
}