using UnityEngine;

/// <summary>
/// Common interface for all buildable definitions (foundations, machines, storage).
/// Used by BuildModeController for grid placement without knowing the specific type.
/// </summary>
public interface IPlaceableDefinition
{
    string PlaceableId { get; }
    Vector2Int Size { get; }
}
