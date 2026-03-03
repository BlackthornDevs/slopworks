using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates the full building placement flow: grid placement, simulation object
/// creation, port node registration, and connection resolution. Plain C# class (D-004).
/// </summary>
public class BuildingPlacementService
{
    private readonly FactoryGrid _grid;
    private readonly PortNodeRegistry _portRegistry;
    private readonly ConnectionResolver _connectionResolver;
    private readonly FactorySimulation _simulation;

    /// <summary>
    /// Maps BuildingData to its simulation object (Machine, StorageContainer, or BeltSegment)
    /// for later removal.
    /// </summary>
    private readonly Dictionary<BuildingData, object> _simulationObjects = new();

    /// <summary>
    /// Tracks cells occupied by automation buildings (belts, machines, storage).
    /// Automation buildings coexist on foundation cells rather than occupying the grid directly.
    /// </summary>
    private readonly HashSet<Vector3Int> _automationCells = new();

    public BuildingPlacementService(
        FactoryGrid grid,
        PortNodeRegistry portRegistry,
        ConnectionResolver connectionResolver,
        FactorySimulation simulation)
    {
        _grid = grid;
        _portRegistry = portRegistry;
        _connectionResolver = connectionResolver;
        _simulation = simulation;
    }

    /// <summary>
    /// Place a machine on the grid. Creates the Machine simulation object, registers
    /// port nodes at the rotated port positions, and auto-wires compatible neighbors.
    /// </summary>
    public PlacementResult PlaceMachine(MachineDefinitionSO def, Vector2Int cell, int rotation, int level = 0)
    {
        var effectiveSize = GetEffectiveSize(def.size, rotation);

        if (!HasFoundationsAndNoOverlap(cell, effectiveSize, level))
            return null;

        var buildingData = new BuildingData(def.machineId, cell, effectiveSize, rotation, level);
        OccupyAutomationCells(cell, effectiveSize, level);

        var machine = new Machine(def);
        _simulation.RegisterMachine(machine);
        _simulationObjects[buildingData] = machine;

        var ports = CreatePortNodes(def.ports, cell, rotation, PortOwnerType.Machine, machine, level);
        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, machine, ports);
    }

    /// <summary>
    /// Place a storage container on the grid.
    /// </summary>
    public PlacementResult PlaceStorage(StorageDefinitionSO def, Vector2Int cell, int rotation, int level = 0)
    {
        var effectiveSize = GetEffectiveSize(def.size, rotation);

        if (!HasFoundationsAndNoOverlap(cell, effectiveSize, level))
            return null;

        var buildingData = new BuildingData(def.storageId, cell, effectiveSize, rotation, level);
        OccupyAutomationCells(cell, effectiveSize, level);

        var storage = new StorageContainer(def.slotCount, def.maxStackSize);
        _simulationObjects[buildingData] = storage;

        var ports = def.ports != null
            ? CreatePortNodes(def.ports, cell, rotation, PortOwnerType.Storage, storage, level)
            : new List<PortNode>();

        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, storage, ports);
    }

    /// <summary>
    /// Place a turret on the grid. Creates the TurretController simulation object,
    /// registers port nodes for ammo input, and auto-wires compatible neighbors.
    /// </summary>
    public PlacementResult PlaceTurret(TurretDefinitionSO def, Vector2Int cell, int rotation, int level = 0, bool skipFoundationCheck = false)
    {
        var effectiveSize = GetEffectiveSize(def.size, rotation);

        if (!skipFoundationCheck && !HasFoundationsAndNoOverlap(cell, effectiveSize, level))
            return null;

        var buildingData = new BuildingData(def.turretId, cell, effectiveSize, rotation, level);
        OccupyAutomationCells(cell, effectiveSize, level);

        var turret = new TurretController(def);
        _simulationObjects[buildingData] = turret;

        // Turret ammo storage is the port owner so inserters can deliver ammo
        var ports = def.ports != null
            ? CreatePortNodes(def.ports, cell, rotation, PortOwnerType.Turret, turret.AmmoStorage, level)
            : new List<PortNode>();

        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, turret, ports);
    }

    /// <summary>
    /// Place a belt on the grid from startCell to endCell. Must be a straight line
    /// (same X or same Z). Creates a BeltSegment with port nodes at both endpoints.
    /// </summary>
    public PlacementResult PlaceBelt(Vector2Int startCell, Vector2Int endCell, int level = 0)
    {
        if (startCell == endCell)
            return null;

        // Must be a straight line
        if (startCell.x != endCell.x && startCell.y != endCell.y)
            return null;

        var diff = endCell - startCell;
        var direction = new Vector2Int(
            diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
            diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);

        int lengthInTiles = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

        // Check all cells have foundations and no automation overlap
        for (int i = 0; i <= lengthInTiles; i++)
        {
            var checkCell = startCell + direction * i;
            if (!HasFoundationsAndNoOverlap(checkCell, Vector2Int.one, level))
                return null;
        }

        // Track automation cells (belts coexist on foundation grid cells)
        var buildingData = new BuildingData("belt", startCell, new Vector2Int(1, 1), 0, level);
        for (int i = 0; i <= lengthInTiles; i++)
        {
            var placeCell = startCell + direction * i;
            _automationCells.Add(new Vector3Int(placeCell.x, placeCell.y, level));
        }

        var belt = new BeltSegment(lengthInTiles);
        _simulation.RegisterBelt(belt);
        _simulationObjects[buildingData] = belt;

        // Input port at start, facing backward (items enter from behind)
        // Output port at end, facing forward (items exit ahead)
        var ports = new List<PortNode>
        {
            new PortNode(startCell, -direction, PortType.Input, PortOwnerType.Belt, belt, level: level),
            new PortNode(endCell, direction, PortType.Output, PortOwnerType.Belt, belt, level: level)
        };

        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, belt, ports);
    }

    /// <summary>
    /// Remove a building from the grid and tear down all its connections.
    /// </summary>
    public void Remove(BuildingData data)
    {
        if (data == null)
            return;

        if (!_simulationObjects.TryGetValue(data, out var simObject))
            return;

        // Tear down connections
        var ports = _portRegistry.GetPortsForOwner(simObject);
        _connectionResolver.RemoveConnectionsFor(ports);

        // Unregister ports
        foreach (var port in ports)
            _portRegistry.Unregister(port);

        // Unregister simulation object
        if (simObject is Machine machine)
            _simulation.UnregisterMachine(machine);
        else if (simObject is BeltSegment belt)
            _simulation.UnregisterBelt(belt);

        // Clear automation cells (not grid -- automation buildings don't occupy the grid)
        if (simObject is BeltSegment removedBelt)
        {
            // Belts span multiple cells along their path -- recalculate from ports
            var beltPorts = ports;
            if (beltPorts.Count == 2)
            {
                var startCell = beltPorts[0].Cell;
                var endCell = beltPorts[1].Cell;
                var diff = endCell - startCell;
                var dir = new Vector2Int(
                    diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
                    diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
                int len = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);
                for (int i = 0; i <= len; i++)
                {
                    var c = startCell + dir * i;
                    _automationCells.Remove(new Vector3Int(c.x, c.y, data.Level));
                }
            }
        }
        else
        {
            ClearAutomationCells(data.Origin, data.Size, data.Level);
        }
        _simulationObjects.Remove(data);
    }

    private List<PortNode> CreatePortNodes(
        MachinePort[] portDefs,
        Vector2Int buildingOrigin,
        int rotation,
        PortOwnerType ownerType,
        object owner,
        int level = 0)
    {
        var ports = new List<PortNode>();
        if (portDefs == null)
            return ports;

        for (int i = 0; i < portDefs.Length; i++)
        {
            var def = portDefs[i];
            var rotatedOffset = GridRotation.Rotate(def.localOffset, rotation);
            var rotatedDir = GridRotation.Rotate(def.direction, rotation);
            var worldCell = buildingOrigin + rotatedOffset;

            int slotIndex = def.type == PortType.Input
                ? CountPortsBefore(portDefs, i, PortType.Input)
                : CountPortsBefore(portDefs, i, PortType.Output);

            ports.Add(new PortNode(
                worldCell, rotatedDir, def.type, ownerType, owner, slotIndex, level));
        }

        return ports;
    }

    private static int CountPortsBefore(MachinePort[] ports, int index, PortType type)
    {
        int count = 0;
        for (int i = 0; i < index; i++)
        {
            if (ports[i].type == type)
                count++;
        }
        return count;
    }

    private void RegisterAndResolve(List<PortNode> ports)
    {
        foreach (var port in ports)
            _portRegistry.Register(port);
        _connectionResolver.ResolveConnectionsFor(ports);
    }

    /// <summary>
    /// Check that all cells in the footprint have a structural foundation
    /// and no existing automation building.
    /// </summary>
    private bool HasFoundationsAndNoOverlap(Vector2Int origin, Vector2Int size, int level)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                var cell = new Vector2Int(x, y);
                var existing = _grid.GetAt(cell, level);
                if (existing == null || !existing.IsStructural)
                    return false;
                if (_automationCells.Contains(new Vector3Int(x, y, level)))
                    return false;
            }
        }
        return true;
    }

    private void OccupyAutomationCells(Vector2Int origin, Vector2Int size, int level)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int y = origin.y; y < origin.y + size.y; y++)
                _automationCells.Add(new Vector3Int(x, y, level));
    }

    private void ClearAutomationCells(Vector2Int origin, Vector2Int size, int level)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int y = origin.y; y < origin.y + size.y; y++)
                _automationCells.Remove(new Vector3Int(x, y, level));
    }

    private static Vector2Int GetEffectiveSize(Vector2Int size, int rotation)
    {
        bool swapped = rotation == 90 || rotation == 270;
        return swapped ? new Vector2Int(size.y, size.x) : size;
    }
}

/// <summary>
/// Result of a successful building placement.
/// </summary>
public class PlacementResult
{
    public BuildingData BuildingData { get; }
    public object SimulationObject { get; }
    public List<PortNode> Ports { get; }

    public PlacementResult(BuildingData buildingData, object simulationObject, List<PortNode> ports)
    {
        BuildingData = buildingData;
        SimulationObject = simulationObject;
        Ports = ports;
    }
}
