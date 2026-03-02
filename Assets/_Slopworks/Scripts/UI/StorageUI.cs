using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Modal split panel showing player inventory and storage side by side.
/// Click a slot to pick up, click on the other side to transfer.
/// Follows RecipeSelectionUI pattern (modal open/close with cursor lock toggle).
/// </summary>
public class StorageUI : MonoBehaviour
{
    private GameObject _panel;
    private StorageBehaviour _currentStorage;
    private PlayerInventory _playerInventory;
    private ItemRegistry _itemRegistry;

    // Slot UI arrays
    private StorageSlot[] _playerSlots;
    private StorageSlot[] _storageSlots;

    // Held item state: which side and slot we picked up from
    private enum Side { None, Player, Storage }
    private Side _heldSide = Side.None;
    private int _heldSlotIndex = -1;

    // Frame counter to skip close input on the frame Open was called
    // (the same E press that triggers Interact is still active this frame)
    private int _openFrame = -1;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        CreatePanel();
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == _openFrame) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame)
            Close();
    }

    public void Open(StorageBehaviour storage, PlayerInventory inventory)
    {
        _currentStorage = storage;
        _playerInventory = inventory;
        _heldSide = Side.None;
        _heldSlotIndex = -1;
        _openFrame = Time.frameCount;

        // Subscribe to live updates
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged += OnPlayerSlotChanged;
        if (_currentStorage?.Container != null)
            _currentStorage.Container.OnSlotChanged += OnStorageSlotChanged;

        RebuildStorageGrid();
        _panel.SetActive(true);
        RefreshAll();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"storage ui: opened for {storage.Definition.displayName}");
    }

    public void Close()
    {
        // Unsubscribe
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnPlayerSlotChanged;
        if (_currentStorage?.Container != null)
            _currentStorage.Container.OnSlotChanged -= OnStorageSlotChanged;

        _panel.SetActive(false);
        _currentStorage = null;
        _heldSide = Side.None;
        _heldSlotIndex = -1;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("storage ui: closed");
    }

    private void OnPlayerSlotChanged(int index)
    {
        if (!IsOpen || _playerSlots == null) return;
        if (index >= 0 && index < _playerSlots.Length)
            RefreshPlayerSlot(index);
    }

    private void OnStorageSlotChanged(int index)
    {
        if (!IsOpen || _storageSlots == null) return;
        if (index >= 0 && index < _storageSlots.Length)
            RefreshStorageSlot(index);
    }

    private void OnSlotClicked(Side side, int slotIndex)
    {
        if (_playerInventory == null || _currentStorage == null) return;

        if (_heldSide == Side.None)
        {
            // Pick up from this slot
            ItemSlot slot = side == Side.Player
                ? _playerInventory.Inventory.GetSlot(slotIndex)
                : _currentStorage.Container.GetSlot(slotIndex);

            if (slot.IsEmpty) return;

            _heldSide = side;
            _heldSlotIndex = slotIndex;
            Debug.Log($"storage ui: picked up from {side} slot {slotIndex}");
        }
        else
        {
            // Place into this slot
            if (side == _heldSide && slotIndex == _heldSlotIndex)
            {
                // Clicked same slot, cancel
                _heldSide = Side.None;
                _heldSlotIndex = -1;
                return;
            }

            // Get source and destination
            ItemSlot srcSlot = _heldSide == Side.Player
                ? _playerInventory.Inventory.GetSlot(_heldSlotIndex)
                : _currentStorage.Container.GetSlot(_heldSlotIndex);

            ItemSlot dstSlot = side == Side.Player
                ? _playerInventory.Inventory.GetSlot(slotIndex)
                : _currentStorage.Container.GetSlot(slotIndex);

            // Swap the two slots
            SetSlot(_heldSide, _heldSlotIndex, dstSlot);
            SetSlot(side, slotIndex, srcSlot);

            Debug.Log($"storage ui: transferred {_heldSide}[{_heldSlotIndex}] <-> {side}[{slotIndex}]");

            _heldSide = Side.None;
            _heldSlotIndex = -1;
        }
    }

    private void SetSlot(Side side, int index, ItemSlot slot)
    {
        if (side == Side.Player)
            _playerInventory.Inventory.SetSlot(index, slot);
        else
            _currentStorage.Container.SetSlot(index, slot);
    }

    private void RefreshAll()
    {
        if (_playerInventory == null || _playerSlots == null) return;
        for (int i = 0; i < _playerSlots.Length; i++)
            RefreshPlayerSlot(i);

        if (_currentStorage == null || _storageSlots == null) return;
        for (int i = 0; i < _storageSlots.Length; i++)
            RefreshStorageSlot(i);
    }

    private void RefreshPlayerSlot(int index)
    {
        var data = _playerInventory.Inventory.GetSlot(index);
        _playerSlots[index].Refresh(data, _itemRegistry);
    }

    private void RefreshStorageSlot(int index)
    {
        var data = _currentStorage.Container.GetSlot(index);
        _storageSlots[index].Refresh(data, _itemRegistry);
    }

    // -- Panel creation --

    private Transform _storageGridParent;
    private TextMeshProUGUI _storageTitleText;

    private void CreatePanel()
    {
        _panel = new GameObject("StoragePanel");
        _panel.transform.SetParent(transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(960, 400);

        // -- Left side: Player Inventory --
        CreatePlayerSide(_panel.transform);

        // -- Divider --
        var divider = new GameObject("Divider");
        divider.transform.SetParent(_panel.transform, false);
        var divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        divImg.raycastTarget = false;
        var divRect = divImg.rectTransform;
        divRect.anchorMin = new Vector2(0.5f, 0);
        divRect.anchorMax = new Vector2(0.5f, 1);
        divRect.pivot = new Vector2(0.5f, 0.5f);
        divRect.sizeDelta = new Vector2(2, 0);
        divRect.anchoredPosition = Vector2.zero;

        // -- Right side: Storage --
        CreateStorageSide(_panel.transform);

        // Close button
        CreateCloseButton(_panel.transform);
    }

    private void CreatePlayerSide(Transform parent)
    {
        var container = new GameObject("PlayerSide");
        container.transform.SetParent(parent, false);
        var containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(0.5f, 1);
        containerRect.offsetMin = new Vector2(8, 8);
        containerRect.offsetMax = new Vector2(-8, -8);

        // Title
        var titleObj = new GameObject("PlayerTitle");
        titleObj.transform.SetParent(container.transform, false);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Player Inventory";
        titleText.fontSize = 16;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 24);
        titleRect.anchoredPosition = Vector2.zero;

        // Grid
        var gridObj = new GameObject("PlayerGrid");
        gridObj.transform.SetParent(container.transform, false);
        var gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(9 * 48 + 8 * 3, 5 * 48 + 4 * 3);
        gridRect.anchoredPosition = new Vector2(0, -10);

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(48, 48);
        grid.spacing = new Vector2(3, 3);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;

        _playerSlots = new StorageSlot[PlayerInventory.TotalSlots];
        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
        {
            int capturedIndex = i;
            _playerSlots[i] = StorageSlot.Create(gridObj.transform, i,
                () => OnSlotClicked(Side.Player, capturedIndex));
        }
    }

    private void CreateStorageSide(Transform parent)
    {
        var container = new GameObject("StorageSide");
        container.transform.SetParent(parent, false);
        var containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.offsetMin = new Vector2(8, 8);
        containerRect.offsetMax = new Vector2(-8, -8);

        // Title
        var titleObj = new GameObject("StorageTitle");
        titleObj.transform.SetParent(container.transform, false);
        _storageTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        _storageTitleText.text = "Storage";
        _storageTitleText.fontSize = 16;
        _storageTitleText.alignment = TextAlignmentOptions.Center;
        _storageTitleText.color = Color.white;
        _storageTitleText.raycastTarget = false;
        var titleRect = _storageTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 24);
        titleRect.anchoredPosition = Vector2.zero;

        // Grid container (rebuilt on each Open since slot count varies)
        var gridObj = new GameObject("StorageGrid");
        gridObj.transform.SetParent(container.transform, false);
        var gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(9 * 48 + 8 * 3, 5 * 48 + 4 * 3);
        gridRect.anchoredPosition = new Vector2(0, -10);

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(48, 48);
        grid.spacing = new Vector2(3, 3);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;

        _storageGridParent = gridObj.transform;
    }

    private void RebuildStorageGrid()
    {
        // Clear old slots
        if (_storageGridParent != null)
        {
            for (int i = _storageGridParent.childCount - 1; i >= 0; i--)
                Destroy(_storageGridParent.GetChild(i).gameObject);
        }

        if (_currentStorage == null) return;

        int slotCount = _currentStorage.Container.SlotCount;
        _storageSlots = new StorageSlot[slotCount];

        if (_storageTitleText != null)
            _storageTitleText.text = _currentStorage.Definition.displayName;

        for (int i = 0; i < slotCount; i++)
        {
            int capturedIndex = i;
            _storageSlots[i] = StorageSlot.Create(_storageGridParent, i,
                () => OnSlotClicked(Side.Storage, capturedIndex));
        }
    }

    /// <summary>
    /// Simple clickable slot for the storage UI. Like InventorySlotUI but with a
    /// callback delegate instead of referencing a parent InventoryUI.
    /// </summary>
    private class StorageSlot : MonoBehaviour, IPointerClickHandler
    {
        private Image _iconImage;
        private TextMeshProUGUI _countText;
        private System.Action _onClick;

        private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0f);

        public static StorageSlot Create(Transform parent, int index, System.Action onClick)
        {
            var obj = new GameObject($"Slot_{index}");
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
            countText.fontSize = 11;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.color = Color.white;
            countText.raycastTarget = false;
            var countRect = countText.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(2, 2);
            countRect.offsetMax = new Vector2(-2, -2);

            var slot = obj.AddComponent<StorageSlot>();
            slot._iconImage = icon;
            slot._countText = countText;
            slot._onClick = onClick;

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
            _onClick?.Invoke();
        }
    }

    private void CreateCloseButton(Transform parent)
    {
        var btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);

        var btnRect = btnImage.rectTransform;
        btnRect.anchorMin = new Vector2(1, 1);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.sizeDelta = new Vector2(28, 28);
        btnRect.anchoredPosition = new Vector2(-4, -4);

        var textObj = new GameObject("X");
        textObj.transform.SetParent(btnObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "X";
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(Close);
    }
}
