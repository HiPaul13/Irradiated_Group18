using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerSpawnOnLoad : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        if (string.IsNullOrEmpty(SceneTransitionState.NextSpawnPointId))
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject == null)
        {
            Debug.LogWarning("PlayerSpawnOnLoad could not find the player.");
            SceneTransitionState.Clear();
            return;
        }

        PlayerSpawnPoint spawnPoint = FindSpawnPoint(SceneTransitionState.NextSpawnPointId);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"PlayerSpawnOnLoad could not find spawn point '{SceneTransitionState.NextSpawnPointId}'.");
            SceneTransitionState.Clear();
            return;
        }

        Transform playerTransform = playerObject.transform;
        playerTransform.SetPositionAndRotation(spawnPoint.transform.position, spawnPoint.transform.rotation);

        Rigidbody rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = spawnPoint.transform.position;
            rb.rotation = spawnPoint.transform.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        FirstPersonController controller = playerObject.GetComponent<FirstPersonController>();
        if (controller != null)
        {
            controller.SyncRotationFromTransform();
        }

        SceneTransitionState.Clear();
    }

    private static PlayerSpawnPoint FindSpawnPoint(string spawnPointId)
    {
        PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);

        foreach (PlayerSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint.SpawnPointId == spawnPointId)
                return spawnPoint;
        }

        return null;
    }
}
