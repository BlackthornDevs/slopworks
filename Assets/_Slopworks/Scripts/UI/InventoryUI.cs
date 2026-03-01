using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full inventory panel. Toggled with Tab (InventoryOpen action).
/// Shows 36 main slots in a 9x4 grid and 9 hotbar slots below.
/// Click to pick up/place items between slots.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private PlayerInventory _playerInventory;
    private ItemRegistry _itemRegistry;
    private InventorySlotUI[] _slots;
    private GameObject _panel;
    private SlopworksControls _controls;
    private int _heldFromSlot = -1;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Initialize(PlayerInventory inventory)
    {
        _playerInventory = inventory;
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        _controls = new SlopworksControls();
        _controls.Exploration.Enable();

        CreatePanel();

        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged += OnSlotChanged;

        _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;
        _controls?.Dispose();
    }

    private void Update()
    {
        if (_controls != null && _controls.Exploration.InventoryOpen.WasPressedThisFrame())
            Toggle();
    }

    public void Toggle()
    {
        if (_panel == null) return;

        bool opening = !_panel.activeSelf;
        _panel.SetActive(opening);

        if (opening)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAll();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _heldFromSlot = -1;
        }

        Debug.Log($"inventory ui: {(opening ? "opened" : "closed")}");
    }

    public void OnSlotClicked(int slotIndex)
    {
        if (_playerInventory == null) return;

        if (_heldFromSlot < 0)
        {
            var slot = _playerInventory.Inventory.GetSlot(slotIndex);
            if (!slot.IsEmpty)
            {
                _heldFromSlot = slotIndex;
                Debug.Log($"inventory ui: picked up from slot {slotIndex}");
            }
        }
        else
        {
            SwapSlots(_heldFromSlot, slotIndex);
            _heldFromSlot = -1;
        }
    }

    private void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            _heldFromSlot = -1;
            return;
        }

        _playerInventory.Inventory.SwapSlots(fromIndex, toIndex);
        Debug.Log($"inventory ui: swapped slot {fromIndex} <-> {toIndex}");
    }

    private void OnSlotChanged(int index)
    {
        if (!IsOpen || _slots == null) return;
        if (index >= 0 && index < _slots.Length)
        {
            var slotData = _playerInventory.Inventory.GetSlot(index);
            _slots[index].Refresh(slotData, _itemRegistry);
        }
    }

    private void RefreshAll()
    {
        if (_playerInventory == null || _slots == null) return;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slotData = _playerInventory.Inventory.GetSlot(i);
            _slots[i].Refresh(slotData, _itemRegistry);
        }
    }

    private void CreatePanel()
    {
        _panel = new GameObject("InventoryPanel");
        _panel.transform.SetParent(transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520, 360);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_panel.transform, false);
        var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "Inventory";
        titleText.fontSize = 18;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -4);

        // Grid
        var gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(_panel.transform, false);
        var gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(9 * 52 + 8 * 4, 5 * 52 + 4 * 4);
        gridRect.anchoredPosition = new Vector2(0, -10);

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(52, 52);
        grid.spacing = new Vector2(4, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;

        _slots = new InventorySlotUI[PlayerInventory.TotalSlots];
        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
            _slots[i] = InventorySlotUI.Create(gridObj.transform, i, this);
    }
}
