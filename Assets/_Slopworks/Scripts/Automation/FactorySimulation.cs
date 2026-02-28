using System;
using System.Collections.Generic;

/// <summary>
/// Tick manager that orchestrates all factory subsystems in the correct order.
/// Plain C# class per D-004 -- fully testable, no MonoBehaviour dependency.
///
/// Tick order:
/// 1. Power network rebuild (if topology changed)
/// 2. Belt segments tick (advance items toward output)
/// 3. Belt network tick (transfer items between connected belts)
/// 4. Inserters tick (transfer items between belts, machines, storage)
/// 5. Machines tick (consume inputs, craft, produce outputs)
/// </summary>
public class FactorySimulation
{
    private readonly List<Machine> _machines = new List<Machine>();
    private readonly List<BeltSegment> _belts = new List<BeltSegment>();
    private readonly List<Inserter> _inserters = new List<Inserter>();
    private readonly BeltNetwork _beltNetwork = new BeltNetwork();
    private readonly PowerNetworkManager _powerManager = new PowerNetworkManager();
    private readonly Func<string, RecipeSO> _recipeLookup;

    private ushort _beltSpeed = 2;

    public int MachineCount => _machines.Count;
    public int BeltCount => _belts.Count;
    public int InserterCount => _inserters.Count;

    /// <summary>
    /// Belt-to-belt connection manager. Use this to connect/disconnect belt segments.
    /// </summary>
    public BeltNetwork BeltNetwork => _beltNetwork;

    /// <summary>
    /// Power network manager. Register IPowerNode instances here.
    /// </summary>
    public PowerNetworkManager PowerManager => _powerManager;

    /// <summary>
    /// Subdivisions per tick that all belts advance. At 50Hz FixedUpdate,
    /// speed 2 = 1 tile/second (100 subdivisions / 2 per tick / 50 ticks).
    /// </summary>
    public ushort BeltSpeed
    {
        get => _beltSpeed;
        set => _beltSpeed = value;
    }

    /// <param name="recipeLookup">Delegate to resolve recipe IDs to RecipeSO assets.</param>
    public FactorySimulation(Func<string, RecipeSO> recipeLookup)
    {
        _recipeLookup = recipeLookup ?? throw new ArgumentNullException(nameof(recipeLookup));
    }

    // -- Machine registration --

    public void RegisterMachine(Machine machine)
    {
        if (machine == null)
            throw new ArgumentNullException(nameof(machine));

        if (_machines.Contains(machine))
            return;

        _machines.Add(machine);
    }

    public void UnregisterMachine(Machine machine)
    {
        if (machine == null)
            return;

        _machines.Remove(machine);
    }

    public IReadOnlyList<Machine> GetMachines()
    {
        return _machines.AsReadOnly();
    }

    // -- Belt registration --

    public void RegisterBelt(BeltSegment belt)
    {
        if (belt == null)
            throw new ArgumentNullException(nameof(belt));

        if (_belts.Contains(belt))
            return;

        _belts.Add(belt);
    }

    public void UnregisterBelt(BeltSegment belt)
    {
        if (belt == null)
            return;

        _belts.Remove(belt);
    }

    public IReadOnlyList<BeltSegment> GetBelts()
    {
        return _belts.AsReadOnly();
    }

    // -- Inserter registration --

    public void RegisterInserter(Inserter inserter)
    {
        if (inserter == null)
            throw new ArgumentNullException(nameof(inserter));

        if (_inserters.Contains(inserter))
            return;

        _inserters.Add(inserter);
    }

    public void UnregisterInserter(Inserter inserter)
    {
        if (inserter == null)
            return;

        _inserters.Remove(inserter);
    }

    public IReadOnlyList<Inserter> GetInserters()
    {
        return _inserters.AsReadOnly();
    }

    // -- Tick --

    /// <summary>
    /// Runs one full simulation tick across all subsystems.
    /// </summary>
    public void Tick(float deltaTime)
    {
        // 1. Rebuild power networks if topology changed
        _powerManager.RebuildIfDirty();

        // 2. Advance all belt items toward output
        for (int i = 0; i < _belts.Count; i++)
            _belts[i].Tick(_beltSpeed);

        // 3. Transfer items between connected belts
        _beltNetwork.Tick();

        // 4. Tick all inserters (grab/swing/deposit)
        for (int i = 0; i < _inserters.Count; i++)
            _inserters[i].Tick(deltaTime);

        // 5. Tick all machines (crafting)
        // TODO: apply power satisfaction as speed multiplier once machines
        // implement IPowerNode: machine.Tick(deltaTime * satisfaction, ...)
        for (int i = 0; i < _machines.Count; i++)
            _machines[i].Tick(deltaTime, _recipeLookup);
    }
}
