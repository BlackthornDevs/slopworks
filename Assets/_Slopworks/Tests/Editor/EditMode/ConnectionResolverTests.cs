using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ConnectionResolverTests
{
    private PortNodeRegistry _registry;
    private FactorySimulation _simulation;
    private ConnectionResolver _resolver;
    private MachineDefinitionSO _machineDef;

    [SetUp]
    public void SetUp()
    {
        _registry = new PortNodeRegistry();
        _simulation = new FactorySimulation(_ => null);
        _resolver = new ConnectionResolver(_registry, _simulation);

        _machineDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _machineDef.machineId = "test_machine";
        _machineDef.machineType = "test";
        _machineDef.inputBufferSize = 1;
        _machineDef.outputBufferSize = 1;
        _machineDef.processingSpeed = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_machineDef);
    }

    private Machine CreateMachine()
    {
        return new Machine(_machineDef);
    }

    private BeltSegment CreateBelt(int tiles = 3)
    {
        return new BeltSegment(tiles);
    }

    private StorageContainer CreateStorage()
    {
        return new StorageContainer(4, 50);
    }

    private PortNode CreateMachinePort(Vector2Int cell, Vector2Int dir, PortType type, Machine machine, int slot = 0)
    {
        return new PortNode(cell, dir, type, PortOwnerType.Machine, machine, slot);
    }

    private PortNode CreateBeltPort(Vector2Int cell, Vector2Int dir, PortType type, BeltSegment belt)
    {
        return new PortNode(cell, dir, type, PortOwnerType.Belt, belt);
    }

    private PortNode CreateStoragePort(Vector2Int cell, Vector2Int dir, PortType type, StorageContainer storage)
    {
        return new PortNode(cell, dir, type, PortOwnerType.Storage, storage);
    }

    private void RegisterAndResolve(List<PortNode> ports)
    {
        foreach (var p in ports)
            _registry.Register(p);
        _resolver.ResolveConnectionsFor(ports);
    }

    // -- Belt to Belt --

    [Test]
    public void Resolve_BeltToBelt_CreatesBeltNetworkConnection()
    {
        var belt1 = CreateBelt();
        var belt2 = CreateBelt();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt1);
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt2);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.IsTrue(_simulation.BeltNetwork.IsConnected(belt1, belt2));
        Assert.IsNotNull(outputPort.Connection);
        Assert.IsNotNull(inputPort.Connection);
        Assert.AreSame(outputPort.Connection, inputPort.Connection);
        Assert.IsInstanceOf<BeltNetworkConnection>(outputPort.Connection);
    }

    // -- Belt to Machine --

    [Test]
    public void Resolve_BeltToMachine_CreatesInserter()
    {
        var belt = CreateBelt();
        var machine = CreateMachine();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt);
        var inputPort = CreateMachinePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, machine, 0);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
        Assert.IsNotNull(outputPort.Connection);
        Assert.IsInstanceOf<Inserter>(outputPort.Connection);
    }

    // -- Machine to Belt --

    [Test]
    public void Resolve_MachineToBelt_CreatesInserter()
    {
        var machine = CreateMachine();
        var belt = CreateBelt();

        var outputPort = CreateMachinePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, machine, 0);
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
        Assert.IsNotNull(outputPort.Connection);
    }

    // -- Storage to Belt --

    [Test]
    public void Resolve_StorageToBelt_CreatesInserter()
    {
        var storage = CreateStorage();
        var belt = CreateBelt();

        var outputPort = CreateStoragePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, storage);
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
    }

    // -- Belt to Storage --

    [Test]
    public void Resolve_BeltToStorage_CreatesInserter()
    {
        var belt = CreateBelt();
        var storage = CreateStorage();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt);
        var inputPort = CreateStoragePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, storage);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
    }

    // -- Machine to Storage --

    [Test]
    public void Resolve_MachineToStorage_CreatesInserter()
    {
        var machine = CreateMachine();
        var storage = CreateStorage();

        var outputPort = CreateMachinePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, machine, 0);
        var inputPort = CreateStoragePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, storage);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
    }

    // -- Storage to Machine --

    [Test]
    public void Resolve_StorageToMachine_CreatesInserter()
    {
        var storage = CreateStorage();
        var machine = CreateMachine();

        var outputPort = CreateStoragePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, storage);
        var inputPort = CreateMachinePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, machine, 0);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.InserterCount);
    }

    // -- No compatible neighbor --

    [Test]
    public void Resolve_NoCompatibleNeighbor_NoConnectionCreated()
    {
        var belt = CreateBelt();
        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt);

        RegisterAndResolve(new List<PortNode> { outputPort });

        Assert.AreEqual(0, _simulation.InserterCount);
        Assert.AreEqual(0, _simulation.BeltNetwork.ConnectionCount);
        Assert.IsNull(outputPort.Connection);
    }

    // -- Already connected port skipped --

    [Test]
    public void Resolve_AlreadyConnectedPort_SkipsIt()
    {
        var belt1 = CreateBelt();
        var belt2 = CreateBelt();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt1);
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt2);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        // Resolve again -- should not create duplicate
        _resolver.ResolveConnectionsFor(new List<PortNode> { outputPort, inputPort });

        Assert.AreEqual(1, _simulation.BeltNetwork.ConnectionCount);
    }

    // -- Removal --

    [Test]
    public void Remove_InserterConnection_UnregistersFromSimulation()
    {
        var belt = CreateBelt();
        var machine = CreateMachine();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt);
        var inputPort = CreateMachinePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, machine, 0);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });
        Assert.AreEqual(1, _simulation.InserterCount);

        _resolver.RemoveConnectionsFor(new List<PortNode> { outputPort });

        Assert.AreEqual(0, _simulation.InserterCount);
        Assert.IsNull(outputPort.Connection);
        Assert.IsNull(inputPort.Connection);
    }

    [Test]
    public void Remove_BeltNetworkConnection_DisconnectsBelts()
    {
        var belt1 = CreateBelt();
        var belt2 = CreateBelt();

        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt1);
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt2);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });
        Assert.IsTrue(_simulation.BeltNetwork.IsConnected(belt1, belt2));

        _resolver.RemoveConnectionsFor(new List<PortNode> { outputPort });

        Assert.IsFalse(_simulation.BeltNetwork.IsConnected(belt1, belt2));
        Assert.IsNull(outputPort.Connection);
        Assert.IsNull(inputPort.Connection);
    }

    [Test]
    public void Remove_NoConnection_DoesNothing()
    {
        var belt = CreateBelt();
        var port = CreateBeltPort(new Vector2Int(0, 0), Vector2Int.right, PortType.Output, belt);
        _registry.Register(port);

        // Should not throw
        _resolver.RemoveConnectionsFor(new List<PortNode> { port });
        Assert.IsNull(port.Connection);
    }

    // -- North-south connection --

    [Test]
    public void Resolve_NorthSouthPorts_CreatesConnection()
    {
        var belt1 = CreateBelt();
        var belt2 = CreateBelt();

        var outputPort = CreateBeltPort(new Vector2Int(5, 3), Vector2Int.up, PortType.Output, belt1);
        var inputPort = CreateBeltPort(new Vector2Int(5, 4), Vector2Int.down, PortType.Input, belt2);

        RegisterAndResolve(new List<PortNode> { outputPort, inputPort });

        Assert.IsTrue(_simulation.BeltNetwork.IsConnected(belt1, belt2));
    }

    // -- Register existing ports, then resolve new adjacent ones --

    [Test]
    public void Resolve_NewPortAdjacentToExisting_CreatesConnection()
    {
        var belt1 = CreateBelt();
        var belt2 = CreateBelt();

        // Register belt1 output port first (no neighbor yet)
        var outputPort = CreateBeltPort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output, belt1);
        _registry.Register(outputPort);
        _resolver.ResolveConnectionsFor(new List<PortNode> { outputPort });
        Assert.IsNull(outputPort.Connection);

        // Now place belt2 input adjacent -- should connect
        var inputPort = CreateBeltPort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input, belt2);
        _registry.Register(inputPort);
        _resolver.ResolveConnectionsFor(new List<PortNode> { inputPort });

        Assert.IsTrue(_simulation.BeltNetwork.IsConnected(belt1, belt2));
    }
}
