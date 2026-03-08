using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure C# graph class (D-004) that manages settlement buildings as nodes
/// and player-built roads as edges. Provides BFS connectivity queries,
/// factory hub reachability, and territory checks.
/// </summary>
public class SettlementGraph
{
    private readonly string _factoryHubId;
    private readonly Dictionary<string, SettlementBuilding> _buildings = new();
    private readonly Dictionary<string, HashSet<string>> _adjacency = new();
    private readonly List<(string a, string b)> _roads = new();

    public IReadOnlyDictionary<string, SettlementBuilding> AllBuildings => _buildings;
    public IReadOnlyList<(string a, string b)> Roads => _roads;

    public SettlementGraph(string factoryHubId)
    {
        _factoryHubId = factoryHubId;
    }

    /// <summary>
    /// Creates a SettlementBuilding from the definition and adds it to the graph.
    /// Returns false if a building with the same ID already exists.
    /// </summary>
    public bool Register(SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        string id = definition.buildingId;

        if (_buildings.ContainsKey(id))
            return false;

        var building = new SettlementBuilding(id, definition, position);
        _buildings.Add(id, building);
        _adjacency.Add(id, new HashSet<string>());
        return true;
    }

    /// <summary>
    /// Returns the building with the given ID, or null if not found.
    /// </summary>
    public SettlementBuilding Get(string buildingId)
    {
        _buildings.TryGetValue(buildingId, out var building);
        return building;
    }

    /// <summary>
    /// Builds a road between two buildings. Validates: not self-loop, both exist,
    /// within range (min of both connectionRanges), not a duplicate.
    /// </summary>
    public bool BuildRoad(string idA, string idB)
    {
        if (idA == idB)
            return false;

        if (!_buildings.TryGetValue(idA, out var buildingA))
            return false;

        if (!_buildings.TryGetValue(idB, out var buildingB))
            return false;

        // duplicate check via adjacency (faster than scanning roads list)
        if (_adjacency[idA].Contains(idB))
            return false;

        // range check: use the smaller of the two connection ranges
        float maxRange = Mathf.Min(
            buildingA.Definition.connectionRange,
            buildingB.Definition.connectionRange);

        float distance = Vector3.Distance(buildingA.Position, buildingB.Position);
        if (distance > maxRange)
            return false;

        // add bidirectional edge
        _adjacency[idA].Add(idB);
        _adjacency[idB].Add(idA);
        _roads.Add((idA, idB));
        return true;
    }

    /// <summary>
    /// BFS to determine if two buildings are connected through any chain of roads.
    /// </summary>
    public bool AreConnected(string idA, string idB)
    {
        if (idA == idB)
            return true;

        if (!_adjacency.ContainsKey(idA) || !_adjacency.ContainsKey(idB))
            return false;

        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(idA);
        visited.Add(idA);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            if (current == idB)
                return true;

            foreach (string neighbor in _adjacency[current])
            {
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the building can reach the factory hub through roads.
    /// The factory hub itself always returns true if registered.
    /// </summary>
    public bool HasFactoryConnection(string buildingId)
    {
        if (!_buildings.ContainsKey(buildingId))
            return false;

        if (!_buildings.ContainsKey(_factoryHubId))
            return false;

        return AreConnected(buildingId, _factoryHubId);
    }

    /// <summary>
    /// Ticks all buildings in the graph, advancing production timers.
    /// </summary>
    public void Tick(float deltaTime)
    {
        foreach (var building in _buildings.Values)
        {
            building.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Returns true if the world position falls within any claimed building's
    /// EffectiveTerritoryRadius.
    /// </summary>
    public bool IsInTerritory(Vector3 worldPosition)
    {
        foreach (var building in _buildings.Values)
        {
            float radius = building.EffectiveTerritoryRadius;
            if (radius <= 0f)
                continue;

            float distance = Vector3.Distance(worldPosition, building.Position);
            if (distance <= radius)
                return true;
        }

        return false;
    }
}
