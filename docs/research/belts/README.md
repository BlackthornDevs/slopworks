# Belt System Research

Research compiled 2026-03-09 for Slopworks conveyor belt implementation.
Modeled after Satisfactory's conveyor belt system.

## Files

### Satisfactory Reference
| File | Contents |
|------|----------|
| `satisfactory-belt-mechanics.md` | Core mechanics, item transport, distance-offset model, constraints |
| `satisfactory-supports-and-structure.md` | Conveyor poles/supports, segment decomposition, connection types |
| `satisfactory-build-modes.md` | Default vs Straight vs Curve placement modes |
| `satisfactory-placement-ux.md` | Frame-by-frame placement workflow, preview, validation feedback |

### Technical Implementation
| File | Contents |
|------|----------|
| `spline-math.md` | Cubic Hermite/Bezier evaluation, arc-length parameterization, RMF, mesh generation |
| `unity-splines-evaluation.md` | Unity Splines package analysis -- initial verdict reversed after deep dive |
| `unity-procedural-geometry.md` | Unity Splines v2.6-2.8 deep dive: SplineExtrude profiles, ConvertIndexUnit, SplineInstantiate |
| `existing-implementations.md` | theor.xyz DOTS, Factorio optimization, open-source references |

### Integration Analysis
| File | Contents |
|------|----------|
| `subdivision-mapping.md` | How integer subdivisions map to arc length (100 subs/meter, no sim changes) |
| `fishnet-serialization.md` | FishNet has built-in Unity.Mathematics serializers, BezierKnot custom serializer, sync patterns |
| `belt-item-interaction.md` | Item pickup prevention, raycast-belt-resolve-item approach, GPU instancing future |
| `splineextrude-performance.md` | SplineExtrude at scale: bake-and-destroy pattern, static batching, preview strategy |
| `design-decisions.md` | Settled decisions: segmentation, support independence, item interaction, sim layer |

### Project State
| File | Contents |
|------|----------|
| `existing-codebase.md` | Current Slopworks belt code inventory and multiplayer status |

## Key Decision Point

The research surfaced two viable approaches:

**Option A: Unity Splines package (v2.7+)**
- SplineExtrude with Road/Spline profile for mesh generation
- ConvertIndexUnit with cached LUTs for arc-length parameterization
- SplineInstantiate for roller/strut placement
- Less custom code, maintained by Unity
- Risk: package perf at factory scale (hundreds of belts), FishNet serialization for BezierKnot

**Option B: Hand-rolled cubic Hermite/Bezier**
- Custom mesh generation via cross-section sweep + RMF
- Custom arc-length lookup table (binary search)
- Full control, no package dependency
- More code to write and maintain, but proven at scale (theor.xyz, Factorio)

Both approaches share the same simulation layer (existing BeltSegment distance-offset model).
The recommendation is to try Option A first and fall back to Option B if issues arise.
