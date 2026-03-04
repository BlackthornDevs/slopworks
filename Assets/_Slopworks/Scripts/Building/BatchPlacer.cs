using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles batch foundation placement: click start corner, drag to opposite corner,
/// preview rectangle, release to place all at once (Zoop-style).
/// Plain C# class (D-004) -- no MonoBehaviour, testable in EditMode.
/// </summary>
public class BatchPlacer
{
    private Vector2Int _startCell;
    private Vector2Int _endCell;
    private bool _isPlacing;
    private int _level;

    public bool IsPlacing => _isPlacing;
    public Vector2Int StartCell => _startCell;
    public Vector2Int EndCell => _endCell;
    public int Level => _level;

    /// <summary>
    /// The min/max corners of the current rectangle.
    /// </summary>
    public (Vector2Int min, Vector2Int max) Rectangle
    {
        get
        {
            var min = new Vector2Int(
                Mathf.Min(_startCell.x, _endCell.x),
                Mathf.Min(_startCell.y, _endCell.y));
            var max = new Vector2Int(
                Mathf.Max(_startCell.x, _endCell.x),
                Mathf.Max(_startCell.y, _endCell.y));
            return (min, max);
        }
    }

    /// <summary>
    /// Total number of cells in the current rectangle.
    /// </summary>
    public int CellCount
    {
        get
        {
            if (!_isPlacing) return 0;
            var (min, max) = Rectangle;
            return (max.x - min.x + 1) * (max.y - min.y + 1);
        }
    }

    /// <summary>
    /// All cells in the current rectangle.
    /// </summary>
    public List<Vector2Int> PreviewCells
    {
        get
        {
            var cells = new List<Vector2Int>();
            if (!_isPlacing) return cells;

            var (min, max) = Rectangle;
            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.y; z <= max.y; z++)
                {
                    cells.Add(new Vector2Int(x, z));
                }
            }
            return cells;
        }
    }

    /// <summary>
    /// Begin batch placement from the given cell at the given level.
    /// </summary>
    public void StartPlacement(Vector2Int cell, int level)
    {
        _startCell = cell;
        _endCell = cell;
        _level = level;
        _isPlacing = true;
    }

    /// <summary>
    /// Update the drag endpoint to the current cursor cell.
    /// </summary>
    public const int MaxZoopDistance = 20;

    public void UpdateDrag(Vector2Int currentCell)
    {
        if (!_isPlacing) return;
        _endCell = new Vector2Int(
            Mathf.Clamp(currentCell.x, _startCell.x - MaxZoopDistance, _startCell.x + MaxZoopDistance),
            Mathf.Clamp(currentCell.y, _startCell.y - MaxZoopDistance, _startCell.y + MaxZoopDistance));
    }

    /// <summary>
    /// Finish placement. Returns the min/max corners if placing,
    /// or null if not in placement mode.
    /// </summary>
    public (Vector2Int min, Vector2Int max)? FinishPlacement()
    {
        if (!_isPlacing)
            return null;

        _isPlacing = false;
        var rect = Rectangle;
        return rect;
    }

    /// <summary>
    /// Cancel the current batch placement.
    /// </summary>
    public void Cancel()
    {
        _isPlacing = false;
    }

    /// <summary>
    /// Validate each cell in the rectangle against the grid.
    /// Returns lists of valid and invalid cells.
    /// </summary>
    public (List<Vector2Int> valid, int invalidCount) ValidateBatch(FactoryGrid grid)
    {
        var valid = new List<Vector2Int>();
        int invalidCount = 0;

        var (min, max) = Rectangle;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var cell = new Vector2Int(x, z);
                if (grid.CanPlace(cell, Vector2Int.one, _level))
                    valid.Add(cell);
                else
                    invalidCount++;
            }
        }

        return (valid, invalidCount);
    }
}
