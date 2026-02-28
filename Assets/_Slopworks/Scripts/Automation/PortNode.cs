using UnityEngine;

/// <summary>
/// A spatial connection point on the factory grid. Every machine, storage, and belt
/// endpoint exposes typed port nodes at world grid positions. When two compatible
/// ports face each other in adjacent cells, a connection (inserter or belt link)
/// is created automatically.
/// </summary>
public class PortNode
{
    /// <summary>
    /// The grid cell this port occupies.
    /// </summary>
    public Vector2Int Cell { get; }

    /// <summary>
    /// The direction this port faces outward (e.g. (1,0) = east).
    /// A compatible port at Cell + Direction must face -Direction.
    /// </summary>
    public Vector2Int Direction { get; }

    /// <summary>
    /// Whether this port accepts items (Input) or emits items (Output).
    /// </summary>
    public PortType Type { get; }

    /// <summary>
    /// What kind of simulation object owns this port.
    /// </summary>
    public PortOwnerType OwnerType { get; }

    /// <summary>
    /// The simulation object (Machine, StorageContainer, or BeltSegment) that owns this port.
    /// </summary>
    public object Owner { get; }

    /// <summary>
    /// For machines with multiple I/O slots, the slot index this port maps to.
    /// -1 for storage and belt ports.
    /// </summary>
    public int SlotIndex { get; }

    /// <summary>
    /// The active connection on this port (Inserter or BeltNetwork connection).
    /// Null if unconnected.
    /// </summary>
    public object Connection { get; set; }

    public PortNode(
        Vector2Int cell,
        Vector2Int direction,
        PortType type,
        PortOwnerType ownerType,
        object owner,
        int slotIndex = -1)
    {
        Cell = cell;
        Direction = direction;
        Type = type;
        OwnerType = ownerType;
        Owner = owner;
        SlotIndex = slotIndex;
    }
}
