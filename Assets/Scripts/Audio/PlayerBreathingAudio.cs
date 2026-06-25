using UnityEngine;

public class PlayerBreathingAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FirstPersonController fpsController;
    [SerializeField] private PlayerDeath           playerDeath;

    [Header("Normal Breathing")]
    [SerializeField] private AK.Wwise.Event normalBreathing;
    [SerializeField] private AK.Wwise.Event normalBreathingStop;

    [Header("Heavy Breathing (Sprint Loop)")]
    [SerializeField] private AK.Wwise.Event heavyBreathingStart;
    [SerializeField] private AK.Wwise.Event heavyBreathingStop;

    private bool isSprinting;

    private void Awake()
    {
        if (fpsController == null) fpsController = GetComponent<FirstPersonController>();
        if (playerDeath   == null) playerDeath   = GetComponent<PlayerDeath>();
    }

    private void Start()
    {
        normalBreathing?.Post(gameObject);
    }

    private void Update()
    {
        if (playerDeath != null && playerDeath.IsDead) return;

        bool nowSprinting = fpsController != null && fpsController.IsSprinting;

        if (nowSprinting && !isSprinting)
        {
            normalBreathingStop?.Post(gameObject);
            heavyBreathingStart?.Post(gameObject);
        }
        else if (!nowSprinting && isSprinting)
        {
            heavyBreathingStop?.Post(gameObject);
            normalBreathing?.Post(gameObject);
        }

        isSprinting = nowSprinting;
    }
}
