using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class GridManager : NetworkBehaviour
{
    public static GridManager Instance { get; private set; }

    private NetworkFactorySimulation _factorySimulation;

    private FactoryGrid _grid;

    private Dictionary<BuildingCategory, GameObject[]> _prefabArrays = new();

    /// <summary>
    /// Height offset from ground to the support's snap anchor (read from prefab).
    /// Belt endpoints on open ground are raised by this amount.
    /// </summary>
    public float SupportAnchorHeight { get; private set; }

    private static readonly Vector2Int[] CardinalDirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public FactoryGrid Grid => _grid;

    // ------------------------------------------------------------------
    // Prefab access
    // ------------------------------------------------------------------

    public GameObject[] GetPrefabs(BuildingCategory category)
    {
        _prefabArrays.TryGetValue(category, out var arr);
        return arr ?? System.Array.Empty<GameObject>();
    }

    public GameObject GetPrefab(BuildingCategory category, int variant)
    {
        var arr = GetPrefabs(category);
        if (arr.Length == 0) return null;
        return arr[Mathf.Clamp(variant, 0, arr.Length - 1)];
    }

    // ------------------------------------------------------------------
    // Placed record tracking
    // ------------------------------------------------------------------

    private struct PlacedRecord
    {
        public BuildingCategory Category;
        public GameObject Instance;
        public float SurfaceY;
        public Vector2Int Direction;
        public NetworkMachine NetMachine;
        public NetworkStorage NetStorage;
        public NetworkBeltSegment NetBelt;
    }

    private readonly Dictionary<long, PlacedRecord> _placedRecords = new();

    private static long RecordKey(Vector2Int cell, float surfaceY, Vector2Int direction = default)
    {
        int bucket = FactoryGrid.YBucket(surfaceY);
        int dirHash = (direction.x + 2) * 5 + (direction.y + 2);
        return ((long)(cell.x & 0xFFFF) << 48) | ((long)(cell.y & 0xFFFF) << 32) |
               ((long)(bucket & 0xFFFF) << 16) | (long)(dirHash & 0xFFFF);
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        _grid = new FactoryGrid();
        _factorySimulation = GetComponent<NetworkFactorySimulation>();
        LoadPrefabVariants();
    }

    private void LoadPrefabVariants()
    {
        _prefabArrays = new Dictionary<BuildingCategory, GameObject[]>();
        var folders = new Dictionary<BuildingCategory, string>
        {
            { BuildingCategory.Foundation, "Prefabs/Buildings/Foundations" },
            { BuildingCategory.Wall, "Prefabs/Buildings/Walls" },
            { BuildingCategory.Ramp, "Prefabs/Buildings/Ramps" },
            { BuildingCategory.Machine, "Prefabs/Buildings/Machines" },
            { BuildingCategory.Storage, "Prefabs/Buildings/Storage" },
            { BuildingCategory.Belt, "Prefabs/Buildings/Belts" },
            { BuildingCategory.Support, "Prefabs/Buildings/Supports" }
        };

        foreach (var kvp in folders)
        {
            var loaded = Resources.LoadAll<GameObject>(kvp.Value);
            _prefabArrays[kvp.Key] = loaded.Length > 0 ? loaded : System.Array.Empty<GameObject>();
        }

        foreach (var kvp in _prefabArrays)
            Debug.Log($"grid: {kvp.Key} variants: {kvp.Value.Length}");

        // Read snap anchor height from support prefab
        var supportPrefab = GetPrefab(BuildingCategory.Support, 0);
        if (supportPrefab != null)
        {
            var anchor = supportPrefab.GetComponentInChildren<BeltSnapAnchor>();
            SupportAnchorHeight = anchor != null ? anchor.transform.localPosition.y : 1.075f;
            Debug.Log($"grid: support anchor height = {SupportAnchorHeight:F3}");
        }
    }

    // ------------------------------------------------------------------
    // Universal placement positions
    // ALL ghost previews and server placement MUST use these methods.
    // NEVER compute placement offsets inline anywhere else.
    // Adding a new building type? Add one method here. Call it from both
    // GridManager (server spawn) and NetworkBuildController (ghost preview).
    //
    // Y offsets are derived from prefab bounds so changing prefab scale
    // doesn't break placement. The object's pivot sits at bounds center,
    // so we offset by extents.y to place the bottom on the surface.
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the scaled extents of a prefab's renderer in world units.
    /// Uses localBounds (valid on uninstantiated prefabs) scaled by lossyScale.
    /// </summary>
    public static Vector3 GetPrefabExtents(GameObject prefab)
    {
        if (prefab == null) return Vector3.one * 0.5f;
        var renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer == null) return Vector3.one * 0.5f;
        var lb = renderer.localBounds;
        var s = renderer.transform.lossyScale;
        return new Vector3(
            lb.extents.x * Mathf.Abs(s.x),
            lb.extents.y * Mathf.Abs(s.y),
            lb.extents.z * Mathf.Abs(s.z));
    }

    /// <summary>
    /// Returns the Y extent (half-height) of a prefab's renderer bounds.
    /// </summary>
    public static float GetPrefabHalfHeight(GameObject prefab)
    {
        return GetPrefabExtents(prefab).y;
    }

    /// <summary>
    /// Returns the Y offset needed to place the mesh bottom on the surface.
    /// Accounts for localBounds.center -- works correctly for both center-origin
    /// meshes (Unity primitives) and center-bottom meshes (Revit FBX exports).
    /// </summary>
    public static float GetPrefabBaseOffset(GameObject prefab)
    {
        if (prefab == null) return 0.5f;
        var renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer == null) return 0.5f;
        var lb = renderer.localBounds;
        var s = renderer.transform.lossyScale;
        float extY = lb.extents.y * Mathf.Abs(s.y);
        float centerY = lb.center.y * Mathf.Abs(s.y);
        // Distance from mesh origin to bottom of bounds
        // Center-origin cube: 0.5 - 0 = 0.5 (origin above bottom)
        // Center-bottom FBX:  h/2 - h/2 = 0 (origin at bottom)
        return extY - centerY;
    }

    /// <summary>
    /// Unified grid placement: snaps the prefab center to the nearest 1m grid intersection.
    /// Y is offset by the prefab's base offset so the bottom sits on the surface.
    /// Adjacency between buildings is handled by snap points, not grid alignment.
    /// </summary>
    public static (Vector3 position, Quaternion rotation) GetGridPlacementPosition(
        Vector3 hitPoint, GameObject prefab, int rotationDeg)
    {
        float posX = SnapAxis(hitPoint.x);
        float posZ = SnapAxis(hitPoint.z);
        float posY = hitPoint.y + GetPrefabBaseOffset(prefab);

        return (new Vector3(posX, posY, posZ), Quaternion.Euler(0f, rotationDeg, 0f));
    }

    /// <summary>
    /// Snap placement: find the ghost prefab's matching snap point (opposite normal,
    /// opposite height tier) and position the ghost so the two snap points meet.
    /// No extent/offset math -- positions are baked into the snap point children.
    /// </summary>
    public static (Vector3 position, Quaternion rotation) GetSnapPlacementPosition(
        BuildingSnapPoint snapPoint, GameObject prefab, int rotationDeg, float surfaceY,
        BuildingCategory ghostCategory = BuildingCategory.Foundation,
        BuildingCategory targetCategory = BuildingCategory.Foundation)
    {
        Vector3 targetPos = snapPoint.transform.position;
        Vector3 targetNormal = snapPoint.Normal;
        Quaternion ghostRot = Quaternion.Euler(0f, rotationDeg, 0f);

        var ghostSnap = FindGhostAttachSnap(prefab, targetNormal, snapPoint.gameObject.name,
            ghostRot, ghostCategory, targetCategory);

        if (ghostSnap != null)
        {
            Vector3 rotatedLocal = ghostRot * ghostSnap.transform.localPosition;
            Vector3 pos = targetPos - rotatedLocal;
            return (pos, ghostRot);
        }

        // Fallback: center ghost on target snap (shouldn't happen if prefab has snap points)
        Debug.LogWarning($"snap placement: no matching ghost snap on {prefab.name} for {snapPoint.gameObject.name}");
        return (targetPos, ghostRot);
    }

    /// <summary>
    /// Find the ghost prefab's snap point that should connect to the target snap.
    /// Structural on structural: opposite normal + opposite height tier.
    /// Machine/Storage on structural: always Center_Bot (sits on surface, never edge-to-edge).
    /// Machine/Storage on machine/storage: opposite normal cardinal _Bot matching (side-by-side).
    /// </summary>
    private static BuildingSnapPoint FindGhostAttachSnap(
        GameObject prefab, Vector3 targetNormal, string targetSnapName, Quaternion ghostRot,
        BuildingCategory ghostCategory, BuildingCategory targetCategory)
    {
        var snaps = prefab.GetComponentsInChildren<BuildingSnapPoint>();
        if (snaps.Length == 0) return null;

        bool ghostIsMachine = ghostCategory == BuildingCategory.Machine
            || ghostCategory == BuildingCategory.Storage;
        bool targetIsStructural = targetCategory == BuildingCategory.Foundation
            || targetCategory == BuildingCategory.Wall
            || targetCategory == BuildingCategory.Ramp;

        // Machine/Storage on structural: always use Center_Bot
        if (ghostIsMachine && targetIsStructural)
        {
            foreach (var s in snaps)
                if (s.gameObject.name.Contains("Center_Bot")) return s;
            return null;
        }

        // Standard matching: opposite normal + tier pairing
        Vector3 desiredLocal = Quaternion.Inverse(ghostRot) * (-targetNormal);

        bool peerSnap = ghostIsMachine
            && (targetCategory == BuildingCategory.Machine || targetCategory == BuildingCategory.Storage);

        string wantTier;
        if (targetSnapName.Contains("HighEdge")) wantTier = "LowEdge";
        else if (targetSnapName.Contains("LowEdge")) wantTier = "HighEdge";
        else if (targetSnapName.Contains("_Bot")) wantTier = peerSnap ? "_Bot" : "_Top";
        else if (targetSnapName.Contains("_Top")) wantTier = "_Bot";
        else wantTier = "_Mid";

        BuildingSnapPoint best = null;
        float bestScore = float.MinValue;

        foreach (var s in snaps)
        {
            float normalDot = Vector3.Dot(s.Normal, desiredLocal);
            if (normalDot < 0.5f) continue;

            float tierBonus = s.gameObject.name.Contains(wantTier) ? 1f : 0f;
            float score = normalDot + tierBonus;

            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }

        return best;
    }

    /// <summary>
    /// Snaps a value to the nearest 1m grid intersection.
    /// Adjacency between buildings is handled by snap points, not grid alignment.
    /// </summary>
    private static float SnapAxis(float hitValue)
    {
        return Mathf.Round(hitValue);
    }

    // ------------------------------------------------------------------
    // Unified CmdPlace RPCs
    // ------------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlace(Vector2Int cell, float surfaceY, int rotation, int variant,
        BuildingCategory category, Vector3 worldPos, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var prefab = GetPrefab(category, variant);
        if (prefab == null)
        {
            Debug.Log($"grid: no prefab for {category} variant {variant}");
            return;
        }

        var key = RecordKey(cell, surfaceY);
        if (_placedRecords.ContainsKey(key))
        {
            Debug.Log($"grid: {category} rejected -- cell occupied at ({cell.x},{cell.y}) y={surfaceY:F1}");
            return;
        }

        if (category == BuildingCategory.Foundation)
        {
            int fs = FactoryGrid.FoundationSize;
            var size = new Vector2Int(fs, fs);
            if (!_grid.CanPlace(cell, size, surfaceY)) return;
            _grid.Place(cell, size, surfaceY, new BuildingData("foundation", cell, size, 0, 0));
        }

        var go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, rotation, 0f));


        var info = go.AddComponent<PlacementInfo>();
        info.Category = category;
        info.Cell = cell;
        info.SurfaceY = surfaceY;
        info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
        ServerManager.Spawn(go);
        BuildingSnapPoint.GenerateFromBounds(go, category);

        var record = new PlacedRecord
        {
            Category = category, Instance = go, SurfaceY = surfaceY,
            NetMachine = go.GetComponent<NetworkMachine>(),
            NetStorage = go.GetComponent<NetworkStorage>()
        };
        _placedRecords[key] = record;

        if (record.NetMachine != null && _factorySimulation != null)
            _factorySimulation.RegisterMachine(record.NetMachine);

        AutoWire(record, cell);
        Debug.Log($"grid: {category} placed at ({cell.x},{cell.y}) y={surfaceY:F1} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceDirectional(Vector2Int cell, float surfaceY, Vector2Int direction,
        int variant, BuildingCategory category, Vector3 worldPos, Quaternion worldRot,
        NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var prefab = GetPrefab(category, variant);
        if (prefab == null) return;

        var key = RecordKey(cell, surfaceY, direction);
        if (_placedRecords.ContainsKey(key))
        {
            Debug.Log($"grid: {category} already exists at ({cell.x},{cell.y}) y={surfaceY:F1} dir ({direction.x},{direction.y})");
            return;
        }

        var go = Instantiate(prefab, worldPos, worldRot);


        var info = go.AddComponent<PlacementInfo>();
        info.Category = category;
        info.Cell = cell;
        info.SurfaceY = surfaceY;
        info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
        info.EdgeDirection = direction;
        ServerManager.Spawn(go);
        BuildingSnapPoint.GenerateFromBounds(go, category);

        _placedRecords[key] = new PlacedRecord
        {
            Category = category, Instance = go, SurfaceY = surfaceY, Direction = direction
        };

        Debug.Log($"grid: {category} placed at ({cell.x},{cell.y}) y={surfaceY:F1} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir,
        byte tier = 0, int variant = 0, byte routingMode = 0,
        bool startFromPort = true, bool endFromPort = true,
        NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var mode = (BeltRoutingMode)routingMode;

        // For free endpoints, startPos/endPos are raw ground positions.
        // Spawn supports first and use their actual anchor positions for the belt.
        var beltStartPos = startPos;
        var beltEndPos = endPos;

        if (!startFromPort)
            beltStartPos = SpawnSupportAt(startPos, Quaternion.LookRotation(startDir), sender);
        if (!endFromPort)
            beltEndPos = SpawnSupportAt(endPos, Quaternion.LookRotation(endDir), sender);

        // Server is final authority -- validate without bypasses
        var validation = BeltPlacementValidator.Validate(beltStartPos, startDir, beltEndPos, endDir);
        if (!validation.IsValid)
        {
            Debug.Log($"grid: belt placement rejected: {validation.Error} by {sender?.ClientId}");
            return;
        }

        var prefab = GetPrefab(BuildingCategory.Belt, variant);
        if (prefab == null) return;

        var waypoints = BeltRouteBuilder.Build(beltStartPos, startDir, beltEndPos, endDir, mode);
        var arcLength = BeltRouteBuilder.ComputeRouteLength(waypoints);
        var segment = BeltSegment.FromArcLength(arcLength);

        var midpoint = BeltRouteBuilder.EvaluateRoute(waypoints, arcLength, 0.5f);
        var go = Instantiate(prefab, midpoint, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        go.layer = PhysicsLayers.Structures;

        var prefabCollider = go.GetComponent<Collider>();
        if (prefabCollider != null)
            DestroyImmediate(prefabCollider);

        var info = go.AddComponent<PlacementInfo>();
        info.Category = BuildingCategory.Belt;
        info.SurfaceY = beltStartPos.y;

        var netBelt = go.GetComponent<NetworkBeltSegment>();
        if (netBelt != null)
            netBelt.ServerInit(segment, beltStartPos, startDir, beltEndPos, endDir, waypoints, tier, mode);

        ServerManager.Spawn(go);

        var material = go.GetComponent<MeshRenderer>()?.sharedMaterial;
        BeltSplineMeshBaker.BakeMesh(go, waypoints, material);

        var meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = go.GetComponent<MeshFilter>()?.sharedMesh;

        var visualizer = go.AddComponent<BeltItemVisualizer>();
        visualizer.Init(netBelt);

        if (netBelt != null && _factorySimulation != null)
            _factorySimulation.RegisterBelt(netBelt);

        AddBeltPort(go, beltStartPos, -startDir, BeltPortDirection.Input, 0);
        AddBeltPort(go, beltEndPos, endDir, BeltPortDirection.Output, 0);

        Debug.Log($"grid: {mode} belt placed from {beltStartPos} to {beltEndPos} route={arcLength:F1}m by {sender?.ClientId}");
    }

    private static void AddBeltPort(GameObject parent, Vector3 worldPos, Vector3 worldDir, BeltPortDirection direction, int slotIndex)
    {
        var portName = direction == BeltPortDirection.Input ? "BeltPort_Input" : "BeltPort_Output";
        var child = new GameObject($"{portName}_{slotIndex}");
        child.transform.SetParent(parent.transform);
        child.transform.position = worldPos;
        child.transform.forward = worldDir;
        child.layer = PhysicsLayers.BeltPorts;

        var port = child.AddComponent<BeltPort>();
        port.Direction = direction;
        port.SlotIndex = slotIndex;

        var col = child.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        col.isTrigger = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceSupport(Vector3 position, Quaternion rotation,
        int variant = 0, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;
        SpawnSupportAt(position, rotation, sender);
    }

    private Vector3 SpawnSupportAt(Vector3 groundPos, Quaternion rotation, NetworkConnection sender = null)
    {
        var prefab = GetPrefab(BuildingCategory.Support, 0);
        if (prefab == null)
        {
            Debug.LogWarning($"grid: no support prefab found at {groundPos}");
            return groundPos;
        }

        var instance = Instantiate(prefab, groundPos, rotation);

        var info = instance.AddComponent<PlacementInfo>();
        info.Category = BuildingCategory.Support;
        info.SurfaceY = groundPos.y;

        ServerManager.Spawn(instance);

        var anchor = instance.GetComponentInChildren<BeltSnapAnchor>();
        var anchorPos = anchor != null ? anchor.WorldPosition : groundPos;

        Debug.Log($"grid: support at {groundPos}, anchor at {anchorPos} by {sender?.ClientId}");
        return anchorPos;
    }

    // ------------------------------------------------------------------
    // Unified CmdDelete RPCs
    // ------------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void CmdDelete(Vector2Int cell, float surfaceY, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var key = RecordKey(cell, surfaceY);
        if (_placedRecords.TryGetValue(key, out var record))
        {
            if (record.Instance != null)
                ServerManager.Despawn(record.Instance);
            _placedRecords.Remove(key);

            if (record.Category == BuildingCategory.Foundation)
            {
                int fs = FactoryGrid.FoundationSize;
                _grid.Remove(cell, new Vector2Int(fs, fs), surfaceY);
            }

            Debug.Log($"grid: deleted {record.Category} at ({cell.x},{cell.y}) y={surfaceY:F1} by {sender?.ClientId}");
            return;
        }

        Debug.Log($"grid: nothing to delete at ({cell.x},{cell.y}) y={surfaceY:F1}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdDeleteDirectional(Vector2Int cell, float surfaceY, Vector2Int direction,
        NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var key = RecordKey(cell, surfaceY, direction);
        if (_placedRecords.TryGetValue(key, out var record))
        {
            if (record.Instance != null)
                ServerManager.Despawn(record.Instance);
            _placedRecords.Remove(key);
            Debug.Log($"grid: deleted {record.Category} at ({cell.x},{cell.y}) y={surfaceY:F1} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
            return;
        }

        Debug.Log($"grid: nothing to delete at ({cell.x},{cell.y}) y={surfaceY:F1} dir ({direction.x},{direction.y})");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdDeleteByNetworkObject(NetworkObject nob, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;
        if (nob == null) return;

        ServerManager.Despawn(nob.gameObject);
        Debug.Log($"grid: deleted network object {nob.gameObject.name} by {sender?.ClientId}");
    }

    // ------------------------------------------------------------------
    // Query methods
    // ------------------------------------------------------------------

    public bool HasBuildingAt(Vector2Int cell, float surfaceY)
    {
        return _placedRecords.ContainsKey(RecordKey(cell, surfaceY));
    }

    public bool HasDirectionalAt(Vector2Int cell, float surfaceY, Vector2Int direction)
    {
        return _placedRecords.ContainsKey(RecordKey(cell, surfaceY, direction));
    }

    // ------------------------------------------------------------------
    // Auto-wiring
    // ------------------------------------------------------------------

    private void AutoWire(PlacedRecord newRecord, Vector2Int cell)
    {
        if (_factorySimulation == null) return;

        for (int d = 0; d < CardinalDirs.Length; d++)
        {
            var neighborCell = cell + CardinalDirs[d];
            var neighborKey = RecordKey(neighborCell, newRecord.SurfaceY);

            if (!_placedRecords.TryGetValue(neighborKey, out var neighbor))
                continue;

            TryCreateInserter(newRecord, neighbor, cell, neighborCell);
            TryCreateInserter(neighbor, newRecord, neighborCell, cell);
        }
    }

    private void TryCreateInserter(PlacedRecord source, PlacedRecord dest, Vector2Int sourceCell, Vector2Int destCell)
    {
        IItemSource itemSource = GetItemSource(source);
        IItemDestination itemDest = GetItemDestination(dest);

        if (itemSource == null || itemDest == null) return;

        var inserter = new Inserter(itemSource, itemDest, 0.5f);
        _factorySimulation.RegisterInserter(inserter);
        Debug.Log($"grid: auto-wired inserter from {source.Category} at ({sourceCell.x},{sourceCell.y}) to {dest.Category} at ({destCell.x},{destCell.y})");
    }

    private IItemSource GetItemSource(PlacedRecord record)
    {
        switch (record.Category)
        {
            case BuildingCategory.Belt when record.NetBelt?.Segment != null:
                return new BeltOutputAdapter(record.NetBelt.Segment);
            case BuildingCategory.Machine when record.NetMachine?.Machine != null:
                return new MachineOutputAdapter(record.NetMachine.Machine, 0);
            case BuildingCategory.Storage when record.NetStorage?.Container != null:
                return record.NetStorage.Container;
            default:
                return null;
        }
    }

    private IItemDestination GetItemDestination(PlacedRecord record)
    {
        switch (record.Category)
        {
            case BuildingCategory.Belt when record.NetBelt?.Segment != null:
                return new BeltInputAdapter(record.NetBelt.Segment);
            case BuildingCategory.Machine when record.NetMachine?.Machine != null:
                return new MachineInputAdapter(record.NetMachine.Machine, 0);
            case BuildingCategory.Storage when record.NetStorage?.Container != null:
                return record.NetStorage.Container;
            default:
                return null;
        }
    }
}
