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
    [SerializeField] private GameObject _machinePrefab;
    [SerializeField] private GameObject _storagePrefab;
    [SerializeField] private GameObject _beltPrefab;

    private NetworkFactorySimulation _factorySimulation;

    private FactoryGrid _grid;
    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _structuralService;

    private readonly Dictionary<Vector3Int, GameObject> _foundationObjects = new();
    private readonly List<WallRecord> _wallRecords = new();
    private readonly List<RampRecord> _rampRecords = new();

    // Building tracking for auto-wiring
    private enum BuildingType { Machine, Storage, Belt }

    private struct PlacedBuilding
    {
        public BuildingType Type;
        public GameObject Instance;
        public NetworkMachine NetMachine;
        public NetworkStorage NetStorage;
        public NetworkBeltSegment NetBelt;
        public Vector2Int Cell;
        public int Level;
    }

    private readonly Dictionary<Vector3Int, PlacedBuilding> _placedBuildings = new();

    private static readonly Vector2Int[] CardinalDirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public FactoryGrid Grid => _grid;
    public StructuralPlacementService StructuralService => _structuralService;
    public SnapPointRegistry SnapRegistry => _snapRegistry;
    public GameObject FoundationPrefab => _foundationPrefab;
    public GameObject WallPrefab => _wallPrefab;
    public GameObject RampPrefab => _rampPrefab;

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
        _factorySimulation = GetComponent<NetworkFactorySimulation>();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceFoundation(Vector2Int cell, int level, NetworkConnection sender = null)
    {
        PlaceFoundationInternal(cell, level, sender);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceFoundationRect(Vector2Int min, Vector2Int max, int level, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        int placed = 0;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                if (PlaceFoundationInternal(new Vector2Int(x, z), level, sender))
                    placed++;
            }
        }

        if (placed > 0)
            Debug.Log($"grid: placed {placed} foundations in rect ({min.x},{min.y})-({max.x},{max.y}) level {level} by {sender?.ClientId}");
    }

    private bool PlaceFoundationInternal(Vector2Int cell, int level, NetworkConnection sender)
    {
        if (!IsServerInitialized) return false;

        int fs = FactoryGrid.FoundationSize;
        // Use raw cell as origin -- no 4x4 snap for single placement
        // Zoop pre-snaps cells before calling this method
        var origin = cell;
        var size = new Vector2Int(fs, fs);

        // Check if this 4x4 block is already placed
        var blockKey = new Vector3Int(origin.x, origin.y, level);
        if (_foundationObjects.ContainsKey(blockKey))
            return false;

        // Check all cells in the 4x4 block are available
        if (!_grid.CanPlace(origin, size, level))
        {
            // Log which cell is blocking
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int z = origin.y; z < origin.y + size.y; z++)
                {
                    if (!_grid.IsInBounds(new Vector2Int(x, z)))
                        Debug.Log($"grid: foundation blocked -- cell ({x},{z}) out of bounds");
                    else if (_grid.GetAt(new Vector2Int(x, z), level) != null)
                        Debug.Log($"grid: foundation blocked -- cell ({x},{z}) level {level} occupied by {_grid.GetAt(new Vector2Int(x, z), level).BuildingId}");
                }
            }
            return false;
        }

        // Occupy all cells
        var data = new BuildingData("foundation", origin, size, 0, level);
        data.IsStructural = true;
        _grid.Place(origin, size, level, data);

        // Position prefab at center of the 4x4 block
        // Push up by half cube height so foundation sits ON the level surface
        float halfBlock = fs * FactoryGrid.CellSize * 0.5f;
        Vector3 worldPos = new Vector3(
            origin.x * FactoryGrid.CellSize + halfBlock,
            level * FactoryGrid.LevelHeight + 0.5f,
            origin.y * FactoryGrid.CellSize + halfBlock);
        var go = Instantiate(_foundationPrefab, worldPos, Quaternion.identity);
        var info = go.AddComponent<PlacementInfo>();
        info.Type = PlacementInfo.PlacementType.Foundation;
        info.Cell = origin;
        info.Size = size;
        info.Level = level;
        ServerManager.Spawn(go);

        data.Instance = go;
        _foundationObjects[blockKey] = go;
        return true;
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

        // Remove the full 4x4 block
        var origin = data.Origin;
        var size = data.Size;
        _grid.Remove(origin, size, level);

        var blockKey = new Vector3Int(origin.x, origin.y, level);
        if (_foundationObjects.TryGetValue(blockKey, out var go))
        {
            ServerManager.Despawn(go);
            _foundationObjects.Remove(blockKey);
        }

        Debug.Log($"grid: foundation removed at ({origin.x},{origin.y}) level {level} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceWall(Vector2Int cell, int level, Vector2Int direction, bool onFoundation = false, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        // Check for duplicate wall
        if (HasWallAt(cell, level, direction))
        {
            Debug.Log($"grid: wall already exists at ({cell.x},{cell.y}) level {level} dir ({direction.x},{direction.y})");
            return;
        }

        var wallData = new WallData("wall", cell, level, direction);

        Vector3 worldPos = GetWallWorldPos(cell, level, direction, onFoundation);
        Quaternion rotation = GetWallRotation(direction);
        var go = Instantiate(_wallPrefab, worldPos, rotation);
        var info = go.AddComponent<PlacementInfo>();
        info.Type = PlacementInfo.PlacementType.Wall;
        info.Cell = cell;
        info.Level = level;
        info.EdgeDirection = direction;
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
    public void CmdPlaceRamp(Vector2Int foundationCell, int level, Vector2Int direction, bool onFoundation = false, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        // Check for duplicate ramp
        if (HasRampAt(foundationCell, level, direction))
        {
            Debug.Log($"grid: ramp already exists at ({foundationCell.x},{foundationCell.y}) level {level} dir ({direction.x},{direction.y})");
            return;
        }

        int footprintLength = 3;
        var rampStart = foundationCell + direction;
        var rampData = new RampData(rampStart, level, direction, footprintLength);

        float rampLength = footprintLength * FactoryGrid.CellSize;
        float slopeLength = Mathf.Sqrt(rampLength * rampLength + FactoryGrid.WallHeight * FactoryGrid.WallHeight);
        Quaternion rotation = GetRampRotation(direction, rampLength);
        Vector3 worldPos = GetRampWorldPos(rampData, rotation, slopeLength, onFoundation);
        var go = Instantiate(_rampPrefab, worldPos, rotation);
        var info = go.AddComponent<PlacementInfo>();
        info.Type = PlacementInfo.PlacementType.Ramp;
        info.Cell = foundationCell;
        info.Level = level;
        info.EdgeDirection = direction;
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
                if (record.Instance != null)
                    ServerManager.Despawn(record.Instance);
                _rampRecords.RemoveAt(i);
                Debug.Log($"grid: ramp removed at ({baseCell.x},{baseCell.y}) level {level} dir ({direction.x},{direction.y}) by {sender?.ClientId}");
                return;
            }
        }

        Debug.Log($"grid: no ramp to remove at ({baseCell.x},{baseCell.y}) level {level} dir ({direction.x},{direction.y})");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdDeleteAt(Vector2Int cell, int level, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var key = new Vector3Int(cell.x, cell.y, level);

        // Priority 1: remove placed building (machine/storage/belt)
        if (_placedBuildings.TryGetValue(key, out var building))
        {
            if (building.Instance != null)
                ServerManager.Despawn(building.Instance);
            _placedBuildings.Remove(key);
            Debug.Log($"grid: deleted {building.Type} at ({cell.x},{cell.y}) level {level} by {sender?.ClientId}");
            return;
        }

        // Priority 2: remove foundation (if no walls attached)
        CmdRemoveFoundation(cell, level, sender);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceMachine(Vector2Int cell, int level, int rotation = 0, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var key = new Vector3Int(cell.x, cell.y, level);
        if (_placedBuildings.ContainsKey(key))
        {
            Debug.Log($"grid: machine rejected -- cell occupied at ({cell.x},{cell.y})");
            return;
        }

        Vector3 worldPos = _grid.CellToWorld(cell, level) + new Vector3(0f, 0.1f, 0f);
        var go = Instantiate(_machinePrefab, worldPos, Quaternion.Euler(0f, rotation, 0f));
        var mInfo = go.AddComponent<PlacementInfo>();
        mInfo.Type = PlacementInfo.PlacementType.Machine;
        mInfo.Cell = cell;
        mInfo.Level = level;

        var netMachine = go.GetComponent<NetworkMachine>();
        if (netMachine != null)
        {
            var def = ScriptableObject.CreateInstance<MachineDefinitionSO>();
            def.machineId = "smelter";
            def.machineType = "smelter";
            def.size = Vector2Int.one;
            def.inputBufferSize = 1;
            def.outputBufferSize = 1;
            def.processingSpeed = 1f;

            var machine = new Machine(def);
            netMachine.ServerInit(def, machine);
        }

        ServerManager.Spawn(go);

        if (netMachine != null && _factorySimulation != null)
            _factorySimulation.RegisterMachine(netMachine);

        var building = new PlacedBuilding
        {
            Type = BuildingType.Machine, Instance = go,
            NetMachine = netMachine, Cell = cell, Level = level
        };
        _placedBuildings[key] = building;
        AutoWire(building);

        Debug.Log($"grid: machine placed at ({cell.x},{cell.y}) level {level} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceStorage(Vector2Int cell, int level, int rotation = 0, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        var key = new Vector3Int(cell.x, cell.y, level);
        if (_placedBuildings.ContainsKey(key))
        {
            Debug.Log($"grid: storage rejected -- cell occupied at ({cell.x},{cell.y})");
            return;
        }

        Vector3 worldPos = _grid.CellToWorld(cell, level) + new Vector3(0f, 0.1f, 0f);
        var go = Instantiate(_storagePrefab, worldPos, Quaternion.Euler(0f, rotation, 0f));
        var sInfo = go.AddComponent<PlacementInfo>();
        sInfo.Type = PlacementInfo.PlacementType.Storage;
        sInfo.Cell = cell;
        sInfo.Level = level;

        var netStorage = go.GetComponent<NetworkStorage>();
        if (netStorage != null)
        {
            var container = new StorageContainer(12, 64);
            netStorage.ServerInit(container);
        }

        ServerManager.Spawn(go);

        var building = new PlacedBuilding
        {
            Type = BuildingType.Storage, Instance = go,
            NetStorage = netStorage, Cell = cell, Level = level
        };
        _placedBuildings[key] = building;
        AutoWire(building);

        Debug.Log($"grid: storage placed at ({cell.x},{cell.y}) level {level} by {sender?.ClientId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlaceBelt(Vector2Int fromCell, Vector2Int toCell, int level, NetworkConnection sender = null)
    {
        if (!IsServerInitialized) return;

        Vector3 startPos = _grid.CellToWorld(fromCell, level) + new Vector3(0f, 0.15f, 0f);
        Vector3 endPos = _grid.CellToWorld(toCell, level) + new Vector3(0f, 0.15f, 0f);

        int length = Mathf.Max(1, Mathf.Abs(toCell.x - fromCell.x) + Mathf.Abs(toCell.y - fromCell.y));

        var go = Instantiate(_beltPrefab, (startPos + endPos) * 0.5f, Quaternion.identity);
        var bInfo = go.AddComponent<PlacementInfo>();
        bInfo.Type = PlacementInfo.PlacementType.Belt;
        bInfo.Cell = fromCell;
        bInfo.Level = level;

        var netBelt = go.GetComponent<NetworkBeltSegment>();
        if (netBelt != null)
        {
            var segment = new BeltSegment(length);
            netBelt.ServerInit(segment, startPos, endPos);
        }

        ServerManager.Spawn(go);

        if (netBelt != null && _factorySimulation != null)
            _factorySimulation.RegisterBelt(netBelt);

        // Register belt at both endpoints
        var fromKey = new Vector3Int(fromCell.x, fromCell.y, level);
        var building = new PlacedBuilding
        {
            Type = BuildingType.Belt, Instance = go,
            NetBelt = netBelt, Cell = fromCell, Level = level
        };
        _placedBuildings[fromKey] = building;

        if (fromCell != toCell)
        {
            var toKey = new Vector3Int(toCell.x, toCell.y, level);
            var toBuilding = new PlacedBuilding
            {
                Type = BuildingType.Belt, Instance = go,
                NetBelt = netBelt, Cell = toCell, Level = level
            };
            _placedBuildings[toKey] = toBuilding;
        }

        AutoWire(building);

        Debug.Log($"grid: belt placed from ({fromCell.x},{fromCell.y}) to ({toCell.x},{toCell.y}) level {level} by {sender?.ClientId}");
    }

    public bool HasBuildingAt(Vector2Int cell, int level)
    {
        var key = new Vector3Int(cell.x, cell.y, level);
        return _placedBuildings.ContainsKey(key);
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

    private void AutoWire(PlacedBuilding newBuilding)
    {
        if (_factorySimulation == null) return;

        for (int d = 0; d < CardinalDirs.Length; d++)
        {
            var neighborCell = newBuilding.Cell + CardinalDirs[d];
            var neighborKey = new Vector3Int(neighborCell.x, neighborCell.y, newBuilding.Level);

            if (!_placedBuildings.TryGetValue(neighborKey, out var neighbor))
                continue;

            // Try to create inserter connections between the two buildings
            TryCreateInserter(newBuilding, neighbor);
            TryCreateInserter(neighbor, newBuilding);
        }
    }

    private void TryCreateInserter(PlacedBuilding source, PlacedBuilding dest)
    {
        IItemSource itemSource = GetItemSource(source);
        IItemDestination itemDest = GetItemDestination(dest);

        if (itemSource == null || itemDest == null) return;

        var inserter = new Inserter(itemSource, itemDest, 0.5f);
        _factorySimulation.RegisterInserter(inserter);
        Debug.Log($"grid: auto-wired inserter from {source.Type} at ({source.Cell.x},{source.Cell.y}) to {dest.Type} at ({dest.Cell.x},{dest.Cell.y})");
    }

    private IItemSource GetItemSource(PlacedBuilding building)
    {
        switch (building.Type)
        {
            case BuildingType.Belt when building.NetBelt?.Segment != null:
                return new BeltOutputAdapter(building.NetBelt.Segment);
            case BuildingType.Machine when building.NetMachine?.Machine != null:
                return new MachineOutputAdapter(building.NetMachine.Machine, 0);
            case BuildingType.Storage when building.NetStorage?.Container != null:
                return building.NetStorage.Container;
            default:
                return null;
        }
    }

    private IItemDestination GetItemDestination(PlacedBuilding building)
    {
        switch (building.Type)
        {
            case BuildingType.Belt when building.NetBelt?.Segment != null:
                return new BeltInputAdapter(building.NetBelt.Segment);
            case BuildingType.Machine when building.NetMachine?.Machine != null:
                return new MachineInputAdapter(building.NetMachine.Machine, 0);
            case BuildingType.Storage when building.NetStorage?.Container != null:
                return building.NetStorage.Container;
            default:
                return null;
        }
    }

    private Vector3 GetWallWorldPos(Vector2Int cell, int level, Vector2Int direction, bool onFoundation)
    {
        float cs = FactoryGrid.CellSize;
        float wallHeight = FactoryGrid.WallHeight;
        float halfWidth = FactoryGrid.WallWidth * cs * 0.5f;
        float baseY = onFoundation
            ? level * FactoryGrid.LevelHeight + 1f
            : level * FactoryGrid.LevelHeight;

        if (onFoundation)
        {
            int fs = FactoryGrid.FoundationSize;
            float blockSize = fs * cs;
            float halfBlock = blockSize * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock,
                baseY,
                cell.y * cs + halfBlock);
            return blockCenter + new Vector3(
                direction.x * halfBlock,
                wallHeight * 0.5f,
                direction.y * halfBlock);
        }

        // On terrain: wall centered on the crosshair cell
        float cellCenter = 0.5f * cs;
        return new Vector3(
            cell.x * cs + cellCenter,
            baseY + wallHeight * 0.5f,
            cell.y * cs + cellCenter);
    }

    private Quaternion GetWallRotation(Vector2Int direction)
    {
        if (direction == Vector2Int.up || direction == Vector2Int.down)
            return Quaternion.identity;
        return Quaternion.Euler(0f, 90f, 0f);
    }

    private Vector3 GetRampWorldPos(RampData ramp, Quaternion rotation, float slopeLength, bool onFoundation)
    {
        Vector2Int foundationCell = ramp.BaseCell - ramp.Direction;
        float cs = FactoryGrid.CellSize;

        float baseY = onFoundation
            ? ramp.BaseLevel * FactoryGrid.LevelHeight + 1f
            : ramp.BaseLevel * FactoryGrid.LevelHeight;

        Vector3 baseEdge;
        if (onFoundation)
        {
            int fs = FactoryGrid.FoundationSize;
            float blockSize = fs * cs;
            float halfBlock = blockSize * 0.5f;
            Vector3 blockCenter = new Vector3(
                foundationCell.x * cs + halfBlock, baseY, foundationCell.y * cs + halfBlock);
            baseEdge = blockCenter + new Vector3(
                ramp.Direction.x * halfBlock, 0f, ramp.Direction.y * halfBlock);
        }
        else
        {
            float cellCenter = 0.5f * cs;
            baseEdge = new Vector3(foundationCell.x * cs + cellCenter, baseY, foundationCell.y * cs + cellCenter);
        }

        Vector3 localForward = rotation * Vector3.forward;
        return baseEdge + localForward * (slopeLength * 0.5f);
    }

    private Quaternion GetRampRotation(Vector2Int direction, float rampLength)
    {
        float pitch = Mathf.Atan2(FactoryGrid.WallHeight, rampLength) * Mathf.Rad2Deg;
        float yAngle = 0f;
        if (direction == Vector2Int.right) yAngle = 90f;
        else if (direction == Vector2Int.down) yAngle = 180f;
        else if (direction == Vector2Int.left) yAngle = 270f;
        return Quaternion.Euler(-pitch, yAngle, 0f);
    }
}
