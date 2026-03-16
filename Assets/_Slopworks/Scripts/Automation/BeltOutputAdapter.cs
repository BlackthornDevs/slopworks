using System;
using System.Collections.Generic;

/// <summary>
/// Wraps the output end of a BeltSegment as an IItemSource.
/// When the segment is reversed, the output port is at the start
/// of the internal list instead of the end.
/// </summary>
public class BeltOutputAdapter : IItemSource
{
    private readonly BeltSegment _belt;

    public BeltSegment Belt => _belt;

    public BeltOutputAdapter(BeltSegment belt)
    {
        _belt = belt ?? throw new ArgumentNullException(nameof(belt));
    }

    public bool HasItemAvailable => _belt.Reversed ? _belt.HasItemAtStart : _belt.HasItemAtEnd;

    public string PeekItemId()
    {
        if (!HasItemAvailable)
            return null;

        IReadOnlyList<BeltItem> items = _belt.GetItems();
        if (items.Count == 0)
            return null;

        return _belt.Reversed ? items[0].itemId : items[items.Count - 1].itemId;
    }

    public bool TryExtract(out string itemId)
    {
        itemId = _belt.Reversed ? _belt.TryExtractFromStart() : _belt.TryExtractFromEnd();
        return itemId != null;
    }
}
