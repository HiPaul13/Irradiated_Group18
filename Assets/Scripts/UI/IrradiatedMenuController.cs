using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class IrradiatedMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string introCutsceneSceneName = "Start_Cutscene";

    private VisualElement mainPage;
    private VisualElement controlsPage;
    private VisualElement settingsPage;

    private Button playButton;
    private Button controlsButton;
    private Button settingsButton;
    private Button controlsBackButton;
    private Button settingsBackButton;
    private Button fullscreenButton;

    private SliderInt masterSlider;
    private SliderInt musicSlider;

    private Label masterValueLabel;
    private Label musicValueLabel;

    private bool fullscreen;

    private void OnEnable()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;

        mainPage = root.Q<VisualElement>("mainPage");
        controlsPage = root.Q<VisualElement>("controlsPage");
        settingsPage = root.Q<VisualElement>("settingsPage");

        playButton = root.Q<Button>("playButton");
        controlsButton = root.Q<Button>("controlsButton");
        settingsButton = root.Q<Button>("settingsButton");
        controlsBackButton = root.Q<Button>("controlsBackButton");
        settingsBackButton = root.Q<Button>("settingsBackButton");
        fullscreenButton = root.Q<Button>("fullscreenButton");

        masterSlider = root.Q<SliderInt>("masterSlider");
        musicSlider = root.Q<SliderInt>("musicSlider");

        masterValueLabel = root.Q<Label>("masterValueLabel");
        musicValueLabel = root.Q<Label>("musicValueLabel");

        playButton.clicked += PlayGame;
        controlsButton.clicked += ShowControlsPage;
        settingsButton.clicked += ShowSettingsPage;
        controlsBackButton.clicked += ShowMainPage;
        settingsBackButton.clicked += ShowMainPage;
        fullscreenButton.clicked += ToggleFullscreen;

        masterSlider.RegisterValueChangedCallback(evt =>
        {
            masterValueLabel.text = evt.newValue.ToString();
            SetMasterVolume(evt.newValue);
        });

        musicSlider.RegisterValueChangedCallback(evt =>
        {
            musicValueLabel.text = evt.newValue.ToString();
            SetMusicVolume(evt.newValue);
        });

        fullscreen = Screen.fullScreen;
        UpdateFullscreenButton();

        ShowMainPage();
    }

    private void PlayGame()
    {
        // Signal that this is a fresh run — CheckpointManager will wipe any previous
        // checkpoint so a death in the new run can't restore progress from last session.
        CheckpointManager.IsNewGame = true;

        SceneManager.LoadScene(introCutsceneSceneName);
    }

    private void ShowMainPage()
    {
        mainPage.RemoveFromClassList("hidden");
        controlsPage.AddToClassList("hidden");
        settingsPage.AddToClassList("hidden");
    }

    private void ShowControlsPage()
    {
        mainPage.AddToClassList("hidden");
        controlsPage.RemoveFromClassList("hidden");
        settingsPage.AddToClassList("hidden");
    }

    private void ShowSettingsPage()
    {
        mainPage.AddToClassList("hidden");
        controlsPage.AddToClassList("hidden");
        settingsPage.RemoveFromClassList("hidden");
    }

    private void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        Screen.fullScreen = fullscreen;
        UpdateFullscreenButton();
    }

    private void UpdateFullscreenButton()
    {
        fullscreenButton.text = fullscreen ? "■ ON" : "■ OFF";

        if (fullscreen)
        {
            fullscreenButton.AddToClassList("toggle-on");
        }
        else
        {
            fullscreenButton.RemoveFromClassList("toggle-on");
        }
    }

    private void SetMasterVolume(int value)
    {
        Debug.Log("Master Volume: " + value);

        // Später mit AudioMixer verbinden.
        // Beispiel:
        // audioMixer.SetFloat("MasterVolume", ConvertToDecibel(value));
    }

    private void SetMusicVolume(int value)
    {
        Debug.Log("Music Volume: " + value);

        // Später mit AudioMixer verbinden.
    }

    private float ConvertToDecibel(int value)
    {
        float normalized = Mathf.Clamp(value / 100f, 0.0001f, 1f);
        return Mathf.Log10(normalized) * 20f;
    }
}