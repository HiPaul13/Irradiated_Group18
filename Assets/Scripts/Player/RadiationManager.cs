using System.Collections;
using UnityEngine;

public class RadiationManager : MonoBehaviour
{
    [Header("Radiation Values")]
    public float currentRadiation = 0f;
    public float maxRadiation = 100f;

    [Header("Default Map Radiation")]
    public float defaultRadiationGainPerSecond = 1f;

    [Header("Current Zone Modifier")]
    public float currentZoneMultiplier = 1f;

    [Header("Safe Zone")]
    public bool isInSafeZone = false;
    public float safeZoneDecreasePerSecond = 2f;

    [Header("Vision Effect Thresholds")]
    public float visionEffectStart = 75f;
    public float heavyVisionEffectStart = 100f;

    [Header("Movement Inversion")]
    public float invertMovementStart = 100f;
    public float minTimeBetweenInverts = 2f;
    public float maxTimeBetweenInverts = 10f;
    public float invertDuration = 1.5f;

    [Header("References")]
    public FirstPersonController playerMovement;
    public RadiationUI radiationUI;
    public RadiationVisionEffect visionEffect;

    private bool  invertRoutineRunning = false;
    private bool  isProtected          = false;
    private float protectionEndTime    = 0f;

    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<FirstPersonController>();
        }
    }

    private void Update()
    {
        UpdateRadiation();
        UpdateUI();
        UpdateVisionEffects();
        HandleMovementInversion();
        UpdateSprintBlock();
    }

    private void UpdateRadiation()
    {
        // Check if protection has expired.
        if (isProtected && Time.time >= protectionEndTime)
        {
            isProtected = false;
            Debug.Log("[Radiation] Potion protection expired — radiation is active again.");
        }

        if (isInSafeZone)
        {
            currentRadiation -= safeZoneDecreasePerSecond * Time.deltaTime;
        }
        else if (!isProtected)
        {
            float radiationGain = defaultRadiationGainPerSecond * currentZoneMultiplier * Time.deltaTime;
            currentRadiation += radiationGain;
        }

        currentRadiation = Mathf.Clamp(currentRadiation, 0f, maxRadiation);
    }

    /// <summary>Blocks radiation gain for the given number of seconds.</summary>
    public void ActivateProtection(float duration)
    {
        isProtected       = true;
        protectionEndTime = Time.time + duration;
        Debug.Log($"[Radiation] Protection active for {duration}s. Radiation blocked until {protectionEndTime:F1}s.");
    }

    public bool IsProtected => isProtected;

    private void UpdateUI()
    {
        if (radiationUI != null)
        {
            radiationUI.UpdateRadiationUI(currentRadiation, maxRadiation);
        }
    }

    private void UpdateVisionEffects()
    {
        if (visionEffect == null) return;

        float intensity = 0f;

        if (currentRadiation >= visionEffectStart)
        {
            intensity = Mathf.InverseLerp(visionEffectStart, maxRadiation, currentRadiation);
        }

        visionEffect.SetEffectIntensity(intensity);
    }

    private void HandleMovementInversion()
    {
        if (currentRadiation >= invertMovementStart && !invertRoutineRunning)
        {
            StartCoroutine(InvertMovementRoutine());
        }
    }

    private IEnumerator InvertMovementRoutine()
    {
        invertRoutineRunning = true;

        while (currentRadiation >= invertMovementStart)
        {
            float waitTime = Random.Range(minTimeBetweenInverts, maxTimeBetweenInverts);
            yield return new WaitForSeconds(waitTime);

            if (playerMovement != null)
            {
                playerMovement.SetMovementInverted(true);
            }

            yield return new WaitForSeconds(invertDuration);

            if (playerMovement != null)
            {
                playerMovement.SetMovementInverted(false);
            }
        }

        if (playerMovement != null)
        {
            playerMovement.SetMovementInverted(false);
        }

        invertRoutineRunning = false;
    }

    public void SetZoneMultiplier(float multiplier)
    {
        currentZoneMultiplier = multiplier;
    }

    public void ResetZoneMultiplier()
    {
        currentZoneMultiplier = 1f;
    }

   public void EnterSafeZone()
    {
        isInSafeZone = true;
    }

    public void ExitSafeZone()
    {
        isInSafeZone = false;
    }

    public void ReduceRadiation(float amount)
    {
        currentRadiation -= amount;
        currentRadiation = Mathf.Clamp(currentRadiation, 0f, maxRadiation);
    }

    private void UpdateSprintBlock()
    {
        if (playerMovement == null) return;

        bool shouldAllowSprint = currentRadiation < 100f;
        playerMovement.SetCanSprint(shouldAllowSprint);
    }
}