# Kevin's Claude -- Session Handoff

Last updated: 2026-03-15
Branch: kevin/belt-bugs (off kevin/belts)
Last commit: 6724283 Fix curved belt item visuals -- use spline not linear interpolation

## What was completed this session

### Belt port flow inheritance (merged to master via PR #72)
- `CmdPlaceBelt` accepts `flipBeltPorts` parameter (`GridManager.cs`)
- Per-endpoint port direction validation (`NetworkBuildController.cs`)

### Belt simulation tick (merged to master via PR #73)
- Generalized `BeltNetwork` to `IItemSource`/`IItemDestination` (D-020)
- `WireBeltSimulationConnection` in GridManager creates connections at placement time
- `TestItemSpawner` T key for debug fill of storage/recipes
- 6 new EditMode tests for generalized connections
- Items verified flowing from storage through belt into machine

### Belt ghost mesh preview (kevin/belts, PR #74 open)
- Replaced line renderer with `BeltSplineMeshBaker` ghost mesh during placement
- Semi-transparent green/red mesh updates every frame during drag
- Line renderer kept as backup

### Support height adjustment (kevin/belts, PR #74 open)
- PgUp/PgDn with independent start/end offsets
- Cylinder scales via Y, top piece and anchor offset upward
- `SupportPoleHeight` read from mesh bounds for correct scaling
- Server-side support scaling matches client ghost

### Bug fixes (kevin/belt-bugs)
- **Item flow direction** -- `BeltSegment.Reversed` flag set from `flipBeltPorts`. Added `TryExtractFromStart`, `TryInsertAtEnd`, `HasItemAtStart`. Adapters check `Reversed` to operate on correct end. Tick moves items toward start when reversed. Commit `1bdc951`.
- **Curved belt item visuals** -- keep `SplineContainer` after mesh bake instead of destroying it. `EvaluatePosition` uses the spline for Bezier curve evaluation instead of linear `EvaluateRoute`. Items now follow the actual belt mesh path. Commit `6724283`.

## What's in progress (not yet committed)
- Terrain data, water material, BeltMat material changes are unstaged (don't commit)

## Known bugs (remaining)

1. ~~**Item flow direction wrong**~~ -- FIXED (commit 1bdc951)

2. ~~**Curved belt item visuals go point-to-point**~~ -- FIXED (commit 6724283)

3. **Delete tool doesn't always work** -- sometimes clicking a placed building with the delete tool does nothing. Not yet reproducible consistently. Could be raycast layer issues, collider gaps, or PlacementInfo lookup failures.

4. **Deleting a belt with items breaks simulation** -- ServerManager.Despawn destroys the belt but never unregisters the BeltSegment from FactorySimulation or removes BeltNetwork connections. Fix needs: unregister segment, disconnect BeltNetwork connections, clean up BeltPort connections. Future improvement: return in-transit items to player inventory.

## Next task to pick up
1. **PR for kevin/belts** -- PR #74 open, wait for checks, merge
2. **PR for kevin/belt-bugs** -- create PR to master after kevin/belts merges
3. **Bug 3 investigation** -- reproduce delete tool failure, identify pattern
4. **Bug 4 implementation** -- belt deletion cleanup (unregister, disconnect, return items)
5. **Machine/Storage UI** -- recipe selection and status display for multiplayer

## Blockers or decisions needed
- None

## Test status
- Tests not run via MCP (corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All

## Key context the next session needs
- **Branch:** `kevin/belt-bugs` off `kevin/belts`. Both need PRs to master.
- **`BeltSegment.Reversed`** -- bool set from `flipBeltPorts`. Adapters check it to call the correct start/end methods. Tick direction flips when reversed.
- **SplineContainer stays on placed belts** after mesh bake. Ghost previews (Decal layer) still destroy it. `EvaluatePosition` uses the spline if present, falls back to `EvaluateRoute`.
- **`BeltPlacementState.PickingStart`** enum value exists but is never used. PgUp/PgDn checks `Idle` for start height.
- **Prefabs need NetworkMachine/NetworkStorage** for simulation. User added manually.
- **TestItemSpawner T key** fills storage and sets recipes. Debug tool.
