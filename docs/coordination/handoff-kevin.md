# Kevin's Claude -- Session Handoff

Last updated: 2026-03-15
Branch: kevin/belts
Last commit: 31d79eb Fix support height gap -- use actual cylinder mesh height

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

### Belt ghost mesh preview (kevin/belts, not yet merged)
- Replaced line renderer with `BeltSplineMeshBaker` ghost mesh during placement (`NetworkBuildController.cs`)
- Semi-transparent green/red mesh updates every frame during drag
- Exact match between ghost and placed belt (both use same waypoints)
- Line renderer kept as backup

### Support height adjustment (kevin/belts, not yet merged)
- PgUp/PgDn adjusts support height in 0.5m steps (0.25m with Shift)
- Independent start/end offsets (`_beltStartHeightOffset`, `_beltEndHeightOffset`)
- Idle state = adjusts start, Dragging state = adjusts end
- Cylinder scales via Y (pivot at bottom), top piece and anchor offset upward
- `SupportPoleHeight` read from mesh bounds for correct scaling (0.926 units, not 1.0)
- Server-side `SpawnSupportAt` applies same scaling via `startHeightOffset`/`endHeightOffset` params on `CmdPlaceBelt`
- User verified: ghost matches placed result, no gap between cylinder and top piece

## What's in progress (not yet committed)
- Terrain data and water material changes are unstaged (user said don't commit)

## Next task to pick up
1. **PR for kevin/belts** -- create PR to master, wait for checks, merge
2. **NetworkMachine/NetworkStorage on prefabs** -- user added manually during testing, verify they're saved to prefabs and committed
3. **Ghost preview mesh** -- consider removing line renderer backup once ghost mesh is proven stable
4. **Machine/Storage UI** -- recipe selection and status display for multiplayer
5. **Belt item visuals** -- upgrade from orange cubes to item-specific meshes

## Known bugs (from 2026-03-15 playtesting)

1. **Item flow direction wrong** -- items on belts travel in the direction the belt was drawn, not based on which end is Input vs Output. When flipBeltPorts is true, the BeltSegment still moves items start-to-end regardless of port assignment. Need to check whether adapters or BeltSegment flow direction needs to account for the flip.

2. **Curved belt item visuals go point-to-point** -- items on curved belts travel in straight lines between waypoints instead of following the spline mesh. They float in mid-air on curves. Compare how BeltSplineMeshBaker evaluates the spline vs how GetItemWorldPositions evaluates it -- they should use the same method.

3. **Delete tool doesn't always work** -- sometimes clicking a placed building with the delete tool does nothing. Not yet reproducible consistently. User needs more time to identify the pattern. Could be raycast layer issues, collider gaps, or PlacementInfo lookup failures.

## Blockers or decisions needed
- Three bugs above need investigation before belt system is shippable

## Test status
- Tests not run via MCP (corrupts FishNet DefaultPrefabObjects)
- 6 new tests from belt simulation tick (on master via PR #73)
- Run manually: Window > General > Test Runner > EditMode > Run All

## Key context the next session needs
- **Branch:** `kevin/belts` with ghost mesh + support height work, not yet merged
- **`SupportPoleHeight`** reads actual cylinder mesh bounds, not anchor position. If the cylinder FBX changes, this auto-adjusts.
- **`ApplySupportHeightOffset`** uses hardcoded Y defaults (-0.075 for top piece, 1.0 for anchor). The spec reviewer flagged this -- if prefab positions change, these need updating. Consider reading defaults from prefab like `SupportPoleHeight`.
- **`BeltPlacementState.PickingStart`** enum value exists but is never used as a state. `Idle` handles both "no belt active" and "hovering before first click". PgUp/PgDn checks `Idle` for start height.
- **TestItemSpawner T key** fills storage and sets recipes. Debug tool, delete when real UI exists.
- **Prefabs need NetworkMachine/NetworkStorage** for simulation to work. User added them manually.
