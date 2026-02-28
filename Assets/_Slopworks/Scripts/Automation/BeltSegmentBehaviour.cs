using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the BeltSegment simulation class.
/// Owns a BeltSegment instance and exposes it for other systems to drive.
/// Handles visual item rendering with a simple object pool.
/// All simulation logic lives in BeltSegment (D-004).
/// </summary>
public class BeltSegmentBehaviour : MonoBehaviour
{
    [SerializeField] private int _lengthInTiles = 1;
    [SerializeField] private GameObject _itemVisualPrefab;
    [SerializeField] private float _itemScale = 0.3f;

    private BeltSegment _segment;
    private Vector3 _startWorldPos;
    private Vector3 _endWorldPos;
    private bool _initialized;

    private readonly List<float> _positionBuffer = new();
    private readonly List<GameObject> _itemPool = new();

    public BeltSegment Segment => _segment;

    private void Awake()
    {
        if (_segment != null)
            return;

        if (_lengthInTiles <= 0)
        {
            Debug.LogError("BeltSegmentBehaviour: length in tiles must be positive", this);
            _lengthInTiles = 1;
        }

        _segment = new BeltSegment(_lengthInTiles);
    }

    /// <summary>
    /// Initialize with an externally created BeltSegment and world endpoints.
    /// Called by BuildingPlacementBehaviour when placing belts.
    /// </summary>
    public void Initialize(BeltSegment segment, Vector3 startWorldPos, Vector3 endWorldPos)
    {
        _segment = segment;
        _startWorldPos = startWorldPos;
        _endWorldPos = endWorldPos;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _segment == null)
            return;

        UpdateItemVisuals();
    }

    private void UpdateItemVisuals()
    {
        _segment.GetItemPositions(_positionBuffer);

        // Ensure pool has enough objects
        while (_itemPool.Count < _positionBuffer.Count)
        {
            var visual = CreateItemVisual();
            _itemPool.Add(visual);
        }

        // Position active items
        for (int i = 0; i < _positionBuffer.Count; i++)
        {
            var visual = _itemPool[i];
            visual.SetActive(true);
            visual.transform.position = Vector3.Lerp(_startWorldPos, _endWorldPos, _positionBuffer[i]);
        }

        // Hide unused pool objects
        for (int i = _positionBuffer.Count; i < _itemPool.Count; i++)
        {
            _itemPool[i].SetActive(false);
        }
    }

    private GameObject CreateItemVisual()
    {
        GameObject visual;
        if (_itemVisualPrefab != null)
        {
            visual = Instantiate(_itemVisualPrefab, transform);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform);
            // Remove collider from pool objects
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        visual.transform.localScale = Vector3.one * _itemScale;
        visual.SetActive(false);
        return visual;
    }

    private void OnDestroy()
    {
        foreach (var visual in _itemPool)
        {
            if (visual != null)
                Destroy(visual);
        }
        _itemPool.Clear();
    }

    // TODO: register with FactorySimulation belt tick when belt integration is added
    // TODO: add SyncList<BeltItem> when NetworkBehaviour is added
}
