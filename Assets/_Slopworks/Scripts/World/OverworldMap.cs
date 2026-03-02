using System.Collections.Generic;

/// <summary>
/// Pure C# node registry for the overworld map (D-004).
/// Thin data model -- the UI reads this, simulation ticks elsewhere.
/// </summary>
public class OverworldMap
{
    private readonly Dictionary<string, OverworldNode> _nodes = new();

    public int NodeCount => _nodes.Count;

    public void RegisterNode(OverworldNode node)
    {
        _nodes[node.NodeId] = node;
    }

    public OverworldNode GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    public IReadOnlyCollection<OverworldNode> GetNodes()
    {
        return _nodes.Values;
    }
}
