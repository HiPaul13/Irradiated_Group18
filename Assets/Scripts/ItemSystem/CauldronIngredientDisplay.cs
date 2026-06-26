using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows required cauldron ingredients while the player looks at the liquid Sphere mesh.
/// Shows a one-time message when the radiation potion has been brewed.
/// </summary>
[RequireComponent(typeof(CauldronCraftingStation))]
public class CauldronIngredientDisplay : MonoBehaviour
{
    [Header("Look Target")]
    [SerializeField] private string sphereObjectName = "Sphere";
    [SerializeField] private float lookDistance = 4f;
    [Range(0.2f, 1f)]
    [SerializeField] private float lookAtDotThreshold = 0.55f;

    [Header("Potion Message")]
    [SerializeField] private float protectionDurationSeconds = 60f;
    [SerializeField] private float potionMessageDuration = 7f;
    [TextArea(2, 4)]
    [SerializeField] private string potionReadyMessage =
        "Your radiation potion is ready!\nPress F to drink it — you will have 60 seconds of protection.";

    private CauldronCraftingStation craftingStation;
    private Transform sphereTarget;
    private Camera playerCamera;
    private HotbarInventory playerInventory;

    private GameObject ingredientPanel;
    private TMP_Text ingredientText;
    private GameObject messagePanel;
    private TMP_Text messageText;

    private Coroutine messageCoroutine;
    private bool potionMessageShown;
    private bool suppressIngredientPanel;

    private void Awake()
    {
        craftingStation = GetComponent<CauldronCraftingStation>();
        CacheSphereTarget();
        BuildUI();
    }

    private void Start()
    {
        CachePlayerReferences();
    }

    private void Update()
    {
        if (sphereTarget == null)
            return;

        if (playerCamera == null)
            CachePlayerReferences();

        bool lookingAtSphere = IsLookingAtSphere();
        bool showIngredientPanel = lookingAtSphere && !suppressIngredientPanel;

        if (ingredientPanel != null)
            ingredientPanel.SetActive(showIngredientPanel);

        if (showIngredientPanel)
            RefreshIngredientText();
    }

    public void ShowPotionReadyMessage()
    {
        if (potionMessageShown)
            return;

        potionMessageShown = true;
        suppressIngredientPanel = true;

        if (ingredientPanel != null)
            ingredientPanel.SetActive(false);

        if (messagePanel == null || messageText == null)
            return;

        string message = potionReadyMessage.Replace("60", Mathf.RoundToInt(protectionDurationSeconds).ToString());
        messageText.text = message;

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        messageCoroutine = StartCoroutine(ShowMessageRoutine(message));
    }

    private IEnumerator ShowMessageRoutine(string message)
    {
        suppressIngredientPanel = true;

        if (ingredientPanel != null)
            ingredientPanel.SetActive(false);

        messageText.text = message;
        messagePanel.SetActive(true);

        yield return new WaitForSeconds(potionMessageDuration);

        messagePanel.SetActive(false);
        suppressIngredientPanel = false;
        messageCoroutine = null;
    }

    private void CacheSphereTarget()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == sphereObjectName)
            {
                sphereTarget = child;
                EnsureSphereCollider(child);
                return;
            }
        }

        Debug.LogWarning($"[Cauldron] Could not find child named '{sphereObjectName}'.");
    }

    private static void EnsureSphereCollider(Transform target)
    {
        if (target.GetComponent<Collider>() != null)
            return;

        SphereCollider collider = target.gameObject.AddComponent<SphereCollider>();
        collider.isTrigger = false;

        float maxScale = Mathf.Max(
            Mathf.Abs(target.lossyScale.x),
            Mathf.Max(Mathf.Abs(target.lossyScale.y), Mathf.Abs(target.lossyScale.z)));
        collider.radius = 0.5f / Mathf.Max(maxScale, 0.001f);
    }

    private void CachePlayerReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        playerInventory = player.GetComponent<HotbarInventory>();
        playerCamera = player.GetComponentInChildren<Camera>();
    }

    private bool IsLookingAtSphere()
    {
        if (playerCamera == null || sphereTarget == null)
            return false;

        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 spherePos = sphereTarget.position;
        Vector3 toSphere = (spherePos - cameraPos).normalized;
        float dot = Vector3.Dot(playerCamera.transform.forward, toSphere);
        if (dot < lookAtDotThreshold)
            return false;

        float distance = Vector3.Distance(cameraPos, spherePos);
        if (distance > lookDistance)
            return false;

        Ray ray = new Ray(cameraPos, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, lookDistance);
        foreach (RaycastHit hit in hits)
        {
            if (IsSphereHit(hit.transform))
                return true;
        }

        return true;
    }

    private bool IsSphereHit(Transform hitTransform)
    {
        return hitTransform == sphereTarget || hitTransform.IsChildOf(sphereTarget);
    }

    private void RefreshIngredientText()
    {
        if (ingredientText == null || craftingStation == null)
            return;

        CraftingRecipe recipe = craftingStation.GetPrimaryRecipe();
        if (recipe == null)
        {
            ingredientText.text = "No recipe configured.";
            return;
        }

        if (GameProgressManager.Instance != null && GameProgressManager.Instance.IsPotionBrewed)
        {
            ingredientText.text = "All ingredients deposited.\nThe radiation potion has been brewed.";
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("CAULDRON — INGREDIENTS NEEDED");
        builder.AppendLine();

        foreach (ItemData item in recipe.requiredItems)
        {
            if (item == null)
                continue;

            builder.AppendLine($"• {item.itemName} — {GetIngredientStatus(item)}");
        }

        builder.AppendLine();
        builder.AppendLine("Press F at the cauldron to deposit plants.");

        ingredientText.text = builder.ToString();
    }

    private string GetIngredientStatus(ItemData item)
    {
        if (GameProgressManager.Instance != null &&
            GameProgressManager.Instance.DepositedIngredients.Contains(item.itemID))
        {
            return "Deposited";
        }

        if (playerInventory != null && playerInventory.HasItem(item.itemID))
            return "In your bag — deposit it";

        if (GameProgressManager.Instance != null &&
            GameProgressManager.Instance.CollectedIngredients.Contains(item.itemID))
        {
            return "Collected — bring it here";
        }

        return "Not collected yet";
    }

    private void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("CauldronUICanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        ingredientPanel = CreateTopLeftPanel(canvas.transform, "CauldronIngredientPanel",
            new Vector2(520f, 260f), new Vector2(24f, -24f),
            new Color(0.08f, 0.06f, 0.04f, 0.88f));
        ingredientText = CreateText(ingredientPanel.transform, 22, TextAlignmentOptions.TopLeft);
        ingredientPanel.SetActive(false);

        messagePanel = CreateTopLeftPanel(canvas.transform, "CauldronPotionMessagePanel",
            new Vector2(520f, 180f), new Vector2(24f, -24f),
            new Color(0.05f, 0.12f, 0.08f, 0.92f));
        messageText = CreateText(messagePanel.transform, 24, TextAlignmentOptions.TopLeft);
        messagePanel.SetActive(false);
    }

    private static GameObject CreateTopLeftPanel(Transform parent, string objectName, Vector2 size,
        Vector2 offsetFromTopLeft, Color color)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offsetFromTopLeft;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return panelObject;
    }

    private static TMP_Text CreateText(Transform parent, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20f, 20f);
        rect.offsetMax = new Vector2(-20f, -20f);

        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
    }
}
