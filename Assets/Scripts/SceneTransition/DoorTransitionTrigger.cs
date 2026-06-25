using System.Collections.Generic;
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

        HotbarInventory inventory = other.GetComponentInParent<HotbarInventory>();
        SnapshotInventoryForTransition(inventory);

        SceneManager.LoadScene(targetSceneName);
    }

    private static void SnapshotInventoryForTransition(HotbarInventory inventory)
    {
        List<string> itemIDs = new List<string>();

        if (inventory != null)
        {
            foreach (ItemData item in inventory.GetItems())
            {
                if (item != null)
                    itemIDs.Add(item.itemID);
            }
        }
        else
        {
            Debug.LogWarning("[DoorTransition] Could not find HotbarInventory on transitioning player.");
        }

        SceneTransitionState.SetTransitionInventory(itemIDs);

        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.SaveInventoryForTransition(inventory);
        else
            Debug.Log($"[DoorTransition] Inventory snapshot stored ({itemIDs.Count} item(s)).");
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
