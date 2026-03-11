using System;
using System.Collections.Generic;

/// <summary>
/// Core belt simulation logic using the distance-offset model. Plain C# class (D-004) --
/// no MonoBehaviour, fully testable in EditMode.
///
/// Items are stored in order from INPUT end to OUTPUT end.
/// - items[0].distanceToNext = gap from the input end to the first item
/// - items[i].distanceToNext (i > 0) = gap from items[i-1] to items[i]
/// - terminalGap = gap from the last item to the output end
///
/// Total belt length = sum of all distanceToNext values + terminalGap.
/// Items have zero width for simplicity.
///
/// On tick, terminalGap decreases by speed (items slide toward output).
/// When terminalGap reaches 0, the last item is ready for extraction.
/// </summary>
public class BeltSegment
{
    private readonly int _totalLength;
    private readonly List<BeltItem> _items = new List<BeltItem>();
    private ushort _terminalGap;

    public int TotalLength => _totalLength;
    public int ItemCount => _items.Count;
    public ushort TerminalGap => _terminalGap;
    public bool IsEmpty => _items.Count == 0;
    public bool HasItemAtEnd => _items.Count > 0 && _terminalGap == 0;

    /// <param name="lengthInTiles">Length of the belt segment in tiles.</param>
    public BeltSegment(int lengthInTiles)
    {
        if (lengthInTiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(lengthInTiles), "Belt length must be positive.");

        _totalLength = lengthInTiles * BeltItem.SubdivisionsPerTile;
        _terminalGap = (ushort)Math.Min(_totalLength, ushort.MaxValue);
    }

    /// <summary>
    /// Create a belt segment from arc length in meters.
    /// Uses SubdivisionsPerTile (100) subdivisions per meter of arc length.
    /// This is the factory method for curved belts where length is computed
    /// from spline arc length rather than Manhattan tile distance.
    /// </summary>
    public static BeltSegment FromArcLength(float arcLengthMeters)
    {
        if (arcLengthMeters <= 0f)
            throw new ArgumentOutOfRangeException(nameof(arcLengthMeters),
                "Arc length must be positive.");

        int subdivisions = Math.Max(1,
            (int)Math.Round(arcLengthMeters * BeltItem.SubdivisionsPerTile));

        return new BeltSegment(subdivisions, fromSubdivisions: true);
    }

    private BeltSegment(int totalSubdivisions, bool fromSubdivisions)
    {
        if (totalSubdivisions <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalSubdivisions),
                "Total subdivisions must be positive.");

        _totalLength = totalSubdivisions;
        _terminalGap = (ushort)Math.Min(_totalLength, ushort.MaxValue);
    }

    /// <summary>
    /// Insert an item at the input end of the belt.
    /// Succeeds only if the gap before the current first item is at least minSpacing.
    /// On an empty belt, the item is placed at the input edge with the full belt length
    /// as the terminal gap.
    /// </summary>
    /// <param name="itemId">The item type to insert.</param>
    /// <param name="minSpacing">Minimum subdivisions required before the first item.</param>
    /// <returns>True if the item was inserted, false if there is not enough room.</returns>
    public bool TryInsertAtStart(string itemId, ushort minSpacing)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        if (_items.Count == 0)
        {
            _items.Add(new BeltItem
            {
                itemId = itemId,
                distanceToNext = 0
            });
            _terminalGap = (ushort)Math.Min(_totalLength, ushort.MaxValue);
            return true;
        }

        if (_items[0].distanceToNext < minSpacing)
            return false;

        _items.Insert(0, new BeltItem
        {
            itemId = itemId,
            distanceToNext = 0
        });
        return true;
    }

    /// <summary>
    /// Remove the item at the output end of the belt and return its itemId.
    /// Only succeeds if the last item has reached the output end (terminalGap == 0).
    /// </summary>
    /// <returns>The itemId of the extracted item, or null if extraction is not possible.</returns>
    public string TryExtractFromEnd()
    {
        if (_items.Count == 0)
            return null;

        if (_terminalGap > 0)
            return null;

        int lastIndex = _items.Count - 1;
        string extractedId = _items[lastIndex].itemId;
        ushort removedDistance = _items[lastIndex].distanceToNext;
        _items.RemoveAt(lastIndex);

        if (_items.Count == 0)
        {
            _terminalGap = (ushort)Math.Min(_totalLength, ushort.MaxValue);
        }
        else
        {
            // The gap that was between the removed item and its predecessor
            // now becomes the new terminal gap
            _terminalGap = removedDistance;
        }

        return extractedId;
    }

    /// <summary>
    /// Advance all items toward the output end by the given speed in subdivisions.
    /// Two values change per tick: items[0].distanceToNext increases (first item moves
    /// away from input end) and terminalGap decreases (last item moves toward output end).
    /// This preserves the invariant: totalLength = sum(distanceToNext) + terminalGap.
    /// O(1) per tick regardless of item count.
    /// </summary>
    /// <param name="speed">Number of subdivisions to advance this tick.</param>
    public void Tick(ushort speed)
    {
        if (_items.Count == 0)
            return;

        ushort actualMove;
        if (speed >= _terminalGap)
        {
            actualMove = _terminalGap;
            _terminalGap = 0;
        }
        else
        {
            actualMove = speed;
            _terminalGap = (ushort)(_terminalGap - speed);
        }

        // Increase first item's distance from input end to preserve the invariant
        var first = _items[0];
        first.distanceToNext = (ushort)(first.distanceToNext + actualMove);
        _items[0] = first;
    }

    /// <summary>
    /// Returns a read-only view of the items on the belt for rendering.
    /// Items are ordered from input end to output end.
    /// </summary>
    public IReadOnlyList<BeltItem> GetItems()
    {
        return _items.AsReadOnly();
    }

    /// <summary>
    /// Returns normalized positions (0 = input end, 1 = output end) for each item
    /// on the belt. Used by the visual layer to position item GameObjects.
    /// </summary>
    /// <param name="positions">List to populate with positions (cleared first).</param>
    public void GetItemPositions(List<float> positions)
    {
        positions.Clear();

        if (_items.Count == 0 || _totalLength == 0)
            return;

        float cumulative = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            cumulative += _items[i].distanceToNext;
            positions.Add(cumulative / _totalLength);
        }
    }
}
