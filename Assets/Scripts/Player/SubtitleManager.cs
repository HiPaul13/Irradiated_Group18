using System.Collections;
using TMPro;
using UnityEngine;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;

    private Coroutine currentSubtitleCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        subtitlePanel.SetActive(false);
    }

    public void ShowText(string text, float duration = 3f)
    {
        StopCurrentSubtitle();

        currentSubtitleCoroutine = StartCoroutine(
            ShowSingleTextRoutine(text, duration)
        );
    }

    public void ShowSequence(
        string[] texts,
        float durationPerText = 3f,
        float pauseBetweenTexts = 0.25f)
    {
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        StopCurrentSubtitle();

        currentSubtitleCoroutine = StartCoroutine(
            ShowSequenceRoutine(
                texts,
                durationPerText,
                pauseBetweenTexts
            )
        );
    }

    private IEnumerator ShowSingleTextRoutine(
        string text,
        float duration)
    {
        subtitleText.text = text;
        subtitlePanel.SetActive(true);

        yield return new WaitForSeconds(duration);

        subtitlePanel.SetActive(false);
        currentSubtitleCoroutine = null;
    }

    private IEnumerator ShowSequenceRoutine(
        string[] texts,
        float durationPerText,
        float pauseBetweenTexts)
    {
        subtitlePanel.SetActive(true);

        foreach (string text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            subtitleText.text = text;

            yield return new WaitForSeconds(durationPerText);

            if (pauseBetweenTexts > 0f)
            {
                subtitlePanel.SetActive(false);

                yield return new WaitForSeconds(pauseBetweenTexts);

                subtitlePanel.SetActive(true);
            }
        }

        subtitlePanel.SetActive(false);
        currentSubtitleCoroutine = null;
    }

    private void StopCurrentSubtitle()
    {
        if (currentSubtitleCoroutine != null)
        {
            StopCoroutine(currentSubtitleCoroutine);
            currentSubtitleCoroutine = null;
        }

        subtitlePanel.SetActive(false);
    }
}