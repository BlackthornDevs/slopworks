using System;

/// <summary>
/// Wraps a specific input slot of a Machine as an IItemDestination.
/// Allows an inserter to push raw materials into a machine.
/// </summary>
public class MachineInputAdapter : IItemDestination
{
    private readonly Machine _machine;
    private readonly int _slotIndex;

    public MachineInputAdapter(Machine machine, int slotIndex)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _slotIndex = slotIndex;
    }

    public bool CanAccept(string itemId)
    {
        var slot = _machine.GetInput(_slotIndex);
        if (slot.IsEmpty)
            return true;

        // Accept if the slot already contains the same item type (stacking)
        return slot.item.definitionId == itemId;
    }

    public bool TryInsert(string itemId)
    {
        return _machine.TryInsertInput(_slotIndex, ItemInstance.Create(itemId), 1);
    }
}
