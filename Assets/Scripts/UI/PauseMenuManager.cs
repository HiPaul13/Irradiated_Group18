using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    [Header("UI — assign once in prefab")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "Menu_Scene";

    private FirstPersonController   playerController;
    private PlayerInteraction       playerInteraction;
    private BookUIManager           bookUIManager;
    private PlayerBookInteractor    playerBookInteractor;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (continueButton != null) continueButton.onClick.AddListener(ContinueGame);
        if (quitButton     != null) quitButton.onClick.AddListener(QuitGame);

        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
    }

    private void Start()
    {
        playerController     = FindFirstObjectByType<FirstPersonController>();
        playerInteraction    = FindFirstObjectByType<PlayerInteraction>();
        bookUIManager        = FindFirstObjectByType<BookUIManager>();
        playerBookInteractor = FindFirstObjectByType<PlayerBookInteractor>();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == mainMenuSceneName) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (bookUIManager != null && bookUIManager.IsOpen) return;

        if (isPaused) ResumeGame();
        else          PauseGame();
    }

    private void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (playerController     != null) playerController.SetControlsEnabled(false);
        if (playerInteraction    != null) playerInteraction.enabled = false;
        if (playerBookInteractor != null) playerBookInteractor.enabled = false;
    }

    private void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        if (playerController     != null) playerController.SetControlsEnabled(true);
        if (playerInteraction    != null) playerInteraction.enabled = true;
        if (playerBookInteractor != null) playerBookInteractor.enabled = true;
    }

    public void ContinueGame() => ResumeGame();

    public void QuitGame()
    {
        isPaused       = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
