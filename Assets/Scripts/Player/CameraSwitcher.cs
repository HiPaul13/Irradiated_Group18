using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    [SerializeField] private Camera frontCamera;
    [SerializeField] private Camera rearCamera;

    private bool isLookingBack;

    private void Start()
    {
        SetRearCamera(false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        SetRearCamera(Keyboard.current.tabKey.isPressed);
    }

    private void SetRearCamera(bool lookBack)
    {
        frontCamera.gameObject.SetActive(!lookBack);
        rearCamera.gameObject.SetActive(lookBack);
    }
}