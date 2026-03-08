using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BuildingPlacementServiceTests
{
    private FactoryGrid _grid;
    private PortNodeRegistry _portRegistry;
    private FactorySimulation _simulation;
    private ConnectionResolver _connectionResolver;
    private BuildingPlacementService _service;

    private MachineDefinitionSO _machineDef;
    private StorageDefinitionSO _storageDef;

    [SetUp]
    public void SetUp()
    {
        _grid = new FactoryGrid();
        _portRegistry = new PortNodeRegistry();
        _simulation = new FactorySimulation(_ => null);
        _connectionResolver = new ConnectionResolver(_portRegistry, _simulation);
        _service = new BuildingPlacementService(_grid, _portRegistry, _connectionResolver, _simulation);

        _machineDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _machineDef.machineId = "smelter";
        _machineDef.machineType = "smelter";
        _machineDef.size = new Vector2Int(2, 2);
        _machineDef.inputBufferSize = 1;
        _machineDef.outputBufferSize = 1;
        _machineDef.processingSpeed = 1f;
        _machineDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = new Vector2Int(0, 0),
                direction = new Vector2Int(-1, 0), // west input
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = new Vector2Int(1, 1),
                direction = new Vector2Int(1, 0), // east output
                type = PortType.Output
            }
        };

        _storageDef = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        _storageDef.storageId = "storage_bin";
        _storageDef.slotCount = 4;
        _storageDef.maxStackSize = 50;
        _storageDef.size = Vector2Int.one;
        _storageDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0), // west input
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(1, 0), // east output
                type = PortType.Output
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_machineDef);
        Object.DestroyImmediate(_storageDef);
    }

    private void PlaceFoundationsAt(params Vector2Int[] cells)
    {
        foreach (var cell in cells)
        {
            var data = new BuildingData("foundation", cell, Vector2Int.one, 0, 0);
            data.IsStructural = true;
            _grid.Place(cell, Vector2Int.one, 0, data);
        }
    }

    private void PlaceFoundationRect(Vector2Int origin, Vector2Int size, int level = 0)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                var cell = new Vector2Int(x, y);
                var data = new BuildingData("foundation", cell, Vector2Int.one, 0, level);
                data.IsStructural = true;
                _grid.Place(cell, Vector2Int.one, level, data);
            }
        }
    }

    // -- Foundation requirement --

    [Test]
    public void PlaceMachine_WithoutFoundation_ReturnsNull()
    {
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        Assert.IsNull(result);
    }

    [Test]
    public void PlaceStorage_WithoutFoundation_ReturnsNull()
    {
        var result = _service.PlaceStorage(_storageDef, new Vector2Int(10, 10), 0);
        Assert.IsNull(result);
    }

    [Test]
    public void PlaceBelt_WithoutFoundation_ReturnsNull()
    {
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.IsNull(result);
    }

    [Test]
    public void PlaceBelt_OnFoundation_Succeeds()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.IsNotNull(result);
    }

    // -- Machine placement --

    [Test]
    public void PlaceMachine_ReturnsResult()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.BuildingData);
        Assert.IsInstanceOf<Machine>(result.SimulationObject);
        Assert.AreEqual(2, result.Ports.Count);
    }

    [Test]
    public void PlaceMachine_RegistersMachineWithSimulation()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        Assert.AreEqual(1, _simulation.MachineCount);
    }

    [Test]
    public void PlaceMachine_FoundationsRemainOnGrid()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);

        // Foundations should still be on the grid (automation doesn't overwrite them)
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(5, 5)));
        Assert.IsTrue(_grid.GetAt(new Vector2Int(5, 5)).IsStructural);
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(6, 6)));
        Assert.IsTrue(_grid.GetAt(new Vector2Int(6, 6)).IsStructural);
    }

    [Test]
    public void PlaceMachine_PortsAtCorrectPositions()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);

        // Input at (5,5) + (0,0) = (5,5), facing west
        var inputPort = result.Ports[0];
        Assert.AreEqual(new Vector2Int(5, 5), inputPort.Cell);
        Assert.AreEqual(new Vector2Int(-1, 0), inputPort.Direction);
        Assert.AreEqual(PortType.Input, inputPort.Type);

        // Output at (5,5) + (1,1) = (6,6), facing east
        var outputPort = result.Ports[1];
        Assert.AreEqual(new Vector2Int(6, 6), outputPort.Cell);
        Assert.AreEqual(new Vector2Int(1, 0), outputPort.Direction);
        Assert.AreEqual(PortType.Output, outputPort.Type);
    }

    [Test]
    public void PlaceMachine_Rotated90_PortsRotated()
    {
        PlaceFoundationRect(new Vector2Int(5, 4), new Vector2Int(2, 2));
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 4), 90);

        // Input at (5,4) + rotate((0,0), 90) = (5,4), direction rotate((-1,0), 90) = (0,1)
        var inputPort = result.Ports[0];
        Assert.AreEqual(new Vector2Int(5, 4), inputPort.Cell);
        Assert.AreEqual(new Vector2Int(0, 1), inputPort.Direction);

        // Output at (5,4) + rotate((1,1), 90) = (5,4) + (1,-1) = (6,3)
        var outputPort = result.Ports[1];
        Assert.AreEqual(new Vector2Int(6, 3), outputPort.Cell);
        Assert.AreEqual(new Vector2Int(0, -1), outputPort.Direction);
    }

    [Test]
    public void PlaceMachine_OverlappingAutomation_ReturnsNull()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(4, 4));
        _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        Assert.IsNull(result);
    }

    // -- Storage placement --

    [Test]
    public void PlaceStorage_ReturnsResult()
    {
        PlaceFoundationsAt(new Vector2Int(10, 10));
        var result = _service.PlaceStorage(_storageDef, new Vector2Int(10, 10), 0);
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<StorageContainer>(result.SimulationObject);
        Assert.AreEqual(2, result.Ports.Count);
    }

    // -- Belt placement --

    [Test]
    public void PlaceBelt_StraightLine_ReturnsResult()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.IsNotNull(result);
        Assert.IsInstanceOf<BeltSegment>(result.SimulationObject);
        Assert.AreEqual(2, result.Ports.Count);
    }

    [Test]
    public void PlaceBelt_RegistersBeltWithSimulation()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.AreEqual(1, _simulation.BeltCount);
    }

    [Test]
    public void PlaceBelt_PortsAtEndpoints()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));

        // Input at start (3,5), facing west (opposite of belt direction east)
        var inputPort = result.Ports[0];
        Assert.AreEqual(new Vector2Int(3, 5), inputPort.Cell);
        Assert.AreEqual(new Vector2Int(-1, 0), inputPort.Direction);
        Assert.AreEqual(PortType.Input, inputPort.Type);

        // Output at end (6,5), facing east (same as belt direction)
        var outputPort = result.Ports[1];
        Assert.AreEqual(new Vector2Int(6, 5), outputPort.Cell);
        Assert.AreEqual(new Vector2Int(1, 0), outputPort.Direction);
        Assert.AreEqual(PortType.Output, outputPort.Type);
    }

    [Test]
    public void PlaceBelt_SameCell_ReturnsNull()
    {
        PlaceFoundationsAt(new Vector2Int(3, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(3, 5));
        Assert.IsNull(result);
    }

    [Test]
    public void PlaceBelt_Diagonal_ReturnsNull()
    {
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 8));
        Assert.IsNull(result);
    }

    [Test]
    public void PlaceBelt_OverlappingPath_ReturnsNull()
    {
        // Place foundations covering both belt paths
        PlaceFoundationRect(new Vector2Int(3, 3), new Vector2Int(5, 5));

        _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(4, 3), new Vector2Int(4, 7));
        Assert.IsNull(result);
    }

    // -- Auto-connection --

    [Test]
    public void PlaceBeltAdjacentToMachineOutput_AutoConnects()
    {
        // Machine with east output at (6,6) -- needs 2x2 foundations at (5,5)
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);

        // Belt starting at (7,6) with input facing west -- should connect to machine output
        PlaceFoundationsAt(
            new Vector2Int(7, 6), new Vector2Int(8, 6),
            new Vector2Int(9, 6), new Vector2Int(10, 6));
        _service.PlaceBelt(new Vector2Int(7, 6), new Vector2Int(10, 6));

        Assert.AreEqual(1, _simulation.InserterCount);
    }

    [Test]
    public void PlaceBeltsThenConnect_AutoConnects()
    {
        // Belt 1: (3,5) -> (6,5), output at (6,5) facing east
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));

        // Belt 2: (7,5) -> (10,5), input at (7,5) facing west
        PlaceFoundationsAt(
            new Vector2Int(7, 5), new Vector2Int(8, 5),
            new Vector2Int(9, 5), new Vector2Int(10, 5));
        _service.PlaceBelt(new Vector2Int(7, 5), new Vector2Int(10, 5));

        Assert.AreEqual(1, _simulation.BeltNetwork.ConnectionCount);
    }

    // -- Removal --

    [Test]
    public void Remove_Machine_UnregistersAndCleansUp()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        var result = _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);
        Assert.AreEqual(1, _simulation.MachineCount);

        _service.Remove(result.BuildingData);

        Assert.AreEqual(0, _simulation.MachineCount);
        // Foundations should still be on the grid
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(5, 5)));
        Assert.IsTrue(_grid.GetAt(new Vector2Int(5, 5)).IsStructural);
        Assert.AreEqual(0, _portRegistry.Count);
    }

    [Test]
    public void Remove_Belt_UnregistersAndCleansUp()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.AreEqual(1, _simulation.BeltCount);

        _service.Remove(result.BuildingData);

        Assert.AreEqual(0, _simulation.BeltCount);
        Assert.AreEqual(0, _portRegistry.Count);
    }

    [Test]
    public void Remove_WithConnection_TearsDownConnection()
    {
        PlaceFoundationRect(new Vector2Int(5, 5), new Vector2Int(2, 2));
        _service.PlaceMachine(_machineDef, new Vector2Int(5, 5), 0);

        PlaceFoundationsAt(
            new Vector2Int(7, 6), new Vector2Int(8, 6),
            new Vector2Int(9, 6), new Vector2Int(10, 6));
        var beltResult = _service.PlaceBelt(new Vector2Int(7, 6), new Vector2Int(10, 6));
        Assert.AreEqual(1, _simulation.InserterCount);

        _service.Remove(beltResult.BuildingData);

        Assert.AreEqual(0, _simulation.InserterCount);
    }

    [Test]
    public void RemoveBelt_FreesAutomationCells()
    {
        PlaceFoundationsAt(
            new Vector2Int(3, 5), new Vector2Int(4, 5),
            new Vector2Int(5, 5), new Vector2Int(6, 5));
        var result = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));

        // Should not be able to place another belt on the same cells
        var overlap = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.IsNull(overlap);

        // After removal, should be able to place again
        _service.Remove(result.BuildingData);
        var reuse = _service.PlaceBelt(new Vector2Int(3, 5), new Vector2Int(6, 5));
        Assert.IsNotNull(reuse);
    }
}
