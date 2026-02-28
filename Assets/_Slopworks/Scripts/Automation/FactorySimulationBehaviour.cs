using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around FactorySimulation.
/// Drives the simulation tick from FixedUpdate.
/// </summary>
// TODO: add server authority guard (if (!IsServerInitialized) return;) when NetworkBehaviour is added
public class FactorySimulationBehaviour : MonoBehaviour
{
    [SerializeField] private RecipeRegistry _recipeRegistry;

    [Tooltip("Subdivisions per tick. At 50Hz, speed 2 = 1 tile/sec.")]
    [SerializeField] private ushort _beltSpeed = 2;

    private FactorySimulation _simulation;

    public FactorySimulation Simulation => _simulation;

    private void Awake()
    {
        _simulation = new FactorySimulation(_recipeRegistry.Get);
        _simulation.BeltSpeed = _beltSpeed;
    }

    private void FixedUpdate()
    {
        _simulation.Tick(Time.fixedDeltaTime);
    }
}
