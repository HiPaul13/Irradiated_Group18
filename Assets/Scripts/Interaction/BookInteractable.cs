using System.Collections.Generic;
using UnityEngine;

public class BookInteractable : MonoBehaviour
{
    [TextArea(3, 10)]
    public string bookText = "This text will be defined later.";

    [Header("Pages")]
    public List<BookPage> pages = new List<BookPage>();

    [Header("Prompt")]
    public string promptText = "Press F to read";

    [Header("Subtitle")]
    [TextArea(2, 5)]
    public string interactionSubtitle;

    [Min(0.1f)]
    public float subtitleDuration = 4f;

    public bool showSubtitleOnlyOnce;

    private bool subtitleWasShown;

        [Header("Interaction")]
        public float interactionRadius = 0.45f;

        private SphereCollider interactionTrigger;

        private void Reset()
        {
            FitColliderToBook();
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                FitColliderToBook();
            }
        }
    #endif

    private void Awake()
    {
        FitColliderToBook();
    }

    [ContextMenu("Fit Collider To Book")]
    public void FitColliderToBook()
    {
        RemoveBoxColliders();

        if (!TryGetWorldBounds(out Bounds worldBounds))
        {
            SetupFallbackSphere(Vector3.zero);
            return;
        }

        interactionTrigger = GetComponent<SphereCollider>();
        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.center = transform.InverseTransformPoint(worldBounds.center);

        float maxScale = Mathf.Max(
            transform.lossyScale.x,
            Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)
        );
        interactionTrigger.radius = interactionRadius / Mathf.Max(maxScale, 0.001f);
    }

    public string GetBookText()
    {
        return bookText;
    }

    public BookPage[] GetPages()
    {
        if (pages != null && pages.Count > 0)
        {
            return pages.ToArray();
        }

        return new[]
        {
            new BookPage { text = bookText }
        };
    }

    public string GetPromptText()
    {
        return promptText;
    }

    public bool IsPlayerInRange(Transform origin, float maxDistance)
    {
        return Vector3.Distance(origin.position, GetInteractionPoint()) <= maxDistance;
    }

    public Vector3 GetInteractionPoint()
    {
        if (TryGetWorldBounds(out Bounds worldBounds))
        {
            return worldBounds.center;
        }

        return transform.position;
    }

    private bool TryGetWorldBounds(out Bounds worldBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        worldBounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !HasValidMesh(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    public void ShowInteractionSubtitle()
    {
        Debug.Log($"Trying to show subtitle: {interactionSubtitle}", this);

        if (string.IsNullOrWhiteSpace(interactionSubtitle))
        {
            Debug.LogWarning("Interaction subtitle is empty.", this);
            return;
        }

        if (showSubtitleOnlyOnce && subtitleWasShown)
        {
            Debug.Log("Subtitle was already shown.", this);
            return;
        }

        if (SubtitleManager.Instance == null)
        {
            Debug.LogError("No SubtitleManager instance found.", this);
            return;
        }

        SubtitleManager.Instance.ShowText(
            interactionSubtitle,
            subtitleDuration
        );

        subtitleWasShown = true;
    }

    private void SetupFallbackSphere(Vector3 localCenter)
    {
        interactionTrigger = GetComponent<SphereCollider>();
        if (interactionTrigger == null)
        {
            interactionTrigger = gameObject.AddComponent<SphereCollider>();
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.center = localCenter;
        interactionTrigger.radius = interactionRadius;
    }

    private static bool HasValidMesh(Renderer renderer)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null;
    }

    private void RemoveBoxColliders()
    {
        BoxCollider[] boxColliders = GetComponents<BoxCollider>();
        foreach (BoxCollider boxCollider in boxColliders)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(boxCollider);
                continue;
            }
#endif

            Destroy(boxCollider);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetInteractionPoint(), interactionRadius);
    }
#endif
}
