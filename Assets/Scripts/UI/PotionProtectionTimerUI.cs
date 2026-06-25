using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space countdown shown while radiation potion protection is active.
/// Uses its own overlay canvas so it never attaches to scene UI by mistake.
/// </summary>
[DisallowMultipleComponent]
public class PotionProtectionTimerUI : MonoBehaviour
{
    [SerializeField] private Vector2 offsetFromBottomRight = new Vector2(-24f, 24f);
    [SerializeField] private Vector2 panelSize = new Vector2(160f, 32f);

    private RadiationManager radiationManager;
    private GameObject panel;
    private TMP_Text timerText;

    private void Awake()
    {
        radiationManager = GetComponent<RadiationManager>();
        BuildUI();
    }

    private void Update()
    {
        if (radiationManager == null || panel == null || timerText == null)
            return;

        float remaining = radiationManager.RemainingProtectionSeconds;
        if (remaining <= 0f)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);
        timerText.text = "Potion: " + Mathf.CeilToInt(remaining) + "s";
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("PotionTimerCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        panel = new GameObject("PotionProtectionTimer");
        panel.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = offsetFromBottomRight;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.08f, 0.24f, 0.14f, 0.88f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        timerText = textObject.AddComponent<TextMeshProUGUI>();
        timerText.fontSize = 18;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = new Color(0.67f, 1f, 0.75f, 1f);
        timerText.raycastTarget = false;

        panel.SetActive(false);
    }
}
