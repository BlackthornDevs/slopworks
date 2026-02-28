using System;

/// <summary>
/// Typed slot-based container for machine I/O with per-slot accept filters.
/// Plain C# class per D-004 -- no MonoBehaviour dependency, fully testable.
/// </summary>
public class InventoryContainer
{
    private readonly ItemSlot[] _slots;
    private readonly Func<string, bool>[] _filters;

    public int SlotCount => _slots.Length;

    /// <param name="slotCount">Number of container slots. Must be greater than zero.</param>
    public InventoryContainer(int slotCount)
    {
        if (slotCount <= 0)
            throw new ArgumentException("Slot count must be greater than zero.", nameof(slotCount));

        _slots = new ItemSlot[slotCount];
        _filters = new Func<string, bool>[slotCount];
    }

    /// <summary>
    /// Sets an accept filter for a specific slot. Pass null to accept any item.
    /// </summary>
    public void SetSlotFilter(int index, Func<string, bool> filter)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} is out of range [0, {_slots.Length - 1}].");

        _filters[index] = filter;
    }

    /// <summary>
    /// Attempts to insert items into the specified slot.
    /// Returns false if the filter rejects the item, the slot contains a different item type,
    /// or the parameters are invalid.
    /// </summary>
    public bool TryInsert(int slotIndex, ItemInstance item, int count)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return false;
        if (item.IsEmpty || count <= 0)
            return false;

        var filter = _filters[slotIndex];
        if (filter != null && !filter(item.definitionId))
            return false;

        var existing = _slots[slotIndex];
        if (existing.IsEmpty)
        {
            _slots[slotIndex] = new ItemSlot { item = item, count = count };
            return true;
        }

        if (existing.item.definitionId != item.definitionId)
            return false;

        _slots[slotIndex] = new ItemSlot { item = existing.item, count = existing.count + count };
        return true;
    }

    /// <summary>
    /// Returns the contents of the specified slot. Throws if out of bounds.
    /// </summary>
    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} is out of range [0, {_slots.Length - 1}].");

        return _slots[index];
    }

    /// <summary>
    /// Removes up to the specified count from a slot and returns the extracted items.
    /// Returns an empty slot if the slot is empty or parameters are invalid.
    /// </summary>
    public ItemSlot Extract(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return ItemSlot.Empty;
        if (count <= 0)
            return ItemSlot.Empty;

        var existing = _slots[slotIndex];
        if (existing.IsEmpty)
            return ItemSlot.Empty;

        int extracted = Math.Min(count, existing.count);
        int remaining = existing.count - extracted;

        if (remaining <= 0)
        {
            _slots[slotIndex] = ItemSlot.Empty;
        }
        else
        {
            _slots[slotIndex] = new ItemSlot { item = existing.item, count = remaining };
        }

        return new ItemSlot { item = existing.item, count = extracted };
    }

    /// <summary>
    /// Returns the total count of an item across all slots.
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
    /// Empties all slots in the container.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = ItemSlot.Empty;
    }
}
