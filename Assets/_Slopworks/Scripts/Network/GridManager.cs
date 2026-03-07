using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class GridManager : NetworkBehaviour
{
    public static GridManager Instance { get; private set; }

    private NetworkFactorySimulation _factorySimulation;

    private FactoryGrid _grid;
    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _structuralService;

    private Dictionary<BuildingCategory, GameObject[]> _prefabArrays = new();

    private static readonly Vector2Int[] CardinalDirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public FactoryGrid Grid => _grid;
    public StructuralPlacementService StructuralService => _structuralService;
    public SnapPointRegistry SnapRegistry => _snapRegistry;

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
        _snapRegistry = new SnapPointRegistry();
        _structuralService = new StructuralPlacementService(_grid, _snapRegistry);
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
    /// Returns the Y extent (half-height) of a prefab's renderer bounds.
    /// Used to place objects so their bottom sits on the surface.
    /// </summary>
    public static float GetPrefabHalfHeight(GameObject prefab)
    {
        if (prefab == null) return 0.5f;
        var renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer == null) return 0.5f;
        return renderer.bounds.extents.y;
    }

    /// <summary>
    /// World position for a 1x1 building (machine, storage) at the given cell and surface Y.
    /// </summary>
    public Vector3 GetPlacementPos(Vector2Int cell, float surfaceY, GameObject prefab)
    {
        float halfHeight = GetPrefabHalfHeight(prefab);
        float x = cell.x * FactoryGrid.CellSize + FactoryGrid.CellSize * 0.5f;
        float z = cell.y * FactoryGrid.CellSize + FactoryGrid.CellSize * 0.5f;
        return new Vector3(x, surfaceY + halfHeight, z);
    }

    /// <summary>
    /// World position for a foundation block at the given origin cell and surface Y.
    /// </summary>
    public Vector3 GetFoundationPlacementPos(Vector2Int origin, float surfaceY, GameObject prefab)
    {
        int fs = FactoryGrid.FoundationSize;
        float halfBlock = fs * FactoryGrid.CellSize * 0.5f;
        float halfHeight = GetPrefabHalfHeight(prefab);
        return new Vector3(
            origin.x * FactoryGrid.CellSize + halfBlock,
            surfaceY + halfHeight,
            origin.y * FactoryGrid.CellSize + halfBlock);
    }

    /// <summary>
    /// World position and rotation for directional buildings (walls, ramps).
    /// </summary>
    public void GetDirectionalPlacement(Vector2Int cell, float surfaceY, Vector2Int direction,
        GameObject prefab, BuildingCategory category, out Vector3 position, out Quaternion rotation)
    {
        float cs = FactoryGrid.CellSize;

        if (category == BuildingCategory.Wall)
        {
            float wallHeight = FactoryGrid.WallHeight;
            int fs = FactoryGrid.FoundationSize;
            float halfBlock = fs * cs * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock, surfaceY, cell.y * cs + halfBlock);
            position = blockCenter + new Vector3(
                direction.x * halfBlock, wallHeight * 0.5f, direction.y * halfBlock);

            rotation = (direction == Vector2Int.up || direction == Vector2Int.down)
                ? Quaternion.identity
                : Quaternion.Euler(0f, 90f, 0f);
            return;
        }

        if (category == BuildingCategory.Ramp)
        {
            float rampLength = 3f * cs;
            float rampRise = FactoryGrid.WallHeight;
            float slopeLength = Mathf.Sqrt(rampLength * rampLength + rampRise * rampRise);

            float pitch = Mathf.Atan2(rampRise, rampLength) * Mathf.Rad2Deg;
            float yAngle = 0f;
            if (direction == Vector2Int.right) yAngle = 90f;
            else if (direction == Vector2Int.down) yAngle = 180f;
            else if (direction == Vector2Int.left) yAngle = 270f;
            rotation = Quaternion.Euler(-pitch, yAngle, 0f);

            int fs = FactoryGrid.FoundationSize;
            float halfBlock = fs * cs * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock, surfaceY, cell.y * cs + halfBlock);
            Vector3 baseEdge = blockCenter + new Vector3(
                direction.x * halfBlock, 0f, direction.y * halfBlock);

            Vector3 localForward = rotation * Vector3.forward;
            position = baseEdge + localForward * (slopeLength * 0.5f);
            return;
        }

        // Fallback for other categories
        position = GetPlacementPos(cell, surfaceY, prefab);
        rotation = Quaternion.identity;
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
