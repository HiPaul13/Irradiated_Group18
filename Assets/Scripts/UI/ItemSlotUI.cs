using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;

    public void SetItem(ItemData item)
    {
        if (item == null)
        {
            ClearSlot();
            return;
        }

        iconImage.sprite = item.icon;
        iconImage.enabled = true;
    }

    public void ClearSlot()
    {
        iconImage.sprite = null;
        iconImage.enabled = false;
    }
}