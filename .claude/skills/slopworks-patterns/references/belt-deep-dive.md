# Belt system — detailed reference

## Data model

Do not store absolute item positions. Store the gap between consecutive items:

```csharp
struct BeltItem {
    public ItemType Type;
    public ushort DistanceToNext;  // integer subdivisions per tile (e.g. 100)
}

// On a belt segment NetworkBehaviour (one NetworkObject per segment)
[SyncObject]
private readonly SyncList<BeltItem> _items = new();
ushort terminalGap;  // space from last item to end of segment
```

Each tick: only `terminalGap` changes when the belt flows freely. All other values are static during steady-state flow. This gives O(1) belt updates.

**Why integer distances:** Float math is not deterministic across platforms. Integer subdivisions keep the simulation identical on all machines, which matters for server authority and future-proofing for lockstep if needed.

## Segment merging

Merge consecutive belt tiles into one segment:
- A straight run of 20 tiles = one entity with one item list, not 20 entities
- Split segments only at inserter attachment points (inserters need a fixed position to read from)
- This is the biggest performance lever for belt-heavy factories

## Performance targets

| Approach | Max active items at 60fps |
|----------|--------------------------|
| MonoBehaviour per item | ~500 |
| MonoBehaviour per segment + SyncList | ~50,000 |
| DOTS + Burst (if migrated) | ~1,000,000 |

Start with MonoBehaviour per segment. Profile before migrating to DOTS — only move hot paths if needed.

## Rendering items on belts

Items on belts are NOT GameObjects. Rendering them as GameObjects at scale kills draw call counts.

Use `Graphics.DrawMeshInstancedProcedural` with a `NativeArray<float3>` of positions computed from belt segment data each frame:

```csharp
// Each frame, compute visual positions from segment data
// Pass to GPU instancing — one draw call per item type across all belts
Graphics.DrawMeshInstancedProcedural(itemMesh, 0, itemMaterial, bounds, instanceCount, propertyBlock);
```

## Network LOD

FishNet's Network LOD (interest management) automatically throttles update rate for belt segments far from any player. Configure per-scene:

```csharp
networkManager.ObserverManager.DefaultCondition = new DistanceCondition(viewDistanceTiles);
```

Home Base: large view distance. Buildings: smaller (interior). Overworld: very large (isometric).

## The three machine states

```
IDLE    → waiting for inputs (buffers empty or recipe not met)
WORKING → inputs consumed, production timer counting down
BLOCKED → recipe complete, output buffer full, can't eject
```

All machines must handle BLOCKED correctly or the factory deadlocks. When blocked, the machine stops consuming inputs (it's already produced — it just can't push the output yet). On the next tick, it checks again whether the output buffer has room.

## Tick structure

```
Pre-tick:   reset temp output buffers
Tick:       each machine tries grab() then produce()
Post-tick:  flush temp → final output buffers
```

The temp buffer prevents outputs from being consumed within the same tick as production. This gives consistent frame boundaries — a machine can't produce and have that output consumed in the same tick.

## In-transit items are not in inventories

Belt items are entities in a belt segment buffer. They are not in any `NetworkInventory` or `InventoryContainer`. This means:
- You can't query "how much iron is in transit" via inventory APIs
- Belt contents are separate from machine input/output buffers
- Inserters are the boundary between belt items and machine buffers
