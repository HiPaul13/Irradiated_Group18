using System.Collections;
using TMPro;
using UnityEngine;

public class SubtitleManager : MonoBehaviour
{
    public static SubtitleManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TMP_Text subtitleText;

    private Coroutine subtitleCoroutine;

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

    public void ShowText(string text, float duration)
    {
        if (subtitleCoroutine != null)
        {
            StopCoroutine(subtitleCoroutine);
        }

        subtitleCoroutine = StartCoroutine(
            ShowTextRoutine(text, duration)
        );
    }

    private IEnumerator ShowTextRoutine(string text, float duration)
    {
        subtitleText.text = text;
        subtitlePanel.SetActive(true);

        yield return new WaitForSeconds(duration);

        subtitlePanel.SetActive(false);
        subtitleCoroutine = null;
    }
}