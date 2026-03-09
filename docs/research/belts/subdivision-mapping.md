# Belt Subdivision Mapping for Curved Belts

Research compiled 2026-03-09.

## Decision: 100 Subdivisions Per Meter of Arc Length

Current system: `SubdivisionsPerTile = 100`, tiles are ~1m. For curved belts, "tile" becomes "meter of arc length." The value (100) stays the same, the meaning shifts from grid-based to distance-based.

This preserves all existing speed math: at belt speed 2, 50Hz FixedUpdate = 1 m/s throughput.

## BeltSegment Changes Required

### Constructor
Add factory method accepting arc length:
```csharp
public static BeltSegment FromArcLength(float arcLengthMeters)
{
    int totalSubs = Mathf.RoundToInt(arcLengthMeters * BeltItem.SubdivisionsPerTile);
    return new BeltSegment(totalSubs);
}
```

### Everything Else: No Changes
- `Tick()` -- purely subdivision-based, geometry-agnostic
- `TryInsertAtStart()` -- same
- `TryExtractFromEnd()` -- same
- `GetItemPositions()` -- returns normalized [0,1] values, unchanged
- Length invariant holds: totalLength = sum(distanceToNext) + terminalGap

## Rendering Layer Changes

Replace `Vector3.Lerp(start, end, t)` with `spline.EvaluatePosition(t)`:
- `BeltSegmentBehaviour.UpdateItemVisuals()` -- spline eval instead of lerp
- `NetworkBeltSegment.GetItemWorldPositions()` -- same change
- `BeltItemVisualizer` -- same change

## ushort Overflow

Not a concern. ushort max = 65,535. At 100 subs/m:
- 20m belt = 2,000 subdivisions (safe)
- Would need 655m belt to overflow (impossible in practice)
- `_totalLength` is `int`, not `ushort` -- stores true length
- `distanceToNext` (ushort) only holds gaps between items, always < totalLength

## Files Requiring Changes

| File | Change | Breaking? |
|------|--------|-----------|
| BeltSegment.cs | Add arc-length constructor | No |
| BeltItem.cs | Optional: rename SubdivisionsPerTile to SubdivisionsPerMeter | No |
| BeltSegmentBehaviour.cs | Lerp -> spline eval | No |
| NetworkBeltSegment.cs | Lerp -> spline eval, accept spline in ServerInit | No |
| GridManager.cs | Pass arc length to BeltSegment constructor | No |
| BeltNetwork.cs | No changes | N/A |
| FactorySimulation.cs | No changes | N/A |
| BeltInputAdapter.cs | No changes | N/A |
| BeltOutputAdapter.cs | No changes | N/A |

## Factorio Comparison

Factorio avoids arc length entirely: every segment is exactly one 1x1 tile. "Curves" are 90-degree turns within a single tile. Each tile uses 256 positions (vs our 100). No continuous arc length calculation needed.

Our approach (long continuous segments with arc-length subdivisions) is different but valid. Trade-off: fewer BeltSegment objects and BeltNetwork connections, but requires spline evaluation for rendering.
