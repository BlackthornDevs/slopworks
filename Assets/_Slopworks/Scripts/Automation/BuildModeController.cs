using UnityEngine;

/// <summary>
/// Core build mode logic for placing buildings on the factory grid.
/// Plain C# class (D-004) -- no MonoBehaviour, testable in EditMode.
/// Accepts any IPlaceableDefinition (foundation, machine, storage).
/// </summary>
public class BuildModeController
{
    private IPlaceableDefinition _currentDefinition;
    private Vector2Int _snappedCell;
    private int _rotation;
    private bool _isValid;

    public bool IsInBuildMode => _currentDefinition != null;
    public IPlaceableDefinition CurrentDefinition => _currentDefinition;
    public Vector2Int SnappedCell => _snappedCell;
    public int Rotation => _rotation;
    public bool IsValidPlacement => _isValid;

    /// <summary>
    /// Returns the effective size after accounting for rotation.
    /// At 90 or 270 degrees, the X and Y dimensions are swapped.
    /// </summary>
    public Vector2Int EffectiveSize
    {
        get
        {
            if (_currentDefinition == null)
                return Vector2Int.zero;

            bool swapped = _rotation == 90 || _rotation == 270;
            var size = _currentDefinition.Size;
            return swapped ? new Vector2Int(size.y, size.x) : size;
        }
    }

    /// <summary>
    /// Enter build mode with the given placeable definition.
    /// </summary>
    public void EnterBuildMode(IPlaceableDefinition definition)
    {
        _currentDefinition = definition;
        _rotation = 0;
        _snappedCell = Vector2Int.zero;
        _isValid = false;
    }

    /// <summary>
    /// Exit build mode, clearing all state.
    /// </summary>
    public void ExitBuildMode()
    {
        _currentDefinition = null;
        _rotation = 0;
        _snappedCell = Vector2Int.zero;
        _isValid = false;
    }

    /// <summary>
    /// Update the preview position by snapping the cursor world position to the grid
    /// and checking placement validity.
    /// </summary>
    public void UpdatePreview(Vector3 cursorWorldPos, FactoryGrid grid)
    {
        _snappedCell = grid.WorldToCell(cursorWorldPos);
        _isValid = grid.CanPlace(_snappedCell, EffectiveSize);
    }

    /// <summary>
    /// Attempt to place the current building on the grid.
    /// Returns true if placement succeeded, false if the position is invalid.
    /// </summary>
    public bool TryPlace(FactoryGrid grid)
    {
        if (!IsInBuildMode)
            return false;

        var effectiveSize = EffectiveSize;

        if (!grid.CanPlace(_snappedCell, effectiveSize))
            return false;

        var data = new BuildingData(
            _currentDefinition.PlaceableId,
            _snappedCell,
            effectiveSize,
            _rotation
        );

        grid.Place(_snappedCell, effectiveSize, data);
        return true;
    }

    /// <summary>
    /// Rotate the preview by 90 degrees clockwise (wraps at 360).
    /// </summary>
    public void RotatePreview()
    {
        _rotation = (_rotation + 90) % 360;
    }
}
