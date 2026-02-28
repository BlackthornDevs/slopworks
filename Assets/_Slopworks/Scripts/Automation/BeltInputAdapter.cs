using System;

/// <summary>
/// Wraps the input end of a BeltSegment as an IItemDestination.
/// Allows an inserter to push items onto the start of a belt.
/// </summary>
public class BeltInputAdapter : IItemDestination
{
    private readonly BeltSegment _belt;
    private readonly ushort _minSpacing;

    /// <param name="belt">The belt segment to insert into.</param>
    /// <param name="minSpacing">Minimum subdivisions required before the first item on the belt. Defaults to 50.</param>
    public BeltInputAdapter(BeltSegment belt, ushort minSpacing = 50)
    {
        _belt = belt ?? throw new ArgumentNullException(nameof(belt));
        _minSpacing = minSpacing;
    }

    public bool CanAccept(string itemId)
    {
        if (_belt.IsEmpty)
            return true;

        // Check if there is enough room at the input end.
        // The first item's distanceToNext represents how far it is from the input edge.
        var items = _belt.GetItems();
        return items[0].distanceToNext >= _minSpacing;
    }

    public bool TryInsert(string itemId)
    {
        return _belt.TryInsertAtStart(itemId, _minSpacing);
    }
}
