# Belt Sync: Mesh Serialization Approach

Research date: 2026-03-10

## Context

Alternative to waypoint sync: serialize the baked mesh on the server and send raw
vertex/triangle data to clients. Zero route computation, zero mesh generation on clients.

## Unity Mesh Serialization Methods

**Option A: Manual array extraction (simplest)**
mesh.vertices, mesh.triangles, mesh.normals, mesh.uv -> byte[] via Buffer.BlockCopy.
Each call allocates a managed array copy. Fine for one-time serialization.

**Option B: Mesh.MeshDataArray (zero-copy, Unity 2020.1+)**
GetVertexData<byte>() and GetIndexData<byte>() return NativeArray -- no managed alloc.
Vertex data is interleaved, need to store vertex attribute layout to reconstruct.

**Option C: AllocateWritableMeshData + ApplyAndDisposeWritableMeshData**
Fastest write path for reconstruction. Single API call applies all data. 2-5x faster
than setting vertices/triangles/normals individually.

## Size Estimates

Belt mesh parameters (from BeltSplineMeshBaker):
- Cross-section: 4 verts per ring (rectangular)
- Resolution: 4 segments/meter
- Typical belt: ~3m average

For a 3m belt:
- 13 rings x 5 verts + 8 cap verts = 73 vertices
- 104 triangles
- Position (12B) + Normal (12B) + UV (8B) = 32 bytes/vertex
- Vertex total: 2,336 bytes
- Index buffer (UInt16): 624 bytes
- **Total raw: ~3 KB per 3m belt**
- 10m belt: ~8.5 KB

Comparison: current sync = 48 bytes (62x smaller).

## Compression Options

| Method | Result |
|--------|--------|
| Quantization (half-float positions, octahedral normals) | ~1,500 bytes (50% reduction) |
| LZ4/Deflate on raw | ~1-1.5 KB (belt normals highly repetitive) |
| Both combined | ~1 KB |

## FishNet Transport Limits

- Tugboat MTU: 1432 bytes max per packet
- Large messages auto-split via splitLargeMessages
- 3 KB mesh = 2-3 fragments, fine
- 100 belts late-join = 300 KB burst. FishNet handles via send window, adds join delay.
- SyncVar<byte[]> sends entire array on any change (no delta). OK for write-once data.

## Performance Comparison (100 belts late-join)

| Method | Time |
|--------|------|
| Current (rebuild all) | ~40ms |
| Mesh deserialization (ApplyAndDispose) | ~8ms |
| Mesh deserialization (individual setters) | ~30ms |

Improvement is real but modest. Frame-spread coroutine works equally well either way.

## Real-World Precedent

**This is generally considered an anti-pattern.** No mainstream multiplayer game syncs
raw mesh vertex/triangle data for procedural gameplay objects.

Standard approach everywhere: sync parameters, reconstruct deterministically.
- Satisfactory, Factorio, Dyson Sphere Program: all sync parameters, not meshes
- Minecraft, Valheim: sync voxel data, mesh locally
- VRChat: user-imported models use CDN asset bundles, not real-time sync

## Recommendation: Do NOT Sync Mesh Data

The current 48-byte parameter sync (or the waypoint SyncList at ~328 bytes) is nearly
optimal. The late-join reconstruction cost is a scheduling problem (spread across frames),
not a sync architecture problem.

Problems with mesh sync:
1. 62x bandwidth increase for identical visual result
2. Complexity explosion: custom serializer, vertex layout, compression, error handling
3. Memory doubling: server keeps serialized byte[] alongside actual Mesh
4. Breaks existing architecture (D-BLT-006)
5. No industry precedent

The right fix for late-join hitch: frame-spread BakeMesh() via coroutine queue.
