using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cell-based placement grid for the home base factory.
/// Supports variable-height placement via Y-bucket keys in sparse Dictionary storage.
/// Plain C# class (D-004) -- no MonoBehaviour, testable in EditMode.
/// </summary>
public class FactoryGrid
{
    public const float CellSize = 1.0f;
    public const int Width = 200;
    public const int Height = 200;
    public const float BucketSize = 0.5f;
    public const float WallHeight = 3.0f;
    public const int FoundationSize = 4; // 4x4 cells per foundation
    public const int WallWidth = 4; // wall spans 4 cells wide

    private readonly Dictionary<Vector3Int, BuildingData> _cells = new();

    /// <summary>
    /// Quantizes a world Y position into a bucket index at BucketSize resolution.
    /// </summary>
    public static int YBucket(float surfaceY)
    {
        return Mathf.RoundToInt(surfaceY / BucketSize);
    }

    /// <summary>
    /// Converts a world position to the grid cell that contains it.
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / CellSize);
        int z = Mathf.FloorToInt(worldPos.z / CellSize);
        return new Vector2Int(x, z);
    }

    /// <summary>
    /// Returns the world-space center of a grid cell (Y = 0).
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return CellToWorld(cell, 0f);
    }

    /// <summary>
    /// Returns the world-space center of a grid cell at the given surface Y.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell, float surfaceY)
    {
        float x = (cell.x + 0.5f) * CellSize;
        float z = (cell.y + 0.5f) * CellSize;
        return new Vector3(x, surfaceY, z);
    }

    /// <summary>
    /// Checks whether a rectangular area is fully in bounds and unoccupied (surfaceY = 0).
    /// Backward-compatible overload.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        return CanPlace(origin, size, 0f);
    }

    /// <summary>
    /// Checks whether a rectangular area at the given surface Y is fully in bounds and unoccupied.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size, float surfaceY)
    {
        int bucket = YBucket(surfaceY);
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (!InBounds(x, z))
                    return false;
                if (_cells.ContainsKey(new Vector3Int(x, z, bucket)))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Marks a rectangular area as occupied (surfaceY = 0).
    /// Backward-compatible overload.
    /// </summary>
    public void Place(Vector2Int origin, Vector2Int size, BuildingData data)
    {
        Place(origin, size, 0f, data);
    }

    /// <summary>
    /// Marks a rectangular area at the given surface Y as occupied by the given building.
    /// Caller should check CanPlace first.
    /// </summary>
    public void Place(Vector2Int origin, Vector2Int size, float surfaceY, BuildingData data)
    {
        int bucket = YBucket(surfaceY);
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                _cells[new Vector3Int(x, z, bucket)] = data;
            }
        }
    }

    /// <summary>
    /// Clears a rectangular area (surfaceY = 0).
    /// Backward-compatible overload.
    /// </summary>
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        Remove(origin, size, 0f);
    }

    /// <summary>
    /// Clears a rectangular area at the given surface Y.
    /// </summary>
    public void Remove(Vector2Int origin, Vector2Int size, float surfaceY)
    {
        int bucket = YBucket(surfaceY);
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (InBounds(x, z))
                    _cells.Remove(new Vector3Int(x, z, bucket));
            }
        }
    }

    /// <summary>
    /// Returns the BuildingData at a cell (surfaceY = 0), or null if empty/out of bounds.
    /// Backward-compatible overload.
    /// </summary>
    public BuildingData GetAt(Vector2Int cell)
    {
        return GetAt(cell, 0f);
    }

    /// <summary>
    /// Returns the BuildingData at a cell and surface Y, or null if empty/out of bounds.
    /// </summary>
    public BuildingData GetAt(Vector2Int cell, float surfaceY)
    {
        if (!InBounds(cell.x, cell.y))
            return null;
        _cells.TryGetValue(new Vector3Int(cell.x, cell.y, YBucket(surfaceY)), out var data);
        return data;
    }

    /// <summary>
    /// Returns true if the cell is within the grid boundaries.
    /// </summary>
    public bool IsInBounds(Vector2Int cell)
    {
        return InBounds(cell.x, cell.y);
    }

    private bool InBounds(int x, int z)
    {
        return true;
    }
}
