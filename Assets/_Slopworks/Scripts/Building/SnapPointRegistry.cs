using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy snap point registry for single-player playtest system.
/// Multiplayer uses BuildingSnapPoint instead.
/// </summary>
public class SnapPointRegistry
{
    private readonly Dictionary<(int, int, int, int, int), SnapPoint> _snapPoints = new();
    private readonly Dictionary<BuildingData, List<SnapPoint>> _pointsByOwner = new();

    public int Count => _snapPoints.Count;

    private static (int, int, int, int, int) GetKey(Vector2Int cell, float surfaceY, Vector2Int edgeDir)
    {
        return (cell.x, cell.y, FactoryGrid.YBucket(surfaceY), edgeDir.x, edgeDir.y);
    }

    public void Register(SnapPoint point)
    {
        var key = GetKey(point.Cell, point.SurfaceY, point.EdgeDirection);
        _snapPoints[key] = point;

        if (!_pointsByOwner.TryGetValue(point.Owner, out var list))
        {
            list = new List<SnapPoint>();
            _pointsByOwner[point.Owner] = list;
        }
        list.Add(point);
    }

    public void Unregister(SnapPoint point)
    {
        var key = GetKey(point.Cell, point.SurfaceY, point.EdgeDirection);
        _snapPoints.Remove(key);

        if (_pointsByOwner.TryGetValue(point.Owner, out var list))
        {
            list.Remove(point);
            if (list.Count == 0)
                _pointsByOwner.Remove(point.Owner);
        }
    }

    public SnapPoint GetAt(Vector2Int cell, float surfaceY, Vector2Int edgeDirection)
    {
        var key = GetKey(cell, surfaceY, edgeDirection);
        _snapPoints.TryGetValue(key, out var point);
        return point;
    }

    public List<SnapPoint> GetPointsForOwner(BuildingData owner)
    {
        if (_pointsByOwner.TryGetValue(owner, out var list))
            return new List<SnapPoint>(list);
        return new List<SnapPoint>();
    }

    public List<SnapPoint> GetAvailableAt(Vector2Int cell, float surfaceY)
    {
        var result = new List<SnapPoint>();
        var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        foreach (var dir in directions)
        {
            var key = GetKey(cell, surfaceY, dir);
            if (_snapPoints.TryGetValue(key, out var point) && !point.IsOccupied)
                result.Add(point);
        }

        return result;
    }
}
