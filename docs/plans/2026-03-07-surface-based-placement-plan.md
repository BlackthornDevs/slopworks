# Surface-Based Placement Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace integer level system with surface-based Y placement so variable-height prefabs stack correctly.

**Architecture:** Raycast hit point determines placement Y. FactoryGrid uses Y-bucket keys (0.5m resolution) for occupancy. Seven CmdPlace RPCs consolidate to three. Components self-initialize.

**Tech Stack:** Unity, FishNet (NetworkBehaviour, ServerRpc, SyncVar), C#

**Design doc:** `docs/plans/2026-03-07-surface-based-placement-design.md`

---

### Task 1: Add BuildingCategory enum and update PlacementInfo

**Files:**
- Create: `Assets/_Slopworks/Scripts/Network/BuildingCategory.cs`
- Modify: `Assets/_Slopworks/Scripts/Network/PlacementInfo.cs`

**Step 1: Create BuildingCategory enum**

```csharp
// BuildingCategory.cs
public enum BuildingCategory
{
    Foundation,
    Wall,
    Ramp,
    Machine,
    Storage,
    Belt
}
```

**Step 2: Update PlacementInfo to use float surfaceY and objectHeight**

Replace the full file:

```csharp
using UnityEngine;

public class PlacementInfo : MonoBehaviour
{
    public BuildingCategory Category;
    public Vector2Int Cell;
    public Vector2Int Size;
    public float SurfaceY;
    public float ObjectHeight;
    public Vector2Int EdgeDirection;
}
```

`PlacementType` enum is removed -- replaced by `BuildingCategory`.

**Step 3: Commit**

```
Add BuildingCategory enum, update PlacementInfo to float surfaceY
```

---

### Task 2: Update FactoryGrid to use Y-bucket keys

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Automation/FactoryGrid.cs`

**Step 1: Replace level constants and methods**

Remove `LevelHeight` and `MaxLevels`. Add `BucketSize` constant and `YBucket` helper.

```csharp
public const float BucketSize = 0.5f;

public static int YBucket(float surfaceY)
{
    return Mathf.RoundToInt(surfaceY / BucketSize);
}
```

**Step 2: Update CellToWorld**

```csharp
public Vector3 CellToWorld(Vector2Int cell)
{
    return CellToWorld(cell, 0f);
}

public Vector3 CellToWorld(Vector2Int cell, float surfaceY)
{
    float x = (cell.x + 0.5f) * CellSize;
    float z = (cell.y + 0.5f) * CellSize;
    return new Vector3(x, surfaceY, z);
}
```

**Step 3: Update CanPlace, Place, Remove, GetAt, IsInBounds**

All methods that take `int level` change to `float surfaceY`. Internally they call `YBucket(surfaceY)` for the dictionary key's Z component.

```csharp
public bool CanPlace(Vector2Int origin, Vector2Int size, float surfaceY)
{
    int bucket = YBucket(surfaceY);
    for (int x = origin.x; x < origin.x + size.x; x++)
    {
        for (int z = origin.y; z < origin.y + size.y; z++)
        {
            if (!InBounds(x, z)) return false;
            if (_cells.ContainsKey(new Vector3Int(x, z, bucket))) return false;
        }
    }
    return true;
}

public void Place(Vector2Int origin, Vector2Int size, float surfaceY, BuildingData data)
{
    int bucket = YBucket(surfaceY);
    for (int x = origin.x; x < origin.x + size.x; x++)
        for (int z = origin.y; z < origin.y + size.y; z++)
            _cells[new Vector3Int(x, z, bucket)] = data;
}

public void Remove(Vector2Int origin, Vector2Int size, float surfaceY)
{
    int bucket = YBucket(surfaceY);
    for (int x = origin.x; x < origin.x + size.x; x++)
        for (int z = origin.y; z < origin.y + size.y; z++)
            _cells.Remove(new Vector3Int(x, z, bucket));
}

public BuildingData GetAt(Vector2Int cell, float surfaceY)
{
    _cells.TryGetValue(new Vector3Int(cell.x, cell.y, YBucket(surfaceY)), out var data);
    return data;
}
```

Keep backward-compat level-0 overloads (`CanPlace(origin, size)` calls `CanPlace(origin, size, 0f)`) for any callers outside the multiplayer path.

**Step 4: Commit**

```
Update FactoryGrid to Y-bucket keys, remove integer level system
```

---

### Task 3: Update WallData and RampData to float surfaceY

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/WallData.cs`
- Modify: `Assets/_Slopworks/Scripts/Building/RampData.cs`
- Modify: `Assets/_Slopworks/Scripts/Building/SnapPoint.cs`

**Step 1: WallData -- replace int Level with float SurfaceY**

```csharp
public float SurfaceY { get; }
// Remove: public int Level { get; }

public WallData(string wallId, Vector2Int cell, float surfaceY, Vector2Int edgeDirection)
{
    WallId = wallId;
    Cell = cell;
    SurfaceY = surfaceY;
    EdgeDirection = edgeDirection;
}
```

Keep the SnapPoint constructor overload but update it to read `attachPoint.SurfaceY` instead of `attachPoint.Level`.

**Step 2: RampData -- replace int BaseLevel with float BaseSurfaceY**

```csharp
public float BaseSurfaceY { get; }
// Remove: public int BaseLevel { get; }

public RampData(Vector2Int baseCell, float baseSurfaceY, Vector2Int direction, int footprintLength)
{
    BaseCell = baseCell;
    BaseSurfaceY = baseSurfaceY;
    Direction = direction;
    FootprintLength = footprintLength;
}
```

**Step 3: SnapPoint -- replace int Level with float SurfaceY**

```csharp
public float SurfaceY { get; }
// Remove: public int Level { get; }

public SnapPoint(Vector2Int cell, float surfaceY, Vector2Int edgeDirection, SnapPointType type, BuildingData owner)
{
    Cell = cell;
    SurfaceY = surfaceY;
    EdgeDirection = edgeDirection;
    Type = type;
    Owner = owner;
}
```

**Step 4: Commit**

```
Update WallData, RampData, SnapPoint to float surfaceY
```

---

### Task 4: NetworkMachine and NetworkStorage self-initialization

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/NetworkMachine.cs:26-31`
- No changes needed for NetworkStorage (already self-initializes)

**Step 1: Update NetworkMachine.OnStartServer to self-initialize**

```csharp
public override void OnStartServer()
{
    base.OnStartServer();
    if (_machine == null)
    {
        if (_definition == null)
        {
            _definition = ScriptableObject.CreateInstance<MachineDefinitionSO>();
            _definition.machineId = "smelter";
            _definition.machineType = "smelter";
            _definition.size = Vector2Int.one;
            _definition.inputBufferSize = 1;
            _definition.outputBufferSize = 1;
            _definition.processingSpeed = 1f;
        }
        _machine = new Machine(_definition);
    }
}
```

**Step 2: Commit**

```
NetworkMachine self-initializes in OnStartServer
```

---

### Task 5: Rewrite GridManager -- unified data structures

This is the largest task. Rewrite GridManager's data structures: prefab dictionary, unified PlacedRecord, universal placement methods.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`

**Step 1: Replace prefab fields and record collections**

Remove:
- Six `_*PrefabFallback` serialized fields
- Six `_*Prefabs` arrays and their public getters
- `_foundationObjects`, `_wallRecords`, `_placedBuildings` dictionaries
- `WallRecord`, `PlacedBuilding` structs
- `BuildingType` enum

Add:

```csharp
[SerializeField] private string[] _categoryFolders;

private Dictionary<BuildingCategory, GameObject[]> _prefabArrays = new();

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

private struct PlacedRecord
{
    public BuildingCategory Category;
    public GameObject Instance;
    public float SurfaceY;
    public Vector2Int Direction; // zero for non-directional
    public NetworkMachine NetMachine;
    public NetworkStorage NetStorage;
    public NetworkBeltSegment NetBelt;
}

private readonly Dictionary<long, PlacedRecord> _placedRecords = new();

// Encode cell + yBucket + direction into a unique long key
private static long RecordKey(Vector2Int cell, float surfaceY, Vector2Int direction = default)
{
    int bucket = FactoryGrid.YBucket(surfaceY);
    // Pack: cellX (16 bits) | cellZ (16 bits) | bucket (16 bits) | dirHash (16 bits)
    int dirHash = (direction.x + 2) * 5 + (direction.y + 2);
    return ((long)(cell.x & 0xFFFF) << 48) | ((long)(cell.y & 0xFFFF) << 32) |
           ((long)(bucket & 0xFFFF) << 16) | (long)(dirHash & 0xFFFF);
}
```

**Step 2: Update LoadPrefabVariants**

```csharp
private void LoadPrefabVariants()
{
    _prefabArrays = new Dictionary<BuildingCategory, GameObject[]>();
    foreach (BuildingCategory cat in System.Enum.GetValues(typeof(BuildingCategory)))
    {
        string folder = $"Prefabs/Buildings/{cat}";
        // Pluralize folder name to match existing convention
        if (cat == BuildingCategory.Foundation) folder = "Prefabs/Buildings/Foundations";
        else if (cat == BuildingCategory.Wall) folder = "Prefabs/Buildings/Walls";
        else if (cat == BuildingCategory.Ramp) folder = "Prefabs/Buildings/Ramps";
        else if (cat == BuildingCategory.Machine) folder = "Prefabs/Buildings/Machines";
        else if (cat == BuildingCategory.Storage) folder = "Prefabs/Buildings/Storage";
        else if (cat == BuildingCategory.Belt) folder = "Prefabs/Buildings/Belts";

        var loaded = Resources.LoadAll<GameObject>(folder);
        _prefabArrays[cat] = loaded.Length > 0 ? loaded : System.Array.Empty<GameObject>();
    }

    foreach (var kvp in _prefabArrays)
        Debug.Log($"grid: {kvp.Key} variants: {kvp.Value.Length}");
}
```

Remove `_*PrefabFallback` serialized fields -- no longer needed since Resources is the only source.

**Step 3: Replace five placement methods with two**

```csharp
public Vector3 GetPlacementPos(Vector2Int cell, float surfaceY, GameObject prefab)
{
    float halfHeight = GetPrefabHalfHeight(prefab);
    float x = cell.x * FactoryGrid.CellSize + FactoryGrid.CellSize * 0.5f;
    float z = cell.y * FactoryGrid.CellSize + FactoryGrid.CellSize * 0.5f;
    return new Vector3(x, surfaceY + halfHeight, z);
}

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

    // Fallback
    position = GetPlacementPos(cell, surfaceY, prefab);
    rotation = Quaternion.identity;
}
```

Note: `GetFoundationPlacementPos` is separate because foundations use the 4x4 block center, not the 1x1 cell center. This is a data difference, not special-casing.

**Step 4: Commit**

```
Rewrite GridManager data structures for surface-based placement
```

---

### Task 6: Rewrite GridManager -- unified CmdPlace RPCs

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`

**Step 1: Remove all old CmdPlace/CmdRemove methods**

Remove: `CmdPlaceFoundation`, `CmdPlaceFoundationRect`, `PlaceFoundationInternal`, `CmdPlaceWall`, `CmdPlaceRamp`, `CmdPlaceMachine`, `CmdPlaceStorage`, `CmdPlaceBelt`, `CmdRemoveFoundation`, `CmdRemoveWall`, `CmdRemoveRamp`, `CmdDeleteAt`.

Remove: `HasWallAt`, `HasRampAt`, `HasBuildingAt`.

**Step 2: Add unified placement RPCs**

```csharp
[ServerRpc(RequireOwnership = false)]
public void CmdPlace(Vector2Int cell, float surfaceY, int rotation, int variant,
    BuildingCategory category, NetworkConnection sender = null)
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

    Vector3 worldPos;
    if (category == BuildingCategory.Foundation)
    {
        int fs = FactoryGrid.FoundationSize;
        var size = new Vector2Int(fs, fs);
        if (!_grid.CanPlace(cell, size, surfaceY)) return;

        _grid.Place(cell, size, surfaceY, new BuildingData("foundation", cell, size, 0, 0));
        worldPos = GetFoundationPlacementPos(cell, surfaceY, prefab);
    }
    else
    {
        worldPos = GetPlacementPos(cell, surfaceY, prefab);
    }

    var go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, rotation, 0f));
    var info = go.AddComponent<PlacementInfo>();
    info.Category = category;
    info.Cell = cell;
    info.SurfaceY = surfaceY;
    info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
    ServerManager.Spawn(go);

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
    int variant, BuildingCategory category, NetworkConnection sender = null)
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

    GetDirectionalPlacement(cell, surfaceY, direction, prefab, category,
        out var worldPos, out var rotation);
    var go = Instantiate(prefab, worldPos, rotation);
    var info = go.AddComponent<PlacementInfo>();
    info.Category = category;
    info.Cell = cell;
    info.SurfaceY = surfaceY;
    info.ObjectHeight = GetPrefabHalfHeight(prefab) * 2f;
    info.EdgeDirection = direction;
    ServerManager.Spawn(go);

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

    Vector3 startPos = GetPlacementPos(fromCell, surfaceY, prefab);
    Vector3 endPos = GetPlacementPos(toCell, surfaceY, prefab);
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
```

**Step 3: Add unified delete RPCs**

```csharp
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
```

**Step 4: Update AutoWire to use new record structure**

```csharp
private void AutoWire(PlacedRecord newRecord, Vector2Int cell)
{
    if (_factorySimulation == null) return;

    for (int d = 0; d < CardinalDirs.Length; d++)
    {
        var neighborCell = cell + CardinalDirs[d];
        var neighborKey = RecordKey(neighborCell, newRecord.SurfaceY);

        if (!_placedRecords.TryGetValue(neighborKey, out var neighbor))
            continue;

        TryCreateInserter(newRecord, neighbor);
        TryCreateInserter(neighbor, newRecord);
    }
}
```

`TryCreateInserter`, `GetItemSource`, `GetItemDestination` stay the same but reference `PlacedRecord` fields instead of the old `PlacedBuilding` fields.

**Step 5: Update HasBuildingAt**

```csharp
public bool HasBuildingAt(Vector2Int cell, float surfaceY)
{
    return _placedRecords.ContainsKey(RecordKey(cell, surfaceY));
}

public bool HasDirectionalAt(Vector2Int cell, float surfaceY, Vector2Int direction)
{
    return _placedRecords.ContainsKey(RecordKey(cell, surfaceY, direction));
}
```

**Step 6: Commit**

```
Rewrite GridManager RPCs: unified CmdPlace, CmdPlaceDirectional, CmdPlaceBelt
```

---

### Task 7: Update NetworkBuildController -- surface Y and nudge

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Step 1: Replace state variables**

Remove `_lastLevel`, `_levelOverrideFrames`, `_lastHitFoundation`. Add:

```csharp
private float _surfaceY;
private float _nudgeOffset;
private float EffectiveY => _surfaceY + _nudgeOffset;
```

**Step 2: Replace HandleAutoLevel + HandleLevelChange with UpdateSurfaceY and HandleNudge**

```csharp
private void UpdateSurfaceY()
{
    if (_zoopStartSet || _beltStartSet) return;

    var ray = new Ray(_camera.transform.position, _camera.transform.forward);
    if (Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
    {
        var info = hit.collider.GetComponentInParent<PlacementInfo>();
        if (info != null)
            _surfaceY = info.SurfaceY + info.ObjectHeight;
        else
            _surfaceY = hit.point.y;
    }
}

private void HandleNudge(Keyboard kb)
{
    bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
    float step = shift ? 0.5f : 1f;

    if (kb.pageUpKey.wasPressedThisFrame)
    {
        _nudgeOffset += step;
        Debug.Log($"build: nudge +{_nudgeOffset:F1}m");
    }
    if (kb.pageDownKey.wasPressedThisFrame)
    {
        _nudgeOffset -= step;
        Debug.Log($"build: nudge {_nudgeOffset:F1}m");
    }
}
```

**Step 3: Reset nudge on tool/mode switch**

In `SwitchTool()`, add `_nudgeOffset = 0f;`

When toggling build mode (`_buildMode = !_buildMode`), add `_nudgeOffset = 0f;`

When toggling delete mode (`_deleteMode = !_deleteMode`), add `_nudgeOffset = 0f;`

**Step 4: Update Update() to call new methods**

Replace `HandleLevelChange(kb)` and `HandleAutoLevel(mouse)` with:
```csharp
UpdateSurfaceY();
HandleNudge(kb);
```

**Step 5: Update all tool handlers to use EffectiveY**

Every call to `gm.CmdPlaceFoundation(cell, _lastLevel, ...)` becomes `gm.CmdPlace(cell, EffectiveY, 0, CurrentVariant, BuildingCategory.Foundation)`.

Every call to `gm.CmdPlaceWall(cell, _lastLevel, edgeDir, _lastHitFoundation, ...)` becomes `gm.CmdPlaceDirectional(cell, EffectiveY, edgeDir, CurrentVariant, BuildingCategory.Wall)`.

Same pattern for ramps, machines, storage. Belt stays `gm.CmdPlaceBelt(from, to, EffectiveY, CurrentVariant)`.

Delete calls: `gm.CmdDelete(cell, EffectiveY)` and `gm.CmdDeleteDirectional(cell, EffectiveY, direction)`.

**Step 6: Update GetVariantsForCurrentTool and GetSelectedPrefab**

```csharp
private GameObject[] GetVariantsForCurrentTool()
{
    var gm = GridManager.Instance;
    if (gm == null) return null;
    return gm.GetPrefabs(ToolToCategory(_currentTool));
}

private GameObject GetSelectedPrefab()
{
    var gm = GridManager.Instance;
    if (gm == null) return null;
    return gm.GetPrefab(ToolToCategory(_currentTool), CurrentVariant);
}

private static BuildingCategory ToolToCategory(BuildTool tool)
{
    return tool switch
    {
        BuildTool.Foundation => BuildingCategory.Foundation,
        BuildTool.Wall => BuildingCategory.Wall,
        BuildTool.Ramp => BuildingCategory.Ramp,
        BuildTool.Machine => BuildingCategory.Machine,
        BuildTool.Storage => BuildingCategory.Storage,
        BuildTool.Belt => BuildingCategory.Belt,
        _ => BuildingCategory.Foundation
    };
}
```

**Step 7: Update ghost positioning**

Foundation ghost: `gm.GetFoundationPlacementPos(snapped, EffectiveY, GetSelectedPrefab())`

Machine/storage ghost: `gm.GetPlacementPos(cell, EffectiveY, prefab)`

Wall/ramp ghost: `gm.GetDirectionalPlacement(cell, EffectiveY, edgeDir, GetSelectedPrefab(), ToolToCategory(_currentTool), out pos, out rot)`

Belt ghost: `gm.GetPlacementPos(cell, EffectiveY, prefab)` for the endpoint marker

**Step 8: Update RaycastWallPlacement**

Remove `_lastHitFoundation`. The method just returns `cell`, `edgeDir`, and sets `_surfaceY` from the hit:

```csharp
private bool RaycastWallPlacement(out Vector2Int cell, out Vector2Int edgeDir)
{
    var ray = new Ray(_camera.transform.position, _camera.transform.forward);
    if (!Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
    {
        cell = Vector2Int.zero;
        edgeDir = Vector2Int.up;
        return false;
    }

    var placement = hit.collider.GetComponentInParent<PlacementInfo>();
    if (placement != null && placement.Category == BuildingCategory.Foundation)
    {
        _surfaceY = placement.SurfaceY + placement.ObjectHeight;
        edgeDir = GetFacingEdgeDirection();
        cell = placement.Cell;
        return true;
    }

    _surfaceY = hit.point.y;
    cell = GridManager.Instance.Grid.WorldToCell(hit.point);
    edgeDir = GetFacingEdgeDirection();
    return true;
}
```

**Step 9: Update OnGUI**

```csharp
string surfaceLabel = _nudgeOffset != 0f
    ? $"Surface: {EffectiveY:F1}m (nudge: {_nudgeOffset:+0.0;-0.0}m)"
    : $"Surface: {EffectiveY:F1}m";
GUILayout.Label($"BUILD MODE  |  Tool: {_currentTool}  |  {surfaceLabel}{variantLabel}");
```

Replace level keybind text with: `[PgUp/Dn] Nudge (+Shift: 0.5m)`

**Step 10: Update zoop start to store surfaceY instead of level**

Replace `_zoopStartLevel` (int) with `_zoopStartSurfaceY` (float). All zoop placement calls use `_zoopStartSurfaceY` instead of `_zoopStartLevel`.

**Step 11: Commit**

```
Update NetworkBuildController to surface-based placement with nudge
```

---

### Task 8: Update FactoryPrefabSetup editor script

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/FactoryPrefabSetup.cs`

**Step 1: Remove WireGridManager prefab field wiring**

Since GridManager no longer has serialized prefab fields, the `WireGridManager` method only needs to add `NetworkFactorySimulation` if missing. Remove all `WirePrefabField` calls and the `WirePrefabField` method.

**Step 2: Commit**

```
Simplify FactoryPrefabSetup -- remove serialized field wiring
```

---

### Task 9: Update SnapPointRegistry and BuildingData callers

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/SnapPointRegistry.cs`
- Search for any remaining `int level` or `.Level` references across `Scripts/`

**Step 1: Grep for remaining level references**

```
grep -rn "int level\|\.Level\|\.BaseLevel" Assets/_Slopworks/Scripts/
```

Fix each remaining reference to use `float surfaceY` / `.SurfaceY` / `.BaseSurfaceY`.

**Step 2: Commit**

```
Fix remaining int level references across codebase
```

---

### Task 10: Manual playtest verification

**Step 1: Open HomeBase scene, enter play mode as host**

**Step 2: Verify placement flow**

- Press B for build mode
- Place foundations on terrain -- bottom face should sit on terrain surface
- Tab between foundation variants if multiple exist -- ghost and placement Y should match
- Place a wall on a foundation -- wall base should sit on foundation top surface
- PgUp should nudge the ghost up 1m, Shift+PgUp should nudge 0.5m
- Switch tools -- nudge should reset to 0
- Place machine on foundation -- should sit on top surface
- Place storage next to machine -- auto-wire inserter should log
- Place belt between storage and machine
- Delete mode (X) -- click to delete any placed object
- Zoop foundations -- all ghosts should be at correct Y

**Step 3: Commit any fixes found during playtest**

---
