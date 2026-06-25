using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class DisableFogForCamera : MonoBehaviour
{
    private Camera targetCamera;
    private bool previousFogState;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void OnBeginCameraRendering(
        ScriptableRenderContext context,
        Camera camera)
    {
        if (camera != targetCamera)
        {
            return;
        }

        previousFogState = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    private void OnEndCameraRendering(
        ScriptableRenderContext context,
        Camera camera)
    {
        if (camera != targetCamera)
        {
            return;
        }

        RenderSettings.fog = previousFogState;
    }
}