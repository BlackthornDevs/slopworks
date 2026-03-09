# Kevin's Claude -- Session Handoff

Last updated: 2026-03-09 22:00
Branch: kevin/multiplayer-step1
Last commit: (uncommitted -- snap-to-snap placement + center/edge filter)

## What was completed this session

### Snap-to-snap placement system (replaced offset-based math)
- `GridManager.GetSnapPlacementPosition()` completely rewritten -- no more extents, halfHeight, or baseOffset calculations
- New `FindGhostAttachSnap()` finds the ghost prefab's matching snap point (opposite normal + opposite height tier)
- Placement math is now: `ghostPos = targetSnapPos - Rot * ghostSnapLocalPos`
- Ghost prefab snap points encode all attachment geometry -- fix snap positions on the prefab, not in GridManager

### Center/Edge snap filter toggle (scroll wheel)
- `NetworkBuildController.RaycastPlacement()` uses `Physics.RaycastAll` to find all overlapping snap spheres
- `MatchesSnapFilter()` filters by active mode: CENTER (_Mid, _Center) or EDGE (_Top, _Bot)
- Scroll wheel toggles between modes in build mode
- HUD shows active filter and snap point name
- Cyan highlight sphere shows which snap point is active

### Bottom/downward placement fix
- _Bot snap points on target pair with _Top on ghost, extending new building downward
- _Top snap points on target pair with _Bot on ghost, extending upward
- Eliminates the hardcoded "always extend upward" behavior

### Test updates
- `SnapPlacementTests.cs` rewritten for snap-to-snap expected values
- Added `CreateCubeWithSnaps()` helper and `FindSnapByName()` utility
- New tests: `HorizontalBot_ExtendsDownward`, `HorizontalTop_ExtendsUpward`

### Documentation updates
- `.claude/CLAUDE.md`: new engineering principle "Prefer GameObject-oriented data over computed values"
- `.claude/CLAUDE.md`: updated placement hard rule for snap-to-snap approach
- `docs/reference/snap-point-system.md`: updated placement mode descriptions, center/edge filter
- `memory/MEMORY.md`: updated placement section

## What's in progress (not yet committed)
- All changes listed above are uncommitted
- Also: unstaged terrain data, metal materials, old prefab deletions, "test 1" prefabs, FBX Raw assets, Recovery assets (NOT part of snap point work)

## Next task to pick up

### Playtest verification
1. Run all EditMode tests in Test Runner -- verify snap-to-snap tests pass
2. Playtest snap placement: place foundations, snap walls to edges (CENTER and EDGE modes), stack vertically, hang from bottom
3. Verify wall runs (wall-to-wall side snapping) still work
4. Check ramp placement behavior with the new snap-to-snap approach

### Known bugs (from previous session, may still apply)
1. **Zoop ghost shifts to different grid after first click** -- ghost jumps to wrong cell when zoop starts
2. **Ramp zoop should change elevation** -- currently places flat copies
3. **Ramp HighEdge/LowEdge snap points need manual positioning** -- auto-detection from mesh bounds doesn't match actual slope

### After bugs
- Continue Step 4: Machines + Belts + Simulation network wrappers
- Steps 5-7: Combat, Tower+Buildings, Supabase

## Blockers or decisions needed
- None

## Test status
- Tests not run this session (MCP run_tests corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All
- Expect some test adjustments may be needed for FBX-based prefabs vs center-origin test cubes

## Key context the next session needs
- **Branch:** `kevin/multiplayer-step1`, NOT `kevin/main`
- **NEVER use MCP run_tests** -- triggers recompilation that corrupts FishNet DefaultPrefabObjects.asset
- **Snap-to-snap placement**: `GetSnapPlacementPosition` uses `FindGhostAttachSnap` to pair opposite snap points. No extents/offset math. Fix snap geometry on the prefab, not in code.
- **Center/Edge filter**: Scroll wheel toggles which snap points are active. CENTER = _Mid/_Center (default, less overlap). EDGE = _Top/_Bot (for extending from edges).
- **`Physics.RaycastAll`**: `RaycastPlacement` uses RaycastAll to find all overlapping snap sphere hits, then filters by active mode. Falls back to FindNearestFiltered if no snap sphere matched.
- **Ghost prefabs need snap points**: `FindGhostAttachSnap` queries `GetComponentsInChildren<BuildingSnapPoint>()` on the prefab. All building prefabs must have snap point children.
- **Engineering principle**: Prefer baking data into child objects/transforms over computing from renderer bounds. See CLAUDE.md.
- **SnapPoints layer 20**: trigger colliders only, no physics collisions. RaycastAll hits them via Physics.queriesHitTriggers.
- **Editor tool preserves manual edits**: SnapPointPrefabSetup skips prefabs that already have snap points.
- **placeSurfaceY includes nudge**: derived from ghostPos.y - GetPrefabBaseOffset(prefab) for HasBuildingAt check.
- **Zoop skipped during snap detection**: `if (!_zoopMode)` guards snap point checking.
