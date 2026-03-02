using UnityEngine;

/// <summary>
/// MonoBehaviour wrapper around Inventory. Owns a 45-slot inventory:
/// slots 0-8 = hotbar, slots 9-44 = main inventory.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public const int HotbarSlots = 9;
    public const int MainSlots = 36;
    public const int TotalSlots = HotbarSlots + MainSlots;

    private Inventory _inventory;
    private ItemRegistry _itemRegistry;
    private int _selectedHotbarIndex;

    public Inventory Inventory => _inventory;
    public int SelectedHotbarIndex => _selectedHotbarIndex;

    public ItemSlot SelectedSlot => _inventory.GetSlot(_selectedHotbarIndex);

    private void Awake()
    {
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        _inventory = new Inventory(TotalSlots, GetMaxStackSize);
    }

    private int GetMaxStackSize(string definitionId)
    {
        if (_itemRegistry == null) return 64;
        var def = _itemRegistry.Get(definitionId);
        return def != null && def.isStackable ? def.maxStackSize : 1;
    }

    public bool TryAdd(ItemInstance item, int count)
    {
        bool result = _inventory.TryAdd(item, count);
        if (result)
            Debug.Log($"inventory: added {count}x {item.definitionId}");
        return result;
    }

    public bool TryRemove(string definitionId, int count)
    {
        bool result = _inventory.TryRemove(definitionId, count);
        if (result)
            Debug.Log($"inventory: removed {count}x {definitionId}");
        return result;
    }

    public void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= HotbarSlots) return;
        _selectedHotbarIndex = index;
        Debug.Log($"hotbar: selected slot {index}");
    }
}
