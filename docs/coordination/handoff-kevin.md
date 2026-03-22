# Kevin's Claude -- Session Handoff

Last updated: 2026-03-22
Branch: kevin/belt-bugs (off kevin/belts, merged to master)
Last commit: 4da3d84 Merge remote-tracking branch 'origin/master' into kevin/belt-bugs

## What was completed this session
- Synced kevin/belt-bugs with latest master (merged origin/master, picked up GameIdeas docs and PR #74 merge)
- No code changes this session

## What's in progress (not yet committed)
- Terrain data, water material, BeltMat material, HomeBase scene changes are unstaged local modifications (don't commit these)
- Untracked TerrainData asset file (don't commit)

## Next task to pick up
1. **Create PR for kevin/belt-bugs -> master** -- 2 bug fix commits (item flow direction fix, curved belt item visuals fix) plus test results documentation
2. **Bug 3: Delete tool intermittent** -- sometimes clicking a placed building with delete tool does nothing. Investigate raycast layers, collider gaps, PlacementInfo lookup
3. **Bug 4: Belt deletion cleanup** -- despawning a belt doesn't unregister BeltSegment from FactorySimulation or remove BeltNetwork connections. Needs: unregister segment, disconnect connections, optionally return in-transit items
4. **Machine/Storage UI** -- recipe selection and status display for multiplayer

## Blockers or decisions needed
- None

## Test status
- **938 total, 909 passed, 29 failed** (EditMode, last run 2026-03-15)
- 29 failures are pre-existing from multiplayer conversion, not from belt bug fixes
- Run manually: Window > General > Test Runner > EditMode > Run All

## Key context the next session needs
- **kevin/belt-bugs has 5 original commits + 1 merge commit** ahead of master. The 5 commits are the belt bug fixes and test documentation from 2026-03-15.
- **PR #74 (kevin/belts) already merged** to master. kevin/belt-bugs is ready for its own PR.
- **`BeltSegment.Reversed`** -- bool set from `flipBeltPorts`. Adapters check it to call correct start/end methods. Tick direction flips when reversed.
- **SplineContainer stays on placed belts** after mesh bake. Ghost previews (Decal layer) still destroy it. `EvaluatePosition` uses spline for Bezier curve evaluation.
- **Prefabs need NetworkMachine/NetworkStorage** for simulation wiring.
- **TestItemSpawner T key** fills storage and sets recipes. Debug tool.
- **Joe has not been active** -- his handoff is from 2026-03-13, tasks J-030/J-031 still pending.
