using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public HotbarInventory inventory;

    [Header("Interaction")]
    public float interactionDistance = 3f;
    public Key interactKey = Key.F;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<HotbarInventory>();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Debug.Log("F pressed");
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
                continue;

            Debug.Log(interactable.GetInteractionText());
            interactable.Interact(this);
            return;
        }
    }

    public HotbarInventory GetInventory()
    {
        return inventory;
    }
}