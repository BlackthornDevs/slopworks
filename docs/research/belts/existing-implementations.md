# Existing Belt Implementations

Research compiled 2026-03-09. Survey of existing conveyor belt implementations in Unity and elsewhere.

## theor.xyz DOTS Implementation

Blog post: https://theor.xyz/dots-burst-satisfactory-belts/
GitHub: https://github.com/theor/Automation

Focus: simulation throughput only (10M items at 10ms/frame). NOT visual curves.

Data structure:
- BeltSegment with Next/Prev entity chain
- BeltItem with ushort Distance and byte ItemType
- Items use straight-line interpolation between segment endpoints

Rendering: Graphics.DrawMeshInstancedProcedural
- No mesh generation, no curved paths, no placement UX
- Pure performance benchmark for the distance-offset simulation model

Key insight: validates that distance-offset model scales to millions of items.

## Factorio Belt Optimization

Blog post: https://factorio.com/blog/post/fff-176

The gold standard for belt simulation optimization.

Key technique: gap-based data (distances between items, not absolute positions).
- Adjacent segments merge into "transport lines" sharing one item array
- Most ticks only update two terminal gap integers, not individual items
- 50-100x improvement for item movement
- Curved and straight belts merge together with no differentiation

Directly validates Slopworks' existing BeltSegment.Tick() approach (O(1) tick via terminalGap + first item distance).

## Satisfactory Rendering Pipeline

From modding docs: https://docs.ficsit.app/satisfactory-modding/latest/Development/Satisfactory/ConveyorRendering.html

- Belt meshes: instanced spline meshes drawn by custom shader
- Items: positioned via global texture that Factory_Baked shader reads
- World Position Offset generated per-item on GPU
- Pre-Update 5: three LOD material instances per item type
- Post-1.1: conveyor items use Nanite
- 56m max segment length tied to texture resolution

## Open Source References

No single repo implements the full Satisfactory-style curved belt pipeline.

### Closest references:

**yasirkula/UnityBezierSolution**
- GitHub: https://github.com/yasirkula/UnityBezierSolution
- Bezier spline with constant-speed traversal
- Useful for the arc-length parameterization approach

**AdultLink/TexturePanner**
- GitHub: https://github.com/AdultLink/TexturePanner
- Shader for belt surface scrolling animation
- UV offset approach for animated belt texture

**elrod/Unity-SplineRoadUtils**
- Road mesh generation using Unity Splines package
- Custom cross-section extrusion (not just tubes)
- Closest to what we need for belt mesh generation

### Educational resources:

**Freya Holmer -- Procedural Geometry Stream**
- 6.5-hour stream on procedural mesh generation in Unity
- Covers spline evaluation, mesh extrusion, UV mapping
- Best single educational resource for this problem

**Catlike Coding -- Curves and Splines**
- Tutorial: https://catlikecoding.com/unity/tutorials/curves-and-splines/
- Covers Bezier curves, spline evaluation, editor tools
- Good introduction to the math

**Habrador -- Move Along Curve**
- Tutorial: https://www.habrador.com/tutorials/interpolation/3-move-along-curve/
- Covers arc-length parameterization for constant-speed movement

**A Primer on Bezier Curves**
- https://pomax.github.io/bezierinfo/
- Comprehensive reference on all Bezier math

## Hermite vs Bezier for Belts

Mathematically equivalent. Hermite is the natural authoring format for belts:
- Port position + port direction = endpoint + tangent (Hermite form)
- Convert to Bezier for evaluation: B1 = P0 + T0/3, B2 = P1 - T1/3

Tangent magnitude: 0.33 * distance is a good default. 0.5 for rounder curves.

## Mesh Generation Approach

Standard algorithm: cross-section sweep.
1. Define 2D profile (belt shape: flat top, sides, bottom)
2. Sample spline at N arc-length-equidistant points
3. Build coordinate frame at each sample (tangent + projected world-up for horizontal belts)
4. Place profile vertices at each frame
5. Stitch quads between adjacent rings
6. UV.v maps to accumulated arc length for scrolling animation

## Item Movement on Curves

Recommended: pre-sampled polyline lookup table.
- ~50 samples per belt, ~600 bytes
- Build once at placement time
- O(1) per item per frame via binary search + lerp
- Existing distance-offset simulation unchanged -- distance = arc length

## Sources

- [theor.xyz DOTS belts](https://theor.xyz/dots-burst-satisfactory-belts/)
- [Factorio Friday Facts #176](https://factorio.com/blog/post/fff-176)
- [Satisfactory Conveyor Rendering](https://docs.ficsit.app/satisfactory-modding/latest/Development/Satisfactory/ConveyorRendering.html)
- [Catlike Coding - Curves and Splines](https://catlikecoding.com/unity/tutorials/curves-and-splines/)
- [A Primer on Bezier Curves](https://pomax.github.io/bezierinfo/)
- [Habrador - Move Along Curve](https://www.habrador.com/tutorials/interpolation/3-move-along-curve/)
- [CMU - Bezier/Hermite Conversion](http://15462.courses.cs.cmu.edu/fall2020/article/10)
