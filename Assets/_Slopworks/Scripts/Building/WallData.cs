using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime data for a placed wall segment.
/// Tracks which snap points this wall connects.
/// </summary>
public class WallData
{
    public string WallId { get; }
    public SnapPoint AttachPoint { get; }
    public Vector2Int Cell { get; }
    public float SurfaceY { get; }
    public Vector2Int EdgeDirection { get; }
    public GameObject Instance { get; set; }

    /// <summary>
    /// WallEnd snap points created by this wall for chaining.
    /// </summary>
    public List<SnapPoint> WallEndPoints { get; } = new();

    public WallData(string wallId, SnapPoint attachPoint)
    {
        WallId = wallId;
        AttachPoint = attachPoint;
        Cell = attachPoint.Cell;
        SurfaceY = attachPoint.SurfaceY;
        EdgeDirection = attachPoint.EdgeDirection;
    }

    public WallData(string wallId, Vector2Int cell, float surfaceY, Vector2Int edgeDirection)
    {
        WallId = wallId;
        Cell = cell;
        SurfaceY = surfaceY;
        EdgeDirection = edgeDirection;
    }
}
