# Kevin's Claude -- Session Handoff

Last updated: 2026-03-10 23:30
Branch: kevin/belts-supports
Last commit: 359c648 PR review fixes, camera jitter fix, belt validation overhaul

## What was completed this session

### PR #58 review fixes (from Joe's review)
- Resolved git conflict markers in `Assets/FBX Raw.meta` and `Assets/_Slopworks/Resources/Prefabs/Buildings/Supports.meta`
- Removed `Assets/_Recovery/` crash artifacts from tracking, added to `.gitignore`
- Removed server-side TurnTooSharp bypass in `GridManager.cs:402` -- validator now allows U-turns directly

### Camera jitter fix (NetworkPlayerController.cs)
- Replaced `transform.Rotate()` with `rb.MoveRotation()` in `Look()` so yaw goes through Rigidbody interpolation alongside position
- `Look()` runs in `LateUpdate()`, `CheckJump()` stays in `Update()`

### Belt validation overhaul
- **Default mode** now builds actual route waypoints and validates per-segment slope via new `BeltRouteBuilder.ValidateRoute()` instead of skipping all extended checks
- **MaxSlopeAngle** lowered from 45 to 30 degrees (now references `BeltRouteBuilder.MaxRampAngle` directly)
- **TurnTooSharp bypass removed** from both client (`NetworkBuildController`) and server (`GridManager`). Validator handles U-turns: zero endDir rejected, all other angles including 180-degree U-turns pass. Turn geometry handled by per-mode validation.
- **Validation paths separated**: Default mode validates actual route geometry. Straight/Curved keeps existing along/cross/turn math.

### Host mode double-bake optimization (NetworkBeltSegment.cs)
- `OnStartClient()` skips route rebuild + mesh bake when `IsServerInitialized` (host already baked in GridManager)

### Belt sync research (docs/research/belts/)
- `waypoint-sync-approach.md` -- SyncList<Waypoint> analysis: bandwidth, FishNet behavior, implementation plan
- `mesh-serialization-approach.md` -- raw mesh serialization analysis: size estimates, transport limits, industry precedent
- Both saved for review next session

## What's in progress (not yet committed)
- Two research docs in docs/research/belts/ (not yet committed)
- Unity asset changes (terrain data, materials, scene, prefab, DefaultPrefabObjects) -- unstaged, not part of code commits

## Next task to pick up
1. **Bug: Supports allow double belt connections** -- a support with an existing belt still allows another belt to attach to it
2. **Bug: Wrong path direction on port-to-port belts** -- belt routes go opposite direction from source, through buildings. See screenshot in PR #58 review comments. DeriveEndDirection picks wrong direction when connecting between machine/storage ports.
3. **Feature: R key to change endpoint direction** -- cycle end direction during belt drag so player can force horizontal offset or sideways turn instead of auto-derived direction
4. **Review belt sync research** -- read waypoint-sync-approach.md and mesh-serialization-approach.md, make architectural decision
5. **Ghost preview mesh** during belt placement (currently just line renderer)
6. **Belt simulation tick** and item transport on placed belts

## Blockers or decisions needed
- PR #58 updated with fixes, awaiting re-review from Joe
- PR #59 (Joe's terrain work) has merge conflicts with us on terrain assets. Whichever merges first, the other rebases.
- Belt sync architecture decision pending (waypoint SyncList vs current endpoint SyncVars)

## Test status
- Tests not run this session (MCP run_tests corrupts FishNet DefaultPrefabObjects)
- Run manually: Window > General > Test Runner > EditMode > Run All

## Key context the next session needs
- **Branch:** `kevin/belts-supports` (PR #58 open to master)
- **Validation is now mode-aware:** Default mode uses `ValidateRoute()` on actual waypoints. Straight/Curved uses along/cross geometry math. Don't mix them.
- **No TurnTooSharp bypass anywhere.** The validator allows U-turns directly. If U-turn behavior breaks, fix the validator, don't add bypasses back to callers.
- **Camera jitter fixed** with `rb.MoveRotation()`. If jitter returns, the issue is Rigidbody interpolation timing, not Update/LateUpdate placement.
- **FBX RAW/ (uppercase)** folder at repo root is untracked duplicate of Assets/FBX Raw/. Can be deleted.
- **DefaultPrefabObjects.asset** is modified locally -- if FishNet acts up, restore with `git checkout HEAD -- Assets/DefaultPrefabObjects.asset` and reopen Unity.
