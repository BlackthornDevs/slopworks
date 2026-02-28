using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around FactorySimulation.
/// Drives the simulation tick from FixedUpdate.
/// </summary>
// TODO: add server authority guard (if (!IsServerInitialized) return;) when NetworkBehaviour is added
public class FactorySimulationBehaviour : MonoBehaviour
{
    [SerializeField] private RecipeRegistry _recipeRegistry;

    private FactorySimulation _simulation;

    public FactorySimulation Simulation => _simulation;

    private void Awake()
    {
        _simulation = new FactorySimulation(_recipeRegistry.Get);
    }

    private void FixedUpdate()
    {
        _simulation.Tick(Time.fixedDeltaTime);
    }
}
