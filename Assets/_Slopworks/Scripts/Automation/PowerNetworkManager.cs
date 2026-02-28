using System;
using System.Collections.Generic;

/// <summary>
/// Manages all power networks in the factory. Uses BFS flood-fill to discover
/// connected components among registered power nodes. Recalculates only when
/// topology changes (building placed or removed), not every tick.
/// </summary>
public class PowerNetworkManager
{
    private readonly HashSet<IPowerNode> _registeredNodes = new HashSet<IPowerNode>();
    private readonly Dictionary<IPowerNode, PowerNetwork> _nodeToNetwork = new Dictionary<IPowerNode, PowerNetwork>();
    private readonly List<PowerNetwork> _networks = new List<PowerNetwork>();

    private bool _isDirty;

    /// <summary>
    /// All discovered power networks after the last rebuild.
    /// </summary>
    public IReadOnlyList<PowerNetwork> Networks => _networks;

    /// <summary>
    /// Number of discovered power networks.
    /// </summary>
    public int NetworkCount => _networks.Count;

    /// <summary>
    /// Whether topology has changed since the last rebuild.
    /// </summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Registers a node to participate in power network discovery.
    /// Marks topology as dirty so networks will be recalculated on next rebuild.
    /// </summary>
    public void RegisterNode(IPowerNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (_registeredNodes.Add(node))
        {
            _isDirty = true;
        }
    }

    /// <summary>
    /// Removes a node from power network participation.
    /// Marks topology as dirty so networks will be recalculated on next rebuild.
    /// </summary>
    public void UnregisterNode(IPowerNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (_registeredNodes.Remove(node))
        {
            _isDirty = true;
        }
    }

    /// <summary>
    /// Rebuilds all power networks using BFS flood-fill on the connection graph.
    /// Each connected component of registered nodes becomes a separate PowerNetwork.
    /// Clears the dirty flag.
    /// </summary>
    public void Rebuild()
    {
        _networks.Clear();
        _nodeToNetwork.Clear();

        var visited = new HashSet<IPowerNode>();
        var queue = new Queue<IPowerNode>();
        var component = new List<IPowerNode>();

        foreach (var startNode in _registeredNodes)
        {
            if (visited.Contains(startNode))
                continue;

            // BFS flood-fill from this unvisited node
            component.Clear();
            queue.Clear();
            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                var neighbors = current.GetConnectedNodes();
                if (neighbors == null)
                    continue;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    var neighbor = neighbors[i];

                    // Skip nodes that are not registered or already visited
                    if (neighbor == null || !_registeredNodes.Contains(neighbor) || visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            // Create a PowerNetwork for this connected component
            var network = new PowerNetwork(component);
            _networks.Add(network);

            for (int i = 0; i < component.Count; i++)
            {
                _nodeToNetwork[component[i]] = network;
            }
        }

        _isDirty = false;
    }

    /// <summary>
    /// Only rebuilds when the dirty flag is set. Call this from the simulation tick
    /// to avoid unnecessary recalculations.
    /// </summary>
    public void RebuildIfDirty()
    {
        if (_isDirty)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Returns the power network containing the given node, or null if the node
    /// is not part of any network (unregistered or not yet rebuilt).
    /// </summary>
    public PowerNetwork GetNetworkForNode(IPowerNode node)
    {
        if (node == null)
            return null;

        _nodeToNetwork.TryGetValue(node, out var network);
        return network;
    }

    /// <summary>
    /// Shortcut to get the satisfaction ratio for the network containing this node.
    /// Returns 1.0 if the node is not in any network.
    /// </summary>
    public float GetSatisfaction(IPowerNode node)
    {
        var network = GetNetworkForNode(node);
        return network?.Satisfaction ?? 1f;
    }
}
