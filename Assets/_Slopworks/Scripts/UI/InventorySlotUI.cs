using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI element for a single inventory slot. Clickable for item movement.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    private Image _iconImage;
    private TextMeshProUGUI _countText;
    private int _slotIndex;
    private InventoryUI _parent;

    private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0f);

    public static InventorySlotUI Create(Transform parent, int slotIndex, InventoryUI inventoryUI)
    {
        var obj = new GameObject($"Slot_{slotIndex}");
        obj.transform.SetParent(parent, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Icon
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        var icon = iconObj.AddComponent<Image>();
        icon.color = EmptyIconColor;
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // Count
        var countObj = new GameObject("Count");
        countObj.transform.SetParent(obj.transform, false);
        var countText = countObj.AddComponent<TextMeshProUGUI>();
        countText.fontSize = 12;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.color = Color.white;
        countText.raycastTarget = false;
        var countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(2, 2);
        countRect.offsetMax = new Vector2(-2, -2);

        var slot = obj.AddComponent<InventorySlotUI>();
        slot._iconImage = icon;
        slot._countText = countText;
        slot._slotIndex = slotIndex;
        slot._parent = inventoryUI;

        return slot;
    }

    public void Refresh(ItemSlot data, ItemRegistry registry)
    {
        if (data.IsEmpty)
        {
            _iconImage.color = EmptyIconColor;
            _iconImage.sprite = null;
            _countText.text = "";
        }
        else
        {
            var def = registry?.Get(data.item.definitionId);
            _iconImage.sprite = def?.icon;
            _iconImage.color = def?.icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _countText.text = data.count > 1 ? data.count.ToString() : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _parent?.OnSlotClicked(_slotIndex);
    }
}
