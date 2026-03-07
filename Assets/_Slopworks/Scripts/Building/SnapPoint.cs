using UnityEngine;

/// <summary>
/// A structural attachment point on a foundation edge. Walls, ramps,
/// and other structural pieces connect through snap points.
/// Separate from PortNode which handles item I/O flow.
/// </summary>
public class SnapPoint
{
    /// <summary>
    /// Grid cell this snap point belongs to.
    /// </summary>
    public Vector2Int Cell { get; }

    /// <summary>
    /// World-space Y position of the surface this snap point sits on.
    /// </summary>
    public float SurfaceY { get; }

    /// <summary>
    /// Edge direction (N/E/S/W) this snap point faces.
    /// North = (0,1), East = (1,0), South = (0,-1), West = (-1,0).
    /// </summary>
    public Vector2Int EdgeDirection { get; }

    /// <summary>
    /// What kind of snap point this is.
    /// </summary>
    public SnapPointType Type { get; }

    /// <summary>
    /// The BuildingData that owns this snap point.
    /// </summary>
    public BuildingData Owner { get; }

    /// <summary>
    /// Whether a wall or ramp is currently attached to this snap point.
    /// </summary>
    public bool IsOccupied { get; set; }

    public SnapPoint(Vector2Int cell, float surfaceY, Vector2Int edgeDirection, SnapPointType type, BuildingData owner)
    {
        Cell = cell;
        SurfaceY = surfaceY;
        EdgeDirection = edgeDirection;
        Type = type;
        Owner = owner;
    }
}
