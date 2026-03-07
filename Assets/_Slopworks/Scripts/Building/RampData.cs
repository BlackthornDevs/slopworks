using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime data for a placed ramp segment.
/// Ramps connect level N to N+1 at foundation edges.
/// </summary>
public class RampData
{
    /// <summary>
    /// The cell at the base of the ramp (lower level).
    /// </summary>
    public Vector2Int BaseCell { get; }

    /// <summary>
    /// World-space Y position of the surface at the base of the ramp.
    /// </summary>
    public float BaseSurfaceY { get; }

    /// <summary>
    /// Direction the ramp goes (from base toward top).
    /// One of the 4 cardinal directions.
    /// </summary>
    public Vector2Int Direction { get; }

    /// <summary>
    /// How many cells the ramp occupies on the lower level.
    /// </summary>
    public int FootprintLength { get; }

    /// <summary>
    /// The snap point at the base of the ramp.
    /// </summary>
    public SnapPoint BaseSnapPoint { get; set; }

    /// <summary>
    /// The snap point at the top of the ramp (upper level).
    /// </summary>
    public SnapPoint TopSnapPoint { get; set; }

    public GameObject Instance { get; set; }

    /// <summary>
    /// All cells occupied by the ramp on the lower level.
    /// </summary>
    public List<Vector2Int> OccupiedCells { get; } = new();

    public RampData(Vector2Int baseCell, float baseSurfaceY, Vector2Int direction, int footprintLength)
    {
        BaseCell = baseCell;
        BaseSurfaceY = baseSurfaceY;
        Direction = direction;
        FootprintLength = footprintLength;
    }
}
