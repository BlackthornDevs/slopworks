using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cursor-to-nearest-snap-point logic for wall placement UX.
/// Plain C# class (D-004).
/// </summary>
public class WallPlacementController
{
    private readonly SnapPointRegistry _snapRegistry;

    public SnapPoint NearestSnapPoint { get; private set; }

    public WallPlacementController(SnapPointRegistry snapRegistry)
    {
        _snapRegistry = snapRegistry;
    }

    /// <summary>
    /// Given a world cursor position, find the nearest unoccupied foundation edge
    /// snap point within range. Updates NearestSnapPoint.
    /// </summary>
    public void UpdateFromCursor(Vector3 cursorWorldPos, FactoryGrid grid, float surfaceY, float maxDistance = 1.5f)
    {
        var cursorCell = grid.WorldToCell(cursorWorldPos);
        NearestSnapPoint = null;
        float bestDist = float.MaxValue;

        // Search the cursor cell and its immediate neighbors
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var checkCell = new Vector2Int(cursorCell.x + dx, cursorCell.y + dz);
                var available = _snapRegistry.GetAvailableAt(checkCell, surfaceY);

                foreach (var snap in available)
                {
                    var snapWorldPos = GetSnapWorldPosition(snap, grid);
                    float dist = Vector3.Distance(cursorWorldPos, snapWorldPos);

                    if (dist < bestDist && dist <= maxDistance)
                    {
                        bestDist = dist;
                        NearestSnapPoint = snap;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get the world position of a snap point (at the center of the cell edge it represents).
    /// </summary>
    public static Vector3 GetSnapWorldPosition(SnapPoint snap, FactoryGrid grid)
    {
        var cellCenter = grid.CellToWorld(snap.Cell, snap.SurfaceY);
        float halfCell = FactoryGrid.CellSize * 0.5f;

        return cellCenter + new Vector3(
            snap.EdgeDirection.x * halfCell,
            0f,
            snap.EdgeDirection.y * halfCell);
    }
}
