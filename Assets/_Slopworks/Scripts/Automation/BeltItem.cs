using System;

/// <summary>
/// A single item on a conveyor belt, stored as part of the distance-offset model.
/// The distanceToNext field represents the gap (in integer subdivisions) between
/// this item and the previous item (or from the input end for the first item).
/// </summary>
[Serializable]
public struct BeltItem
{
    /// <summary>
    /// Number of integer subdivisions per tile. Provides precision without floats.
    /// </summary>
    public const int SubdivisionsPerTile = 100;

    /// <summary>
    /// The item type on the belt. Matches ItemDefinitionSO.itemId.
    /// </summary>
    public string itemId;

    /// <summary>
    /// Integer subdivisions to the previous item, or to the input end for the first item.
    /// </summary>
    public ushort distanceToNext;
}
