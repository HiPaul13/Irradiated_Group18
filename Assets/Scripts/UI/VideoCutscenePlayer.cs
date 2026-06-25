using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Plays a video clip full-screen and then loads the next scene.
///
/// Setup per cutscene scene (e.g. Start_Cutscene, End_Cutscene):
///   1. Add an empty GameObject, attach this component.
///   2. Attach a VideoPlayer to the same GameObject (or assign via Inspector).
///   3. Set the VideoPlayer's Video Clip.
///   4. Choose Render Mode:
///        Camera Far Plane — select your Main Camera and set Alpha = 1. Simple.
///        Render Texture  — create a RenderTexture asset, assign it to the VideoPlayer
///                          and to a full-screen RawImage in a Screen-Space Overlay Canvas.
///   5. Set nextSceneName ("Forest_Environment" for intro, "Menu_Scene" for outro).
///   6. Optionally tick skipOnAnyKey (default true) so players can skip with any key.
/// </summary>
public class VideoCutscenePlayer : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private string      nextSceneName;
    [SerializeField] private bool        skipOnAnyKey = true;

    private bool isLoading;

    private void Start()
    {
        Time.timeScale   = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
        {
            Debug.LogError("[Cutscene] No VideoPlayer component found — loading next scene immediately.");
            LoadNextScene();
            return;
        }

        videoPlayer.loopPointReached += _ => LoadNextScene();
        videoPlayer.Play();
    }

    private void Update()
    {
        if (!skipOnAnyKey || isLoading) return;

        bool skipPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        if (skipPressed) LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (isLoading) return;
        isLoading = true;

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogError("[Cutscene] nextSceneName is not set on VideoCutscenePlayer!");
    }
}
