# Unity Splines Package -- Deep Dive Reference

Research compiled 2026-03-09. Comprehensive technical reference for `com.unity.splines` procedural geometry capabilities, evaluated for conveyor belt implementation in Slopworks.

**Package:** `com.unity.splines` (not currently installed in this project)
**Latest stable:** 2.8.3 (2026-01-30)
**Minimum Unity:** 2022.3+

---

## 1. SplineExtrude Component -- Beyond Tubes

Prior research only covered tube extrusion. As of v2.7.1, SplineExtrude supports **four cross-section profiles**:

### Profile Types

| Profile | Description | Use Case |
|---------|-------------|----------|
| **Circle** | Round cross-section. `Sides` parameter controls smoothness (min 2). | Pipes, wires, ropes |
| **Square** | Square cross-section. | Structural beams, ducts |
| **Road** | Flat cross-section with a slight lip (raised edges). | Roads, paths, **conveyor belts** |
| **Spline** | Uses another spline as an arbitrary cross-section template. | Any custom profile |

### The Road Profile

The Road profile generates a flat surface with slight raised edges -- essentially a ribbon with lip. This is the closest built-in profile to a conveyor belt surface. No custom code needed for basic flat-belt geometry.

### The Spline Profile (Custom Cross-Sections)

The Spline Profile mode lets you define **any arbitrary 2D cross-section** by drawing it as a separate spline:

- **Template:** Reference to a SplineContainer holding the cross-section spline
- **Spline Index:** Which spline in the container to use as the profile
- **Side Count:** Surface smoothness (min 2)
- **Axis:** Which axis of the template spline to follow

For a conveyor belt, you could draw a cross-section spline shaped like a belt profile: flat top surface, angled side walls, bottom channel for rollers. The SplineExtrude component then sweeps this profile along the path spline.

### Key Parameters (All Profiles)

| Parameter | Purpose |
|-----------|---------|
| `Radius` | Width of extrusion from the spline path |
| `SegmentsPerUnit` | Curve smoothness (higher = more triangles) |
| `Sides` | Cross-section smoothness (Circle/Spline profiles) |
| `Cap Ends` | Fill open ends of the mesh |
| `Flip Normals` | Reveal interior surfaces |
| `Range` | Partial extrusion (normalized 0-1) |
| `Update Colliders` | Auto-sync collider geometry with mesh |
| `Auto Refresh Generation` | Rebuild mesh when spline changes (configurable frequency, max 30/sec) |

### Runtime Extrusion Code

Official example from Unity docs (v2.8):

```csharp
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class ExtrudeExample : MonoBehaviour
{
    void Start()
    {
        var splineContainer = new GameObject("Spline").AddComponent<SplineContainer>();
        splineContainer.Spline = new Spline();
        splineContainer.Spline.AddRange(new float3[]
        {
            new (0, 0, 0),
            new (0, 0, 1),
            new (1, 0, 1),
            new (1, 0, 0)
        });

        var go = splineContainer.gameObject;
        var extrudeComponent = go.AddComponent<SplineExtrude>();
        extrudeComponent.Container = splineContainer;

        var hasMeshFilter = go.TryGetComponent<MeshFilter>(out var meshFilter);
        if (hasMeshFilter)
        {
            if (meshFilter.sharedMesh == null)
            {
                var extrudeMesh = new Mesh();
                extrudeMesh.name = "Spline Extrude Mesh";
                meshFilter.sharedMesh = extrudeMesh;
            }

            extrudeComponent.Radius = 0.25f;
            extrudeComponent.SegmentsPerUnit = 20;
            extrudeComponent.Sides = 8;
            extrudeComponent.Range = new float2(0, 100);

            go.TryGetComponent<MeshRenderer>(out var meshRenderer);
            if (meshRenderer != null)
                meshRenderer.material = new Material(Shader.Find("Standard"));
        }
    }
}
```

**Important:** `AddComponent<SplineExtrude>()` automatically adds `MeshFilter` and `MeshRenderer` if missing.

---

## 2. SplineMesh Static API -- Low-Level Extrusion

`SplineMesh` is a static utility class for programmatic mesh generation. Use this for full control instead of the SplineExtrude component.

### Extrude Method Overloads

```csharp
// 1. Simple: single spline, fixed segment count
SplineMesh.Extrude<T>(T spline, Mesh mesh, float radius, int sides, int segments, bool capped = true);

// 2. With range: partial extrusion
SplineMesh.Extrude<T>(T spline, Mesh mesh, float radius, int sides, int segments, bool capped, float2 range);

// 3. Multiple splines: segments-per-unit density
SplineMesh.Extrude<T>(IReadOnlyList<T> splines, Mesh mesh, float radius, int sides, float segmentsPerUnit, bool capped, float2 range);

// 4. Native arrays: zero-alloc, custom vertex layout
SplineMesh.Extrude<TSplineType, TVertexType, TIndexType>(
    TSplineType spline,
    NativeArray<TVertexType> vertices,
    NativeArray<TIndexType> indices,
    float radius, int sides, int segments, bool capped, float2 range)
    where TVertexType : struct, SplineMesh.ISplineVertexData
    where TIndexType : struct; // UInt16 or UInt32
```

### ISplineVertexData Interface

For custom vertex layouts with the NativeArray overload:

```csharp
public interface SplineMesh.ISplineVertexData
{
    Vector3 position { get; set; }
    Vector3 normal { get; set; }
    Vector2 texture { get; set; }  // UV0
}
```

Implement this interface if you want custom vertex data beyond the default layout.

### Buffer Size Calculation

```csharp
SplineMesh.GetVertexAndIndexCount(
    int sides, int segments, bool capped, bool closed,
    Vector2 range,
    out int vertexCount, out int indexCount);
```

Call this before allocating NativeArrays for the Extrude overload 4.

### Limitations of SplineMesh.Extrude

All Extrude overloads generate **radial cross-sections** (tube-like geometry). They take `radius` and `sides`, not a custom profile. For flat belt surfaces, you need either:
1. The SplineExtrude **component** with Road or Spline profile (v2.7.1+)
2. Custom mesh generation using SplineUtility evaluation methods (see Section 4)

---

## 3. SplineInstantiate -- Objects Along Splines

Places prefabs/GameObjects at intervals along a spline. Ideal for belt rollers, support struts, guide rails.

### Instantiation Methods

| Method | Description |
|--------|-------------|
| **Instance Count** | Fixed number of items (exact or random range) |
| **Spline Distance** | Items spaced by distance measured along the spline curve |
| **Linear Distance** | Items spaced by world-space distance. "Auto" fits maximum non-overlapping items |

### Orientation Control

- **Align To Spline Element:** Rotation interpolated from nearby knot rotations
- **Align To Spline Object:** Matches the spline container's orientation
- **World Space:** Ignores spline rotation entirely

Configurable forward/up axis per item.

### Randomization

Position, rotation, and scale offsets can be randomized per-axis with min/max ranges. Each item in the prefab list has an individual probability weight.

### Runtime Capability

SplineInstantiate supports runtime operation:
- **Auto Refresh Generation:** Regenerates when spline/values change
- **Regenerate:** Reinstantiate without clearing
- **Clear:** Remove all instantiated items
- **Randomize:** Recalculate random values and reinstantiate

### Conveyor Belt Application

For belt rollers at regular intervals:
```
SplineInstantiate.InstantiateMethod = SplineDistance
SplineInstantiate.SplineDistance = 0.5f  // roller every 0.5 units
SplineInstantiate.AlignTo = SplineElement
SplineInstantiate.ForwardAxis = ZAxis
```

This would automatically place roller prefabs along the belt curve, properly oriented to the belt surface. One-time setup at belt placement.

---

## 4. Procedural Mesh Generation -- Manual Approach

When the built-in profiles are insufficient, generate the mesh yourself using SplineUtility for evaluation.

### The Cross-Section Sweep Algorithm

This is the standard approach for roads, belts, ribbons. Already documented in `curved-belt-research.md` Section 4, but here is how it maps to Unity Splines API:

```csharp
// 1. Create spline from belt control points
var spline = new Spline();
spline.AddRange(new float3[] { startPos, cp1, cp2, endPos });

// 2. Calculate total length
float totalLength = SplineUtility.CalculateLength(spline, float4x4.identity);

// 3. Determine sample count
int segments = Mathf.CeilToInt(totalLength * segmentsPerUnit);

// 4. For each sample point along the spline:
for (int i = 0; i <= segments; i++)
{
    float distance = (totalLength * i) / segments;

    // Convert distance to spline t parameter
    float t = spline.ConvertIndexUnit(distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);

    // Or use: SplineUtility.ConvertIndexUnit(spline, distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);

    // Evaluate position, tangent, up
    float3 pos = SplineUtility.EvaluatePosition(spline, t);
    float3 tangent = SplineUtility.EvaluateTangent(spline, t);
    float3 up = SplineUtility.EvaluateUpVector(spline, t);

    // Build coordinate frame
    float3 right = math.normalize(math.cross(up, tangent));
    up = math.cross(tangent, right); // re-orthogonalize

    // Place cross-section vertices
    // vertex_left  = pos - right * halfWidth;
    // vertex_right = pos + right * halfWidth;
}

// 5. Stitch triangles between consecutive cross-sections
// 6. Assign UVs: U across width, V = accumulatedArcLength / totalLength
```

### PathIndexUnit Enum

Critical for distance-to-parameter conversion:

| Value | Meaning |
|-------|---------|
| `PathIndexUnit.Distance` | World-space distance along the spline |
| `PathIndexUnit.Normalized` | 0-1 normalized parameter |
| `PathIndexUnit.Knot` | Integer knot index + fractional interpolation |

### ConvertIndexUnit -- The Key Method

```csharp
// Convert distance (meters) to normalized t
float t = SplineUtility.ConvertIndexUnit(spline, distanceInMeters, PathIndexUnit.Distance, PathIndexUnit.Normalized);

// Convert normalized t to distance
float dist = SplineUtility.ConvertIndexUnit(spline, normalizedT, PathIndexUnit.Normalized, PathIndexUnit.Distance);
```

This handles arc-length parameterization internally. The Spline class caches distance-to-interpolation lookup tables, so repeated conversions are efficient.

---

## 5. Spline Mesh Deformation -- Bending Existing Meshes

Can you take a straight belt mesh and bend it along a curve? Three approaches:

### Approach A: CPU Vertex Deformation

Read the source mesh vertices, for each vertex compute where it maps along the spline based on its local Z (length axis), then reposition it using the spline's coordinate frame at that point. This is the "template mesh" approach.

**Pros:** Works with any mesh. Full control over vertex placement.
**Cons:** CPU cost scales with vertex count. Must rebuild on spline change.

### Approach B: GPU Spline Deformation (Roy Theunissen)

Bake spline deformation into a texture, apply via vertex shader:
1. Each pixel holds RGBA = one row of a 4x4 transform matrix
2. Four vertical samples per spline position = one full matrix
3. Vertex shader reads matrix from texture based on vertex's normalized Z coordinate
4. Multiply vertex by matrix to deform it along the spline

**Pros:** Extremely performant at runtime (texture sample + matrix multiply per vertex).
**Cons:** Texture rebake needed when spline changes (editor-speed, not runtime-speed). No new geometry -- mesh must have enough subdivisions to deform smoothly.

**Source:** https://github.com/RoyTheunissen/GPU-Spline-Deformation (open source)

### Approach C: SplineExtrude with Spline Profile (Recommended for Belts)

Use the built-in Spline Profile mode to define the belt cross-section as a spline, then extrude along the path spline. No custom deformation code needed. The engine handles vertex placement, normals, UVs.

### Verdict for Slopworks

**Approach C is the winner** for belt surfaces. Define a belt cross-section spline once (flat top, side walls), reuse it for all belt segments via SplineExtrude with Spline Profile. For belt items, use the manual evaluation approach (Section 6) -- items don't need mesh deformation, just position along the curve.

---

## 6. Arc-Length Parameterization -- Distance-Based Evaluation

### The Problem (Recap)

Spline parameter `t` is not proportional to distance. Items placed at equal `t` intervals bunch up on tight curves.

### Unity's Built-In Solutions

**`ConvertIndexUnit` (primary method):**
```csharp
// "Give me position at 5.0 meters along the spline"
float t = SplineUtility.ConvertIndexUnit(spline, 5.0f, PathIndexUnit.Distance, PathIndexUnit.Normalized);
float3 pos = SplineUtility.EvaluatePosition(spline, t);
```

The Spline class internally caches `DistanceToInterpolation` lookup tables (populated by `CurveUtility.CalculateCurveLengths`). This means repeated distance-to-t conversions are fast after the first call.

**`GetPointAtLinearDistance`:**
```csharp
// "From parameter t=0.3, walk 2.0 meters forward along the spline"
float3 point = SplineUtility.GetPointAtLinearDistance(spline, 0.3f, 2.0f, out float resultT);
```

Returns both the 3D position and the corresponding `t` parameter. Supports negative distances for backward traversal.

**`CurveUtility.GetDistanceToInterpolation`:**
```csharp
// Low-level: convert distance to t on a single BezierCurve
float t = CurveUtility.GetDistanceToInterpolation(curve, distance);

// Efficient version with pre-computed lookup table
var lut = new DistanceToInterpolation[resolution];
CurveUtility.CalculateCurveLengths(curve, lut);
float t = CurveUtility.GetDistanceToInterpolation(lut, distance);
```

### What Changed in v2.6+

- **v2.6.0:** Fixed GC allocation issues in `SplineContainer` evaluation (`[SPLB-246]`). Spline evaluation no longer allocates heap memory per call. This was the main performance blocker for runtime use.
- **v2.6.0:** New constructors accepting `float3[]` knot positions directly (bypass BezierKnot creation).
- **v2.6.0:** `SplineAnimate` completion event added.
- **v2.6.1:** Fixed `SplineExtrude` and `SplineAnimate` runtime instantiation errors.
- **v2.7.1:** New extrusion profiles (Circle, Square, Road, Spline).
- **v2.8.0:** SplineExtrude can target a mesh in the asset library. Multiple SplineExtrude instances no longer conflict.

### Caching Architecture

Built-in `Spline` and `NativeSpline` classes cache distance-to-interpolation lookup tables internally. You do NOT need to build your own arc-length table (as described in `curved-belt-research.md` Section 8) if using the Unity Splines package. `ConvertIndexUnit` uses the cache automatically.

However, `SplineSlice<T>` does NOT cache and is explicitly documented as not performant for repeated evaluation. If you need to evaluate a sub-section of a spline repeatedly, convert it to a `NativeSpline` first.

### Performance Implication for Belt Items

With the v2.6 GC fix, calling `ConvertIndexUnit` + `EvaluatePosition` per belt item per frame is viable. For a factory with 100 visible belt segments and 10 items each:
- 1000 calls to `ConvertIndexUnit` + `EvaluatePosition` per frame
- Each call: lookup table binary search + cubic polynomial evaluation
- Estimated cost: sub-millisecond on modern hardware

This eliminates the need for a custom `ArcLengthTable` struct. The Unity Splines package does it for you with cached LUTs.

---

## 7. SplineAnimate -- Moving Objects Along Splines

### Animation Methods

| Method | Behavior |
|--------|----------|
| **Time** | Traverse entire spline in `Duration` seconds. Speed varies with curve density. |
| **Speed** | Traverse at `MaxSpeed` units/second. Duration varies with spline length. Constant world-space speed. |

Speed mode uses arc-length parameterization internally, giving constant visual speed along the curve.

### Easing Modes

When `EasingMode` is `None` and method is `Speed`, traversal is at constant `MaxSpeed` throughout. Otherwise, speed ramps from 0 to `MaxSpeed` based on the easing curve.

### Loop Modes

- **Once:** Play once, stop at end
- **Loop:** Restart from beginning
- **PingPong:** Reverse direction at each end

### Completion Event (v2.6+)

```csharp
splineAnimate.Updated += OnAnimateUpdated;
// Fires each frame the animation updates
```

The `NormalizedTime` property tracks progress: integer part = completed loop count, fractional part = current progress 0-1.

### Performance State (v2.7-2.8)

- v2.7.2: Fixed SplineAnimate malfunction with non-uniform scale transforms
- v2.8.1: Fixed null reference during IL2CPP shutdown
- v2.8.2: Fixed animation issues with Scene/Domain reload disabled

SplineAnimate is now stable for runtime use. However, it is a **MonoBehaviour with per-frame Update()**, so using one SplineAnimate per belt item is not viable at factory scale. Use direct `ConvertIndexUnit` + `EvaluatePosition` calls instead (Section 6).

### When to Use SplineAnimate

Good for: camera paths, single special objects, cutscene movement.
Bad for: hundreds of belt items (use manual evaluation instead).

---

## 8. SplineData<T> -- Custom Data Along Splines

Store arbitrary typed data at points along a spline without modifying the Spline class.

```csharp
// Attach width data to a spline
SplineData<float> widthData = new SplineData<float>();
widthData.Add(new DataPoint<float>(0f, 0.5f));   // at t=0, width=0.5
widthData.Add(new DataPoint<float>(0.5f, 1.0f)); // at t=0.5, width=1.0
widthData.Add(new DataPoint<float>(1f, 0.5f));    // at t=1, width=0.5

// Evaluate interpolated width at any t
float width = widthData.Evaluate(spline, 0.25f, PathIndexUnit.Normalized, new LerpFloat());
```

Supports custom interpolators for any type T. The `PathIndexUnit` parameter controls whether data point indices are distance-based, normalized, or knot-based.

### Belt Application

Could store per-point belt width, height offset, or speed multiplier. Not critical for v1 implementation but useful for variable-width belts or terrain-following later.

---

## 9. Knot Tangent Modes

### Available Modes

| Mode | Behavior | Tangent Control |
|------|----------|-----------------|
| **Linear** | Straight segments. Tangent length = 0, points directly at neighbors. Sharp corners. | Automatic, not editable |
| **Auto** (Catmull-Rom) | Smooth curves computed from neighboring knot positions. | Automatic, not editable |
| **Bezier Mirrored** | Tangents opposite direction, equal length. | Manual, symmetric |
| **Bezier Continuous** | Tangents opposite direction, independent lengths. | Manual, directional lock |
| **Bezier Broken** | Tangents fully independent (direction and length). | Manual, full control |

### Recommendation for Conveyor Belts

**Bezier Continuous** or **Bezier Broken** is best for belts because:

1. Belt tangent directions come from machine port forward vectors (known, explicit)
2. Tangent magnitudes control curve tightness (needs tuning per connection)
3. Auto/Catmull-Rom ignores port directions -- bad for belts
4. Linear gives no curves at all

When creating belt splines programmatically, set knot tangents explicitly from port data:
```csharp
var spline = new Spline();
spline.Add(new BezierKnot(startPos, -startDir * tangentScale, startDir * tangentScale, quaternion.identity));
spline.Add(new BezierKnot(endPos, -endDir * tangentScale, endDir * tangentScale, quaternion.identity));
```

This matches the Hermite-to-Bezier approach in `curved-belt-research.md` Section 6, but using Unity's native BezierKnot struct instead of a custom BeltCurve struct.

---

## 10. Sample Projects and Official Resources

### Package Samples (Import via Package Manager)

The Splines package includes importable "Spline Examples" demonstrating:
- Road creation along a spline
- GameObject animation along a spline
- Prefab instantiation along a spline (environments)

Import via: Window > Package Manager > Splines > Samples > Import.

### Official Unity Blog

Unity published "Building better paths with Splines" covering the three main components (SplineExtrude, SplineInstantiate, SplineAnimate) with use cases for roads, rivers, camera tracks, fences, and trees.

### Open Source Mirror

The package source is mirrored at https://github.com/needle-mirror/com.unity.splines for code inspection. Key files:
- `Runtime/SplineExtrude.cs` -- mesh generation component
- `Runtime/SplineInstantiate.cs` -- object placement
- `Runtime/SplineAnimate.cs` -- object movement
- `Runtime/SplineMesh.cs` -- static mesh utility

### GPU Spline Deformation (Third-Party, Open Source)

https://github.com/RoyTheunissen/GPU-Spline-Deformation
Bakes spline deformation to texture, applies via vertex shader. Performant for static splines, not ideal for runtime-changing paths.

---

## 11. Performance Summary

### One-Time Costs (Belt Placement)

| Operation | Cost | Notes |
|-----------|------|-------|
| Create Spline (2-4 knots) | Microseconds | Just struct allocation |
| SplineExtrude mesh generation | Low milliseconds | Depends on segments. 20 seg/unit * 10m belt = 200 segments. ~1ms estimate. |
| SplineInstantiate (rollers) | Low milliseconds | Prefab instantiation cost |
| Arc-length LUT cache build | Automatic | Built on first evaluation, cached thereafter |

### Per-Frame Costs (Runtime)

| Operation | Cost per Call | Notes |
|-----------|-------------|-------|
| ConvertIndexUnit (distance to t) | ~microseconds | Binary search on cached LUT |
| EvaluatePosition | ~microseconds | Cubic polynomial evaluation |
| 1000 item position updates | Sub-millisecond | 100 belts * 10 items |

### Key Optimizations in Recent Versions

- **v2.6.0:** Eliminated GC allocations in SplineContainer evaluation
- **v2.6.0:** Internal LUT caching for distance-to-interpolation
- **v2.7.1:** New profile types (Road profile = instant flat belt mesh)
- **v2.8.0:** Multiple SplineExtrude instances no longer conflict

---

## 12. Implications for Slopworks Belt Implementation

### What Unity Splines Gives Us For Free

1. **Belt mesh generation:** SplineExtrude with Road profile or custom Spline Profile. One component, no custom mesh code.
2. **Arc-length parameterization:** `ConvertIndexUnit` with cached LUTs. No custom ArcLengthTable needed.
3. **Belt item positioning:** `ConvertIndexUnit` + `EvaluatePosition` per item per frame. Fast enough for factory scale.
4. **Roller/strut placement:** SplineInstantiate with Spline Distance mode. Automatic orientation.
5. **Belt preview during placement:** Create temporary Spline, attach SplineExtrude, update knots each frame.
6. **Collider sync:** SplineExtrude auto-updates MeshCollider when `UpdateColliders` is enabled.

### What We Still Need Custom

1. **Hermite-to-BezierKnot conversion:** Port positions/directions to BezierKnot tangents (trivial).
2. **Belt simulation integration:** Distance-offset items map to `ConvertIndexUnit(distance, PathIndexUnit.Distance)`.
3. **UV scroll shader:** SplineExtrude generates the mesh with UVs, but scrolling animation needs a custom shader or Shader Graph node.
4. **Network sync:** Sync BezierKnot data (position + tangents per knot, ~48 bytes for 2-knot belt).

### Recommended Approach (Revised)

The `curved-belt-research.md` Section 10 recommended a custom `BeltCurve` struct + custom mesh generation + custom `ArcLengthTable`. With the Unity Splines package (especially v2.7+), **most of that custom code is unnecessary:**

1. **Spline creation:** `new Spline()` with `BezierKnot` entries from port data
2. **Mesh generation:** `SplineExtrude` component with Road profile (or Spline Profile for custom cross-section)
3. **Item positioning:** `SplineUtility.ConvertIndexUnit` + `EvaluatePosition` (replaces custom ArcLengthTable)
4. **Roller placement:** `SplineInstantiate` component
5. **Preview:** Same SplineExtrude component, update knot positions in Update()

The custom-code approach from the earlier research is still valid as a fallback if the Splines package introduces unexpected issues, but the package-first approach should be tried first -- it is significantly less code to write and maintain.

---

## Sources

- [SplineExtrude Component Reference (v2.8)](https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/extrude-component.html)
- [Runtime Extrusion Example (v2.8)](https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/extrude-runtime.html)
- [SplineMesh API (v2.6)](https://docs.unity3d.com/Packages/com.unity.splines@2.6/api/UnityEngine.Splines.SplineMesh.html)
- [ISplineVertexData Interface (v2.0)](https://docs.unity3d.com/Packages/com.unity.splines@2.0/api/UnityEngine.Splines.SplineMesh.ISplineVertexData.html)
- [SplineInstantiate Component (v2.4)](https://docs.unity3d.com/Packages/com.unity.splines@2.4/manual/instantiate-component.html)
- [SplineInstantiate API (v2.0)](https://docs.unity3d.com/Packages/com.unity.splines@2.0/api/UnityEngine.Splines.SplineInstantiate.html)
- [SplineUtility API (v2.6)](https://docs.unity3d.com/Packages/com.unity.splines@2.6/api/UnityEngine.Splines.SplineUtility.html)
- [CurveUtility API (v2.6)](https://docs.unity3d.com/Packages/com.unity.splines@2.6/api/UnityEngine.Splines.CurveUtility.html)
- [SplineAnimate API (v2.5)](https://docs.unity3d.com/Packages/com.unity.splines@2.5/api/UnityEngine.Splines.SplineAnimate.html)
- [SplineAnimate.Method Enum (v2.7)](https://docs.unity3d.com/Packages/com.unity.splines@2.7/api/UnityEngine.Splines.SplineAnimate.Method.html)
- [Tangent Modes (v2.5)](https://docs.unity3d.com/Packages/com.unity.splines@2.5/manual/tangent-modes.html)
- [Changelog (v2.8)](https://docs.unity3d.com/Packages/com.unity.splines@2.8/changelog/CHANGELOG.html)
- [Changelog (v2.6)](https://docs.unity3d.com/Packages/com.unity.splines@2.6/changelog/CHANGELOG.html)
- [SplineSlice Performance (v2.7)](https://docs.unity3d.com/Packages/com.unity.splines@2.7/api/UnityEngine.Splines.SplineSlice-1.html)
- [SplineData API (v2.0)](https://docs.unity3d.com/Packages/com.unity.splines@2.0/api/UnityEngine.Splines.SplineData-1.html)
- [Unity Blog: Building Better Paths with Splines](https://unity.com/blog/engine-platform/building-better-paths-with-splines-in-2022-2)
- [Package Source Mirror (GitHub)](https://github.com/needle-mirror/com.unity.splines)
- [GPU Spline Deformation (Roy Theunissen)](https://github.com/RoyTheunissen/GPU-Spline-Deformation)
- [SplineExtrude Source Code](https://github.com/needle-mirror/com.unity.splines/blob/master/Runtime/SplineExtrude.cs)
