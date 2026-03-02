using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI element for a single hotbar slot. Shows item icon and count.
/// Highlights when selected.
/// </summary>
public class HotbarSlotUI : MonoBehaviour
{
    private Image _background;
    private Image _iconImage;
    private TextMeshProUGUI _countText;
    private TextMeshProUGUI _keyText;
    private int _slotIndex;
    private PlayerInventory _playerInventory;
    private ItemRegistry _itemRegistry;

    private static readonly Color NormalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color SelectedColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
    private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0f);

    public static HotbarSlotUI Create(Transform parent, int index)
    {
        var obj = new GameObject($"HotbarSlot_{index}");
        obj.transform.SetParent(parent, false);

        // Background
        var bg = obj.AddComponent<Image>();
        bg.color = NormalColor;
        bg.raycastTarget = false;

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

        // Count text
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

        // Key number text
        var keyObj = new GameObject("Key");
        keyObj.transform.SetParent(obj.transform, false);
        var keyText = keyObj.AddComponent<TextMeshProUGUI>();
        keyText.fontSize = 10;
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.color = new Color(1f, 1f, 1f, 0.5f);
        keyText.text = (index + 1).ToString();
        keyText.raycastTarget = false;
        var keyRect = keyText.rectTransform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = new Vector2(2, 2);
        keyRect.offsetMax = new Vector2(-2, -2);

        var slot = obj.AddComponent<HotbarSlotUI>();
        slot._background = bg;
        slot._iconImage = icon;
        slot._countText = countText;
        slot._keyText = keyText;
        slot._slotIndex = index;

        return slot;
    }

    public void Bind(PlayerInventory inventory, int slotIndex)
    {
        // Unsubscribe from previous inventory first to prevent duplicate subscriptions
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;

        _playerInventory = inventory;
        _slotIndex = slotIndex;
        if (_itemRegistry == null)
            _itemRegistry = FindAnyObjectByType<ItemRegistry>();

        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged += OnSlotChanged;

        Refresh();
    }

    private void OnDestroy()
    {
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(int index)
    {
        if (index == _slotIndex)
            Refresh();
    }

    /// <summary>
    /// Display a non-inventory entry (build tool, etc).
    /// Pass null displayName to show an empty slot.
    /// Unbinds from inventory while showing an entry.
    /// </summary>
    public void SetEntry(string displayName, Color color)
    {
        // Unbind from inventory events while showing non-inventory content
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;
        _playerInventory = null;

        if (string.IsNullOrEmpty(displayName))
        {
            _iconImage.color = EmptyIconColor;
            _iconImage.sprite = null;
            _countText.text = "";
        }
        else
        {
            _iconImage.sprite = null;
            _iconImage.color = color;
            _countText.text = displayName;
        }
    }

    public void SetSelected(bool selected)
    {
        if (_background != null)
            _background.color = selected ? SelectedColor : NormalColor;
    }

    private void Refresh()
    {
        if (_playerInventory == null) return;

        var slot = _playerInventory.Inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty)
        {
            _iconImage.color = EmptyIconColor;
            _iconImage.sprite = null;
            _countText.text = "";
        }
        else
        {
            var def = _itemRegistry?.Get(slot.item.definitionId);
            _iconImage.sprite = def?.icon;
            _iconImage.color = def?.icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _countText.text = slot.count > 1 ? slot.count.ToString() : "";
        }
    }
}
