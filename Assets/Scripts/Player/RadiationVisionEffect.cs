using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadiationVisionEffect : MonoBehaviour
{
    [Header("Post Processing")]
    public Volume volume;

    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private FilmGrain filmGrain;
    private ColorAdjustments colorAdjustments;

    [Header("Effect Strength")]
    public float maxVignetteIntensity = 0.55f;
    public float maxChromaticAberration = 0.8f;
    public float maxFilmGrain = 0.7f;
    public float minSaturation = -45f;
    public float maxContrast = 30f;

    private void Awake()
    {
        if (volume == null)
        {
            volume = GetComponent<Volume>();
        }

        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out chromaticAberration);
            volume.profile.TryGet(out filmGrain);
            volume.profile.TryGet(out colorAdjustments);
        }
    }

    public void SetEffectIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0f, maxVignetteIntensity, intensity);
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Lerp(0f, maxChromaticAberration, intensity);
        }

        if (filmGrain != null)
        {
            float pulse = Mathf.Sin(Time.time * 4f) * 0.15f;
            float pulsedIntensity = Mathf.Lerp(0f, maxFilmGrain, intensity) + pulse * intensity;

            filmGrain.intensity.value = Mathf.Clamp01(pulsedIntensity);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(0f, minSaturation, intensity);
            colorAdjustments.contrast.value = Mathf.Lerp(0f, maxContrast, intensity);
        }
    }
}