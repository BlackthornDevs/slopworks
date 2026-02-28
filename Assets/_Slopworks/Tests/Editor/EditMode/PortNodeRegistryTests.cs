using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PortNodeRegistryTests
{
    private PortNodeRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new PortNodeRegistry();
    }

    private PortNode CreatePort(
        Vector2Int cell,
        Vector2Int direction,
        PortType type,
        object owner = null,
        PortOwnerType ownerType = PortOwnerType.Machine,
        int slotIndex = -1)
    {
        return new PortNode(cell, direction, type, ownerType, owner ?? new object(), slotIndex);
    }

    // -- Register / Count --

    [Test]
    public void NewRegistry_CountIsZero()
    {
        Assert.AreEqual(0, _registry.Count);
    }

    [Test]
    public void Register_IncrementsCount()
    {
        _registry.Register(CreatePort(Vector2Int.zero, Vector2Int.right, PortType.Output));
        Assert.AreEqual(1, _registry.Count);
    }

    [Test]
    public void Register_MultiplePorts_CountMatchesTotal()
    {
        _registry.Register(CreatePort(new Vector2Int(0, 0), Vector2Int.right, PortType.Output));
        _registry.Register(CreatePort(new Vector2Int(1, 0), Vector2Int.left, PortType.Input));
        _registry.Register(CreatePort(new Vector2Int(2, 0), Vector2Int.right, PortType.Output));
        Assert.AreEqual(3, _registry.Count);
    }

    // -- Unregister --

    [Test]
    public void Unregister_DecrementsCount()
    {
        var port = CreatePort(Vector2Int.zero, Vector2Int.right, PortType.Output);
        _registry.Register(port);
        _registry.Unregister(port);
        Assert.AreEqual(0, _registry.Count);
    }

    [Test]
    public void Unregister_NonexistentPort_DoesNothing()
    {
        var port = CreatePort(Vector2Int.zero, Vector2Int.right, PortType.Output);
        _registry.Unregister(port);
        Assert.AreEqual(0, _registry.Count);
    }

    // -- GetPortsAt --

    [Test]
    public void GetPortsAt_EmptyCell_ReturnsEmpty()
    {
        var ports = _registry.GetPortsAt(new Vector2Int(5, 5));
        Assert.AreEqual(0, ports.Count);
    }

    [Test]
    public void GetPortsAt_ReturnsRegisteredPort()
    {
        var port = CreatePort(new Vector2Int(3, 4), Vector2Int.right, PortType.Output);
        _registry.Register(port);

        var ports = _registry.GetPortsAt(new Vector2Int(3, 4));
        Assert.AreEqual(1, ports.Count);
        Assert.AreSame(port, ports[0]);
    }

    [Test]
    public void GetPortsAt_MultiplePortsSameCell_ReturnsAll()
    {
        var port1 = CreatePort(new Vector2Int(1, 1), Vector2Int.right, PortType.Output);
        var port2 = CreatePort(new Vector2Int(1, 1), Vector2Int.up, PortType.Input);
        _registry.Register(port1);
        _registry.Register(port2);

        var ports = _registry.GetPortsAt(new Vector2Int(1, 1));
        Assert.AreEqual(2, ports.Count);
    }

    [Test]
    public void GetPortsAt_AfterUnregister_ReturnsEmpty()
    {
        var port = CreatePort(new Vector2Int(2, 2), Vector2Int.right, PortType.Output);
        _registry.Register(port);
        _registry.Unregister(port);

        var ports = _registry.GetPortsAt(new Vector2Int(2, 2));
        Assert.AreEqual(0, ports.Count);
    }

    // -- FindCompatiblePort --

    [Test]
    public void FindCompatiblePort_AdjacentFacingOpposite_ReturnsMatch()
    {
        // Output at (3, 5) facing east, Input at (4, 5) facing west
        var output = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var input = CreatePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input);
        _registry.Register(output);
        _registry.Register(input);

        var found = _registry.FindCompatiblePort(output);
        Assert.AreSame(input, found);
    }

    [Test]
    public void FindCompatiblePort_SymmetricLookup()
    {
        var output = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var input = CreatePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input);
        _registry.Register(output);
        _registry.Register(input);

        // Looking from the input side should find the output
        var found = _registry.FindCompatiblePort(input);
        Assert.AreSame(output, found);
    }

    [Test]
    public void FindCompatiblePort_SameType_ReturnsNull()
    {
        // Two outputs facing each other -- not compatible
        var port1 = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var port2 = CreatePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Output);
        _registry.Register(port1);
        _registry.Register(port2);

        Assert.IsNull(_registry.FindCompatiblePort(port1));
    }

    [Test]
    public void FindCompatiblePort_SameDirection_ReturnsNull()
    {
        // Both facing right -- not facing each other
        var port1 = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var port2 = CreatePort(new Vector2Int(4, 5), Vector2Int.right, PortType.Input);
        _registry.Register(port1);
        _registry.Register(port2);

        Assert.IsNull(_registry.FindCompatiblePort(port1));
    }

    [Test]
    public void FindCompatiblePort_NotAdjacent_ReturnsNull()
    {
        // Two cells apart, not adjacent
        var port1 = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var port2 = CreatePort(new Vector2Int(5, 5), Vector2Int.left, PortType.Input);
        _registry.Register(port1);
        _registry.Register(port2);

        Assert.IsNull(_registry.FindCompatiblePort(port1));
    }

    [Test]
    public void FindCompatiblePort_AlreadyConnected_ReturnsNull()
    {
        var output = CreatePort(new Vector2Int(3, 5), Vector2Int.right, PortType.Output);
        var input = CreatePort(new Vector2Int(4, 5), Vector2Int.left, PortType.Input);
        input.Connection = new object(); // already connected
        _registry.Register(output);
        _registry.Register(input);

        Assert.IsNull(_registry.FindCompatiblePort(output));
    }

    [Test]
    public void FindCompatiblePort_NorthSouth_ReturnsMatch()
    {
        var output = CreatePort(new Vector2Int(5, 3), Vector2Int.up, PortType.Output);
        var input = CreatePort(new Vector2Int(5, 4), Vector2Int.down, PortType.Input);
        _registry.Register(output);
        _registry.Register(input);

        Assert.AreSame(input, _registry.FindCompatiblePort(output));
    }

    [Test]
    public void FindCompatiblePort_NoPortsAtTarget_ReturnsNull()
    {
        var port = CreatePort(new Vector2Int(0, 0), Vector2Int.right, PortType.Output);
        _registry.Register(port);

        Assert.IsNull(_registry.FindCompatiblePort(port));
    }

    // -- GetPortsForOwner --

    [Test]
    public void GetPortsForOwner_ReturnsAllPortsWithSameOwner()
    {
        var owner = new object();
        var port1 = CreatePort(new Vector2Int(0, 0), Vector2Int.right, PortType.Output, owner);
        var port2 = CreatePort(new Vector2Int(1, 0), Vector2Int.left, PortType.Input, owner);
        var otherPort = CreatePort(new Vector2Int(2, 0), Vector2Int.right, PortType.Output);
        _registry.Register(port1);
        _registry.Register(port2);
        _registry.Register(otherPort);

        var result = _registry.GetPortsForOwner(owner);
        Assert.AreEqual(2, result.Count);
        Assert.Contains(port1, result);
        Assert.Contains(port2, result);
    }

    [Test]
    public void GetPortsForOwner_NoMatch_ReturnsEmpty()
    {
        _registry.Register(CreatePort(Vector2Int.zero, Vector2Int.right, PortType.Output));
        var result = _registry.GetPortsForOwner(new object());
        Assert.AreEqual(0, result.Count);
    }
}
