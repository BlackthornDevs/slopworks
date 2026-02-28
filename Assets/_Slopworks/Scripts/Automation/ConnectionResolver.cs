using System.Collections.Generic;

/// <summary>
/// Automatically creates connections (inserters or belt links) between compatible
/// port nodes. When a building or belt is placed, its ports are checked against
/// neighboring ports. If an Output port faces an adjacent Input port, the resolver
/// creates the appropriate adapter pair and registers it with the simulation.
/// </summary>
public class ConnectionResolver
{
    private const float DefaultSwingDuration = 0.5f;
    private const ushort DefaultBeltSpacing = 50;

    private readonly PortNodeRegistry _registry;
    private readonly FactorySimulation _simulation;

    public ConnectionResolver(PortNodeRegistry registry, FactorySimulation simulation)
    {
        _registry = registry;
        _simulation = simulation;
    }

    /// <summary>
    /// For each port in the list, find a compatible neighbor and create a connection.
    /// Call this after registering new ports with the registry.
    /// </summary>
    public void ResolveConnectionsFor(List<PortNode> newPorts)
    {
        for (int i = 0; i < newPorts.Count; i++)
        {
            var port = newPorts[i];
            if (port.Connection != null)
                continue;

            var compatible = _registry.FindCompatiblePort(port);
            if (compatible == null)
                continue;

            if (port.Type == PortType.Output)
                Connect(port, compatible);
            else
                Connect(compatible, port);
        }
    }

    /// <summary>
    /// Tear down all connections on the given ports and unregister from simulation.
    /// </summary>
    public void RemoveConnectionsFor(List<PortNode> ports)
    {
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            if (port.Connection == null)
                continue;

            Disconnect(port);
        }
    }

    /// <summary>
    /// Create a connection between an output port and an input port.
    /// The connection type depends on the owner types of each port.
    /// </summary>
    private void Connect(PortNode output, PortNode input)
    {
        // Belt-to-belt: use BeltNetwork (no inserter needed)
        if (output.OwnerType == PortOwnerType.Belt && input.OwnerType == PortOwnerType.Belt)
        {
            var fromBelt = (BeltSegment)output.Owner;
            var toBelt = (BeltSegment)input.Owner;
            _simulation.BeltNetwork.Connect(fromBelt, toBelt);

            // Store connection info so we can tear it down later
            var connection = new BeltNetworkConnection(fromBelt, toBelt);
            output.Connection = connection;
            input.Connection = connection;
            return;
        }

        // All other combinations: create adapter pair + inserter
        var source = CreateSource(output);
        var destination = CreateDestination(input);
        var inserter = new Inserter(source, destination, DefaultSwingDuration);
        _simulation.RegisterInserter(inserter);

        output.Connection = inserter;
        input.Connection = inserter;
    }

    /// <summary>
    /// Remove a connection from its port pair and unregister from simulation.
    /// </summary>
    private void Disconnect(PortNode port)
    {
        var connection = port.Connection;
        if (connection == null)
            return;

        // Find the partner port that shares this connection
        var partnerCell = port.Cell + port.Direction;
        var oppositeDir = -port.Direction;
        var portsAtPartner = _registry.GetPortsAt(partnerCell);
        for (int i = 0; i < portsAtPartner.Count; i++)
        {
            if (portsAtPartner[i].Connection == connection)
            {
                portsAtPartner[i].Connection = null;
                break;
            }
        }

        if (connection is BeltNetworkConnection beltConn)
        {
            _simulation.BeltNetwork.Disconnect(beltConn.From, beltConn.To);
        }
        else if (connection is Inserter inserter)
        {
            _simulation.UnregisterInserter(inserter);
        }

        port.Connection = null;
    }

    private IItemSource CreateSource(PortNode outputPort)
    {
        switch (outputPort.OwnerType)
        {
            case PortOwnerType.Belt:
                return new BeltOutputAdapter((BeltSegment)outputPort.Owner);
            case PortOwnerType.Machine:
                return new MachineOutputAdapter((Machine)outputPort.Owner, outputPort.SlotIndex);
            case PortOwnerType.Storage:
                return (IItemSource)(StorageContainer)outputPort.Owner;
            default:
                throw new System.ArgumentException(
                    $"Unknown output port owner type: {outputPort.OwnerType}");
        }
    }

    private IItemDestination CreateDestination(PortNode inputPort)
    {
        switch (inputPort.OwnerType)
        {
            case PortOwnerType.Belt:
                return new BeltInputAdapter((BeltSegment)inputPort.Owner, DefaultBeltSpacing);
            case PortOwnerType.Machine:
                return new MachineInputAdapter((Machine)inputPort.Owner, inputPort.SlotIndex);
            case PortOwnerType.Storage:
                return (IItemDestination)(StorageContainer)inputPort.Owner;
            default:
                throw new System.ArgumentException(
                    $"Unknown input port owner type: {inputPort.OwnerType}");
        }
    }
}

/// <summary>
/// Tracks a belt-to-belt connection for later teardown.
/// </summary>
public class BeltNetworkConnection
{
    public BeltSegment From { get; }
    public BeltSegment To { get; }

    public BeltNetworkConnection(BeltSegment from, BeltSegment to)
    {
        From = from;
        To = to;
    }
}
