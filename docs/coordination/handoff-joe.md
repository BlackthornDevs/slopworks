# Joe's session handoff

Updated by Joe's Claude at the end of each session.

---

## Last updated: 2026-03-02 (by Kevin -- bootstrapper refactoring)

### What was completed by Kevin (2026-03-02)

**MAJOR CHANGE: StructuralPlaytestSetup.cs no longer exists.**

It was split into 5 files to eliminate merge conflicts:

| File | Location | Owner | Purpose |
|------|----------|-------|---------|
| `PlaytestContext.cs` | `Scripts/Debug/` | Shared | Data class holding all refs from bootstrap |
| `PlaytestBootstrap.cs` | `Scripts/Debug/` | Shared (Kevin primary) | Plain C# one-shot setup (grid, sim, player, HUD, combat) |
| `PlaytestToolController.cs` | `Scripts/Debug/` | Shared (Kevin primary) | All shared tool handling, visuals, OnGUI, simulation tick |
| `KevinPlaytestSetup.cs` | `Scripts/Debug/` | Kevin exclusive | Building exploration, supply chain, overworld map |
| `JoePlaytestSetup.cs` | `Scripts/Debug/` | Joe exclusive | Skeleton with commented turret placeholders |

Scene renamed: `StructuralPlaytest.unity` -> `KevinPlaytest.unity`

**How to test after merging master:**
1. Run all EditMode tests in Unity Test Runner (Window > General > Test Runner > EditMode tab > Run All). Expect 789 passing.
2. Create a scene with a GameObject that has `JoePlaytestSetup` as its only component.
3. Hit Play. Verify shared tools work (foundation, wall, belt, machine, etc.).
4. Port turret code into `JoePlaytestSetup.cs` (see J-023 in tasks-joe.md for details).
5. Re-test with turrets wired up.

**Previous Kevin work (still on kevin/main):**
- Phase 6: Building Exploration -- building simulation, warehouse layout, MEP restore, portals, building enemies
- Phase 8: Supply Chain Network -- supply lines, overworld map, supply dock
- Phase 6 pattern note: `renderer.material.color` causes EditMode test failures due to material leak. Use `var mat = new Material(renderer.sharedMaterial); mat.color = color; renderer.sharedMaterial = mat;` instead.

### What was completed

- **J-013: Auto-turret simulation layer** (`6312a91`) -- TurretController (pure C#), TurretDefinitionSO, 22 passing tests. Done in previous session.
- **J-014: Turret MonoBehaviour wrapper and placement** (`2e4cc15`) -- TurretBehaviour wrapper with OverlapSphere enemy detection and barrel rotation. PlaceTurret added to BuildingPlacementService. Turret tool mode in StructuralPlaytestSetup (slot 7). Pre-loads 32 ammo per turret.
- **J-015: Turret playtest scene** (`0a99c34`) -- Extended PreSeedFactory with ammo storage -> belt -> turret chain. Fixed ConnectionResolver to handle PortOwnerType.Turret (was throwing on turret input ports). Added P key to trigger pre-seed at runtime. Full loop verified: ammo flows from storage through belt to turret, turret fires at enemies, enemies die.
- **PlaytestEnvironment** (`04a3ecd`) -- Reusable post-apocalyptic arena generator with procedural ground texture, perimeter walls, interior ruins, props, lighting, fog, and dust particles.
- **Bug fixes:**
  - PlayerController ground check now includes GridPlane layer (jumping on runtime ground)
  - DestroySceneCameras() added to bootstrapper (scene camera conflict)
  - ConnectionResolver.CreateSource/CreateDestination handle PortOwnerType.Turret

### Shared file changes (CRITICAL)

- **`Scripts/Core/PhysicsLayers.cs`** -- added `FaunaMask`
- **`Scripts/Automation/PortOwnerType.cs`** -- added `Turret` value to enum
- **`Scripts/Automation/BuildingPlacementService.cs`** -- added `PlaceTurret` method with `skipFoundationCheck` parameter
- **`Scripts/Automation/ConnectionResolver.cs`** -- added `PortOwnerType.Turret` cases to CreateSource and CreateDestination (falls through to Storage since turret ports own a StorageContainer)
- **`Scripts/Player/PlayerController.cs`** -- added `(1 << PhysicsLayers.GridPlane)` to GroundMask

No asmdef changes. No ProjectSettings changes. No new packages.

### What needs attention

- ConnectionResolver now has Turret cases that fall through to Storage. If Kevin adds a new PortOwnerType, both CreateSource and CreateDestination need updating or they'll throw.
- The `skipFoundationCheck` parameter on PlaceTurret defaults to `false`. Only the manual turret placement in the playtest bootstrapper passes `true`. PreSeedFactory turret uses `false` (foundations are laid first).
- PlaytestEnvironment generates ruins with a 12-unit clear zone around center. If arena size changes, this value may need adjusting.
- Known minor issue: CameraModeController gets null PlayerController reference (GetComponent before AddComponent in CreatePlayer). FPS mode works, isometric V-key toggle would break.

### Next task

**J-023 (Critical): Merge master into joe/main and port turret code to JoePlaytestSetup.cs.** StructuralPlaytestSetup.cs is deleted on master. The merge should be mostly clean -- no more massive conflict resolution. After merging, port turret code into the commented placeholders in `JoePlaytestSetup.cs`. See J-023 in tasks-joe.md for the full API reference and step-by-step instructions.

After J-023: J-016 (Tower data model and simulation layer, Phase 7 start). Pure C# simulation following D-004 pattern. Read `docs/plans/2026-02-28-tower-design.md` before starting.

### Blockers

None. Phase 4 (Turret Defenses) is fully complete.

### Test status

697/697 passing, 0 failing, 0 skipped, 0 compilation errors, 0 warnings.

No new test files this session (J-013 tests added in previous session).

### Key context

- Turret visual: dark red cylinder base + barrel pivot + elongated cube barrel. Barrel rotates toward current target.
- Turret pre-loads 32 "iron_scrap" ammo via TryInsertStack. When ammo runs out, turret stops firing.
- Turret has 1 input port at direction (-1,0) so inserters can deliver ammo from belts to the west.
- PreSeedFactory turret chain: ammo storage at (5,5) -> belt (6,5)-(8,5) -> turret at (9,5). 200 iron scrap pre-loaded in ammo storage.
- P key triggers PreSeedFactory at runtime (one-shot, guarded by _preSeedTriggered flag).
- PlaytestEnvironment uses seeded System.Random (seed 42, arena size 40). Ground texture is procedural 512x512 Texture2D.
