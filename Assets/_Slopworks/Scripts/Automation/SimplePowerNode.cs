using System;
using System.Collections.Generic;

/// <summary>
/// Basic implementation of IPowerNode for testing and simple buildings.
/// Supports bidirectional connections via ConnectTo/DisconnectFrom.
/// </summary>
public class SimplePowerNode : IPowerNode
{
    private readonly List<IPowerNode> _connections = new List<IPowerNode>();

    public string NodeId { get; }
    public float PowerGeneration { get; }
    public float PowerConsumption { get; }

    public SimplePowerNode(string nodeId, float generation, float consumption)
    {
        if (nodeId == null)
            throw new ArgumentNullException(nameof(nodeId));

        NodeId = nodeId;
        PowerGeneration = generation;
        PowerConsumption = consumption;
    }

    public IReadOnlyList<IPowerNode> GetConnectedNodes()
    {
        return _connections;
    }

    /// <summary>
    /// Creates a bidirectional connection between this node and another.
    /// If the other node is a SimplePowerNode, adds the reverse connection as well.
    /// Does nothing if the connection already exists.
    /// </summary>
    public void ConnectTo(IPowerNode other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        if (!_connections.Contains(other))
        {
            _connections.Add(other);
        }

        if (other is SimplePowerNode otherSimple && !otherSimple._connections.Contains(this))
        {
            otherSimple._connections.Add(this);
        }
    }

    /// <summary>
    /// Removes the bidirectional connection between this node and another.
    /// If the other node is a SimplePowerNode, removes the reverse connection as well.
    /// </summary>
    public void DisconnectFrom(IPowerNode other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        _connections.Remove(other);

        if (other is SimplePowerNode otherSimple)
        {
            otherSimple._connections.Remove(this);
        }
    }
}
