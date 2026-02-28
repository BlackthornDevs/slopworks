using System;
using System.Collections.Generic;

/// <summary>
/// Tick manager that orchestrates all registered machines in the factory.
/// Plain C# class per D-004 -- fully testable, no MonoBehaviour dependency.
/// </summary>
public class FactorySimulation
{
    private readonly List<Machine> _machines = new List<Machine>();
    private readonly Func<string, RecipeSO> _recipeLookup;

    public int MachineCount => _machines.Count;

    /// <param name="recipeLookup">Delegate to resolve recipe IDs to RecipeSO assets.</param>
    public FactorySimulation(Func<string, RecipeSO> recipeLookup)
    {
        _recipeLookup = recipeLookup ?? throw new ArgumentNullException(nameof(recipeLookup));
    }

    /// <summary>
    /// Adds a machine to the simulation. Duplicate registrations are ignored.
    /// </summary>
    public void RegisterMachine(Machine machine)
    {
        if (machine == null)
            throw new ArgumentNullException(nameof(machine));

        if (_machines.Contains(machine))
            return;

        _machines.Add(machine);
    }

    /// <summary>
    /// Removes a machine from the simulation. No-op if the machine is not registered.
    /// </summary>
    public void UnregisterMachine(Machine machine)
    {
        if (machine == null)
            return;

        _machines.Remove(machine);
    }

    /// <summary>
    /// Runs one simulation tick across all registered machines.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last tick in seconds.</param>
    public void Tick(float deltaTime)
    {
        // Pre-tick: reserved for future use (power calculation, etc.)

        for (int i = 0; i < _machines.Count; i++)
        {
            _machines[i].Tick(deltaTime, _recipeLookup);
        }

        // Post-tick: reserved for future use (flush temp buffers, etc.)
    }

    /// <summary>
    /// Returns a read-only view of all registered machines.
    /// </summary>
    public IReadOnlyList<Machine> GetMachines()
    {
        return _machines.AsReadOnly();
    }
}
