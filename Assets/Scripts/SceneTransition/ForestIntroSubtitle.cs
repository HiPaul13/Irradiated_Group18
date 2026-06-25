using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ForestIntroSubtitle : MonoBehaviour
{
    [Header("Dialogue")]
    [TextArea(2, 5)]
    [SerializeField] private string[] dialogueLines;

    [ContextMenu("Reset Forest Intro")]
    private void ResetForestIntro()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        Debug.Log("Forest intro subtitle has been reset.");
    }

    [Header("Timing")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float durationPerLine = 3f;
    [SerializeField] private float pauseBetweenLines = 0.2f;

    private const string SceneName = "Forest_Environment";
    private const string SaveKey =
        "ForestEnvironment_IntroSubtitleShown";

    private IEnumerator Start()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
        {
            yield break;
        }

        if (PlayerPrefs.GetInt(SaveKey, 0) == 1)
        {
            yield break;
        }

        yield return new WaitForSeconds(startDelay);

        if (SubtitleManager.Instance == null)
        {
            Debug.LogWarning(
                "[ForestIntroSubtitle] No SubtitleManager found."
            );

            yield break;
        }

        SubtitleManager.Instance.ShowSequence(
            dialogueLines,
            durationPerLine,
            pauseBetweenLines
        );

        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.Save();
    }
}