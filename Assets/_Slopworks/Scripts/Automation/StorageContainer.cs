using System;

/// <summary>
/// Slot-based storage container with stacking support. Plain C# class per D-004.
/// Implements IItemSource and IItemDestination for inserter compatibility.
/// Items with the same definitionId stack up to maxStackSize per slot.
/// Overflow goes to the next available slot.
/// </summary>
public class StorageContainer : IItemSource, IItemDestination
{
    private readonly ItemSlot[] _slots;
    private readonly int _maxStackSize;

    /// <summary>
    /// Fired after a slot is modified. Argument is the slot index.
    /// </summary>
    public event Action<int> OnSlotChanged;

    public int SlotCount => _slots.Length;

    public bool IsEmpty
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty)
                    return false;
            }
            return true;
        }
    }

    public bool IsFull
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty || _slots[i].count < _maxStackSize)
                    return false;
            }
            return true;
        }
    }

    /// <param name="slotCount">Number of storage slots. Must be greater than zero.</param>
    /// <param name="maxStackSize">Maximum items per stack. Must be greater than zero.</param>
    public StorageContainer(int slotCount, int maxStackSize)
    {
        if (slotCount <= 0)
            throw new ArgumentException("Slot count must be greater than zero.", nameof(slotCount));
        if (maxStackSize <= 0)
            throw new ArgumentException("Max stack size must be greater than zero.", nameof(maxStackSize));

        _slots = new ItemSlot[slotCount];
        _maxStackSize = maxStackSize;
    }

    // -- IItemSource --

    public bool HasItemAvailable
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsEmpty)
                    return true;
            }
            return false;
        }
    }

    public string PeekItemId()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].IsEmpty)
                return _slots[i].item.definitionId;
        }
        return null;
    }

    public bool TryExtract(out string itemId)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
                continue;

            itemId = _slots[i].item.definitionId;
            int remaining = _slots[i].count - 1;

            if (remaining <= 0)
            {
                _slots[i] = ItemSlot.Empty;
            }
            else
            {
                _slots[i] = new ItemSlot { item = _slots[i].item, count = remaining };
            }

            OnSlotChanged?.Invoke(i);
            return true;
        }

        itemId = null;
        return false;
    }

    // -- IItemDestination --

    public bool CanAccept(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
                return true;

            if (_slots[i].item.definitionId == itemId && _slots[i].count < _maxStackSize)
                return true;
        }

        return false;
    }

    public bool TryInsert(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        // First pass: find a matching slot with room
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
                continue;

            if (_slots[i].item.definitionId == itemId && _slots[i].count < _maxStackSize)
            {
                _slots[i] = new ItemSlot { item = _slots[i].item, count = _slots[i].count + 1 };
                OnSlotChanged?.Invoke(i);
                return true;
            }
        }

        // Second pass: find an empty slot
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
            {
                _slots[i] = new ItemSlot { item = ItemInstance.Create(itemId), count = 1 };
                OnSlotChanged?.Invoke(i);
                return true;
            }
        }

        return false;
    }

    // -- Additional methods --

    /// <summary>
    /// Returns the contents of the specified slot.
    /// </summary>
    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Slot index {index} is out of range [0, {_slots.Length - 1}].");

        return _slots[index];
    }

    /// <summary>
    /// Directly sets a slot's contents. Used by UI for click-to-transfer operations.
    /// </summary>
    public void SetSlot(int index, ItemSlot slot)
    {
        if (index < 0 || index >= _slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        _slots[index] = slot;
        OnSlotChanged?.Invoke(index);
    }

    /// <summary>
    /// Returns the total number of items across all slots.
    /// </summary>
    public int GetTotalItemCount()
    {
        int total = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].IsEmpty)
                total += _slots[i].count;
        }
        return total;
    }

    /// <summary>
    /// Returns the count of a specific item across all slots.
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
    /// Attempts to insert multiple items at once. Atomic: checks space first,
    /// fails without modifying state if there is not enough room.
    /// </summary>
    public bool TryInsertStack(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return false;

        // Calculate available space for this item
        int availableSpace = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
            {
                availableSpace += _maxStackSize;
            }
            else if (_slots[i].item.definitionId == itemId)
            {
                availableSpace += _maxStackSize - _slots[i].count;
            }

            // Early exit if we already have enough space
            if (availableSpace >= count)
                break;
        }

        if (availableSpace < count)
            return false;

        // Space confirmed, now insert
        int remaining = count;

        // First pass: fill matching slots
        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
                continue;

            if (_slots[i].item.definitionId == itemId && _slots[i].count < _maxStackSize)
            {
                int canAdd = _maxStackSize - _slots[i].count;
                int toAdd = Math.Min(canAdd, remaining);
                _slots[i] = new ItemSlot { item = _slots[i].item, count = _slots[i].count + toAdd };
                remaining -= toAdd;
                OnSlotChanged?.Invoke(i);
            }
        }

        // Second pass: fill empty slots
        for (int i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                int toAdd = Math.Min(_maxStackSize, remaining);
                _slots[i] = new ItemSlot { item = ItemInstance.Create(itemId), count = toAdd };
                remaining -= toAdd;
                OnSlotChanged?.Invoke(i);
            }
        }

        return true;
    }

    /// <summary>
    /// Removes all items with the specified definitionId from all slots.
    /// Returns the total count removed.
    /// </summary>
    public int ExtractAll(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        int totalRemoved = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].IsEmpty && _slots[i].item.definitionId == itemId)
            {
                totalRemoved += _slots[i].count;
                _slots[i] = ItemSlot.Empty;
                OnSlotChanged?.Invoke(i);
            }
        }
        return totalRemoved;
    }
}
