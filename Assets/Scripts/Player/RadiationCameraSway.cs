using UnityEngine;
using UnityEngine.SceneManagement;

public class RadiationCameraSway : MonoBehaviour
{
    [Header("References")]
    public RadiationManager radiationManager;

    [Header("Sway Settings")]
    public float startRadiation = 90f;
    public float maxRadiation = 100f;

    public float maxSwayAmount = 0.15f;
    public float swaySpeed = 2.5f;

    private Vector3 startLocalPosition;

    private void Start()
    {
        startLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (radiationManager == null)
            return;

        if (SceneManager.GetActiveScene().name == "HouseInterior")
        {
            transform.localPosition = startLocalPosition;
            return;
        }

        float intensity = Mathf.InverseLerp(
            startRadiation,
            maxRadiation,
            radiationManager.currentRadiation
        );

        if (intensity <= 0f)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                startLocalPosition,
                Time.deltaTime * 5f
            );

            return;
        }

        float x = Mathf.Sin(Time.time * swaySpeed) * maxSwayAmount * intensity;
        float y = Mathf.Cos(Time.time * swaySpeed * 0.7f) * maxSwayAmount * intensity;

        transform.localPosition = startLocalPosition + new Vector3(x, y, 0f);
    }
}