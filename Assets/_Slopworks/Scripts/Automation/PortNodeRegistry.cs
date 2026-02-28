using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spatial lookup for port nodes on the factory grid. Stores ports by grid cell
/// for O(1) neighbor queries when resolving connections.
/// </summary>
public class PortNodeRegistry
{
    private readonly Dictionary<Vector2Int, List<PortNode>> _portsByCell = new();

    /// <summary>
    /// Total number of registered port nodes.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Register a port node at its grid cell.
    /// </summary>
    public void Register(PortNode node)
    {
        if (!_portsByCell.TryGetValue(node.Cell, out var list))
        {
            list = new List<PortNode>();
            _portsByCell[node.Cell] = list;
        }
        list.Add(node);
        Count++;
    }

    /// <summary>
    /// Remove a port node from the registry.
    /// </summary>
    public void Unregister(PortNode node)
    {
        if (_portsByCell.TryGetValue(node.Cell, out var list))
        {
            if (list.Remove(node))
            {
                Count--;
                if (list.Count == 0)
                    _portsByCell.Remove(node.Cell);
            }
        }
    }

    /// <summary>
    /// Get all port nodes at a specific grid cell. Returns empty list if none.
    /// </summary>
    public IReadOnlyList<PortNode> GetPortsAt(Vector2Int cell)
    {
        if (_portsByCell.TryGetValue(cell, out var list))
            return list;
        return System.Array.Empty<PortNode>();
    }

    /// <summary>
    /// Find a port that is compatible with the given port for connection.
    /// A compatible port is at cell + direction, faces the opposite direction,
    /// and has the complementary type (Input matches Output).
    /// Returns null if no compatible port found.
    /// </summary>
    public PortNode FindCompatiblePort(PortNode node)
    {
        var targetCell = node.Cell + node.Direction;
        var oppositeDir = -node.Direction;
        var complementaryType = node.Type == PortType.Input ? PortType.Output : PortType.Input;

        var portsAtTarget = GetPortsAt(targetCell);
        for (int i = 0; i < portsAtTarget.Count; i++)
        {
            var candidate = portsAtTarget[i];
            if (candidate.Direction == oppositeDir
                && candidate.Type == complementaryType
                && candidate.Connection == null)
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Get all port nodes belonging to a specific owner object.
    /// Used when removing a building to find all its ports.
    /// </summary>
    public List<PortNode> GetPortsForOwner(object owner)
    {
        var result = new List<PortNode>();
        foreach (var kvp in _portsByCell)
        {
            foreach (var node in kvp.Value)
            {
                if (node.Owner == owner)
                    result.Add(node);
            }
        }
        return result;
    }
}
