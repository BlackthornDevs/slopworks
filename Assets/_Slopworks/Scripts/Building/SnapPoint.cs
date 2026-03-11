using UnityEngine;

/// <summary>
/// Legacy snap point for single-player playtest system.
/// Multiplayer uses BuildingSnapPoint instead.
/// </summary>
public class SnapPoint
{
    public Vector2Int Cell { get; }
    public float SurfaceY { get; }
    public Vector2Int EdgeDirection { get; }
    public SnapPointType Type { get; }
    public BuildingData Owner { get; }
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
