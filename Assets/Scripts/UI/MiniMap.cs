using UnityEngine;
using UnityEngine.InputSystem;

public class MiniMap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject minimapRoot;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private Canvas minimapCanvas;
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private Camera playerCamera;

    [Header("Camera")]
    [SerializeField] private float cameraHeight = 747f;
    [SerializeField] private float fieldOfView = 60f;
    [SerializeField] private float cameraDepth = 10f;

    private bool isOpen;

    private void Awake()
    {
        CacheReferences();
        ConfigureCamera();
        SetOpen(false);
    }

    private void CacheReferences()
    {
        if (minimapRoot == null)
        {
            GameObject found = GameObject.Find("MiniMapTest");
            if (found != null)
                minimapRoot = found;
        }

        if (minimapRoot != null)
        {
            if (minimapCamera == null)
                minimapCamera = minimapRoot.GetComponentInChildren<Camera>(true);

            if (minimapCanvas == null)
                minimapCanvas = minimapRoot.GetComponentInChildren<Canvas>(true);
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return;

        if (playerController == null)
            playerController = playerObject.GetComponent<FirstPersonController>();

        if (playerInteraction == null)
            playerInteraction = playerObject.GetComponent<PlayerInteraction>();

        if (playerCamera == null)
            playerCamera = playerObject.GetComponentInChildren<Camera>(true);
    }

    private void ConfigureCamera()
    {
        if (minimapCamera == null)
            return;

        minimapCamera.orthographic = false;
        minimapCamera.fieldOfView = fieldOfView;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.clearFlags = CameraClearFlags.Skybox;
        minimapCamera.targetTexture = null;
        minimapCamera.depth = cameraDepth;

        AudioListener listener = minimapCamera.GetComponent<AudioListener>();

        if (listener != null)
            listener.enabled = false;

        Terrain terrain = FindFirstObjectByType<Terrain>();

        if (terrain != null)
        {
            TerrainData data = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;

            Vector3 terrainCenter = terrainPosition + new Vector3(
                terrainSize.x * 0.5f,
                0f,
                terrainSize.z * 0.5f
            );

            minimapCamera.transform.position = new Vector3(
                terrainCenter.x,
                terrainPosition.y + cameraHeight,
                terrainCenter.z
            );
        }

        if (minimapCanvas != null)
            minimapCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            SetOpen(!isOpen);
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (minimapRoot != null)
            minimapRoot.SetActive(open);

        if (minimapCanvas != null)
            minimapCanvas.gameObject.SetActive(false);

        if (minimapCamera != null)
        {
            minimapCamera.enabled = open;
            minimapCamera.gameObject.SetActive(open);
        }

        if (playerCamera != null)
            playerCamera.enabled = !open;

        // Fixed: FirstPersonController has no SetInputEnabled() method.
        // So we disable/enable the whole movement script instead.
        if (playerController != null)
            playerController.enabled = !open;

        if (playerInteraction != null)
            playerInteraction.enabled = !open;
    }
}