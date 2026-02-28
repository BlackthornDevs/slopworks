using System;
using System.Collections.Generic;

/// <summary>
/// Represents a single connected power network discovered by BFS flood-fill.
/// Immutable after construction -- recalculate by creating a new PowerNetwork
/// when topology changes.
/// </summary>
public class PowerNetwork
{
    private readonly List<IPowerNode> _nodes;

    /// <summary>
    /// Sum of all node generation in this network (watts).
    /// </summary>
    public float TotalGeneration { get; }

    /// <summary>
    /// Sum of all node consumption in this network (watts).
    /// </summary>
    public float TotalConsumption { get; }

    /// <summary>
    /// Power satisfaction ratio, clamped 0..1.
    /// 1.0 means all consumers are fully powered.
    /// Less than 1.0 means machines should slow proportionally.
    /// </summary>
    public float Satisfaction { get; }

    /// <summary>
    /// All nodes in this network.
    /// </summary>
    public IReadOnlyList<IPowerNode> Nodes => _nodes;

    /// <summary>
    /// Number of nodes in this network.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Creates a new PowerNetwork from the given nodes, computing totals immediately.
    /// </summary>
    public PowerNetwork(IReadOnlyList<IPowerNode> nodes)
    {
        if (nodes == null)
            throw new ArgumentNullException(nameof(nodes));

        _nodes = new List<IPowerNode>(nodes);

        float totalGen = 0f;
        float totalCon = 0f;

        for (int i = 0; i < _nodes.Count; i++)
        {
            totalGen += _nodes[i].PowerGeneration;
            totalCon += _nodes[i].PowerConsumption;
        }

        TotalGeneration = totalGen;
        TotalConsumption = totalCon;

        if (totalCon == 0f)
        {
            Satisfaction = 1f;
        }
        else
        {
            float ratio = Math.Min(totalGen, totalCon) / totalCon;
            Satisfaction = Math.Max(0f, Math.Min(1f, ratio));
        }
    }
}
