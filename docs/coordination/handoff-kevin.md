# Kevin's Claude -- Session Handoff

Last updated: 2026-03-09 23:30
Branch: kevin/multiplayer-step1
Last commit: (uncommitted -- zoop snap-chain + ramp/wall fixes + delete mask)

## What was completed this session

### Zoop system rewrite (snap-chain based)
- `NetworkBuildController.cs`: Zoop preview and placement now chain snap-to-snap instead of computing cell offsets and footprints
- `UpdateZoopPreview()` builds a chain: each ghost snaps to the previous ghost's snap point along the zoop direction
- `PlaceZoopLine()` reads positions directly from ghost transforms, no recalculation
- `FindZoopSnapPoint()` finds the snap whose rotated normal aligns with the zoop direction, with Edge bonus for ramp HighEdge/LowEdge
- Removed `GetZoopCells()` -- no longer needed
- `MaxZoopCount = 5` hard cap on chain length

### Zoop first-click snap support
- First click in zoop mode uses full snap/grid placement (same as non-zoop)
- `RaycastPlacement()` allows snap detection when `_zoopStartSet == false`
- Stores `_zoopStartPos` and `_zoopStartRot` on first click for anchor

### Zoop ray-plane extension
- After first click, uses `Plane.Raycast` at anchor Y height instead of geometry raycast
- Works when looking at sky, ramp edges, or beyond terrain
- Clamped to `_placementRange` to prevent runaway at low angles

### Wall zoop axis lock
- Walls forced to zoop along their length axis (perpendicular to facing)
- Uses `_zoopStartRot` (actual rotation from first click including snap auto-yaw)
- Prevents face-to-face wall stacking

### Ramp snap point overhaul
- `BuildingSnapPoint.cs` and `SnapPointPrefabSetup.cs`: ramps now get North_Bot, East_Bot, West_Bot, HighEdge, LowEdge, Bot_Center
- No Top_Center, no _Mid, no _Top on ramps
- HighEdge at top of slope (high Y, forward Z), LowEdge at bottom (low Y, back Z)
- `FindGhostAttachSnap()` in GridManager.cs: HighEdge pairs with LowEdge and vice versa

### Delete mode fix
- Added `DeleteMask` (Structures layer only) -- delete raycast no longer hits snap point triggers
- Previously required aiming at bottom edge of slabs to delete

### PR #52 merged to master
- Snap-to-snap placement + center/edge filter from previous session

## What's in progress (not yet committed)
- All zoop/ramp/delete changes listed above are uncommitted
- Also: unstaged terrain data, metal materials, old prefab deletions, "test 1" prefabs, FBX Raw assets, Recovery assets (NOT part of this work)

## Next task to pick up

### Playtest verification
1. Run all EditMode tests in Test Runner -- verify snap/placement tests still pass
2. Playtest zoop: foundations (should chain side-by-side), walls (chain along length), ramps (chain uphill via HighEdge/LowEdge)
3. Verify delete mode works reliably (should highlight on crosshair, not require bottom-edge aiming)
4. Re-run SnapPointPrefabSetup (Tools > Slopworks > Add Snap Points to Prefabs) on ramp prefabs after deleting old snap point children

### After verification
- Continue Step 4: Machines + Belts + Simulation network wrappers
- Steps 5-7: Combat, Tower+Buildings, Supabase

## Blockers or decisions needed
- Existing ramp prefabs may still have old snap points (Mid + Top_Center). Need to delete old children and re-run the editor tool.

## Test status
- Tests not run this session (MCP run_tests corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All
- SnapPlacementTests may need adjustment for new ramp snap point names

## Key context the next session needs
- **Branch:** `kevin/multiplayer-step1`
- **NEVER use MCP run_tests** -- triggers recompilation that corrupts FishNet DefaultPrefabObjects.asset
- **Snap-chain zoop**: `UpdateZoopPreview` chains ghosts by finding matching snap points on each successive ghost. `FindZoopSnapPoint` picks the snap whose normal aligns with zoop direction.
- **Wall zoop axis**: Forced to length axis via `_zoopStartRot`. Wall at 0 deg -> zoop along X only.
- **Ramp snap points**: HighEdge (top of slope, forward), LowEdge (bottom of slope, back), cardinal _Bot only, Bot_Center. No _Mid/_Top/Top_Center.
- **HighEdge/LowEdge pairing**: `FindGhostAttachSnap` maps HighEdge->LowEdge and vice versa. `FindZoopSnapPoint` gives 0.1 bonus to Edge snaps so they win over cardinal _Bot.
- **Delete mask**: `DeleteMask = (1 << PhysicsLayers.Structures)` -- only hits building meshes, not snap sphere triggers.
- **Zoop ray-plane**: After first click, uses `Plane.Raycast` at anchor Y for end cell. No geometry needed.
- **MaxZoopCount = 5**: Hard cap in chain loop.
- **Ramp prefabs need re-setup**: Delete old snap children, re-run Tools > Slopworks > Add Snap Points to Prefabs.
