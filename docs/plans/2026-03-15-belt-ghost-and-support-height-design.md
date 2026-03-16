# Belt Ghost Preview and Support Height Design

**Date:** 2026-03-15
**Branch:** kevin/belts
**Status:** Approved

## Problem

1. Belt placement preview uses a 30-point line renderer that poorly approximates curves. The actual belt mesh is much higher fidelity. Players can't tell exactly where the belt will go.

2. Belt supports are fixed height. Players can't raise or lower belts to create elevation changes between buildings or route belts over obstacles.

## Feature 1: Support height adjustment

PgUp/PgDn adjusts support height during belt placement in 0.5m steps.

### How it works

- New field `_supportHeightOffset` (float, default 0) on NetworkBuildController
- PgUp increments by 0.5, PgDn decrements by 0.5 (clamped to min 0)
- Belt endpoint Y = ground Y + SupportAnchorHeight + _supportHeightOffset
- Offset resets to 0 when placement finishes or tool changes

### Support ghost scaling

The BELT SUPPORT prefab hierarchy:
- `Extrusion [3155]` -- cylinder pole (pivot at bottom, Y scale stretches upward)
- `Extrusion [3029]` -- rectangular top piece (localPosition.y = -0.075)
- `BeltSnapAnchor` -- connection point (localPosition.y = 1)

When height offset changes:
- Cylinder: `localScale.y = 1 + (_supportHeightOffset / defaultCylinderHeight)`
- Top piece: `localPosition.y = defaultTopY + _supportHeightOffset`
- Anchor: `localPosition.y = defaultAnchorY + _supportHeightOffset`

The cylinder pivot is at the bottom so Y scaling only extends upward. No position compensation needed.

### Server side

No changes to CmdPlaceBelt. The adjusted height is already baked into the start/end positions sent to the server. SpawnSupportAt receives the ground position and spawns the support there -- it will need to also scale the cylinder and offset the top piece to match the height the client used.

## Feature 2: Belt ghost mesh preview

Replace the line renderer with the actual belt mesh during placement.

### How it works

- During belt dragging, call `BeltSplineMeshBaker.BakeMesh` on a ghost GameObject using the same waypoints the line renderer currently uses
- Apply semi-transparent ghost material (green for valid, red for invalid) instead of the belt's real material
- Rebuild every frame as the player drags
- If performance is an issue, add a distance/angle threshold check before rebuilding (skip if endpoint hasn't moved since last bake)

### Why this fixes the curve mismatch

The line renderer samples the route at 30 evenly-spaced t values, which poorly approximates tight Bezier curves. The mesh baker uses the spline knots directly with much higher fidelity. Both use the same `BeltRouteBuilder.Build()` waypoints -- the only difference is rendering resolution. Using the mesh eliminates the approximation entirely.

### Ghost lifecycle

- Create ghost GameObject with MeshFilter + MeshRenderer when entering Dragging state
- Bake mesh each frame using current waypoints
- Destroy ghost when placement finishes, cancels, or tool changes
- Keep the line renderer as a fallback (can remove later once ghost mesh is proven)
