using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates foundation snap-point registration, wall placement/validation,
/// and removal with dependency checking. Plain C# class (D-004).
/// </summary>
public class StructuralPlacementService
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,    // North (0, 1)
        Vector2Int.right, // East  (1, 0)
        Vector2Int.down,  // South (0, -1)
        Vector2Int.left   // West  (-1, 0)
    };

    private readonly FactoryGrid _grid;
    private readonly SnapPointRegistry _snapRegistry;
    private readonly Dictionary<BuildingData, List<WallData>> _wallsByFoundation = new();
    private readonly List<RampData> _ramps = new();

    public StructuralPlacementService(FactoryGrid grid, SnapPointRegistry snapRegistry)
    {
        _grid = grid;
        _snapRegistry = snapRegistry;
    }

    /// <summary>
    /// Place a foundation at the given cell and surface Y. Registers snap points on
    /// all perimeter edges, suppressing shared edges with adjacent foundations.
    /// </summary>
    public BuildingData PlaceFoundation(FoundationDefinitionSO def, Vector2Int cell, float surfaceY)
    {
        if (!_grid.CanPlace(cell, def.size, surfaceY))
            return null;

        var data = new BuildingData(def.foundationId, cell, def.size, 0, Mathf.RoundToInt(surfaceY));
        data.IsStructural = true;
        _grid.Place(cell, def.size, surfaceY, data);

        if (def.generatesSnapPoints)
            RegisterFoundationSnapPoints(data, cell, def.size, surfaceY);

        return data;
    }

    /// <summary>
    /// Place a wall at the given snap point. The snap point must be unoccupied
    /// and of type FoundationEdge or WallEnd.
    /// </summary>
    public WallData PlaceWall(WallDefinitionSO def, SnapPoint attachPoint)
    {
        if (attachPoint == null)
            return null;

        if (attachPoint.IsOccupied)
            return null;

        if (attachPoint.Type != SnapPointType.FoundationEdge && attachPoint.Type != SnapPointType.WallEnd)
            return null;

        attachPoint.IsOccupied = true;

        var wallData = new WallData(def.wallId, attachPoint);

        // Track wall against its foundation for removal dependency checking
        if (!_wallsByFoundation.TryGetValue(attachPoint.Owner, out var walls))
        {
            walls = new List<WallData>();
            _wallsByFoundation[attachPoint.Owner] = walls;
        }
        walls.Add(wallData);

        return wallData;
    }

    /// <summary>
    /// Place a wall on any foundation cell edge. Does not require an exterior snap point --
    /// works for interior edges where snap points are suppressed.
    /// The foundation cell must contain a structural building.
    /// </summary>
    public WallData PlaceWall(WallDefinitionSO def, Vector2Int cell, float surfaceY, Vector2Int direction)
    {
        var source = _grid.GetAt(cell, surfaceY);
        if (source == null || !source.IsStructural)
            return null;

        // If an exterior snap point exists at this edge, use the snap point path
        var edgeSnap = _snapRegistry.GetAt(cell, surfaceY, direction);
        if (edgeSnap != null)
            return PlaceWall(def, edgeSnap);

        // Interior edge -- no snap point. Check no wall already exists here.
        // (Tracked via _wallsByFoundation, checked at removal time.)
        var wallData = new WallData(def.wallId, cell, surfaceY, direction);

        // Track wall against its foundation for removal dependency checking
        if (!_wallsByFoundation.TryGetValue(source, out var walls))
        {
            walls = new List<WallData>();
            _wallsByFoundation[source] = walls;
        }
        walls.Add(wallData);

        return wallData;
    }

    /// <summary>
    /// Remove a foundation. Fails if any walls are still attached.
    /// Returns true if removed, false if walls block removal.
    /// </summary>
    public bool RemoveFoundation(BuildingData data)
    {
        if (data == null)
            return false;

        // Check for attached walls
        if (_wallsByFoundation.TryGetValue(data, out var walls) && walls.Count > 0)
            return false;

        // Remove snap points and restore neighbor edges
        var snapPoints = _snapRegistry.GetPointsForOwner(data);
        foreach (var sp in snapPoints)
            _snapRegistry.Unregister(sp);

        // Restore neighbor edge snap points that were suppressed
        float surfaceY = data.Level;
        RestoreNeighborEdges(data.Origin, data.Size, surfaceY);

        _grid.Remove(data.Origin, data.Size, surfaceY);
        _wallsByFoundation.Remove(data);
        return true;
    }

    /// <summary>
    /// Remove a wall, freeing its snap point.
    /// </summary>
    public void RemoveWall(WallData wallData)
    {
        if (wallData == null)
            return;

        // Free the snap point if one exists (exterior edges)
        if (wallData.AttachPoint != null)
        {
            wallData.AttachPoint.IsOccupied = false;

            if (_wallsByFoundation.TryGetValue(wallData.AttachPoint.Owner, out var snapWalls))
                snapWalls.Remove(wallData);
        }
        else
        {
            // Interior wall -- find the foundation at this cell and remove tracking
            var source = _grid.GetAt(wallData.Cell, wallData.SurfaceY);
            if (source != null && _wallsByFoundation.TryGetValue(source, out var cellWalls))
                cellWalls.Remove(wallData);
        }
    }

    /// <summary>
    /// Place a ramp extending from a foundation edge snap point.
    /// </summary>
    public RampData PlaceRamp(RampDefinitionSO def, SnapPoint baseEdgeSnap)
    {
        if (baseEdgeSnap == null || baseEdgeSnap.IsOccupied)
            return null;

        if (baseEdgeSnap.Type != SnapPointType.FoundationEdge)
            return null;

        var ramp = PlaceRampInternal(def, baseEdgeSnap.Cell, baseEdgeSnap.SurfaceY, baseEdgeSnap.EdgeDirection);
        if (ramp != null)
            baseEdgeSnap.IsOccupied = true;

        return ramp;
    }

    /// <summary>
    /// Place a ramp from any foundation cell in any direction. Does not require an
    /// exterior snap point -- works for interior edges where snap points are suppressed.
    /// The foundation cell must contain a structural building.
    /// </summary>
    public RampData PlaceRamp(RampDefinitionSO def, Vector2Int foundationCell, float surfaceY, Vector2Int direction)
    {
        // Verify the source cell has a structural building (foundation)
        var source = _grid.GetAt(foundationCell, surfaceY);
        if (source == null || !source.IsStructural)
            return null;

        // If an exterior snap point exists at this edge, mark it occupied
        var edgeSnap = _snapRegistry.GetAt(foundationCell, surfaceY, direction);
        if (edgeSnap != null && edgeSnap.IsOccupied)
            return null;

        var ramp = PlaceRampInternal(def, foundationCell, surfaceY, direction);
        if (ramp != null && edgeSnap != null)
            edgeSnap.IsOccupied = true;

        return ramp;
    }

    private RampData PlaceRampInternal(RampDefinitionSO def, Vector2Int sourceCell, float baseSurfaceY, Vector2Int direction)
    {
        var rampStart = sourceCell + direction;

        // Check all footprint cells: must be in bounds and either empty or structural (foundation).
        // Ramps live on a separate layer from the grid -- they don't overwrite foundations.
        for (int i = 0; i < def.footprintLength; i++)
        {
            var cell = rampStart + direction * i;
            if (!_grid.IsInBounds(cell))
                return null;

            var existing = _grid.GetAt(cell, baseSurfaceY);
            if (existing != null && !existing.IsStructural)
                return null;

            // Check no other ramp already occupies this cell
            foreach (var existingRamp in _ramps)
            {
                if (Mathf.Approximately(existingRamp.BaseSurfaceY, baseSurfaceY) && existingRamp.OccupiedCells.Contains(cell))
                    return null;
            }
        }

        // Create ramp data
        var rampData = new RampData(rampStart, baseSurfaceY, direction, def.footprintLength);

        // BuildingData for snap point ownership (not placed on grid)
        var rampOwner = new BuildingData(def.rampId, rampStart, Vector2Int.one, 0, Mathf.RoundToInt(baseSurfaceY));

        // Track footprint cells (ramps don't write to the grid -- foundations stay underneath)
        for (int i = 0; i < def.footprintLength; i++)
        {
            var cell = rampStart + direction * i;
            rampData.OccupiedCells.Add(cell);
        }

        // Create RampBase snap point at the base
        var baseSnapPoint = new SnapPoint(rampStart, baseSurfaceY, -direction,
            SnapPointType.RampBase, rampOwner);
        _snapRegistry.Register(baseSnapPoint);
        rampData.BaseSnapPoint = baseSnapPoint;

        // Create RampTop snap point at the top (upper surface, at the far end)
        var topCell = rampStart + direction * (def.footprintLength - 1);
        float topSurfaceY = baseSurfaceY + FactoryGrid.WallHeight;
        var topSnapPoint = new SnapPoint(topCell, topSurfaceY, direction,
            SnapPointType.RampTop, rampOwner);
        _snapRegistry.Register(topSnapPoint);
        rampData.TopSnapPoint = topSnapPoint;

        _ramps.Add(rampData);
        return rampData;
    }

    /// <summary>
    /// Remove a ramp, clearing its grid cells and snap points.
    /// </summary>
    public void RemoveRamp(RampData rampData)
    {
        if (rampData == null)
            return;

        // Free the foundation edge snap point
        // Find the snap point on the foundation that this ramp was attached to
        var foundationCell = rampData.BaseCell - rampData.Direction;
        var foundationEdgeSnap = _snapRegistry.GetAt(foundationCell, rampData.BaseSurfaceY, rampData.Direction);
        if (foundationEdgeSnap != null)
            foundationEdgeSnap.IsOccupied = false;

        // Remove ramp snap points
        if (rampData.BaseSnapPoint != null)
            _snapRegistry.Unregister(rampData.BaseSnapPoint);
        if (rampData.TopSnapPoint != null)
            _snapRegistry.Unregister(rampData.TopSnapPoint);

        _ramps.Remove(rampData);
    }

    /// <summary>
    /// Get snap points for a foundation that are available (unoccupied).
    /// </summary>
    public List<SnapPoint> GetAvailableSnapPoints(BuildingData foundation)
    {
        var all = _snapRegistry.GetPointsForOwner(foundation);
        var available = new List<SnapPoint>();
        foreach (var sp in all)
        {
            if (!sp.IsOccupied)
                available.Add(sp);
        }
        return available;
    }

    private void RegisterFoundationSnapPoints(BuildingData data, Vector2Int origin, Vector2Int size, float surfaceY)
    {
        // Register snap points on all 4 edges of each cell in the foundation footprint
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                var cell = new Vector2Int(x, z);

                foreach (var dir in CardinalDirections)
                {
                    // Check if this edge is internal (shared with another cell in the same foundation)
                    var neighborCell = cell + dir;
                    bool isInternal = IsWithinFootprint(neighborCell, origin, size);

                    if (isInternal)
                        continue;

                    // Adjacent foundation: keep both edge snap points so walls can be placed
                    // on interior edges between foundations.

                    var snapPoint = new SnapPoint(cell, surfaceY, dir, SnapPointType.FoundationEdge, data);
                    _snapRegistry.Register(snapPoint);
                }
            }
        }
    }

    private void RestoreNeighborEdges(Vector2Int origin, Vector2Int size, float surfaceY)
    {
        // For each perimeter cell of the removed foundation, check if neighbors
        // need their edge snap points restored
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                var cell = new Vector2Int(x, z);

                foreach (var dir in CardinalDirections)
                {
                    var neighborCell = cell + dir;
                    if (IsWithinFootprint(neighborCell, origin, size))
                        continue;

                    var neighborData = _grid.GetAt(neighborCell, surfaceY);
                    if (neighborData == null)
                        continue;

                    // Neighbor exists: restore its edge facing us (the now-removed cell)
                    var existingSnap = _snapRegistry.GetAt(neighborCell, surfaceY, -dir);
                    if (existingSnap == null)
                    {
                        var restoredPoint = new SnapPoint(neighborCell, surfaceY, -dir,
                            SnapPointType.FoundationEdge, neighborData);
                        _snapRegistry.Register(restoredPoint);
                    }
                }
            }
        }
    }

    private static bool IsWithinFootprint(Vector2Int cell, Vector2Int origin, Vector2Int size)
    {
        return cell.x >= origin.x && cell.x < origin.x + size.x
            && cell.y >= origin.y && cell.y < origin.y + size.y;
    }
}
