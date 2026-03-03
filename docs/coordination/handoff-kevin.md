# Kevin's Claude -- Session Handoff

Last updated: 2026-03-02
Branch: kevin/main
Last commit: 37701a6 Structural building UX: auto-level, ghosts, zoop, delete highlight

## What was completed this session

### C-009 turret ghost cleanup fix
- Added `RegisterToolCleanup(Action)` to `PlaytestToolController.cs` -- custom tool handlers register cleanup callbacks invoked by `CancelAllPending()`
- Both `KevinPlaytestSetup.cs` and `JoePlaytestSetup.cs` register `DestroyTurretGhost`
- C-009 resolved in contradictions.md

### Joe's PR #16 reviewed and merged
- Code review found only Co-Authored-By tag issue (J-026 created)
- Merged to master, synced kevin/main
- Unblocked J-024, added J-026/J-027/J-028 to tasks-joe.md

### D-014 decision added
- MasterPlaytest scene must pass before merging any PR to master
- CameraModeController NRE fix (null guards on cameras in SwitchToFPS/SwitchToIsometric)

### Structural building UX (plan: auto-level, ghosts, zoop)
- **Auto-level detection:** Structural tools use ray-plane intersection at `_currentLevel * LevelHeight`. Auto-level locks during foundation drag. PageUp/PageDown override with 1.5s cooldown. Level indicator plane (color-coded: blue/yellow/red).
- **Wall and ramp hover ghosts:** Continuous ghost preview with green/red valid/invalid tinting. R key cycles direction lock. Single-click placement replaces 2-step flow.
- **Wall zoop:** Click-drag to place line of walls along foundation edges. Axis derived from edge direction (perpendicular walk). Ghost pool for batch preview.
- **Delete tool hover highlight:** Red tint on target object. `DeleteMask` includes Structures + Interactable layers.
- **Placement fixes:** Structural visuals on layer 13 (Structures). `StructuralPlacementMask` for structural raycasts. `GetStructuralWorldPos()` uses level-plane intersection instead of physics raycast.
- **Wall zoop axis fix:** Zoop walks perpendicular to edge direction, not drag direction. Removed `_axisLocked`/`_lockedToX` fields.

### Key files modified
- `Scripts/Debug/PlaytestToolController.cs` (~200 lines added/rewritten)
- `Scripts/Building/WallZoopController.cs` (simplified, axis from edge direction)
- `Scripts/Player/CameraModeController.cs` (null guards)
- `Scripts/Debug/KevinPlaytestSetup.cs` (RegisterToolCleanup)
- `Scripts/Debug/JoePlaytestSetup.cs` (RegisterToolCleanup)
- `docs/coordination/decisions.md` (D-014)
- `docs/coordination/contradictions.md` (C-009 resolved)
- `docs/coordination/tasks-joe.md` (J-026/J-027/J-028)

## What's in progress (not yet committed)

None -- all committed.

## Next task to pick up

- **Manual playtest in MasterPlaytest** to verify all structural building UX works in the unified scene (per D-014, before merging to master)
- Then create PR to master and merge
- After merge: Phase 9 planning or vertical slice polish

## Blockers or decisions needed

None.

## Test status

- 886/886 EditMode tests passing, 0 failures
- WallZoopController: 9/9 tests passing
- Manual playtest in KevinPlaytest: all features verified working

## Key context the next session needs

- **Ray-plane intersection pattern:** `GetStructuralWorldPos()` always uses `RaycastToLevelPlane()` (math intersection at `level * LevelHeight`), NOT physics raycast. Auto-level still uses structural raycast to detect level from existing structures, but position always comes from plane intersection. This lets you build away from walls on upper levels.
- **`_isDragging` guard:** `HandleAutoLevel()` skips auto-level detection while `_isDragging` is true (set by foundation batch placer). Prevents level switching mid-drag.
- **`DeleteMask`:** Combines `StructuralPlacementMask | (1 << Interactable)` so delete tool hits machines/storage/belts in addition to structures.
- **Wall zoop axis:** Determined by `Origin.EdgeDirection`, not drag direction. North/south edges (edgeDir.y != 0) walk X axis. East/west edges (edgeDir.x != 0) walk Y axis.
- **`RegisterToolCleanup(Action)`:** Custom tool handlers register ghost cleanup callbacks. Called by `CancelAllPending()` on tool switch.
- **Untracked files:** `JoePlaytest/` and `MasterPlaytest/` lighting data directories are untracked -- this is normal Unity behavior when scenes are loaded/saved.
