using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class BookUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject bookPanel;
    public TMP_Text promptText;
    public Button closeButton;

    [Header("Content")]
    public float imageHeight = 280f;
    public int textFontSize = 22;

    [Header("Player")]
    public FirstPersonController firstPersonController;

    [Header("Prompt")]
    public string defaultPrompt = "Press F to read";

    [Header("Debug")]
    public bool logDebug;

    private RectTransform bookContentRoot;
    private ScrollRect bookScrollRect;
    private TMP_FontAsset bookFont;
    private Button prevPageButton;
    private Button nextPageButton;
    private TMP_Text pageIndicatorText;
    private BookPage[] currentPages;
    private int currentPageIndex;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        bookFont = LoadBookFont();
        RebuildBookUI();

        if (firstPersonController == null)
        {
            firstPersonController = FindFirstObjectByType<FirstPersonController>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseBook);
        }

        if (bookPanel != null)
        {
            bookPanel.SetActive(false);
        }

        ShowPrompt(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseBook);
        }

        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveListener(ShowPreviousPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(ShowNextPage);
        }
    }

    public void OpenBook(string text)
    {
        OpenBook(new[] { new BookPage { text = text } });
    }

    public void OpenBook(BookPage[] pages)
    {
        if (isOpen)
        {
            return;
        }

        if (bookContentRoot == null)
        {
            RebuildBookUI();
        }

        isOpen = true;
        currentPages = pages;
        currentPageIndex = 0;
        ShowPage(currentPageIndex);

        if (bookPanel != null)
        {
            bookPanel.SetActive(true);
        }

        if (bookScrollRect != null)
        {
            bookScrollRect.verticalNormalizedPosition = 1f;
        }

        UpdateNavigationButtons();
        StartCoroutine(RefreshLayoutNextFrame());
        ShowPrompt(false);
        SetGameplayEnabled(false);
    }

    public void CloseBook()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        if (bookPanel != null)
        {
            bookPanel.SetActive(false);
        }

        ClearContent();
        currentPages = null;
        currentPageIndex = 0;
        SetGameplayEnabled(true);
    }

    private void ShowPreviousPage()
    {
        if (!isOpen || currentPages == null || currentPageIndex <= 0)
            return;

        currentPageIndex--;
        ShowPage(currentPageIndex);
        UpdateNavigationButtons();
        StartCoroutine(RefreshLayoutNextFrame());
    }

    private void ShowNextPage()
    {
        if (!isOpen || currentPages == null || currentPageIndex >= currentPages.Length - 1)
            return;

        currentPageIndex++;
        ShowPage(currentPageIndex);
        UpdateNavigationButtons();
        StartCoroutine(RefreshLayoutNextFrame());
    }

    private void ShowPage(int pageIndex)
    {
        ClearContent();

        if (bookContentRoot == null)
            return;

        BookPage page = GetPageAt(pageIndex);
        if (page == null)
        {
            CreateTextBlock("This text will be defined later.");
            return;
        }

        bool hasImages = false;
        foreach (Sprite sprite in page.GetImages())
        {
            CreateImageBlock(sprite);
            hasImages = true;
        }

        if (!string.IsNullOrWhiteSpace(page.text))
            CreateTextBlock(page.text);
        else if (!hasImages)
            CreateTextBlock("This text will be defined later.");

        if (bookScrollRect != null)
            bookScrollRect.verticalNormalizedPosition = 1f;
    }

    private BookPage GetPageAt(int pageIndex)
    {
        if (currentPages == null || currentPages.Length == 0)
            return null;

        if (pageIndex < 0 || pageIndex >= currentPages.Length)
            return null;

        return currentPages[pageIndex];
    }

    private void UpdateNavigationButtons()
    {
        int pageCount = currentPages != null ? currentPages.Length : 0;
        bool hasMultiplePages = pageCount > 1;

        if (prevPageButton != null)
        {
            prevPageButton.gameObject.SetActive(hasMultiplePages);
            prevPageButton.interactable = currentPageIndex > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(hasMultiplePages);
            nextPageButton.interactable = currentPageIndex < pageCount - 1;
        }

        if (pageIndicatorText != null)
        {
            pageIndicatorText.gameObject.SetActive(hasMultiplePages);
            pageIndicatorText.text = hasMultiplePages
                ? $"{currentPageIndex + 1} / {pageCount}"
                : string.Empty;
        }
    }

    public void ShowPrompt(bool visible, string message = null)
    {
        if (promptText == null)
        {
            return;
        }

        if (isOpen || !visible)
        {
            promptText.gameObject.SetActive(false);
            return;
        }

        promptText.text = string.IsNullOrEmpty(message) ? defaultPrompt : message;
        promptText.gameObject.SetActive(true);
    }

    [ContextMenu("Rebuild Book UI")]
    public void RebuildBookUI()
    {
        Canvas canvas = GetOrCreateCanvas();
        Transform canvasTransform = canvas.transform;

        if (promptText == null)
        {
            promptText = CreateText(canvasTransform, "InteractionPrompt", 24, TextAlignmentOptions.Center);
            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 40f);
            promptRect.sizeDelta = new Vector2(500f, 50f);
            promptText.text = defaultPrompt;
            promptText.gameObject.SetActive(false);
        }

        if (bookPanel != null)
        {
            Destroy(bookPanel);
            bookPanel = null;
            bookScrollRect = null;
            bookContentRoot = null;
            closeButton = null;
            prevPageButton = null;
            nextPageButton = null;
            pageIndicatorText = null;
        }

        bookPanel = CreatePanel(canvasTransform, "BookPanel");
        RectTransform panelRect = bookPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 600f);
        bookPanel.SetActive(false);

        CreateScrollArea(bookPanel.transform);
        CreatePageNavigation(bookPanel.transform);
        closeButton = CreateCloseButton(bookPanel.transform);
    }

    private void CreateTextBlock(string text)
    {
        TextMeshProUGUI textElement = CreateText(bookContentRoot, "BookTextBlock", textFontSize, TextAlignmentOptions.TopLeft);
        textElement.text = text;
        textElement.color = new Color(0.95f, 0.92f, 0.86f, 1f);

        ContentSizeFitter fitter = textElement.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = textElement.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 700f;
        layout.flexibleWidth = 1f;
    }

    private void CreateImageBlock(Sprite sprite)
    {
        GameObject imageObject = new GameObject("BookImageBlock");
        imageObject.transform.SetParent(bookContentRoot, false);

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;

        LayoutElement layout = imageObject.AddComponent<LayoutElement>();
        layout.preferredHeight = imageHeight;
        layout.minWidth = 700f;
        layout.flexibleWidth = 1f;
    }

    private void ClearContent()
    {
        if (bookContentRoot == null)
        {
            return;
        }

        for (int i = bookContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(bookContentRoot.GetChild(i).gameObject);
        }
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return null;

        if (bookContentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(bookContentRoot);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (firstPersonController != null)
        {
            firstPersonController.SetControlsEnabled(enabled);
        }

        if (enabled && firstPersonController != null && firstPersonController.lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private Canvas GetOrCreateCanvas()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 200;
            return canvas;
        }

        GameObject canvasObject = new GameObject("BookUICanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        return canvas;
    }

    private void CreateScrollArea(Transform panelTransform)
    {
        GameObject scrollObject = new GameObject("BookScrollView");
        scrollObject.transform.SetParent(panelTransform, false);

        RectTransform scrollRectTransform = scrollObject.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(30f, 80f);
        scrollRectTransform.offsetMax = new Vector2(-30f, -80f);

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0f, 0f, 0f, 0.25f);

        bookScrollRect = scrollObject.AddComponent<ScrollRect>();
        bookScrollRect.horizontal = false;
        bookScrollRect.movementType = ScrollRect.MovementType.Clamped;
        bookScrollRect.scrollSensitivity = 30f;
        bookScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        bookScrollRect.verticalScrollbarSpacing = 6f;

        GameObject viewportObject = new GameObject("Viewport");
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewportObject.AddComponent<RectMask2D>();

        Scrollbar verticalScrollbar = CreateVerticalScrollbar(scrollObject.transform);
        bookScrollRect.verticalScrollbar = verticalScrollbar;

        GameObject contentObject = new GameObject("Content");
        contentObject.transform.SetParent(viewportObject.transform, false);

        bookContentRoot = contentObject.AddComponent<RectTransform>();
        bookContentRoot.anchorMin = new Vector2(0f, 1f);
        bookContentRoot.anchorMax = new Vector2(1f, 1f);
        bookContentRoot.pivot = new Vector2(0.5f, 1f);
        bookContentRoot.anchoredPosition = Vector2.zero;
        bookContentRoot.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layoutGroup = contentObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(16, 16, 16, 16);
        layoutGroup.spacing = 16f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bookScrollRect.viewport = viewportRect;
        bookScrollRect.content = bookContentRoot;
    }

    private void CreatePageNavigation(Transform panelTransform)
    {
        GameObject navObject = new GameObject("PageNavigation");
        navObject.transform.SetParent(panelTransform, false);

        RectTransform navRect = navObject.AddComponent<RectTransform>();
        navRect.anchorMin = new Vector2(0f, 0f);
        navRect.anchorMax = new Vector2(1f, 0f);
        navRect.pivot = new Vector2(0.5f, 0f);
        navRect.anchoredPosition = Vector2.zero;
        navRect.sizeDelta = new Vector2(0f, 56f);

        prevPageButton = CreateArrowButton(navObject.transform, "PrevPageButton", "\u2190", new Vector2(0.5f, 0.5f), new Vector2(-120f, 0f));
        prevPageButton.onClick.AddListener(ShowPreviousPage);

        nextPageButton = CreateArrowButton(navObject.transform, "NextPageButton", "\u2192", new Vector2(0.5f, 0.5f), new Vector2(120f, 0f));
        nextPageButton.onClick.AddListener(ShowNextPage);

        pageIndicatorText = CreateText(navObject.transform, "PageIndicator", 20, TextAlignmentOptions.Center);
        RectTransform indicatorRect = pageIndicatorText.rectTransform;
        indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRect.anchoredPosition = Vector2.zero;
        indicatorRect.sizeDelta = new Vector2(120f, 40f);
        pageIndicatorText.color = new Color(0.9f, 0.86f, 0.8f, 1f);
    }

    private Button CreateArrowButton(Transform parent, string objectName, string label, Vector2 anchor, Vector2 position)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = anchor;
        buttonRect.anchorMax = anchor;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(56f, 56f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.2f, 0.15f, 1f);

        Button button = buttonObject.AddComponent<Button>();

        TextMeshProUGUI buttonLabel = CreateText(buttonObject.transform, "Label", 32, TextAlignmentOptions.Center);
        RectTransform labelRect = buttonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonLabel.text = label;
        buttonLabel.raycastTarget = false;

        return button;
    }

    private static Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject("Scrollbar Vertical");
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(18f, 0f);

        Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
        scrollbarBackground.color = new Color(0.12f, 0.1f, 0.08f, 0.9f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingAreaObject = new GameObject("Sliding Area");
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);

        RectTransform slidingAreaRect = slidingAreaObject.AddComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(3f, 6f);
        slidingAreaRect.offsetMax = new Vector2(-3f, -6f);

        GameObject handleObject = new GameObject("Handle");
        handleObject.transform.SetParent(slidingAreaObject.transform, false);

        RectTransform handleRect = handleObject.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.color = new Color(0.5f, 0.42f, 0.32f, 1f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        return scrollbar;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private TextMeshProUGUI CreateText(Transform parent, string objectName, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        if (bookFont != null)
        {
            text.font = bookFont;
        }

        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return text;
    }

    private static TMP_FontAsset LoadBookFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    private static GameObject CreatePanel(Transform parent, string objectName)
    {
        GameObject panelObject = new GameObject(objectName);
        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.06f, 0.04f, 0.95f);

        return panelObject;
    }

    private Button CreateCloseButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("CloseButton");
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-20f, -20f);
        buttonRect.sizeDelta = new Vector2(120f, 40f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.2f, 0.15f, 1f);

        Button button = buttonObject.AddComponent<Button>();

        TextMeshProUGUI label = CreateText(buttonObject.transform, "Text", 18, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.text = "Close";

        return button;
    }
}
