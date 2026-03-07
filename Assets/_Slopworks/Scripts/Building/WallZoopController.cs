using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure C# controller for "zoop" batch wall placement.
/// Click-drag to place a line of walls along connected foundation edges.
/// Follows D-004 pattern -- testable without Unity MonoBehaviours.
/// </summary>
public class WallZoopController
{
    public bool IsActive { get; private set; }
    public SnapPoint Origin { get; private set; }
    public IReadOnlyList<SnapPoint> PlannedWalls => _planned;

    private readonly List<SnapPoint> _planned = new();

    public void Begin(SnapPoint origin)
    {
        IsActive = true;
        Origin = origin;
        _planned.Clear();
        _planned.Add(origin);
    }

    public void Update(SnapPoint current, SnapPointRegistry registry, float surfaceY)
    {
        if (!IsActive || Origin == null) return;

        _planned.Clear();

        var edgeDir = Origin.EdgeDirection;

        // Zoop runs perpendicular to the edge direction.
        // North/south edges (edgeDir.y != 0) run along X, so walk X.
        // East/west edges (edgeDir.x != 0) run along Y, so walk Y.
        bool walkX = edgeDir.y != 0;

        const int maxZoopDistance = 20;

        int start = walkX ? Origin.Cell.x : Origin.Cell.y;
        int end = walkX ? current.Cell.x : current.Cell.y;
        end = Mathf.Clamp(end, start - maxZoopDistance, start + maxZoopDistance);

        int step = end >= start ? 1 : -1;
        for (int i = start; ; i += step)
        {
            var cell = walkX
                ? new Vector2Int(i, Origin.Cell.y)
                : new Vector2Int(Origin.Cell.x, i);

            var snap = registry.GetAt(cell, surfaceY, edgeDir);
            if (snap != null && !snap.IsOccupied)
                _planned.Add(snap);

            if (i == end) break;
        }
    }

    public IReadOnlyList<SnapPoint> End()
    {
        var result = new List<SnapPoint>(_planned);
        Cancel();
        return result;
    }

    public void Cancel()
    {
        IsActive = false;
        Origin = null;
        _planned.Clear();
    }
}
