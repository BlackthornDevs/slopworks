# Snap Point System Design

## How it works

Every building prefab has child GameObjects with a `BuildingSnapPoint` component. Each snap point defines:
- **Position**: local offset from the prefab root, placed at the center of a face
- **Normal**: outward direction of that face (stored as `_normalOverride`, transformed by parent rotation at runtime via `transform.TransformDirection`)
- **SurfaceSize**: width and height of the attachment area

When the player's crosshair raycast hits an existing building (layer 13, Structures), `BuildingSnapPoint.FindNearest()` returns the closest snap point to the hit position. The placement system then offsets the new building along that normal by half the incoming prefab's depth, so the two buildings sit edge-to-edge.

## Normal direction and rotation

`_normalOverride` is stored in **local space** (e.g., `(0,0,1)` for north). At runtime, the `Normal` property returns `transform.TransformDirection(_normalOverride)`, which rotates the normal with the building. A foundation placed at 180 degrees has its "north" snap point physically at the south side, and its normal correctly points south.

This means snap normals are always correct regardless of building rotation. No special handling needed per rotation.

## Placement modes

| Mode | Trigger | Position calculation |
|------|---------|---------------------|
| **Grid** | Raycast hits Terrain (layer 12) | `Mathf.Round(hit.x/z)` centers on 1m grid, Y from terrain + baseOffset |
| **Snap** | Raycast hits Structure (layer 13) with snap points | Offset along snap normal by half-depth, Y from `surfaceY + baseOffset` |

## Surface Y tracking

`surfaceY` is the Y of the ground/surface the building sits on. It determines vertical alignment:
- **Side snap**: `surfaceY = existingBuilding.PlacementInfo.SurfaceY` (same ground level)
- **Top snap**: `surfaceY = existingBuilding.SurfaceY + existingBuilding.ObjectHeight` (top of existing)
- **Grid**: `surfaceY = terrain hit.point.y`

Buildings placed side-by-side on the same surface share the same `surfaceY`, so their bottoms align.

## Base offset calculation

`GetPrefabBaseOffset(prefab)` handles both mesh origins:
- **Center-origin** (Unity cubes): `extents.y - center.y = 0.5 - 0 = 0.5` (offset up by half-height)
- **Center-bottom** (Revit FBX): `extents.y - center.y = h/2 - h/2 = 0` (origin already at bottom)

All Y positioning uses: `posY = surfaceY + baseOffset`

---

## Current prefabs and their snap points

All structural prefabs have 5 snap points: North (+Z), South (-Z), East (+X), West (-X), Top (+Y).

| Prefab | Dimensions (W x H x D) | Category | Notes |
|--------|------------------------|----------|-------|
| SLAB_1m | 4 x 1 x 4 | Foundation | Standard floor slab |
| SLAB_2m | 4 x 2 x 4 | Foundation | Double-height slab |
| SLAB_4m | 4 x 4 x 4 | Foundation | Full cube slab |
| WALL_0.5m | 4 x 4 x 0.5 | Wall | Thin wall panel |
| RAMP 4x1 | 4 x 1 x 4 | Ramp | Shallow ramp |
| RAMP 4x2 | 4 x 2 x 4 | Ramp | Steep ramp |

---

## Placement scenarios

### 1. Foundation next to foundation (side snap)

Player looks at an existing foundation's side face. Nearest snap point is on that face. New foundation is offset along the normal by its own half-depth (2m for a 4m foundation). Both share the same `surfaceY`.

**Result**: Edge-to-edge, same height. Works for all 4 cardinal directions.

**Snap points needed**: North, South, East, West on all foundations.

### 2. Foundation on top of foundation (top snap)

Player looks at the top face. Nearest snap is the Top point. New foundation is placed with `posY = topSnapY + baseOffset`. The `surfaceY` is set to `existing.SurfaceY + existing.ObjectHeight`.

**Result**: Stacked vertically, bottom of new sits on top of existing.

**Snap points needed**: Top on all foundations.

### 3. Wall on foundation edge (side snap)

Player looks at a foundation's side face. Wall (4x4x0.5) is offset along the normal by its half-depth (0.25m). Wall height centers at `surfaceY + baseOffset` (wall is 4m tall, so center is 2m up from surface).

**Result**: Wall flush against foundation edge, bottom aligned with foundation bottom.

**Snap points needed**: North, South, East, West on foundations.

### 4. Wall on top of wall (top snap)

Player looks at an existing wall's top face. New wall stacks vertically.

**Result**: Second story wall.

**Snap points needed**: Top on walls.

### 5. Wall next to wall (side snap, along the wall)

Player looks at a wall's side face (the narrow 0.5m edge). New wall is offset along that narrow direction.

**Current issue**: The narrow edge snap points (East/West on a wall) would offset by half the new wall's depth (0.25m), placing walls 0.5m apart. But walls running in a line should share a face, not have a gap.

**Question**: Do we want walls to snap side-by-side along their length (forming a continuous wall run)? If so, walls need snap points on their LONG faces too, with normals pointing along the wall's length, not perpendicular to the thin face. Or the East/West snaps need special handling for same-type snapping.

### 6. Foundation next to wall (snap from wall to place foundation)

Player looks at a wall's thick face (4m wide). Nearest snap is the front/back face. Foundation is offset along that normal by its own half-depth (2m).

**Result**: Foundation placed adjacent to wall.

**Snap points needed**: North, South (front/back) on walls.

### 7. Ramp on foundation edge

Player looks at a foundation's side face. Ramp is offset along the normal. Ramp auto-rotates to face along the normal direction (autoYaw from snap normal).

**Current issue**: Ramps are directional -- they go UP in one direction. The auto-yaw from the snap normal rotates the ramp to face outward from the foundation. But the player might want the ramp going left or right along the edge, not outward.

**Question**: Should ramp rotation be overridable via the R key even in snap mode? Currently autoYaw is always applied for horizontal snaps.

### 8. Ramp next to ramp

Two ramps side by side forming a wider ramp path.

**Snap points needed**: East, West on ramps (side-by-side). Top for stacking.

### 9. Wall on ramp edge

A wall placed along the edge of a ramp to form a railing.

**Snap points needed**: North, South, East, West on ramps. Wall auto-rotates to face along the ramp's edge normal.

### 10. Machine/Storage on foundation top

Player looks at a foundation's top face. Machine or storage is placed on top.

**Current state**: Machines and Storage don't have snap points on their prefabs. They use grid placement only. The top snap of a foundation would still work for placing them.

**Question**: Should machines/storage have their own snap points for snapping to each other or to walls?

### 11. Belt endpoints on machine/storage faces

Belts connect to machines and storage. Currently belt placement is a separate 2-click system (start cell, end cell) that doesn't use snap points.

**Question**: Should belt endpoints snap to machine/storage faces in the future?

---

## What's missing from prefabs

### Bottom snap point

No prefab has a Bottom (-Y) snap point. This would be needed for:
- Hanging something from the underside of a foundation (lights, pipes)
- Placing a wall below a foundation edge

**Recommendation**: Add if/when underside attachment is needed. Not critical for basic building.

### Wall length-wise snaps

Walls are 4x4x0.5. The current East/West snaps are on the 0.5m-thick edges. Snapping wall-to-wall along their length (forming a continuous wall line) would require the offset to match the wall's WIDTH (2m), not its thin depth (0.25m).

**Options**:
1. Add extra snap points at the left/right ends of the wall's front face, with normals pointing left/right along the wall. These would use the wall's half-width as offset.
2. Handle wall-to-wall specially in code: detect when snapping same-category and use width instead of depth.
3. Accept that wall lines are placed via zoop on foundation edges, not by snapping wall-to-wall.

### Ramp directional snaps

Ramps go up in one direction. The top edge and bottom edge of the ramp slope are different heights. Current snap points are at the bounding box faces (center height), which doesn't account for the slope.

**Options**:
1. Add slope-aware snap points: "TopEdge" at the high side, "BottomEdge" at the low side, with appropriate Y positions.
2. Keep current snap points and rely on manual nudge (PgUp/PgDn) for ramp-to-ramp height matching.

### Machine/Storage snap points

Currently no snap points on Machine or Storage prefabs. These are placed via grid only.

**Future consideration**: Add front/back/side snaps for machines to enable direct machine-to-machine or machine-to-belt snapping.

---

## Decisions

1. **Wall-to-wall length-wise snapping**: YES -- needed for continuous wall runs.
   - PLAN: Add snap points at the left/right ends of the wall face. Wall is 4m wide, so these sit at +/-2 on the wall's local X axis with normals pointing left/right. When snapping wall-to-wall, the offset uses the incoming wall's half-width (2m), producing a flush 4m+4m run.

2. **Ramp rotation override**: YES -- R key overrides autoYaw in snap mode.
   - PLAN: In `GetSnapPlacementPosition`, when `rotationDeg != 0`, use `rotationDeg` instead of `autoYaw` for horizontal snaps. This lets the player aim at a foundation edge, then press R to rotate the ramp along the edge instead of outward.

3. **Ramp slope-aware snaps**: YES -- high edge and low edge snaps needed.
   - PLAN: Add "HighEdge" and "LowEdge" snap points on ramp prefabs. These sit at the top and bottom of the slope with Y positions matching the actual ramp surface height at that edge (not bounding box center). This lets ramp-to-foundation and ramp-to-ramp connections match height correctly without manual nudge.

4. **Bottom snap points**: YES -- needed for underside building.
   - PLAN: Add Bottom (-Y) snap points to foundations and ramps. Enables hanging walls, lights, or other structures from the underside. `surfaceY` for bottom snaps = `existing.SurfaceY` (the underside level).

5. **Machine/Storage snaps**: YES, deferred.
   - Will add snap points to Machine and Storage prefabs when we reach the automation multiplayer step (Step 4). Input/output faces for belt connections.

6. **Belt snap integration**: Deferred.
   - Future system: input/output node GameObjects on machines/storage will act as belt snap targets. Not part of the current structural placement work.

---

## Snap point layout per building type

Based on the decisions above, here is the full snap point layout each building type needs.

### Foundations (SLAB_1m, SLAB_2m, SLAB_4m)

Foundations are axis-aligned boxes. Players build from all directions: above, below, and all four sides. Each side face needs snaps at three heights (top edge, center, bottom edge) so the player can target the correct attachment point regardless of camera angle. Standing on top of a foundation and looking over the edge, you hit the top-edge snap. Standing below and looking up at the side, you hit the bottom-edge snap.

**Cardinal snaps (4 directions x 3 heights = 12):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| North_Top | (0, topY, +halfD) | (0, 0, 1) | Snap to north face, top edge |
| North_Mid | (0, centerY, +halfD) | (0, 0, 1) | Snap to north face, center |
| North_Bot | (0, bottomY, +halfD) | (0, 0, 1) | Snap to north face, bottom edge |
| South_Top | (0, topY, -halfD) | (0, 0, -1) | Snap to south face, top edge |
| South_Mid | (0, centerY, -halfD) | (0, 0, -1) | Snap to south face, center |
| South_Bot | (0, bottomY, -halfD) | (0, 0, -1) | Snap to south face, bottom edge |
| East_Top | (+halfW, topY, 0) | (1, 0, 0) | Snap to east face, top edge |
| East_Mid | (+halfW, centerY, 0) | (1, 0, 0) | Snap to east face, center |
| East_Bot | (+halfW, bottomY, 0) | (1, 0, 0) | Snap to east face, bottom edge |
| West_Top | (-halfW, topY, 0) | (-1, 0, 0) | Snap to west face, top edge |
| West_Mid | (-halfW, centerY, 0) | (-1, 0, 0) | Snap to west face, center |
| West_Bot | (-halfW, bottomY, 0) | (-1, 0, 0) | Snap to west face, bottom edge |

**Face center snaps (2):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| Top_Center | (0, topY, 0) | (0, 1, 0) | Stack on top, place machines |
| Bot_Center | (0, bottomY, 0) | (0, -1, 0) | Hang from underside |

That's **14 snap points** per foundation.

`centerY` = bounds center Y in local space. `topY` = center + extents.y. `bottomY` = center - extents.y.

All cardinal snaps share the same outward normal regardless of height -- the height only affects which snap is nearest to the player's crosshair hit point. `FindNearest` picks the closest one, so looking at the top edge of the north face returns `North_Top`, and looking at the bottom edge returns `North_Bot`. The placement result is identical (same normal, same offset), but `surfaceY` derivation could differ based on which height the player targeted.

### Walls (WALL_0.5m)

Walls are thin panels (4w x 4h x 0.5d). Players build walls in runs (left/right), stack them (top), and attach to both faces (front/back). Same multi-height pattern as foundations: top-edge, center, and bottom-edge snaps on each face so the player can target precisely from any angle.

**Front/Back face snaps (2 faces x 3 heights = 6):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| Front_Top | (0, topY, +halfD) | (0, 0, 1) | Attach to front face, top edge |
| Front_Mid | (0, centerY, +halfD) | (0, 0, 1) | Attach to front face, center |
| Front_Bot | (0, bottomY, +halfD) | (0, 0, 1) | Attach to front face, bottom edge |
| Back_Top | (0, topY, -halfD) | (0, 0, -1) | Attach to back face, top edge |
| Back_Mid | (0, centerY, -halfD) | (0, 0, -1) | Attach to back face, center |
| Back_Bot | (0, bottomY, -halfD) | (0, 0, -1) | Attach to back face, bottom edge |

**Left/Right run snaps (2 sides x 3 heights = 6):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| Left_Top | (-halfW, topY, 0) | (-1, 0, 0) | Extend wall run left, top edge |
| Left_Mid | (-halfW, centerY, 0) | (-1, 0, 0) | Extend wall run left, center |
| Left_Bot | (-halfW, bottomY, 0) | (-1, 0, 0) | Extend wall run left, bottom edge |
| Right_Top | (+halfW, topY, 0) | (1, 0, 0) | Extend wall run right, top edge |
| Right_Mid | (+halfW, centerY, 0) | (1, 0, 0) | Extend wall run right, center |
| Right_Bot | (+halfW, bottomY, 0) | (1, 0, 0) | Extend wall run right, bottom edge |

**Face center snaps (2):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| Top_Center | (0, topY, 0) | (0, 1, 0) | Stack wall vertically |
| Bot_Center | (0, bottomY, 0) | (0, -1, 0) | Attach below wall |

That's **14 snap points** per wall.

Note on wall runs: Left/Right normals point along the wall's length axis. When another wall snaps to Left, autoYaw aligns the new wall to face the same direction, so `extents.z` maps to the run direction (2m offset). This should produce flush wall-to-wall runs without code changes.

### Ramps (RAMP 4x1, RAMP 4x2)

Ramps are sloped surfaces. Same multi-height snap pattern on cardinal faces, plus slope-aware HighEdge/LowEdge snaps. The bounding box cardinal snaps handle side-to-side and general attachment. The slope snaps handle ramp-to-foundation and ramp-to-ramp height matching.

**Cardinal snaps (4 directions x 3 heights = 12):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| North_Top | (0, topY, +halfD) | (0, 0, 1) | North face, top edge |
| North_Mid | (0, centerY, +halfD) | (0, 0, 1) | North face, center |
| North_Bot | (0, bottomY, +halfD) | (0, 0, 1) | North face, bottom edge |
| South_Top | (0, topY, -halfD) | (0, 0, -1) | South face, top edge |
| South_Mid | (0, centerY, -halfD) | (0, 0, -1) | South face, center |
| South_Bot | (0, bottomY, -halfD) | (0, 0, -1) | South face, bottom edge |
| East_Top | (+halfW, topY, 0) | (1, 0, 0) | East face, top edge |
| East_Mid | (+halfW, centerY, 0) | (1, 0, 0) | East face, center |
| East_Bot | (+halfW, bottomY, 0) | (1, 0, 0) | East face, bottom edge |
| West_Top | (-halfW, topY, 0) | (-1, 0, 0) | West face, top edge |
| West_Mid | (-halfW, centerY, 0) | (-1, 0, 0) | West face, center |
| West_Bot | (-halfW, bottomY, 0) | (-1, 0, 0) | West face, bottom edge |

**Face center snaps (2):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| Top_Center | (0, topY, 0) | (0, 1, 0) | Stack on top |
| Bot_Center | (0, bottomY, 0) | (0, -1, 0) | Attach below |

**Slope-aware snaps (2):**

| Snap point | Local position | Normal | Purpose |
|------------|---------------|--------|---------|
| HighEdge | (0, highY, +highZ) | (0, 0, 1) | Connect to upper foundation/ramp at slope top height |
| LowEdge | (0, lowY, -lowZ) | (0, 0, -1) | Connect to lower foundation/ramp at slope bottom height |

That's **16 snap points** per ramp.

HighEdge and LowEdge positions depend on the actual ramp mesh geometry -- the Y values must match the ramp surface height at each end. These need to be manually positioned per ramp prefab or computed from the slope angle. For RAMP 4x1 (1m rise over 4m), highY = top of slope, lowY = bottom of slope. For RAMP 4x2 (2m rise), the height difference is larger.

---

## Building from below (missed scenario)

When the player stands UNDER a foundation and looks up, the raycast hits the foundation's bottom face. Currently there's no Bottom snap point, so `FindNearest` returns the closest existing point (probably a side snap seen from below), which produces unexpected placement.

With Bottom snap points added:
- Raycast hits underside of foundation
- FindNearest returns Bottom snap (normal = (0, -1, 0))
- `GetSnapPlacementPosition` detects vertical normal (abs(normal.y) > 0.9)
- New building placed below: `posY = snapPos.y - baseOffset` (negative direction)
- Wall hanging from underside: wall center = bottom snap Y - wall halfHeight

This also covers placing a second foundation underneath an elevated one (e.g., filling in below a ramp). The bottom snap gives the correct attachment point.

### Code change needed for bottom snap

Currently `GetSnapPlacementPosition` handles vertical normals:
```
float yOffset = normal.y > 0 ? baseOffset : -baseOffset;
```
This already supports downward normals -- `normal.y < 0` produces `-baseOffset`, placing the new building's center BELOW the snap point. The bottom of the new building's bounding box would touch the snap point. This should work without code changes.

`surfaceY` for bottom snaps: `_surfaceY = placement.SurfaceY - placement.ObjectHeight` (the underside level). This needs a code addition in `RaycastPlacement` to detect downward-facing snaps and set surfaceY accordingly.

---

## Implementation priority

**Now (this session):**
1. Add Bottom snap to all 6 structural prefabs (update SnapPointPrefabSetup, re-run)
2. Rename wall snaps for clarity (Front/Back/Left/Right instead of N/S/E/W)
3. Update `RaycastPlacement` to handle bottom snaps (surfaceY calculation)

**Next session:**
4. Add wall Left/Right snap points for wall runs (test wall-to-wall snapping)
5. Add R key rotation override for snap mode
6. Add ramp HighEdge/LowEdge snap points (requires measuring actual ramp mesh geometry)

**Deferred:**
7. Machine/Storage snap points (Step 4: automation)
8. Belt input/output node snap points (Step 4: automation)