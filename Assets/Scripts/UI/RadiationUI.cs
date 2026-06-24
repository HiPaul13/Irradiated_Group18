using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RadiationUI : MonoBehaviour
{
    [Header("Needle Angles")]
    [Tooltip("Nadelwinkel bei 0 Prozent.")]
    [SerializeField] private float minimumAngle = -110f;

    [Tooltip("Nadelwinkel bei 100 Prozent.")]
    [SerializeField] private float maximumAngle = 110f;

    [Header("Needle Movement")]
    [Tooltip("Wie weich und träge die Nadel reagiert.")]
    [SerializeField] private float needleSmoothTime = 0.18f;

    [Header("Thresholds")]
    [Range(0f, 1f)]
    [SerializeField] private float greenThreshold = 0.5f;

    [Range(0f, 1f)]
    [SerializeField] private float orangeThreshold = 0.75f;

    [Header("Colors")]
    [SerializeField] private Color greenColor =
        new Color(0.2f, 1f, 0.25f);

    [SerializeField] private Color orangeColor =
        new Color(1f, 0.5f, 0.05f);

    [SerializeField] private Color redColor =
        new Color(1f, 0.08f, 0.04f);

    [Header("Danger Glow")]
    [Range(0f, 1f)]
    [SerializeField] private float glowStart = 0.75f;

    [Range(0f, 1f)]
    [SerializeField] private float maximumGlowOpacity = 0.65f;

    [SerializeField] private float glowPulseSpeed = 4f;

    private UIDocument uiDocument;

    private VisualElement needlePivot;
    private VisualElement needle;
    private VisualElement dangerGlow;
    private Label radiationValueLabel;

    private float targetNormalizedValue;
    private float displayedNormalizedValue;
    private float needleVelocity;

    private bool uiIsReady;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        VisualElement root = uiDocument.rootVisualElement;

        needlePivot =
            root.Q<VisualElement>("needle-pivot");

        needle =
            root.Q<VisualElement>("needle");

        dangerGlow =
            root.Q<VisualElement>("danger-glow");

        radiationValueLabel =
            root.Q<Label>("radiation-value");

        if (needlePivot == null)
        {
            Debug.LogError(
                "RadiationUI: Element 'needle-pivot' wurde nicht gefunden.",
                this
            );

            uiIsReady = false;
            return;
        }

        uiIsReady = true;
        ApplyGaugeVisuals(0f);
    }

    private void Update()
    {
        if (!uiIsReady)
        {
            return;
        }

        displayedNormalizedValue = Mathf.SmoothDamp(
            displayedNormalizedValue,
            targetNormalizedValue,
            ref needleVelocity,
            needleSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

        ApplyGaugeVisuals(displayedNormalizedValue);
    }

    public void UpdateRadiationUI(
        float currentRadiation,
        float maxRadiation
    )
    {
        if (maxRadiation <= 0f)
        {
            targetNormalizedValue = 0f;
        }
        else
        {
            targetNormalizedValue = Mathf.Clamp01(
                currentRadiation / maxRadiation
            );
        }

        if (radiationValueLabel != null)
        {
            int percentage = Mathf.RoundToInt(
                targetNormalizedValue * 100f
            );

            radiationValueLabel.text = percentage + "%";
        }
    }

    private void ApplyGaugeVisuals(float normalizedValue)
    {
        RotateNeedle(normalizedValue);
        UpdateNeedleColor(normalizedValue);
        UpdateDangerGlow(normalizedValue);
    }

    private void RotateNeedle(float normalizedValue)
    {
        float angle = Mathf.Lerp(
            minimumAngle,
            maximumAngle,
            normalizedValue
        );

        needlePivot.style.rotate =
            new StyleRotate(
                new Rotate(
                    new Angle(angle, AngleUnit.Degree)
                )
            );
    }

    private void UpdateNeedleColor(float normalizedValue)
    {
        if (needle == null)
        {
            return;
        }

        if (normalizedValue < greenThreshold)
        {
            needle.style.backgroundColor = greenColor;
        }
        else if (normalizedValue < orangeThreshold)
        {
            needle.style.backgroundColor = orangeColor;
        }
        else
        {
            needle.style.backgroundColor = redColor;
        }
    }

    private void UpdateDangerGlow(float normalizedValue)
    {
        if (dangerGlow == null)
        {
            return;
        }

        if (normalizedValue < glowStart)
        {
            dangerGlow.style.opacity = 0f;
            return;
        }

        float dangerStrength = Mathf.InverseLerp(
            glowStart,
            1f,
            normalizedValue
        );

        float pulse =
            0.75f +
            Mathf.Sin(
                Time.unscaledTime * glowPulseSpeed
            ) * 0.25f;

        float opacity =
            dangerStrength *
            maximumGlowOpacity *
            pulse;

        dangerGlow.style.opacity = Mathf.Clamp01(opacity);
    }
}