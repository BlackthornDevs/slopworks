# Belt Sync: Waypoint SyncList Approach

Research date: 2026-03-10

## Context

Current system syncs 4x Vector3 (48 bytes) via SyncVars. Clients deterministically
rebuild full route via BeltRouteBuilder.Build() then bake mesh via BeltSplineMeshBaker.
This is cheap on bandwidth but expensive on CPU for late-joining clients.

Proposed: sync the full waypoint list so clients skip Build() and go straight to mesh baking.

## FishNet SyncList for Custom Structs

FishNet auto-generates serializers for structs whose fields are all serializable types.
`BeltRouteBuilder.Waypoint` has three Vector3 fields (Position, TangentIn, TangentOut),
all natively supported. No custom serializer needed.

SyncList mechanics (from FishNet source):
- `WriteFull()`: writes boolean header, count, then each item. Used for initial state to new clients.
- `WriteDelta()`: writes only changed entries. Belt waypoints are write-once at spawn, so after initial full write, zero delta traffic.
- Each Waypoint = 36 bytes (3x Vector3). With SyncList overhead (~5 bytes per entry), ~41 bytes on wire.

## Bandwidth Impact

| Metric | Current (4x SyncVar) | Proposed (SyncList) |
|--------|---------------------|---------------------|
| Per belt (typical 8 waypoints) | 50 bytes | ~328 bytes |
| Per belt (worst case 15 waypoints) | 50 bytes | ~625 bytes |
| 100 belts late-join | ~5 KB | ~33-63 KB |
| 200 belts late-join | ~10 KB | ~66-125 KB |

Completely acceptable. 125 KB is a single TCP packet burst. Co-op game with 2-4 players
on LAN or internet handles this trivially.

## FishNet Late-Join Behavior

When a new client connects, FishNet calls WriteFull() for every SyncList on every
spawned NetworkObject. This happens during the spawn message batch, not throttled.

- No built-in rate limiting for SyncList initial state
- Tugboat/FishySteamworks handle large payloads via fragmentation
- 63 KB across 100 SyncLists is well within normal operating range
- Join spike is serialization-CPU-limited, not bandwidth-limited

## Alternatives Considered

| Option | Pros | Cons | Verdict |
|--------|------|------|---------|
| SyncList<Waypoint> | Auto late-join, delta sync free, native pattern | More bandwidth than SyncVars | Recommended |
| TargetRpc on join | Can compress manually | Violates "never use RPCs for persistent state" rule | Rejected |
| ObserversRpc BufferLast | Replayed automatically | Still RPC pattern, fragile | Not recommended |
| byte[] SyncVar (compressed) | Minimal bandwidth | Custom serialization, harder to debug | Premature optimization |

## Real-World Precedent

- **Satisfactory**: Syncs spline control points, not recomputed client-side. Items positioned on GPU.
- **Factorio**: Deterministic lockstep, all clients run identical simulation. Not applicable to FishNet.
- **Dyson Sphere Program**: Control points in save files, meshes regenerated on load. Bulk regeneration is their biggest perf bottleneck.

Industry consensus for client-server: sync the control points/waypoints, not just endpoints.

## Implementation Plan

Complexity: Low. Change isolated to NetworkBeltSegment.

```
Current flow (client):
  OnStartClient -> BeltRouteBuilder.Build() -> BeltSplineMeshBaker.BakeMesh()

Proposed flow (client):
  OnStartClient -> read SyncList -> BeltSplineMeshBaker.BakeMesh()
```

Changes required:
1. NetworkBeltSegment: Replace 4 geometry SyncVars + 2 mode SyncVars with SyncList<Waypoint>
2. ServerInit(): Populate SyncList from waypoints parameter
3. OnStartClient(): Read waypoints from SyncList, skip Build()
4. OnStartServer(): Read waypoints from SyncList for fallback path

Remaining bottleneck: BakeMesh() at ~0.3ms per belt. For 100 belts = 30ms in one frame.
Spread across frames with coroutine queue.

## Recommendation

Go with SyncList<Waypoint>. Bandwidth cost negligible, CPU savings real, implementation
straightforward, follows FishNet native patterns.
