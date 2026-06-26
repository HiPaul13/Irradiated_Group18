using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RadiationManager : MonoBehaviour
{
    private const string IndoorSceneName = "HouseInterior";
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

    [Header("Death at Max Radiation")]
    [Tooltip("Wie viele Sekunden der Player bei 100% Radiation überleben kann bevor er stirbt. " +
             "0 = sofortiger Tod.")]
    public float timeAtMaxRadiationBeforeDeath = 5f;

    [Header("References")]
    public FirstPersonController playerMovement;
    public RadiationUI radiationUI;
    public RadiationVisionEffect visionEffect;

    private bool  invertRoutineRunning = false;
    private bool  isProtected          = false;
    private float protectionEndTime    = 0f;
    private bool  isIndoorScene;

    private float   timeAtMaxRadiation = 0f;
    private bool    deathTriggered     = false;
    private PlayerDeath playerDeath;

    private void Awake()
    {
        isIndoorScene = SceneManager.GetActiveScene().name == IndoorSceneName;

        if (playerMovement == null)
        {
            playerMovement = GetComponent<FirstPersonController>();
        }

        if (isIndoorScene)
        {
            radiationUI = null;
            DisableSceneRadiationUI();
        }
        else if (radiationUI == null)
        {
            radiationUI = FindFirstObjectByType<RadiationUI>();
        }

        if (GetComponent<PotionProtectionTimerUI>() == null)
            gameObject.AddComponent<PotionProtectionTimerUI>();

        if (GetComponent<TransitionInventoryRestorer>() == null)
            gameObject.AddComponent<TransitionInventoryRestorer>();
    }

    private static void DisableSceneRadiationUI()
    {
        RadiationUI[] uiElements = FindObjectsByType<RadiationUI>(FindObjectsSortMode.None);
        foreach (RadiationUI ui in uiElements)
        {
            if (ui != null)
                ui.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        playerDeath = GetComponent<PlayerDeath>();
        PotionProtectionState.TryRestore(this);
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
        if (isProtected && Time.time >= protectionEndTime)
        {
            isProtected = false;
            PotionProtectionState.Clear();
            Debug.Log("[Radiation] Potion protection expired — radiation is active again.");
        }

        if (isIndoorScene)
        {
            if (currentRadiation > 0f)
                currentRadiation -= safeZoneDecreasePerSecond * Time.deltaTime;

            currentRadiation = Mathf.Clamp(currentRadiation, 0f, maxRadiation);
            return;
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
        AkSoundEngine.SetRTPCValue("RadiationLevel", currentRadiation);

        CheckRadiationDeath();
    }

    private void CheckRadiationDeath()
    {
        if (deathTriggered || isIndoorScene || isProtected) return;

        if (currentRadiation >= maxRadiation)
        {
            timeAtMaxRadiation += Time.deltaTime;

            if (timeAtMaxRadiation >= timeAtMaxRadiationBeforeDeath)
            {
                deathTriggered = true;
                Debug.Log($"[Radiation] Player bei 100% Radiation für {timeAtMaxRadiation:F1}s — Tod.");
                if (playerDeath == null) playerDeath = GetComponent<PlayerDeath>();
                if (playerDeath != null) playerDeath.KillPlayer();
                else Debug.LogWarning("[Radiation] Kein PlayerDeath Script gefunden.");
            }
        }
        else
        {
            timeAtMaxRadiation = 0f;
        }
    }

    /// <summary>Blocks radiation gain for the given number of seconds and resets current radiation to 0.</summary>
    public void ActivateProtection(float duration)
    {
        isProtected        = true;
        protectionEndTime  = Time.time + duration;
        currentRadiation   = 0f;
        timeAtMaxRadiation = 0f;
        deathTriggered     = false;
        PotionProtectionState.SetProtectionEndTime(protectionEndTime);
        Debug.Log($"[Radiation] Protection active for {duration}s — radiation reset to 0.");
    }

    public void RestoreProtection(float endTime)
    {
        if (Time.time >= endTime)
            return;

        isProtected       = true;
        protectionEndTime = endTime;
        Debug.Log($"[Radiation] Protection restored — {RemainingProtectionSeconds:F0}s remaining.");
    }

    public bool IsProtected => isProtected;

    public float RemainingProtectionSeconds =>
        isProtected ? Mathf.Max(0f, protectionEndTime - Time.time) : 0f;

    private void UpdateUI()
    {
        if (isIndoorScene || radiationUI == null)
            return;

        radiationUI.UpdateRadiationUI(currentRadiation, maxRadiation);
    }

    private void UpdateVisionEffects()
    {
        if (isIndoorScene || visionEffect == null) return;

        float intensity = 0f;

        if (currentRadiation >= visionEffectStart)
        {
            intensity = Mathf.InverseLerp(visionEffectStart, maxRadiation, currentRadiation);
        }

        visionEffect.SetEffectIntensity(intensity);
    }

    private void HandleMovementInversion()
    {
        if (isIndoorScene) return;

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