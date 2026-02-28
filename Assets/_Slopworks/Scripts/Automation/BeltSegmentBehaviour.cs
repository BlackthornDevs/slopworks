using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the BeltSegment simulation class.
/// Owns a BeltSegment instance and exposes it for other systems to drive.
/// All simulation logic lives in BeltSegment (D-004).
/// </summary>
public class BeltSegmentBehaviour : MonoBehaviour
{
    [SerializeField] private int _lengthInTiles = 1;

    private BeltSegment _segment;

    public BeltSegment Segment => _segment;

    private void Awake()
    {
        if (_lengthInTiles <= 0)
        {
            Debug.LogError("BeltSegmentBehaviour: length in tiles must be positive", this);
            _lengthInTiles = 1;
        }

        _segment = new BeltSegment(_lengthInTiles);
    }

    // TODO: register with FactorySimulation belt tick when belt integration is added
    // TODO: add SyncList<BeltItem> when NetworkBehaviour is added
}
