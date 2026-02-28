using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles belt placement state machine: click to start, drag to extend,
/// release to place. Constrains to straight lines (same X or same Z).
/// Plain C# class (D-004).
/// </summary>
public class BeltPlacer
{
    private Vector2Int _startCell;
    private Vector2Int _endCell;
    private bool _isPlacing;

    public bool IsPlacing => _isPlacing;
    public Vector2Int StartCell => _startCell;
    public Vector2Int EndCell => _endCell;

    /// <summary>
    /// The cells the belt would occupy based on current drag state.
    /// Empty if not placing.
    /// </summary>
    public List<Vector2Int> PreviewCells { get; } = new();

    /// <summary>
    /// The direction of the belt from start to end.
    /// Zero if not placing or start == end.
    /// </summary>
    public Vector2Int PreviewDirection { get; private set; }

    /// <summary>
    /// Length of the belt in tiles (number of cells - 1).
    /// </summary>
    public int PreviewLength { get; private set; }

    /// <summary>
    /// Begin belt placement from the given grid cell.
    /// </summary>
    public void StartPlacement(Vector2Int cell)
    {
        _startCell = cell;
        _endCell = cell;
        _isPlacing = true;
        UpdatePreview();
    }

    /// <summary>
    /// Update the drag endpoint. Constrains to straight line by snapping
    /// to the axis with the greater delta from start.
    /// </summary>
    public void UpdateDrag(Vector2Int currentCell)
    {
        if (!_isPlacing)
            return;

        var diff = currentCell - _startCell;
        int absDx = Mathf.Abs(diff.x);
        int absDy = Mathf.Abs(diff.y);

        // Snap to the dominant axis
        if (absDx >= absDy)
            _endCell = new Vector2Int(currentCell.x, _startCell.y);
        else
            _endCell = new Vector2Int(_startCell.x, currentCell.y);

        UpdatePreview();
    }

    /// <summary>
    /// Finish placement. Returns the start and end cells if valid (length >= 1),
    /// or null if the placement is invalid.
    /// </summary>
    public (Vector2Int start, Vector2Int end)? FinishPlacement()
    {
        if (!_isPlacing)
            return null;

        _isPlacing = false;

        if (_startCell == _endCell)
        {
            PreviewCells.Clear();
            PreviewDirection = Vector2Int.zero;
            PreviewLength = 0;
            return null;
        }

        var result = (_startCell, _endCell);
        PreviewCells.Clear();
        PreviewDirection = Vector2Int.zero;
        PreviewLength = 0;
        return result;
    }

    /// <summary>
    /// Cancel the current placement.
    /// </summary>
    public void Cancel()
    {
        _isPlacing = false;
        PreviewCells.Clear();
        PreviewDirection = Vector2Int.zero;
        PreviewLength = 0;
    }

    private void UpdatePreview()
    {
        PreviewCells.Clear();

        if (_startCell == _endCell)
        {
            PreviewCells.Add(_startCell);
            PreviewDirection = Vector2Int.zero;
            PreviewLength = 0;
            return;
        }

        var diff = _endCell - _startCell;
        var dir = new Vector2Int(
            diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
            diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);

        int length = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

        for (int i = 0; i <= length; i++)
            PreviewCells.Add(_startCell + dir * i);

        PreviewDirection = dir;
        PreviewLength = length;
    }
}
