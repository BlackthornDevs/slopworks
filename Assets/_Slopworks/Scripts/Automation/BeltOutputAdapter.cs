using System;
using System.Collections.Generic;

/// <summary>
/// Wraps the output end of a BeltSegment as an IItemSource.
/// Allows an inserter to pull items from the end of a belt.
/// </summary>
public class BeltOutputAdapter : IItemSource
{
    private readonly BeltSegment _belt;

    public BeltOutputAdapter(BeltSegment belt)
    {
        _belt = belt ?? throw new ArgumentNullException(nameof(belt));
    }

    public bool HasItemAvailable => _belt.HasItemAtEnd;

    public string PeekItemId()
    {
        if (!_belt.HasItemAtEnd)
            return null;

        IReadOnlyList<BeltItem> items = _belt.GetItems();
        if (items.Count == 0)
            return null;

        // The last item in the list is at the output end
        return items[items.Count - 1].itemId;
    }

    public bool TryExtract(out string itemId)
    {
        itemId = _belt.TryExtractFromEnd();
        return itemId != null;
    }
}
