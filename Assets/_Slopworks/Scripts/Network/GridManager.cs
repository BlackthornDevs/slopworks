using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class GridManager : NetworkBehaviour
{
    public static GridManager Instance { get; private set; }

    [SerializeField] private GameObject _foundationPrefab;
    [SerializeField] private GameObject _wallPrefab;
    [SerializeField] private GameObject _rampPrefab;

    private FactoryGrid _grid;
    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _structuralService;

    private readonly Dictionary<Vector3Int, GameObject> _foundationObjects = new();
    private readonly List<WallRecord> _wallRecords = new();
    private readonly List<RampRecord> _rampRecords = new();

    public FactoryGrid Grid => _grid;
    public StructuralPlacementService StructuralService => _structuralService;
    public SnapPointRegistry SnapRegistry => _snapRegistry;

    private struct WallRecord
    {
        public WallData Data;
        public GameObject Instance;
    }

    private struct RampRecord
    {
        public RampData Data;
        public GameObject Instance;
    }

    private void Awake()
    {
        Instance = this;
        _grid = new FactoryGrid();
        _snapRegistry = new SnapPointRegistry();
        _structuralService = new StructuralPlacementService(_grid, _snapRegistry);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceFoundation(Vector2Int cell, int level, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var def = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        def.foundationId = "foundation";
        def.size = Vector2Int.one;
        def.generatesSnapPoints = true;

        var data = _structuralService.PlaceFoundation(def, cell, level);
        if (data == null)
        {
            Debug.Log($"grid: foundation rejected at ({cell.x},{cell.y}) level {level}");
            return;
        }

        Vector3 worldPos = _grid.CellToWorld(cell, level);
        var go = Instantiate(_foundationPrefab, worldPos, Quaternion.identity);
        ServerManager.Spawn(go);

        data.Instance = go;
        _foundationObjects[new Vector3Int(cell.x, cell.y, level)] = go;

        Debug.Log($"grid: foundation placed at ({cell.x},{cell.y}) level {level} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRemoveFoundation(Vector2Int cell, int level, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var data = _grid.GetAt(cell, level);
        if (data == null || !data.IsStructural)
        {
            Debug.Log($"grid: nothing to remove at ({cell.x},{cell.y}) level {level}");
            return;
        }

        bool removed = _structuralService.RemoveFoundation(data);
        if (!removed)
        {
            Debug.Log($"grid: foundation has walls attached at ({cell.x},{cell.y}) level {level}");
            return;
        }

        var key = new Vector3Int(cell.x, cell.y, level);
        if (_foundationObjects.TryGetValue(key, out var go))
        {
            ServerManager.Despawn(go);
            _foundationObjects.Remove(key);
        }

        Debug.Log($"grid: foundation removed at ({cell.x},{cell.y}) level {level} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceWall(Vector2Int cell, int level, Vector2Int direction, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var def = ScriptableObject.CreateInstance<WallDefinitionSO>();
        def.wallId = "wall";

        var wallData = _structuralService.PlaceWall(def, cell, level, direction);
        if (wallData == null)
        {
            Debug.Log($"grid: wall rejected at ({cell.x},{cell.y}) level {level} dir ({direction.x},{direction.y})");
            return;
        }

        Vector3 worldPos = GetWallWorldPos(cell, level, direction);
        Quaternion rotation = GetWallRotation(direction);
        var go = Instantiate(_wallPrefab, worldPos, rotation);
        ServerManager.Spawn(go);

        wallData.Instance = go;
        _wallRecords.Add(new WallRecord { Data = wallData, Instance = go });

        Debug.Log($"grid: wall placed at ({cell.x},{cell.y}) level {level} edge ({direction.x},{direction.y}) by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRemoveWall(Vector2Int cell, int level, Vector2Int direction, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        for (int i = _wallRecords.Count - 1; i >= 0; i--)
        {
            var record = _wallRecords[i];
            if (record.Data.Cell == cell && record.Data.Level == level && record.Data.EdgeDirection == direction)
            {
                _structuralService.RemoveWall(record.Data);
                if (record.Instance != null)
                    ServerManager.Despawn(record.Instance);
                _wallRecords.RemoveAt(i);
                Debug.Log($"grid: wall removed at ({cell.x},{cell.y}) level {level} edge ({direction.x},{direction.y}) by {sender?.ClientId}");
                return;
            }
        }

        Debug.Log($"grid: no wall to remove at ({cell.x},{cell.y}) level {level} edge ({direction.x},{direction.y})");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceRamp(Vector2Int foundationCell, int level, Vector2Int direction, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var def = ScriptableObject.CreateInstance<RampDefinitionSO>();
        def.rampId = "ramp";
        def.footprintLength = 3;

        var rampData = _structuralService.PlaceRamp(def, foundationCell, level, direction);
        if (rampData == null)
        {
            Debug.Log($"grid: ramp rejected at ({foundationCell.x},{foundationCell.y}) level {level} dir ({direction.x},{direction.y})");
            return;
        }

        float rampLength = def.footprintLength * FactoryGrid.CellSize;
        float slopeLength = Mathf.Sqrt(rampLength * rampLength + FactoryGrid.LevelHeight * FactoryGrid.LevelHeight);
        Quaternion rotation = GetRampRotation(direction, rampLength);
        Vector3 worldPos = GetRampWorldPos(rampData, rotation, slopeLength);
        var go = Instantiate(_rampPrefab, worldPos, rotation);
        go.transform.localScale = new Vector3(FactoryGrid.CellSize, 0.1f, slopeLength);
        ServerManager.Spawn(go);

        rampData.Instance = go;
        _rampRecords.Add(new RampRecord { Data = rampData, Instance = go });

        Debug.Log($"grid: ramp placed at ({foundationCell.x},{foundationCell.y}) level {level} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdRemoveRamp(Vector2Int baseCell, int level, Vector2Int direction, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        for (int i = _rampRecords.Count - 1; i >= 0; i--)
        {
            var record = _rampRecords[i];
            if (record.Data.BaseCell == baseCell && record.Data.BaseLevel == level && record.Data.Direction == direction)
            {
                _structuralService.RemoveRamp(record.Data);
                if (record.Instance != null)
                    ServerManager.Despawn(record.Instance);
                _rampRecords.RemoveAt(i);
                Debug.Log($"grid: ramp removed at ({baseCell.x},{baseCell.y}) level {level} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
                return;
            }
        }

        Debug.Log($"grid: no ramp to remove at ({baseCell.x},{baseCell.y}) level {level} dir ({direction.x},{direction.y})");
    }

    public bool HasWallAt(Vector2Int cell, int level, Vector2Int direction)
    {
        foreach (var record in _wallRecords)
        {
            if (record.Data.Cell == cell && record.Data.Level == level && record.Data.EdgeDirection == direction)
                return true;
        }
        return false;
    }

    public bool HasRampAt(Vector2Int foundationCell, int level, Vector2Int direction)
    {
        foreach (var record in _rampRecords)
        {
            if (record.Data.BaseCell == foundationCell + direction &&
                record.Data.BaseLevel == level &&
                record.Data.Direction == direction)
                return true;
        }
        return false;
    }

    private Vector3 GetWallWorldPos(Vector2Int cell, int level, Vector2Int direction)
    {
        Vector3 cellCenter = _grid.CellToWorld(cell, level);
        float halfCell = FactoryGrid.CellSize * 0.5f;
        float wallHeight = FactoryGrid.LevelHeight * 0.5f;

        return cellCenter
            + new Vector3(direction.x * halfCell, wallHeight, direction.y * halfCell);
    }

    private Quaternion GetWallRotation(Vector2Int direction)
    {
        if (direction == Vector2Int.up || direction == Vector2Int.down)
            return Quaternion.identity;
        return Quaternion.Euler(0f, 90f, 0f);
    }

    private Vector3 GetRampWorldPos(RampData ramp, Quaternion rotation, float slopeLength)
    {
        // Base edge: the foundation cell adjacent to ramp start
        Vector2Int foundationCell = ramp.BaseCell - ramp.Direction;
        Vector3 cellCenter = _grid.CellToWorld(foundationCell, ramp.BaseLevel);
        float halfCell = FactoryGrid.CellSize * 0.5f;
        Vector3 baseEdge = cellCenter + new Vector3(
            ramp.Direction.x * halfCell, 0f, ramp.Direction.y * halfCell);

        Vector3 localForward = rotation * Vector3.forward;
        return baseEdge + localForward * (slopeLength * 0.5f);
    }

    private Quaternion GetRampRotation(Vector2Int direction, float rampLength)
    {
        float pitch = Mathf.Atan2(FactoryGrid.LevelHeight, rampLength) * Mathf.Rad2Deg;
        float yAngle = 0f;
        if (direction == Vector2Int.right) yAngle = 90f;
        else if (direction == Vector2Int.down) yAngle = 180f;
        else if (direction == Vector2Int.left) yAngle = 270f;
        return Quaternion.Euler(-pitch, yAngle, 0f);
    }
}
