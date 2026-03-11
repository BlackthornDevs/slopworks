# Kevin's Claude -- Session Handoff

Last updated: 2026-03-10 21:30
Branch: kevin/belts-supports
Last commit: 65f1a13 Update terrain assets from Unity reimport after PR #57 merge

## What was completed this session

### Belt validation fixes (NetworkBuildController.cs)
- Reject straight-backward belt placement: zero endDir check before validator prevents TurnTooSharp bypass from accepting backward belts
- Fix curved U-turn validation: only require cross distance (not along distance) for U-turns, since route builder generates forward overshoot
- Skip elevation check for U-turns (route builder adds overshoot distance)
- Add direction indicator line on ghost support during PickingStart state (1m green line showing R-key direction)
- Line renderer positionCount switches between 2 (direction stub) and 30 (full preview) when transitioning from PickingStart to Dragging

### Route builder fixes (BeltRouteBuilder.cs)
- Tighter U-turn overshoot: cap arc radius at crossDist/2 instead of crossDist (two arcs share the cross distance)
- Replace chord-based tangents with Catmull-Rom in BuildFreeform to fix ramp ripple/squiggle on default mode belts
- Added BeltRoutingMode.Default enum value

### Physics layers (PhysicsLayers.cs)
- Added BeltPorts = 21

### Asset tracking
- FBX Raw folder (10 FBX models + metas) now tracked via Git LFS
- Building Materials (concrete foundation PBR textures)
- Terrain prefabs
- Brackeys sci-fi weapon assets (raw source files)
- Revit source models for buildings (.rfa files)
- Recovery assets

### PR management
- Merged PR #57 (Joe's terrain/settlement/GitHub automation work) into master
- Rebased kevin/belts-supports on updated master

## What's in progress (not yet committed)
- None -- all committed

## Next task to pick up
- Ghost preview mesh during belt placement (currently just line renderer)
- Belt simulation tick and item transport on placed belts
- Support placement CmdPlaceSupport input handler wiring
- Double-bake optimization in host mode (server + client both bake mesh)

## Blockers or decisions needed
- None

## Test status
- Tests not run this session (MCP run_tests corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All

## Key context the next session needs
- **Branch:** `kevin/belts-supports` (off multiplayer-step1, merged with latest master)
- **PhysicsLayers.cs was modified** -- added BeltPorts layer 21. This is a shared/Core file (D-013). Should be PR'd to master separately if Joe needs it.
- **BeltRoutingMode.cs was modified** -- added Default enum value. Also shared.
- Catmull-Rom tangents: `(P[i+1] - P[i-1]) / 2` scaled by 1/3 for Bezier. If freeform belts still have issues, this is where to look.
- U-turn overshoot: `maxRadius = Max(crossDist * 0.5, 0.5)`. Floor of 0.5 prevents degenerate zero-radius arcs.
- Direction line creates the LineRenderer in PickingStart if it doesn't exist. When clicking to Dragging, positionCount resets to 30.
- FBX RAW/ (uppercase) folder exists at repo root -- appears to be a duplicate of Assets/FBX Raw/. Not tracked, can be deleted.
