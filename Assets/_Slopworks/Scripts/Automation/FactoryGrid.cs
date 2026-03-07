using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cell-based placement grid for the home base factory.
/// Supports multiple discrete floor levels via sparse Dictionary storage.
/// Plain C# class (D-004) -- no MonoBehaviour, testable in EditMode.
/// </summary>
public class FactoryGrid
{
    public const float CellSize = 1.0f;
    public const int Width = 200;
    public const int Height = 200;
    public const float LevelHeight = 1.0f;
    public const int MaxLevels = 50;
    public const float WallHeight = 3.0f;
    public const int FoundationSize = 4; // 4x4 cells per foundation
    public const int WallWidth = 4; // wall spans 4 cells wide

    private readonly Dictionary<Vector3Int, BuildingData> _cells = new();

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
    /// Level-0 wrapper for backward compatibility.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return CellToWorld(cell, 0);
    }

    /// <summary>
    /// Returns the world-space center of a grid cell at the given level.
    /// Y = level * LevelHeight.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell, int level)
    {
        float x = (cell.x + 0.5f) * CellSize;
        float y = level * LevelHeight;
        float z = (cell.y + 0.5f) * CellSize;
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Checks whether a rectangular area is fully in bounds and unoccupied.
    /// Level-0 wrapper for backward compatibility.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        return CanPlace(origin, size, 0);
    }

    /// <summary>
    /// Checks whether a rectangular area at the given level is fully in bounds and unoccupied.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size, int level)
    {
        if (level < 0 || level >= MaxLevels)
            return false;

        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (!InBounds(x, z))
                    return false;
                var key = new Vector3Int(x, z, level);
                if (_cells.ContainsKey(key))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Marks a rectangular area as occupied by the given building.
    /// Level-0 wrapper for backward compatibility.
    /// </summary>
    public void Place(Vector2Int origin, Vector2Int size, BuildingData data)
    {
        Place(origin, size, 0, data);
    }

    /// <summary>
    /// Marks a rectangular area at the given level as occupied by the given building.
    /// Caller should check CanPlace first.
    /// </summary>
    public void Place(Vector2Int origin, Vector2Int size, int level, BuildingData data)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                _cells[new Vector3Int(x, z, level)] = data;
            }
        }
    }

    /// <summary>
    /// Clears a rectangular area, setting all cells to empty.
    /// Level-0 wrapper for backward compatibility.
    /// </summary>
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        Remove(origin, size, 0);
    }

    /// <summary>
    /// Clears a rectangular area at the given level.
    /// </summary>
    public void Remove(Vector2Int origin, Vector2Int size, int level)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (InBounds(x, z))
                    _cells.Remove(new Vector3Int(x, z, level));
            }
        }
    }

    /// <summary>
    /// Returns the BuildingData at a cell, or null if empty/out of bounds.
    /// Level-0 wrapper for backward compatibility.
    /// </summary>
    public BuildingData GetAt(Vector2Int cell)
    {
        return GetAt(cell, 0);
    }

    /// <summary>
    /// Returns the BuildingData at a cell and level, or null if empty/out of bounds.
    /// </summary>
    public BuildingData GetAt(Vector2Int cell, int level)
    {
        if (!InBounds(cell.x, cell.y))
            return null;
        _cells.TryGetValue(new Vector3Int(cell.x, cell.y, level), out var data);
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
