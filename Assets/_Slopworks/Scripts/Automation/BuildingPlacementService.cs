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
    public PlacementResult PlaceMachine(MachineDefinitionSO def, Vector2Int cell, int rotation)
    {
        var effectiveSize = GetEffectiveSize(def.size, rotation);

        if (!_grid.CanPlace(cell, effectiveSize))
            return null;

        var buildingData = new BuildingData(def.machineId, cell, effectiveSize, rotation);
        _grid.Place(cell, effectiveSize, buildingData);

        var machine = new Machine(def);
        _simulation.RegisterMachine(machine);
        _simulationObjects[buildingData] = machine;

        var ports = CreatePortNodes(def.ports, cell, rotation, PortOwnerType.Machine, machine);
        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, machine, ports);
    }

    /// <summary>
    /// Place a storage container on the grid.
    /// </summary>
    public PlacementResult PlaceStorage(StorageDefinitionSO def, Vector2Int cell, int rotation)
    {
        var effectiveSize = GetEffectiveSize(def.size, rotation);

        if (!_grid.CanPlace(cell, effectiveSize))
            return null;

        var buildingData = new BuildingData(def.storageId, cell, effectiveSize, rotation);
        _grid.Place(cell, effectiveSize, buildingData);

        var storage = new StorageContainer(def.slotCount, def.maxStackSize);
        _simulationObjects[buildingData] = storage;

        var ports = def.ports != null
            ? CreatePortNodes(def.ports, cell, rotation, PortOwnerType.Storage, storage)
            : new List<PortNode>();

        RegisterAndResolve(ports);

        return new PlacementResult(buildingData, storage, ports);
    }

    /// <summary>
    /// Place a belt on the grid from startCell to endCell. Must be a straight line
    /// (same X or same Z). Creates a BeltSegment with port nodes at both endpoints.
    /// </summary>
    public PlacementResult PlaceBelt(Vector2Int startCell, Vector2Int endCell)
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

        // Check all cells in the path are empty
        for (int i = 0; i <= lengthInTiles; i++)
        {
            var checkCell = startCell + direction * i;
            if (!_grid.CanPlace(checkCell, Vector2Int.one))
                return null;
        }

        // Place all cells on the grid
        var buildingData = new BuildingData("belt", startCell, new Vector2Int(1, 1), 0);
        for (int i = 0; i <= lengthInTiles; i++)
        {
            var placeCell = startCell + direction * i;
            _grid.Place(placeCell, Vector2Int.one, buildingData);
        }

        var belt = new BeltSegment(lengthInTiles);
        _simulation.RegisterBelt(belt);
        _simulationObjects[buildingData] = belt;

        // Input port at start, facing backward (items enter from behind)
        // Output port at end, facing forward (items exit ahead)
        var ports = new List<PortNode>
        {
            new PortNode(startCell, -direction, PortType.Input, PortOwnerType.Belt, belt),
            new PortNode(endCell, direction, PortType.Output, PortOwnerType.Belt, belt)
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

        // Remove from grid
        _grid.Remove(data.Origin, data.Size);
        _simulationObjects.Remove(data);
    }

    private List<PortNode> CreatePortNodes(
        MachinePort[] portDefs,
        Vector2Int buildingOrigin,
        int rotation,
        PortOwnerType ownerType,
        object owner)
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
                worldCell, rotatedDir, def.type, ownerType, owner, slotIndex));
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
