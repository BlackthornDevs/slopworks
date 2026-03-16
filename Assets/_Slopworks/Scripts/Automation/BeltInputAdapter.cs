using System;

/// <summary>
/// Wraps the input end of a BeltSegment as an IItemDestination.
/// When the segment is reversed, the input port is at the end
/// of the internal list instead of the start.
/// </summary>
public class BeltInputAdapter : IItemDestination
{
    private readonly BeltSegment _belt;
    private readonly ushort _minSpacing;

    public BeltSegment Belt => _belt;

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

        if (_belt.Reversed)
            return _belt.TerminalGap >= _minSpacing;

        var items = _belt.GetItems();
        return items[0].distanceToNext >= _minSpacing;
    }

    public bool TryInsert(string itemId)
    {
        return _belt.Reversed
            ? _belt.TryInsertAtEnd(itemId, _minSpacing)
            : _belt.TryInsertAtStart(itemId, _minSpacing);
    }
}
