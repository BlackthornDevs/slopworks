# Unified Placement System

Date: 2026-03-07
Status: Approved
Branch: kevin/multiplayer-step1

## Problem

The current placement system has three separate code paths for foundations, walls, and ramps. Each uses different raycasting, different cell snapping, and different position formulas. A uniform 1x1 cube placed by each tool ends up at a different location on the grid. Adding new building types means writing yet another special-case positioning path.

## Decision

Replace the per-type placement logic with a two-mode system inspired by Satisfactory's buildable hologram pattern:

1. **Grid mode** (crosshair hits terrain) -- all building types use the same position formula
2. **Snap mode** (crosshair hits existing building) -- ghost attaches to the nearest snap point on the hit building

Snap points are defined on the prefab, not the build tool. The build tool is type-agnostic.

## Pivot Convention

All building prefabs use **center-bottom pivot**: origin at the center of the bottom face of the bounding box. Enforced at FBX export time in Blender. The placement math depends on this -- if a prefab's pivot is elsewhere, positioning breaks.

## Grid Placement (Mode 1)

When the raycast hits terrain:

- Crosshair hit point determines placement
- Object's center-bottom snaps to the nearest grid-aligned position for its footprint size
- A 4x4 foundation snaps to the nearest 4m grid intersection
- A 1x1 machine snaps to the nearest 1m cell center
- Thin objects (walls, 4x0.5): the long axis snaps to cell boundaries normally, the **back face** snaps flush to the nearest grid line (not centered on it)
- Y = terrain hit point Y + nudge offset
- Rotation from R key (0/90/180/270)

One formula for all building types. The only variable is the prefab's footprint size.

## Snap-to-Building Placement (Mode 2)

When the raycast hits an existing building:

1. Get all `BuildingSnapPoint` components on the hit building via `GetComponentsInChildren<BuildingSnapPoint>()`
2. Find the snap point closest to `hit.point`
3. Position the ghost so its matching face is flush against the snap point:
   - Offset = `snapPoint.position + snapPoint.normal * incomingHalfDepthAlongNormal`
4. Rotation auto-aligns to the snap point's normal; R key still cycles
5. Nudge offset applies on top, along the snap normal

### Examples

**Foundation next to foundation:** Snap point at center of north face, normal (0,0,1). New foundation offset by half its depth (2m) along normal. Result: flush adjacent, same Y.

**Wall on foundation edge:** Snap point at center of north face, normal (0,0,1). Wall offset by half its depth (0.25m) along normal. Result: wall's back face flush with foundation edge.

**Foundation on top of foundation:** Snap point at center of top face, normal (0,1,0). New foundation's center-bottom sits directly on the snap point. Result: stacked.

### Activation

Automatic based on raycast target. No key press. Terrain = grid mode. Building = snap mode.

### Snap point selection

Pure proximity -- nearest snap point to hit.point wins. No filtering by building type. If the placement is physically invalid (overlapping), the ghost turns red.

## BuildingSnapPoint Component

MonoBehaviour placed on child GameObjects of building prefabs:

```
BuildingSnapPoint : MonoBehaviour
    Normal (Vector3) -- outward direction, defaults to transform.forward
    SurfaceSize (Vector2) -- width and height of attachment area
```

Position comes from the child GameObject's transform.position at runtime.

### Auto-generation fallback

When a building spawns with zero BuildingSnapPoint children, `BuildingSnapPoint.GenerateFromBounds(go)` creates them from renderer bounds:

- 4 cardinal faces (north, south, east, west) + top = 5 snap points
- Each at face center, normal facing outward
- SurfaceSize from face dimensions

Manual snap points on the prefab skip auto-generation entirely.

### What this replaces

- `SnapPointRegistry` -- removed. Snap points live on GameObjects, discovered by raycast + GetComponentsInChildren
- `SnapPoint.cs` (pure C# class) -- replaced by BuildingSnapPoint MonoBehaviour
- `SnapPointType` enum -- removed. All snap points are uniform

## NetworkBuildController Changes

### One raycast replaces three

```
RaycastPlacement(out hit, out mode)
    mode = Grid (hit terrain) or Snap (hit building with BuildingSnapPoint)
```

Replaces: `RaycastGrid()`, `RaycastWallPlacement()`, `GetFacingEdgeDirection()`

### One build handler replaces many

Per-tool handlers (`HandleFoundationInput`, `HandleWallInput`, `HandleRampInput`) collapse into one `HandleBuildInput`. The selected tool only determines which prefab category and variant to use. Ghost positioning is identical for all types.

### What stays

- Tool selection (1-6 keys)
- Variant cycling (Tab)
- Nudge (PgUp/PgDn, 1m default, 0.5m with Shift, resets on tool/mode switch)
- Zoop for batch placement
- Delete mode (raycast + despawn)
- Ghost color (green valid, red invalid)

## GridManager Changes

### Unified position method

```csharp
GetPlacementPosition(Vector3 hitPoint, GameObject prefab, int rotation)
    -> (Vector3 position, Quaternion rotation)
```

Replaces: `GetFoundationPlacementPos()`, `GetDirectionalPlacement()`, `GetPlacementPos()`

### Snap point generation on spawn

After instantiating a building, if no BuildingSnapPoint children exist, auto-generates them from bounds. Every placed building immediately has snap points for subsequent placements.

### ServerRpcs

CmdPlace, CmdPlaceDirectional, CmdPlaceBelt stay. Server uses the same unified position formula as the client ghost.

## What Stays Unchanged

- **FactoryGrid** -- cell occupancy, CanPlace, Place, Remove
- **PlacementInfo** -- on every spawned building for raycast identification
- **BuildingCategory** -- Foundation, Wall, Ramp, Machine, Storage, Belt
- **Ghost system** -- EnsurePrefabGhost, pools, coloring
- **FishNet authority** -- client shows ghost, server validates and spawns
- **Factory automation** -- PortNode for machine I/O is separate from BuildingSnapPoint. Structural snapping and item flow snapping coexist as different systems

## Out of Scope

**Belt placement** is handled separately. Belts need free-form routing on surfaces (sub-grid snapping, point-to-point), not the structural grid/snap system described here. Belt design is a separate pass.
