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
    private List<BeltRouteBuilder.Waypoint> _routeWaypoints;
    private float _routeLength;

    // Spline geometry -- synced once at spawn (D-BLT-006)
    private readonly SyncVar<Vector3> _syncStartPos = new();
    private readonly SyncVar<Vector3> _syncStartTangent = new();
    private readonly SyncVar<Vector3> _syncEndPos = new();
    private readonly SyncVar<Vector3> _syncEndTangent = new();
    private readonly SyncVar<byte> _syncTier = new();
    private readonly SyncVar<byte> _syncRoutingMode = new();

    // Item state -- synced every tick
    private readonly SyncList<BeltItem> _syncItems = new();
    private readonly SyncVar<ushort> _syncTerminalGap = new();

    public BeltSegment Segment => _segment;
    public BeltSplineData SplineData => _splineData;
    public BeltRoutingMode RoutingMode => (BeltRoutingMode)_syncRoutingMode.Value;
    public int ItemCount => _syncItems.Count;
    public Vector3 StartPos => _syncStartPos.Value;
    public Vector3 EndPos => _syncEndPos.Value;
    public byte Tier => _syncTier.Value;

    /// <summary>
    /// Server-side initialization with curved spline data.
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
        _syncRoutingMode.Value = (byte)BeltRoutingMode.Curved;
    }

    /// <summary>
    /// Server-side initialization with straight route data.
    /// Stores start/end pos+dir so clients can reconstruct waypoints.
    /// </summary>
    public void ServerInitStraight(BeltSegment segment, Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir, List<BeltRouteBuilder.Waypoint> waypoints, byte tier = 0)
    {
        _segment = segment;
        _routeWaypoints = waypoints;
        _routeLength = BeltRouteBuilder.ComputeRouteLength(waypoints);

        _syncStartPos.Value = startPos;
        _syncStartTangent.Value = startDir;
        _syncEndPos.Value = endPos;
        _syncEndTangent.Value = endDir;
        _syncTier.Value = tier;
        _syncRoutingMode.Value = (byte)BeltRoutingMode.Straight;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        var mode = (BeltRoutingMode)_syncRoutingMode.Value;
        var material = GetComponent<MeshRenderer>()?.sharedMaterial;

        if (mode == BeltRoutingMode.Straight)
        {
            // Reconstruct waypoints deterministically from synced start/end
            _routeWaypoints = BeltRouteBuilder.Build(
                _syncStartPos.Value, _syncStartTangent.Value,
                _syncEndPos.Value, _syncEndTangent.Value);
            _routeLength = BeltRouteBuilder.ComputeRouteLength(_routeWaypoints);
            BeltSplineMeshBaker.BakeMesh(gameObject, _routeWaypoints, material);
        }
        else
        {
            _splineData = new BeltSplineData(
                _syncStartPos.Value,
                _syncStartTangent.Value,
                _syncEndPos.Value,
                _syncEndTangent.Value);
            BeltSplineMeshBaker.BakeMesh(gameObject, _splineData, material);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_segment != null) return;

        var mode = (BeltRoutingMode)_syncRoutingMode.Value;
        if (mode == BeltRoutingMode.Straight)
        {
            _routeWaypoints = BeltRouteBuilder.Build(
                _syncStartPos.Value, _syncStartTangent.Value,
                _syncEndPos.Value, _syncEndTangent.Value);
            _routeLength = BeltRouteBuilder.ComputeRouteLength(_routeWaypoints);
            _segment = BeltSegment.FromArcLength(_routeLength);
        }
        else
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
    /// Evaluate a position along the belt at parameter t in [0, 1].
    /// Works for both curved and straight routing modes.
    /// </summary>
    public Vector3 EvaluatePosition(float t)
    {
        if (RoutingMode == BeltRoutingMode.Straight && _routeWaypoints != null)
            return BeltRouteBuilder.EvaluateRoute(_routeWaypoints, _routeLength, t);
        if (_splineData != null)
            return _splineData.Evaluate(t);
        return transform.position;
    }

    /// <summary>
    /// Get world-space positions for belt items.
    /// Clients call this for visual rendering.
    /// </summary>
    public void GetItemWorldPositions(List<Vector3> positions)
    {
        positions.Clear();
        if (_syncItems.Count == 0) return;

        float arcLength = RoutingMode == BeltRoutingMode.Straight
            ? _routeLength
            : (_splineData?.ArcLength ?? 0f);

        int totalLength = _segment?.TotalLength ??
            (int)System.Math.Round(arcLength * BeltItem.SubdivisionsPerTile);
        if (totalLength == 0) return;

        float cumulative = 0f;
        for (int i = 0; i < _syncItems.Count; i++)
        {
            cumulative += _syncItems[i].distanceToNext;
            float t = cumulative / totalLength;
            positions.Add(EvaluatePosition(t));
        }
    }
}
