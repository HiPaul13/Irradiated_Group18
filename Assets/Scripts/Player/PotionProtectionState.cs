using UnityEngine;

/// <summary>
/// Persists radiation potion protection across scene loads using absolute Time.time.
/// </summary>
public static class PotionProtectionState
{
    public static float ProtectionEndTime { get; private set; }

    public static bool IsActive => ProtectionEndTime > 0f && Time.time < ProtectionEndTime;

    public static float RemainingSeconds =>
        IsActive ? Mathf.Max(0f, ProtectionEndTime - Time.time) : 0f;

    public static void SetProtectionEndTime(float endTime)
    {
        ProtectionEndTime = endTime;
    }

    public static void Clear()
    {
        ProtectionEndTime = 0f;
    }

    public static void TryRestore(RadiationManager radiationManager)
    {
        if (radiationManager == null || !IsActive)
            return;

        radiationManager.RestoreProtection(ProtectionEndTime);
    }
}
