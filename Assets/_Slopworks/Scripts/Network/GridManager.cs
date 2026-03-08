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
    /// Used to place objects so their bottom sits on the surface.
    /// </summary>
    public static float GetPrefabHalfHeight(GameObject prefab)
    {
        return GetPrefabExtents(prefab).y;
    }

    /// <summary>
    /// Unified grid placement: snaps center-bottom of prefab to nearest grid-aligned
    /// position based on its footprint. Thin objects (depth less than 1m) snap back face
    /// flush to nearest grid line instead of centering.
    /// </summary>
    /// <param name="hitPoint">World-space point where the crosshair hit terrain</param>
    /// <param name="prefab">The building prefab to place</param>
    /// <param name="rotationDeg">Rotation in degrees (0, 90, 180, 270)</param>
    /// <returns>World position (center of object) and rotation quaternion</returns>
    public static (Vector3 position, Quaternion rotation) GetGridPlacementPosition(
        Vector3 hitPoint, GameObject prefab, int rotationDeg)
    {
        Vector3 extents = GetPrefabExtents(prefab);

        // Determine footprint after rotation: at 90/270, X and Z swap
        float footprintX, footprintZ;
        bool swapped = (rotationDeg == 90 || rotationDeg == 270);
        if (swapped)
        {
            footprintX = extents.z * 2f;
            footprintZ = extents.x * 2f;
        }
        else
        {
            footprintX = extents.x * 2f;
            footprintZ = extents.z * 2f;
        }

        float posX = SnapAxis(hitPoint.x, footprintX, swapped ? extents.z : extents.x);
        float posZ = SnapAxis(hitPoint.z, footprintZ, swapped ? extents.x : extents.z);
        float posY = hitPoint.y + extents.y;

        return (new Vector3(posX, posY, posZ), Quaternion.Euler(0f, rotationDeg, 0f));
    }

    /// <summary>
    /// Snap placement: position a new building flush against an existing building's snap point.
    /// For horizontal normals: offset along normal by half the incoming depth, Y = snapY + halfHeight.
    /// For vertical normals (top): place center-bottom on snap point.
    /// </summary>
    public static (Vector3 position, Quaternion rotation) GetSnapPlacementPosition(
        BuildingSnapPoint snapPoint, GameObject prefab, int rotationDeg)
    {
        Vector3 extents = GetPrefabExtents(prefab);

        Vector3 snapPos = snapPoint.transform.position;
        Vector3 normal = snapPoint.Normal;
        float halfHeight = extents.y;

        // Vertical normal: stack on top
        if (Mathf.Abs(normal.y) > 0.9f)
        {
            float yOffset = normal.y > 0 ? halfHeight : -halfHeight;
            Vector3 position = new Vector3(snapPos.x, snapPos.y + yOffset, snapPos.z);
            Quaternion rotation = Quaternion.Euler(0f, rotationDeg, 0f);
            return (position, rotation);
        }

        // Horizontal normal: auto-align yaw from normal direction
        float autoYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;

        // Auto-yaw rotates the object so its local Z (forward) faces the normal.
        // Therefore depth along the normal is always extents.z (the local forward extent).
        float halfDepth = extents.z;

        Vector3 horizontalNormal = new Vector3(normal.x, 0f, normal.z).normalized;
        Vector3 pos = new Vector3(
            snapPos.x + horizontalNormal.x * halfDepth,
            snapPos.y + halfHeight,
            snapPos.z + horizontalNormal.z * halfDepth);

        return (pos, Quaternion.Euler(0f, autoYaw, 0f));
    }

    /// <summary>
    /// Snaps a single axis value to grid. Thin axes (less than 1m) snap back face flush
    /// to nearest grid line. Wide axes snap center to grid aligned to footprint size.
    /// </summary>
    private static float SnapAxis(float hitValue, float footprint, float halfExtent)
    {
        if (footprint < 1f)
        {
            // Thin: snap back face flush to nearest grid line
            float gridLine = Mathf.Round(hitValue);
            return gridLine + halfExtent;
        }

        // Wide: snap center to the grid-aligned block the point falls in
        float gridStep = footprint;
        float origin = Mathf.Floor(hitValue / gridStep) * gridStep;
        return origin + gridStep * 0.5f;
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
        BuildingSnapPoint.GenerateFromBounds(go);

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
        BuildingSnapPoint.GenerateFromBounds(go);

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
