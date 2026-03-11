# Unity Splines Package Evaluation

Research compiled 2026-03-09. Assessment of com.unity.splines for belt system use.

## Package Overview

- Package: com.unity.splines
- Compatible version: v2.8.3 (ships with Unity 6000.3.10f1)
- NOT currently installed in project
- Provides cubic Bezier splines only (no Catmull-Rom or B-spline)
- Components: SplineContainer, SplineExtrude, SplineAnimate, SplineInstantiate
- SplineUtility static class for evaluation, distance, nearest-point queries

## Runtime Usage

Splines can be created and modified entirely at runtime:

```csharp
var go = new GameObject("Belt");
var container = go.AddComponent<SplineContainer>();
var spline = container.Spline;
spline.Add(new BezierKnot(new float3(0, 0, 0)));
spline.Add(new BezierKnot(new float3(5, 0, 0)));
```

Known gotcha: runtime-created splines may appear as straight lines in editor Scene view until SplineContainer is selected. Editor gizmo issue, not a data issue.

## Distance Evaluation

SplineUtility.GetPointAtLinearDistance() exists but is iterative/approximate:
- Walks the spline sampling positions until accumulated distance matches
- NOT constant-time O(1) lookup
- Cost scales with distance and spline complexity
- For belt items needing "position at distance X," this needs wrapping in a pre-computed lookup table

## Mesh Generation

SplineExtrude generates tube-shaped meshes only (circular cross-section).
- For flat conveyor belt mesh: need custom mesh generation code
- SplineMesh.Extrude() static method exists but also produces tube geometry
- The evaluation API (EvaluatePosition, EvaluateTangent, EvaluateUpVector) can sample positions/tangents for custom extrusion
- SplineExtrude regenerates entire mesh on any spline change -- GC pressure if belts modified frequently

## Performance -- Critical Findings

### SplineAnimate (built-in object-along-spline)
- Bug IN-67540: 20 instances = 200ms/frame in editor, ~100ms in builds
- Partially fixed in v2.6-2.8 with caching, but fundamentally limited

### Raw spline evaluation (Jason Booth benchmarks)
- 1-4ms for 100 evaluations depending on data needed
- 100 belt segments x 5 items = 500 evaluations/frame = 5-20ms
- That's 30-100% of frame budget at 60fps
- Booth's conclusion: too slow for high-volume evaluation
- His solution: pre-sample into lookup tables

### GetNearestPoint
- ~1ms per call on simple spline. Useless at scale.

### Large knot counts
- Splines with 100+ knots become sluggish even in editor
- O(n) operations on modification

### Bottom line
For a factory with hundreds of belt segments and thousands of items needing position updates every frame, raw Unity Spline evaluation is NOT viable without a caching/pre-sampling layer. And if you're building that layer yourself, the package just adds overhead.

## Networking Concerns

BezierKnot struct per knot:
- Position: float3 (12 bytes)
- TangentIn: float3 (12 bytes)
- TangentOut: float3 (12 bytes)
- Rotation: quaternion (16 bytes)
- Total: ~52 bytes per knot

2-knot belt = ~104 bytes. 3-4 knot curved belt = 156-208 bytes.

Issues:
- Uses Unity.Mathematics types (float3, quaternion), not standard Unity types
- Need custom FishNet serializers
- Spline class has internal metadata beyond knots -- don't serialize whole Spline object
- Practical approach: sync only knot array + closed flag, reconstruct on clients

Simpler alternative: sync only endpoint positions + tangent directions (24-48 bytes), reconstruct polyline deterministically on both sides.

## Alternatives Comparison

| Criterion | Unity Splines | Hand-rolled Bezier | Pre-sampled Polyline |
|-----------|--------------|-------------------|---------------------|
| Runtime creation | Yes | Yes | Yes |
| Distance evaluation | Iterative, slow | Needs pre-computation | Array lookup, fastest |
| Network sync cost | ~104 bytes/segment | 48 bytes/segment | 24 bytes (endpoints only) |
| Mesh generation | Tubes only | DIY | DIY |
| Performance at scale | Poor (1-4ms/100 evals) | Good (direct math) | Best (array index) |
| Matches belt model | No (t-based) | Partially | Yes (distance-offset) |
| Package dependency | Yes | No | No |

## Verdict -- REVISED

The initial evaluation (based on v2.4-2.5 capabilities) recommended against Unity Splines.

**Deeper research into v2.6-2.8 reverses that verdict.** Key changes:

1. **v2.6.0 fixed GC allocations** -- SplineContainer evaluation no longer allocates per call. This was the main performance blocker.
2. **v2.7.1 added Road profile** -- SplineExtrude now generates flat belt-like geometry natively. No custom mesh code needed.
3. **v2.7.1 added Spline Profile** -- arbitrary cross-sections defined as a spline. Full control over belt shape.
4. **ConvertIndexUnit uses cached LUTs** -- arc-length parameterization is built-in and fast. No custom ArcLengthTable needed.
5. **SplineInstantiate** -- automatic roller/strut placement along belts at runtime.

**New recommendation: TRY Unity Splines first.** It eliminates the need for:
- Custom arc-length parameterization code
- Custom mesh generation code
- Custom cross-section sweep algorithm
- Custom rotation-minimizing frame implementation

The hand-rolled approach (spline-math.md) remains valid as a fallback if the package introduces issues at scale. But the package-first path is significantly less code.

See `unity-procedural-geometry.md` for the full deep dive on v2.6-2.8 capabilities.

## Sources

- [About Splines 2.4.0](https://docs.unity3d.com/Packages/com.unity.splines@2.4/manual/index.html)
- [SplineUtility API 2.6](https://docs.unity3d.com/Packages/com.unity.splines@2.6/api/UnityEngine.Splines.SplineUtility.html)
- [SplineAnimate performance bug IN-67540](https://discussions.unity.com/t/in-67540-splineanimate-performance-issue-200ms-for-20-splineanimate-objects/938999)
- [Jason Booth: Optimizing Spline Operations](https://medium.com/@jasonbooth_86226/optimizing-spline-operations-d48b5f8fede4)
- [theor.xyz DOTS belts](https://theor.xyz/dots-burst-satisfactory-belts/)
