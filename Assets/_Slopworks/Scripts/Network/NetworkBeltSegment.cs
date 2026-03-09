using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// FishNet wrapper for a belt segment. Syncs spline geometry via SyncVars
/// so clients can reconstruct the mesh deterministically. Syncs item state
/// via SyncList for visual rendering. Server ticks the simulation.
/// </summary>
public class NetworkBeltSegment : NetworkBehaviour
{
    private BeltSegment _segment;
    private BeltSplineData _splineData;

    // Spline geometry -- synced once at spawn (D-BLT-006)
    private readonly SyncVar<Vector3> _syncStartPos = new();
    private readonly SyncVar<Vector3> _syncStartTangent = new();
    private readonly SyncVar<Vector3> _syncEndPos = new();
    private readonly SyncVar<Vector3> _syncEndTangent = new();
    private readonly SyncVar<byte> _syncTier = new();

    // Item state -- synced every tick
    private readonly SyncList<BeltItem> _syncItems = new();
    private readonly SyncVar<ushort> _syncTerminalGap = new();

    public BeltSegment Segment => _segment;
    public BeltSplineData SplineData => _splineData;
    public int ItemCount => _syncItems.Count;
    public Vector3 StartPos => _syncStartPos.Value;
    public Vector3 EndPos => _syncEndPos.Value;
    public byte Tier => _syncTier.Value;

    /// <summary>
    /// Server-side initialization with spline data.
    /// Called by GridManager after spawning the belt.
    /// </summary>
    public void ServerInit(BeltSegment segment, BeltSplineData splineData, byte tier = 0)
    {
        _segment = segment;
        _splineData = splineData;

        _syncStartPos.Value = splineData.P0;
        _syncStartTangent.Value = splineData.T0;
        _syncEndPos.Value = splineData.P1;
        _syncEndTangent.Value = splineData.T1;
        _syncTier.Value = tier;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Reconstruct spline from synced data
        _splineData = new BeltSplineData(
            _syncStartPos.Value,
            _syncStartTangent.Value,
            _syncEndPos.Value,
            _syncEndTangent.Value);

        // Bake mesh on client
        var material = GetComponent<MeshRenderer>()?.sharedMaterial;
        BeltSplineMeshBaker.BakeMesh(gameObject, _splineData, material);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_segment == null)
        {
            _splineData = new BeltSplineData(
                _syncStartPos.Value,
                _syncStartTangent.Value,
                _syncEndPos.Value,
                _syncEndTangent.Value);
            _segment = BeltSegment.FromArcLength(_splineData.ArcLength);
        }
    }

    /// <summary>
    /// Push simulation state to SyncList for client rendering.
    /// Called by NetworkFactorySimulation after each tick.
    /// </summary>
    public void ServerSyncState()
    {
        if (_segment == null) return;

        var items = _segment.GetItems();

        while (_syncItems.Count > items.Count)
            _syncItems.RemoveAt(_syncItems.Count - 1);

        for (int i = 0; i < items.Count; i++)
        {
            if (i < _syncItems.Count)
            {
                if (_syncItems[i].itemId != items[i].itemId ||
                    _syncItems[i].distanceToNext != items[i].distanceToNext)
                    _syncItems[i] = items[i];
            }
            else
            {
                _syncItems.Add(items[i]);
            }
        }

        _syncTerminalGap.Value = _segment.TerminalGap;
    }

    /// <summary>
    /// Get world-space positions for belt items using the spline.
    /// Clients call this for visual rendering.
    /// </summary>
    public void GetItemWorldPositions(List<Vector3> positions)
    {
        positions.Clear();
        if (_syncItems.Count == 0 || _splineData == null) return;

        int totalLength = _segment?.TotalLength ??
            (int)System.Math.Round(_splineData.ArcLength * BeltItem.SubdivisionsPerTile);
        if (totalLength == 0) return;

        float cumulative = 0f;
        for (int i = 0; i < _syncItems.Count; i++)
        {
            cumulative += _syncItems[i].distanceToNext;
            float t = cumulative / totalLength;
            positions.Add(_splineData.Evaluate(t));
        }
    }
}
