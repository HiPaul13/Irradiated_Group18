using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerDeath : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField] private float restartDelay = 4f;

    [Header("Death Screen")]
    [Tooltip("Das Canvas GameObject das den Death Screen enthält (wird ein-/ausgeblendet).")]
    [SerializeField] private GameObject deathScreenCanvas;
    [Tooltip("Wie lange der Death Screen sichtbar bleibt bevor der Respawn startet. " +
             "Sollte <= restartDelay sein.")]
    [SerializeField] private float deathScreenDuration = 3f;
    [Tooltip("Einblende-Zeit des Death Screens in Sekunden (0 = sofort).")]
    [SerializeField] private float fadeInDuration = 0.5f;

    private CanvasGroup canvasGroup;
    private bool        isDead;

    public bool IsDead => isDead;

    private void Awake()
    {
        if (deathScreenCanvas != null)
        {
            canvasGroup = deathScreenCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = deathScreenCanvas.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            deathScreenCanvas.SetActive(false);
        }
    }

    public void KillPlayer()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("[PlayerDeath] Player gestorben.");

        DisablePlayerControl();

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Death screen einblenden
        if (deathScreenCanvas != null)
        {
            deathScreenCanvas.SetActive(true);

            if (fadeInDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                    yield return null;
                }
            }

            canvasGroup.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(deathScreenDuration);

        TriggerRespawn();
    }

    private void DisablePlayerControl()
    {
        // Disable FPC explicitly first so movement stops immediately
        FirstPersonController fpc = GetComponent<FirstPersonController>();
        if (fpc != null) fpc.SetControlsEnabled(false);

        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this) script.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void TriggerRespawn()
    {
        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.RespawnPlayer();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
