# Kevin's Claude -- Session Handoff

Last updated: 2026-03-09 23:45
Branch: kevin/multiplayer-step1
Last commit: 95e007f Machine/storage snap-point placement system

## What was completed this session

### Machine/storage snap-point placement system
- `BuildingSnapPoint.cs`: `GenerateFromBounds()` now accepts `BuildingCategory` instead of `bool isRamp`. Machine gets 5 snaps (4 cardinal Bot + Center_Bot), Storage gets 6 snaps (4 cardinal Bot + Center_Bot + Center_Top for vertical stacking). Combined renderer bounds for multi-mesh FBX models.
- `SnapPointPrefabSetup.cs`: Editor tool auto-discovers prefabs under `Resources/Prefabs/Buildings/` via `AssetDatabase.FindAssets`. Detects category from subfolder path (`/Machines/`, `/Storage/`, etc.). Same Machine/Storage snap split as runtime code.
- `GridManager.cs`: `SetBuildingLayer()` preserves snap point layer (skips children on SnapPoints layer). `FindGhostAttachSnap()` forces Center_Bot when machine/storage snaps to structural. Machine-to-machine pairs Bot-to-Bot. `GetSnapPlacementPosition()` accepts ghostCategory and targetCategory.
- `NetworkBuildController.cs`: Added `_peerSnapMode` for machine/storage tools. Scroll wheel toggles between FOUNDATION (structural Top snaps) and MACHINE/STORAGE (peer equipment snaps). HUD always shows current filter mode. `MatchesSnapFilter()` checks target building category.
- Naming convention fix: `Top_Center` -> `Center_Top`, `Bot_Center` -> `Center_Bot` across all files
- Deleted old placeholder prefabs (Foundation, Wall, Ramp, Machine, Storage cubes + materials)
- Added real FBX prefabs: SLAB 1m/2m/4m, WALL 0.5m, RAMP 4x1/4x2, CONSTRUCTOR, STORAGE CONTAINER

## What's in progress (not yet committed)
- Unstaged terrain data, metal materials, Joe scene, docs images (not part of this session's work)
- `test 1.prefab` files in Foundations/Walls/Ramps subfolders (user's editor test assets, untracked)

## Next task to pick up

### Playtest verification
1. Re-run **Tools > Slopworks > Add Snap Points to Prefabs** -- storage prefabs need new Center_Top snap baked in. Delete existing snap point children first if already present.
2. Run all EditMode tests in Test Runner
3. Playtest: machine placement on foundations, machine-to-machine side-by-side snapping, storage stacking via Center_Top, scroll wheel mode toggle

### After verification
- Continue Step 4: Belts + Simulation network wrappers
- Steps 5-7: Combat, Tower+Buildings, Supabase

## Blockers or decisions needed
- Storage prefabs need snap point re-bake (delete old snap children, re-run editor tool) to get new Center_Top
- Machine prefab (CONSTRUCTOR) needs real NetworkMachine component (currently has EmptyNetworkBehaviour)
- Storage prefab (STORAGE CONTAINER) needs real NetworkStorage component

## Test status
- Tests not run this session (MCP run_tests corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All
- Expected: all existing tests pass. SnapPlacementTests updated for BuildingCategory signature.

## Key context the next session needs
- **Branch:** `kevin/multiplayer-step1`
- **NEVER use MCP run_tests** -- corrupts FishNet DefaultPrefabObjects.asset
- **Snap point counts:** Foundation/Wall = 14, Ramp = 6, Machine = 5 (no Center_Top), Storage = 6 (has Center_Top for stacking)
- **SnapPointPrefabSetup** auto-discovers prefabs by scanning `Resources/Prefabs/Buildings/` subfolders. Category detected from path. Skips prefabs that already have snap points.
- **SetBuildingLayer** skips children on SnapPoints layer to prevent overwriting baked snap colliders
- **Scroll wheel** toggles `_peerSnapMode` for machine/storage tools: FOUNDATION mode (structural Top snaps) vs MACHINE/STORAGE mode (peer equipment snaps)
- **FindGhostAttachSnap** forces Center_Bot when machine/storage targets structural. Bot-to-Bot pairing for machine-to-machine.
- **Combined renderer bounds** for multi-mesh FBX: `GetComponentsInChildren<Renderer>()` + `Encapsulate()`
- **Storage stacking**: Storage-on-Storage uses Center_Bot (ghost) meeting Center_Top (target). FindGhostAttachSnap standard tier pairing handles this automatically.
