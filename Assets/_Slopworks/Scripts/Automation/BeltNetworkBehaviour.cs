using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the BeltNetwork simulation class.
/// Owns a BeltNetwork instance and exposes it for other systems to drive.
/// All simulation logic lives in BeltNetwork (D-004).
/// </summary>
public class BeltNetworkBehaviour : MonoBehaviour
{
    private BeltNetwork _network;

    public BeltNetwork Network => _network;

    private void Awake()
    {
        _network = new BeltNetwork();
    }
}
