using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// FishNet wrapper for a belt segment. Syncs geometry via SyncVars so clients
/// can reconstruct waypoints deterministically. Syncs item state via SyncList
/// for visual rendering. Server ticks the simulation.
/// </summary>
public class NetworkBeltSegment : NetworkBehaviour
{
    private BeltSegment _segment;
    private List<BeltRouteBuilder.Waypoint> _routeWaypoints;
    private float _routeLength;

    // Geometry -- synced once at spawn
    private readonly SyncVar<Vector3> _syncStartPos = new();
    private readonly SyncVar<Vector3> _syncStartDir = new();
    private readonly SyncVar<Vector3> _syncEndPos = new();
    private readonly SyncVar<Vector3> _syncEndDir = new();
    private readonly SyncVar<byte> _syncTier = new();
    private readonly SyncVar<byte> _syncRoutingMode = new();

    // Item state -- synced every tick
    private readonly SyncList<BeltItem> _syncItems = new();
    private readonly SyncVar<ushort> _syncTerminalGap = new();

    public BeltSegment Segment => _segment;
    public BeltRoutingMode RoutingMode => (BeltRoutingMode)_syncRoutingMode.Value;
    public int ItemCount => _syncItems.Count;
    public Vector3 StartPos => _syncStartPos.Value;
    public Vector3 EndPos => _syncEndPos.Value;
    public byte Tier => _syncTier.Value;

    /// <summary>
    /// Server-side initialization. All modes use waypoints.
    /// Called by GridManager after spawning the belt.
    /// </summary>
    public void ServerInit(BeltSegment segment, Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir, List<BeltRouteBuilder.Waypoint> waypoints,
        byte tier = 0, BeltRoutingMode mode = BeltRoutingMode.Default)
    {
        _segment = segment;
        _routeWaypoints = waypoints;
        _routeLength = BeltRouteBuilder.ComputeRouteLength(waypoints);

        _syncStartPos.Value = startPos;
        _syncStartDir.Value = startDir;
        _syncEndPos.Value = endPos;
        _syncEndDir.Value = endDir;
        _syncTier.Value = tier;
        _syncRoutingMode.Value = (byte)mode;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // In host mode the server already baked the mesh in GridManager.
        // Rebuilding route + mesh here would double the work for no benefit.
        if (IsServerInitialized) return;

        var mode = (BeltRoutingMode)_syncRoutingMode.Value;
        _routeWaypoints = BeltRouteBuilder.Build(
            _syncStartPos.Value, _syncStartDir.Value,
            _syncEndPos.Value, _syncEndDir.Value, mode);
        _routeLength = BeltRouteBuilder.ComputeRouteLength(_routeWaypoints);

        var material = GetComponent<MeshRenderer>()?.sharedMaterial;
        BeltSplineMeshBaker.BakeMesh(gameObject, _routeWaypoints, material);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_segment != null) return;

        var mode = (BeltRoutingMode)_syncRoutingMode.Value;
        _routeWaypoints = BeltRouteBuilder.Build(
            _syncStartPos.Value, _syncStartDir.Value,
            _syncEndPos.Value, _syncEndDir.Value, mode);
        _routeLength = BeltRouteBuilder.ComputeRouteLength(_routeWaypoints);
        _segment = BeltSegment.FromArcLength(_routeLength);
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
    /// </summary>
    public Vector3 EvaluatePosition(float t)
    {
        if (_routeWaypoints != null)
            return BeltRouteBuilder.EvaluateRoute(_routeWaypoints, _routeLength, t);
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

        float arcLength = _routeLength;
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
