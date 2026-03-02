# Joe's session handoff

Updated by Joe's Claude at the end of each session.

---

## Last updated: 2026-03-02 (by Kevin -- MasterPlaytestSetup + IPlaytestFeatureProvider)

### CRITICAL: PR #9 cannot be merged. Read this before doing anything else.

PR #9 modifies `StructuralPlaytestSetup.cs`, which **does not exist on master**. It was deleted and split into 5 files. PR #9 also creates `DevTestPlaytestSetup.cs`, a 509-line bootstrapper that duplicates shared infrastructure that already exists. Both of these are wrong.

**Close PR #9 without merging.** The work in it (turret wiring into a bootstrapper) needs to be redone in the correct file.

C-008 (asking whether Dev_Test should use StructuralPlaytestSetup) is moot. StructuralPlaytestSetup no longer exists. The answer is: use `JoePlaytestSetup.cs`. Delete the C-008 entry from your copy of contradictions.md.

---

### How the bootstrapper architecture works

There are 5 files. Three are shared, two are exclusive per developer.

| File | Owner | What it does |
|------|-------|-------------|
| `PlaytestContext.cs` | Shared | Data class that holds all refs created during bootstrap (grid, sim, player, HUD, etc.) |
| `PlaytestBootstrap.cs` | Shared | One-shot setup: creates the grid, simulation, player, HUD, combat systems. Returns a `PlaytestContext`. |
| `PlaytestToolController.cs` | Shared | MonoBehaviour that handles all shared tools (foundation, wall, ramp, belt, machine, storage, delete), ghost previews, OnGUI, simulation tick. |
| `KevinPlaytestSetup.cs` | Kevin only | Kevin's features: building exploration, supply chain, overworld map. **Do not touch this file.** |
| `JoePlaytestSetup.cs` | Joe only | **Your file.** Skeleton with commented placeholders for turret code. This is the ONLY bootstrapper you write code in. |

Other shared files you should not create or duplicate:
- `PlaytestLogger.cs` -- runtime logging singleton (new, just merged to master)
- `PlaytestValidator.cs` -- scene validation checks at startup

**Your scene setup:** Create a scene with a single GameObject. Add `JoePlaytestSetup` as its only component. Hit Play. All shared infrastructure (grid, simulation, player, HUD, combat, all shared tools) bootstraps automatically. You only add your dev-specific features (turrets, future tower systems) inside `JoePlaytestSetup.cs`.

---

### Rules for Joe's work

1. **J-023 is mandatory and must be done first every session until complete.** Merge master. Port turret code into `JoePlaytestSetup.cs`. Delete `DevTestPlaytestSetup.cs` and any leftover `StructuralPlaytestSetup.cs` from your branch.

2. **Never create a separate bootstrapper.** `DevTestPlaytestSetup.cs` should not exist. `JoePlaytestSetup.cs` IS your bootstrapper. It inherits all shared functionality from `PlaytestBootstrap` and `PlaytestToolController`.

3. **Never modify shared files without flagging it.** If you need to change `PlaytestBootstrap.cs`, `PlaytestToolController.cs`, `PlaytestContext.cs`, or anything in `Scripts/Core/`, note it in your handoff under "Shared file changes." Additive changes (new enum values, new methods) are usually fine. Modifying existing behavior requires discussion.

4. **Your exclusive files:** `JoePlaytestSetup.cs`, anything in `Scripts/Combat/` that you created (TurretController, TurretBehaviour, TurretDefinitionSO, FaunaAI, etc.), and future `Scripts/World/Tower*` files. You own these completely.

5. **Merge master frequently.** `git fetch origin master && git merge origin/master` at the start of every session. Kevin pushes new tasks, decisions, and shared infrastructure via master. If you don't merge, you end up working against stale code.

6. **Do not push directly to master.** Push to `joe/main`, then `gh pr create --base master --head joe/main`.

---

### Key API on PlaytestToolController

These are the methods you use from `JoePlaytestSetup.cs` to add your features:

- `RegisterToolHandler(ToolMode mode, Action<Keyboard, Mouse> handler)` -- register input handler for a custom tool (e.g. turret placement)
- `SetTool(ToolMode mode)` -- programmatically switch tools
- `GetCellUnderCursor()` -- returns grid cell under mouse cursor
- `SpawnPortIndicators(PlacementResult result)` -- show port indicators on placed buildings
- `PreSeedFactory()` -- pre-seeds the basic smelting chain
- `CurrentTool` -- read current tool mode
- `CurrentLevel` -- read current vertical level
- `GuiNextY` -- Y position after shared OnGUI content; use as start Y for your OnGUI additions
- `SuppressInput` -- set true to block all tool input (for modal UI like recipe selection)

---

### What was completed by Kevin (2026-03-02)

**PlaytestLogger merged to master (PR #10).** Static singleton with toggleable verbose logging. Logs all inputs (Fire, Reload, Jump, E interact, hotbar slots, tool changes, build clicks, level changes) and events (item pickup, portal entry/exit, page changes). Breadcrumb position logging every 5 seconds. Controlled via inspector toggles on the KevinPlaytest scene.

Files touched: new `PlaytestLogger.cs`, plus one-line `PlaytestLogger.Log()` calls added to `PlaytestToolController.cs`, `PlayerController.cs`, `WeaponBehaviour.cs`, `WorldItem.cs`, `BuildingEntryTrigger.cs`, `BuildingExitTrigger.cs`, `KevinPlaytestSetup.cs`.

**PlaytestValidator merged to master (PR #11).** 23 startup validation checks for the playtest scene.

**MasterPlaytestSetup (PR #12, on kevin/main).** New unified scene where both devs' features must coexist. Key changes:
- New `IPlaytestFeatureProvider` interface -- both bootstrappers now implement it
- `MasterPlaytestSetup.cs` orchestrator discovers providers and calls them in 7-phase order
- Both `KevinPlaytestSetup.cs` and `JoePlaytestSetup.cs` refactored with `_isStandalone` guard in Awake
- When `MasterPlaytestSetup` is on the same GameObject, `_isStandalone = false` and standalone Awake is skipped
- New static methods on `PlaytestToolController`: `CreateSharedBuildPage()`, `CreateGroundPlane()`, `BakeNavMesh()`
- `PlaytestValidator` updated with `ValidateMasterScene()` checks
- `MasterPlaytest.unity` scene created (root GO with all 5 components)

**Your JoePlaytestSetup.cs interface methods are all skeletons.** After porting turret code (J-023), they should work in both standalone and master mode automatically.

**Previous Kevin work (already on master):**
- Bootstrapper refactoring: split StructuralPlaytestSetup into 5 files
- Phase 6: Building exploration (warehouse layout, MEP restore, portals, building enemies)
- Phase 8: Supply chain network (supply lines, overworld map, supply dock)

### What was completed by Joe (before PR #9)

- J-013: TurretController simulation layer (pure C#, 22 tests)
- J-014: TurretBehaviour wrapper with placement
- J-015: Turret playtest scene with ammo chain
- PlaytestEnvironment arena generator

All turret code is functional but currently wired into the deleted `StructuralPlaytestSetup.cs`. Needs to be ported to `JoePlaytestSetup.cs` (that's J-023).

### Shared file changes from Joe (already on joe/main, expect during merge)

- `PhysicsLayers.cs` -- added FaunaMask
- `PortOwnerType.cs` -- added Turret value
- `BuildingPlacementService.cs` -- added PlaceTurret method
- `ConnectionResolver.cs` -- added Turret cases
- `PlayerController.cs` -- added GridPlane to GroundMask

These are all additive and should merge cleanly.

### Next task

**J-023 (Critical): Merge master, port turret code to JoePlaytestSetup.cs, clean up.** Full instructions in tasks-joe.md. This MUST be done before any other work. After porting, your turret code should work in both standalone mode (JoePlaytestSetup alone) and master mode (MasterPlaytestSetup orchestrating).

After J-023: J-024 (verify MasterPlaytest scene integration), then J-016 (Tower data model, Phase 7 start). Read `docs/plans/2026-02-28-tower-design.md` before starting J-016.

### Blockers

None, but PR #9 must be closed first. Do not attempt to merge it.

### Test status (on kevin/main after latest merges)

789/789 passing, 0 failing, 0 skipped, 0 compilation errors.

### Key context

- Turret visual: dark red cylinder base + barrel pivot + elongated cube barrel
- Turret pre-loads 32 iron_scrap ammo, fires at enemies, stops when empty
- Turret has 1 input port at direction (-1,0) for belt ammo delivery
- PlaytestEnvironment: seeded System.Random (seed 42, arena size 40), procedural 512x512 ground texture
- PlaytestLogger: `[LOG]` prefix in console, toggle via `_enabled` inspector bool on PlaytestLogger component
