using System.Collections.Generic;

/// <summary>
/// Manages belt-to-belt connections, transferring items from the output end
/// of one belt segment to the input end of another. Plain C# class per D-004.
///
/// Each connection can hold one item in transit. If the destination belt
/// rejects an insert (full at input end), the item is held and retried
/// on the next tick.
/// </summary>
public class BeltNetwork
{
    private const ushort DefaultInsertSpacing = 50;

    private struct BeltConnection
    {
        public BeltSegment From;
        public BeltSegment To;
        public string HeldItemId;
    }

    private readonly List<BeltConnection> _connections = new List<BeltConnection>();

    public int ConnectionCount => _connections.Count;

    /// <summary>
    /// Register a connection from the output of one belt to the input of another.
    /// Duplicate connections (same from and to pair) are ignored.
    /// </summary>
    public void Connect(BeltSegment from, BeltSegment to)
    {
        if (from == null || to == null)
            return;

        if (IsConnected(from, to))
            return;

        _connections.Add(new BeltConnection
        {
            From = from,
            To = to,
            HeldItemId = null
        });
    }

    /// <summary>
    /// Remove a connection between two belt segments.
    /// </summary>
    public void Disconnect(BeltSegment from, BeltSegment to)
    {
        for (int i = _connections.Count - 1; i >= 0; i--)
        {
            if (_connections[i].From == from && _connections[i].To == to)
            {
                _connections.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Check if a connection exists between two belt segments.
    /// </summary>
    public bool IsConnected(BeltSegment from, BeltSegment to)
    {
        for (int i = 0; i < _connections.Count; i++)
        {
            if (_connections[i].From == from && _connections[i].To == to)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Process all connections. For each connection:
    /// - If holding an item from a previous failed insert, retry inserting it.
    /// - Otherwise, if the source belt has an item at its output end, extract it
    ///   and try to insert into the destination belt.
    /// - If the destination rejects the insert, hold the item until next tick.
    /// </summary>
    public void Tick()
    {
        for (int i = 0; i < _connections.Count; i++)
        {
            var conn = _connections[i];

            if (conn.HeldItemId != null)
            {
                // Retry inserting the held item
                if (conn.To.TryInsertAtStart(conn.HeldItemId, DefaultInsertSpacing))
                {
                    conn.HeldItemId = null;
                    _connections[i] = conn;
                }
                continue;
            }

            if (!conn.From.HasItemAtEnd)
                continue;

            string itemId = conn.From.TryExtractFromEnd();
            if (itemId == null)
                continue;

            if (!conn.To.TryInsertAtStart(itemId, DefaultInsertSpacing))
            {
                // Destination rejected, hold item until next tick
                conn.HeldItemId = itemId;
            }

            _connections[i] = conn;
        }
    }
}
