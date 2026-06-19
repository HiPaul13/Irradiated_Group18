using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;

    public string SpawnPointId => spawnPointId;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.35f);

        Vector3 forward = transform.position + transform.forward;
        Gizmos.DrawLine(transform.position, forward);
    }
#endif
}
