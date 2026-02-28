using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around FactoryGrid (D-004).
/// Owns the grid instance and exposes it to other components.
/// </summary>
public class FactoryGridBehaviour : MonoBehaviour
{
    public FactoryGrid Grid { get; private set; }

    private void Awake()
    {
        Grid = new FactoryGrid();
    }
}
