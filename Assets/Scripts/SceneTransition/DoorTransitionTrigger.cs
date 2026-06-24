using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class DoorTransitionTrigger : MonoBehaviour
{
    [SerializeField] private string targetSceneName;
    [SerializeField] private string targetSpawnPointId;
    [SerializeField] private string playerTag = "Player";

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (SceneTransitionState.IsTransitioning)
            return;

        if (!other.CompareTag(playerTag))
            return;

        SceneTransitionState.NextSpawnPointId = targetSpawnPointId;
        SceneTransitionState.IsTransitioning = true;

        // Snapshot inventory so it survives into the new scene.
        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.SaveInventoryForTransition();

        SceneManager.LoadScene(targetSceneName);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
#endif
}
