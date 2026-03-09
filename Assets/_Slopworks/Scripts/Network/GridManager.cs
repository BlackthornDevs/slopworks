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
            { BuildingCategory.Belt, "Prefabs/Buildings/Belts" }
        };

        foreach (var kvp in folders)
        {
            var loaded = Resources.LoadAll<GameObject>(kvp.Value);
            _prefabArrays[kvp.Key] = loaded.Length > 0 ? loaded : System.Array.Empty<GameObject>();
        }

        foreach (var kvp in _prefabArrays)
            Debug.Log($"grid: {kvp.Key} variants: {kvp.Value.Length}");
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
        BuildingSnapPoint snapPoint, GameObject prefab, int rotationDeg, float surfaceY)
    {
        Vector3 targetPos = snapPoint.transform.position;
        Vector3 targetNormal = snapPoint.Normal;
        Quaternion ghostRot = Quaternion.Euler(0f, rotationDeg, 0f);

        var ghostSnap = FindGhostAttachSnap(prefab, targetNormal, snapPoint.gameObject.name, ghostRot);

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
    /// Picks opposite normal + opposite height tier (_Bot→_Top, _Top→_Bot, _Mid→_Mid).
    /// </summary>
    private static BuildingSnapPoint FindGhostAttachSnap(
        GameObject prefab, Vector3 targetNormal, string targetSnapName, Quaternion ghostRot)
    {
        var snaps = prefab.GetComponentsInChildren<BuildingSnapPoint>();
        if (snaps.Length == 0) return null;

        // Ghost snap's rotated normal should oppose the target normal
        Vector3 desiredLocal = Quaternion.Inverse(ghostRot) * (-targetNormal);

        // Opposite height tier / slope edge pairing
        string wantTier;
        if (targetSnapName.Contains("HighEdge")) wantTier = "LowEdge";
        else if (targetSnapName.Contains("LowEdge")) wantTier = "HighEdge";
        else if (targetSnapName.Contains("_Bot")) wantTier = "_Top";
        else if (targetSnapName.Contains("_Top")) wantTier = "_Bot";
        else wantTier = "_Mid";
        // Center snaps (Top_Center, Bot_Center) have vertical normals --
        // opposite normal match handles them without tier logic

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

    private static void SetBuildingLayer(GameObject go, BuildingCategory category)
    {
        int layer = category switch
        {
            BuildingCategory.Foundation => PhysicsLayers.Structures,
            BuildingCategory.Wall => PhysicsLayers.Structures,
            BuildingCategory.Ramp => PhysicsLayers.Structures,
            _ => go.layer // keep prefab layer (e.g. Interactable for machines)
        };
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
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

        SetBuildingLayer(go, category);
        var info = go.AddComponent<PlacementInfo>();
        info.Category = category;
        info.Cell = cell;
        info.SurfaceY = surfaceY;
        info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
        ServerManager.Spawn(go);
        BuildingSnapPoint.GenerateFromBounds(go, category == BuildingCategory.Ramp);

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

        SetBuildingLayer(go, category);
        var info = go.AddComponent<PlacementInfo>();
        info.Category = category;
        info.Cell = cell;
        info.SurfaceY = surfaceY;
        info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
        info.EdgeDirection = direction;
        ServerManager.Spawn(go);
        BuildingSnapPoint.GenerateFromBounds(go, category == BuildingCategory.Ramp);

        _placedRecords[key] = new PlacedRecord
        {
            Category = category, Instance = go, SurfaceY = surfaceY, Direction = direction
        };

        Debug.Log($"grid: {category} placed at ({cell.x},{cell.y}) y={surfaceY:F1} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceBelt(Vector2Int fromCell, Vector2Int toCell, float surfaceY,
        int variant = 0, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var prefab = GetPrefab(BuildingCategory.Belt, variant);
        if (prefab == null) return;

        float cs = FactoryGrid.CellSize;
        float beltHalfH = GetPrefabHalfHeight(prefab);
        Vector3 startPos = new Vector3(fromCell.x * cs + cs * 0.5f, surfaceY + beltHalfH, fromCell.y * cs + cs * 0.5f);
        Vector3 endPos = new Vector3(toCell.x * cs + cs * 0.5f, surfaceY + beltHalfH, toCell.y * cs + cs * 0.5f);
        int length = Mathf.Max(1, Mathf.Abs(toCell.x - fromCell.x) + Mathf.Abs(toCell.y - fromCell.y));

        var go = Instantiate(prefab, (startPos + endPos) * 0.5f, Quaternion.identity);


        var diff = endPos - startPos;
        float beltLen = diff.magnitude + FactoryGrid.CellSize;
        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
            go.transform.localScale = new Vector3(beltLen, 0.08f, 0.6f);
        else
            go.transform.localScale = new Vector3(0.6f, 0.08f, beltLen);

        var info = go.AddComponent<PlacementInfo>();
        info.Category = BuildingCategory.Belt;
        info.Cell = fromCell;
        info.SurfaceY = surfaceY;
        info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;

        var netBelt = go.GetComponent<NetworkBeltSegment>();
        if (netBelt != null)
        {
            var segment = new BeltSegment(length);
            netBelt.ServerInit(segment, startPos, endPos);
        }

        ServerManager.Spawn(go);
        BuildingSnapPoint.GenerateFromBounds(go);

        var visualizer = go.AddComponent<BeltItemVisualizer>();
        visualizer.Init(netBelt);

        if (netBelt != null && _factorySimulation != null)
            _factorySimulation.RegisterBelt(netBelt);

        var fromKey = RecordKey(fromCell, surfaceY);
        var record = new PlacedRecord
        {
            Category = BuildingCategory.Belt, Instance = go,
            SurfaceY = surfaceY, NetBelt = netBelt
        };
        _placedRecords[fromKey] = record;

        if (fromCell != toCell)
            _placedRecords[RecordKey(toCell, surfaceY)] = record;

        AutoWire(record, fromCell);
        Debug.Log($"grid: belt placed from ({fromCell.x},{fromCell.y}) to ({toCell.x},{toCell.y}) y={surfaceY:F1} by {sender?.ClientId}");
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
