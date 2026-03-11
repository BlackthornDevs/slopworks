# SplineExtrude Performance at Scale

Research compiled 2026-03-09.

## Single Belt Mesh Generation Cost

10m belt, SegmentsPerUnit = 20, Sides = 8 (rectangular cross-section):
- Edge loops: 200
- Vertices: ~1,800
- Triangles: ~3,200
- Estimated generation cost: 0.1-0.5ms per belt
- Memory: ~70 KB per belt mesh

## Idle Cost (200+ Components, Splines Not Changing)

SplineExtrude.Update() when nothing changed:
```csharp
void Update()
{
    if (m_RebuildRequested && Time.time >= m_NextScheduledRebuild)
        Rebuild();
}
```
- One bool check per frame per component = negligible
- MonoBehaviour overhead: ~0.01-0.05ms for 200 components

**Hidden scaling cost:** If `RebuildOnSplineChange` is true (default), SplineExtrude subscribes to a static `Spline.Changed` event. Modifying ONE spline triggers delegate invocations on ALL subscribers (200 components each check if the changed spline is theirs). Must disable this for placed belts.

## Preview During Placement

Single SplineExtrude rebuilding every frame:
- 1,800 vertices = well within budget at 60fps
- BUT: Extrude() allocates managed arrays each call (GC pressure)
- For a single ghost preview: acceptable
- For 200 simultaneous rebuilds: not acceptable

Alternative: LineRenderer with 20-40 spline-sampled points. Negligible cost, acceptable visual quality for ghost preview.

## Bake-and-Destroy Pattern (RECOMMENDED)

The clear winner for production:

```csharp
// At placement time:
splineExtrude.Rebuild();  // Generate mesh once (~0.3ms)
Mesh bakedMesh = meshFilter.mesh;  // Returns instance
bakedMesh.name = "BakedBelt_" + beltId;

// Destroy SplineExtrude + SplineContainer (no longer needed)
Destroy(splineExtrude);
Destroy(splineContainer);

// MeshFilter + MeshRenderer remain with baked mesh
// Optionally mark static for batching:
gameObject.isStatic = true;
StaticBatchingUtility.Combine(beltParent);  // Batch all belt meshes
```

Benefits:
- Zero per-frame cost after placement
- Eligible for static batching (reduces 200 draw calls to 1-3)
- No Spline.Changed event subscriptions
- Memory is just mesh data, no component overhead

Tradeoffs:
- Cannot deform belt after baking (use UV scroll shader for movement illusion)
- Must re-extrude if belt path changes (belts are placed once, rarely moved)
- Must manually destroy baked mesh on GameObject destruction

## Memory at Scale

200 belts * 70 KB = ~14 MB of mesh data. Modest.

Draw calls: 200 separate meshes = 200 draw calls unless batched.
- Static batching: combines into 1-3 draw calls
- SRP Batcher: possible if all belts share material

## Recommended Pipeline

| Phase | Approach | Cost |
|-------|----------|------|
| Preview (cursor moving) | LineRenderer with 20-40 spline-sampled points | <0.1ms/frame |
| Placement confirm | SplineExtrude.Rebuild() once | ~0.3ms one-shot |
| Post-placement | Destroy SplineExtrude + SplineContainer, keep baked mesh, mark static | 0ms/frame |
| 200+ placed belts | Static batched baked meshes, UV-scroll shader | 1-3 draw calls |
| Belt item visuals | Existing SyncList positions + instanced rendering | Separate from belt mesh |

## Key Takeaway

SplineExtrude is a mesh GENERATION tool, not a persistent runtime component. Use it at placement time, bake the result, destroy the component. The belt mesh is static scenery -- UV-scroll shader sells the movement illusion.

## Sources

- [SplineExtrude source (needle-mirror)](https://github.com/needle-mirror/com.unity.splines/blob/master/Runtime/SplineExtrude.cs)
- [Runtime extrude guide v2.8](https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/extrude-runtime.html)
- [DOTS belts at 1M items/60fps](https://theor.xyz/dots-burst-satisfactory-belts/)
- [StaticBatchingUtility](https://docs.unity3d.com/2023.1/Documentation/Manual/static-batching.html)
