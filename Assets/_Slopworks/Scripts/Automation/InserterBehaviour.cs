using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the Inserter plain C# class.
/// Delegates all simulation logic to the Inserter instance.
/// </summary>
public class InserterBehaviour : MonoBehaviour
{
    [SerializeField] private float _swingDuration = 0.5f;

    private Inserter _inserter;

    /// <summary>
    /// The underlying Inserter simulation instance.
    /// Null until SetConnection is called.
    /// </summary>
    public Inserter Inserter => _inserter;

    /// <summary>
    /// Creates the Inserter with the given source and destination.
    /// Call this after the neighboring buildings have been placed and their
    /// adapters are available.
    /// </summary>
    public void SetConnection(IItemSource source, IItemDestination destination)
    {
        _inserter = new Inserter(source, destination, _swingDuration);
    }
}
