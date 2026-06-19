using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RadiationUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider radiationSlider;
    public TMP_Text radiationText;

    public void UpdateRadiationUI(float currentRadiation, float maxRadiation)
    {
        float normalizedValue = currentRadiation / maxRadiation;

        if (radiationSlider != null)
        {
            radiationSlider.value = normalizedValue;
        }

        if (radiationText != null)
        {
            radiationText.text = Mathf.RoundToInt(normalizedValue * 100f) + "%";
        }
    }
}