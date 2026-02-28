using UnityEngine;

/// <summary>
/// Cell-based placement grid for the home base factory.
/// Plain C# class (D-004) -- no MonoBehaviour, testable in EditMode.
/// </summary>
public class FactoryGrid
{
    public const float CellSize = 1.0f;
    public const int Width = 200;
    public const int Height = 200;

    private readonly BuildingData[,] _cells = new BuildingData[Width, Height];

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
        float x = (cell.x + 0.5f) * CellSize;
        float z = (cell.y + 0.5f) * CellSize;
        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Checks whether a rectangular area is fully in bounds and unoccupied.
    /// </summary>
    public bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (!InBounds(x, z) || _cells[x, z] != null)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Marks a rectangular area as occupied by the given building.
    /// Caller should check CanPlace first.
    /// </summary>
    public void Place(Vector2Int origin, Vector2Int size, BuildingData data)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                _cells[x, z] = data;
            }
        }
    }

    /// <summary>
    /// Clears a rectangular area, setting all cells to empty.
    /// </summary>
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (InBounds(x, z))
                    _cells[x, z] = null;
            }
        }
    }

    /// <summary>
    /// Returns the BuildingData at a cell, or null if empty/out of bounds.
    /// </summary>
    public BuildingData GetAt(Vector2Int cell)
    {
        if (!InBounds(cell.x, cell.y))
            return null;
        return _cells[cell.x, cell.y];
    }

    private bool InBounds(int x, int z)
    {
        return x >= 0 && x < Width && z >= 0 && z < Height;
    }
}
