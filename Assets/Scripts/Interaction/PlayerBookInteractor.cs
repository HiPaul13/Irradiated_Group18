using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBookInteractor : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public BookUIManager bookUIManager;

    [Header("Interaction")]
    public float interactionDistance = 2.5f;
    public float closeInteractDistance = 1f;
    [Range(0.2f, 1f)]
    public float lookAtDotThreshold = 0.65f;
    public LayerMask interactionMask = ~0;

    [Header("Debug")]
    public bool drawDebugRay = true;
    public bool logDebug;

    private InputAction interactAction;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (bookUIManager == null)
        {
            bookUIManager = FindFirstObjectByType<BookUIManager>();
        }

        if (bookUIManager == null)
        {
            GameObject uiRoot = new GameObject("BookUI");
            bookUIManager = uiRoot.AddComponent<BookUIManager>();
        }

        interactAction = new InputAction("BookInteract", InputActionType.Button, "<Keyboard>/e");
        interactAction.Enable();
    }

    private void OnDestroy()
    {
        if (interactAction != null)
        {
            interactAction.Disable();
            interactAction.Dispose();
        }
    }

    private void Update()
    {
        if (bookUIManager == null || playerCamera == null)
        {
            return;
        }

        if (bookUIManager.IsOpen)
        {
            bookUIManager.ShowPrompt(false);

            if (WasInteractPressed() || WasEscapePressed())
            {
                bookUIManager.CloseBook();
            }

            return;
        }

        BookInteractable targetBook = FindTargetBook();

        if (targetBook != null)
        {
            bookUIManager.ShowPrompt(true, targetBook.GetPromptText());

            if (WasInteractPressed())
            {
                if (logDebug)
                {
                    Debug.Log($"Opening book: {targetBook.name}", targetBook);
                }

                bookUIManager.OpenBook(targetBook.GetPages());
            }
        }
        else
        {
            bookUIManager.ShowPrompt(false);
        }
    }

    private BookInteractable FindTargetBook()
    {
        BookInteractable raycastBook = FindBookWithRaycast();
        if (raycastBook != null)
        {
            return raycastBook;
        }

        return FindBookWithProximityAndLook();
    }

    private BookInteractable FindBookWithRaycast()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (drawDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.cyan);
        }

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactionDistance,
            interactionMask,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            BookInteractable book = hit.collider.GetComponentInParent<BookInteractable>();
            if (book != null && IsBookAvailable(book))
            {
                return book;
            }
        }

        return null;
    }

    private BookInteractable FindBookWithProximityAndLook()
    {
        BookInteractable[] books = FindObjectsByType<BookInteractable>(FindObjectsSortMode.None);
        BookInteractable closestBook = null;
        float closestDistance = float.MaxValue;

        foreach (BookInteractable book in books)
        {
            if (!IsBookAvailable(book))
            {
                continue;
            }

            float distance = Vector3.Distance(playerCamera.transform.position, book.GetInteractionPoint());
            if (distance > interactionDistance)
            {
                continue;
            }

            if (distance <= closeInteractDistance)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBook = book;
                }

                continue;
            }

            Vector3 toBook = book.GetInteractionPoint() - playerCamera.transform.position;
            if (toBook.sqrMagnitude <= 0.001f)
            {
                continue;
            }

            float lookDot = Vector3.Dot(playerCamera.transform.forward, toBook.normalized);
            if (lookDot < lookAtDotThreshold)
            {
                continue;
            }

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBook = book;
            }
        }

        return closestBook;
    }

    private bool IsBookAvailable(BookInteractable book)
    {
        return book != null && book.isActiveAndEnabled && book.gameObject.activeInHierarchy;
    }

    private bool WasInteractPressed()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            return true;
        }

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool WasEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
