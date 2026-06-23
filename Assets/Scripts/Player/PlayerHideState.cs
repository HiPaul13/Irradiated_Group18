using UnityEngine;

public class PlayerHideState : MonoBehaviour
{
    public bool IsHidden { get; private set; }

    public ToiletHideSpot CurrentHideSpot { get; private set; }

    public void SetHidden(bool hidden, ToiletHideSpot hideSpot)
    {
        IsHidden = hidden;
        CurrentHideSpot = hidden ? hideSpot : null;

        Debug.Log("Player hidden: " + IsHidden);
    }
}