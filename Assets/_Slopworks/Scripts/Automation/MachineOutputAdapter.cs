using System;

/// <summary>
/// Wraps a specific output slot of a Machine as an IItemSource.
/// Allows an inserter to pull finished products from a machine.
/// </summary>
public class MachineOutputAdapter : IItemSource
{
    private readonly Machine _machine;
    private readonly int _slotIndex;

    public MachineOutputAdapter(Machine machine, int slotIndex)
    {
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _slotIndex = slotIndex;
    }

    public bool HasItemAvailable => !_machine.GetOutput(_slotIndex).IsEmpty;

    public string PeekItemId()
    {
        var slot = _machine.GetOutput(_slotIndex);
        if (slot.IsEmpty)
            return null;
        return slot.item.definitionId;
    }

    public bool TryExtract(out string itemId)
    {
        var slot = _machine.ExtractOutput(_slotIndex, 1);
        if (slot.IsEmpty)
        {
            itemId = null;
            return false;
        }

        itemId = slot.item.definitionId;
        return true;
    }
}
