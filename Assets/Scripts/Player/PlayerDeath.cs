using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private float restartDelay = 2f;
    [SerializeField] private bool restartSceneOnDeath = true;

    private bool isDead;

    public bool IsDead => isDead;

    public void KillPlayer()
    {
        if (isDead)
            return;

        isDead = true;

        Debug.Log("PLAYER DIED");

        DisablePlayerControl();

        if (restartSceneOnDeath)
            Invoke(nameof(TriggerRespawn), restartDelay);
    }

    private void DisablePlayerControl()
    {
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script != this)
                script.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void TriggerRespawn()
    {
        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.RespawnPlayer();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}