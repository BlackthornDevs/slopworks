using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper for the PowerNetworkManager.
/// Follows D-004: simulation logic in plain C# classes, MonoBehaviours as thin wrappers.
/// </summary>
public class PowerNetworkBehaviour : MonoBehaviour
{
    private PowerNetworkManager _manager;

    /// <summary>
    /// The underlying power network manager instance.
    /// </summary>
    public PowerNetworkManager Manager => _manager;

    private void Awake()
    {
        _manager = new PowerNetworkManager();
    }
}
