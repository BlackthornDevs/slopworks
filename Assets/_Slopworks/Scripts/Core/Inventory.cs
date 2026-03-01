using System;

/// <summary>
/// Slot-based inventory logic. Plain C# class per D-004 -- no MonoBehaviour dependency.
/// Use a Func callback for stack size lookups so this class stays decoupled from ItemRegistry.
/// </summary>
public class Inventory
{
    private readonly ItemSlot[] _slots;
    private readonly Func<string, int> _getMaxStackSize;

    public int SlotCount => _slots.Length;

    /// <summary>
    /// Fired after a slot is modified. Argument is the slot index.
    /// </summary>
    public event Action<int> OnSlotChanged;

    /// <param name="slotCount">Number of inventory slots.</param>
    /// <param name="getMaxStackSize">
    /// Callback that returns the max stack size for a given definitionId.
    /// If null, defaults to 1 for all items.
    /// </param>
    public Inventory(int slotCount, Func<string, int> getMaxStackSize = null)
    {
        if (slotCount <= 0)
            throw new ArgumentException("Slot count must be greater than zero.", nameof(slotCount));

        _slots = new ItemSlot[slotCount];
        _getMaxStackSize = getMaxStackSize ?? (_ => 1);
    }

    /// <summary>
    /// Returns the slot at the given index. Throws if out of bounds.
    /// </summary>
    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} is out of range [0, {_slots.Length - 1}].");

        return _slots[index];
    }

    /// <summary>
    /// Tries to add items to the inventory. Stacks onto existing matching slots first,
    /// then fills empty slots. Returns true if all items were added, false if not enough space.
    /// On failure, no items are added (atomic operation).
    /// </summary>
    public bool TryAdd(ItemInstance item, int count)
    {
        if (item.IsEmpty || count <= 0)
            return false;

        int maxStack = _getMaxStackSize(item.definitionId);

        // Check if we have enough total space before modifying anything
        if (!HasSpace(item.definitionId, count, maxStack))
            return false;

        int remaining = count;

        // First pass: stack onto existing slots with the same definitionId
        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
                continue;

            if (_slots[i].item.definitionId != item.definitionId)
                continue;

            int spaceInSlot = maxStack - _slots[i].count;
            if (spaceInSlot <= 0)
                continue;

            int toAdd = Math.Min(remaining, spaceInSlot);
            _slots[i].count += toAdd;
            remaining -= toAdd;
            OnSlotChanged?.Invoke(i);
        }

        // Second pass: fill empty slots
        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
                continue;

            int toAdd = Math.Min(remaining, maxStack);
            _slots[i] = new ItemSlot
            {
                item = item,
                count = toAdd
            };
            remaining -= toAdd;
            OnSlotChanged?.Invoke(i);
        }

        return true;
    }

    /// <summary>
    /// Tries to remove a quantity of items with the given definitionId.
    /// Returns true if the full amount was removed. On failure, no items are removed.
    /// </summary>
    public bool TryRemove(string definitionId, int count)
    {
        if (string.IsNullOrEmpty(definitionId) || count <= 0)
            return false;

        int available = GetCount(definitionId);
        if (available < count)
            return false;

        int remaining = count;

        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
                continue;

            if (_slots[i].item.definitionId != definitionId)
                continue;

            int toRemove = Math.Min(remaining, _slots[i].count);
            _slots[i].count -= toRemove;
            remaining -= toRemove;

            if (_slots[i].count <= 0)
                _slots[i] = ItemSlot.Empty;

            OnSlotChanged?.Invoke(i);
        }

        return true;
    }

    /// <summary>
    /// Returns the total count of items with the given definitionId across all slots.
    /// </summary>
    public int GetCount(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return 0;

        int total = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].IsEmpty && _slots[i].item.definitionId == definitionId)
                total += _slots[i].count;
        }

        return total;
    }

    /// <summary>
    /// Checks whether the inventory has enough space for the given quantity of items.
    /// </summary>
    public bool HasSpace(string definitionId, int count, int maxStackSize)
    {
        if (string.IsNullOrEmpty(definitionId) || count <= 0)
            return false;

        int capacity = 0;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
            {
                capacity += maxStackSize;
            }
            else if (_slots[i].item.definitionId == definitionId)
            {
                capacity += maxStackSize - _slots[i].count;
            }

            if (capacity >= count)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Empties all slots in the inventory.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = ItemSlot.Empty;
            OnSlotChanged?.Invoke(i);
        }
    }

    /// <summary>
    /// Returns a copy of all slots. Modifying the returned array does not affect the inventory.
    /// </summary>
    public ItemSlot[] GetAllSlots()
    {
        var copy = new ItemSlot[_slots.Length];
        Array.Copy(_slots, copy, _slots.Length);
        return copy;
    }
}
