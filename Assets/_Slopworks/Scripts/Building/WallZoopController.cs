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
    private bool _axisLocked;
    private bool _lockedToX; // true = X axis, false = Y axis

    public void Begin(SnapPoint origin)
    {
        IsActive = true;
        Origin = origin;
        _planned.Clear();
        _planned.Add(origin);
        _axisLocked = false;
    }

    public void Update(SnapPoint current, SnapPointRegistry registry, int level)
    {
        if (!IsActive || Origin == null) return;

        // Determine axis from origin-to-current cell comparison
        var diff = current.Cell - Origin.Cell;

        if (!_axisLocked)
        {
            if (diff.x != 0 && diff.y == 0)
            {
                _axisLocked = true;
                _lockedToX = true;
            }
            else if (diff.y != 0 && diff.x == 0)
            {
                _axisLocked = true;
                _lockedToX = false;
            }
            else if (diff.x == 0 && diff.y == 0)
            {
                // No movement yet -- keep only origin
                _planned.Clear();
                _planned.Add(Origin);
                return;
            }
            else
            {
                // Diagonal movement -- lock to dominant axis
                _axisLocked = true;
                _lockedToX = Mathf.Abs(diff.x) >= Mathf.Abs(diff.y);
            }
        }

        _planned.Clear();

        // Walk along locked axis from origin to current
        var edgeDir = Origin.EdgeDirection;
        int start, end;

        if (_lockedToX)
        {
            start = Origin.Cell.x;
            end = current.Cell.x;
        }
        else
        {
            start = Origin.Cell.y;
            end = current.Cell.y;
        }

        int step = end >= start ? 1 : -1;
        for (int i = start; ; i += step)
        {
            var cell = _lockedToX
                ? new Vector2Int(i, Origin.Cell.y)
                : new Vector2Int(Origin.Cell.x, i);

            var snap = registry.GetAt(cell, level, edgeDir);
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
        _axisLocked = false;
    }
}
