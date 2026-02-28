# Tasks for Joe (junior developer)

Work on `joe/main`. Merge `master` into `joe/main` regularly to pick up new tasks and coordination updates.

---

## Auto-pickup protocol

**Follow this every session, no exceptions.**

1. `git fetch origin master && git merge origin/master` -- pick up new tasks and decisions
2. Read `docs/coordination/handoff-joe.md` -- context from your last session
3. Read `docs/coordination/contradictions.md` -- check for new issues assigned to you
4. Scan this file for the next task to work on (see priority rules below)
5. Start working. When done, mark the task `Complete`, write handoff notes, commit, push.

### Task priority rules

Pick the **first matching** task from this list:

1. Any task with status `In Progress` (you started it last session, finish it)
2. Any task with status `Pending` and priority `Critical`
3. Any task with status `Pending` and priority `High`
4. Any task with status `Pending` and priority `Medium`
5. Any task with status `Pending` and priority `Low`

If multiple tasks share the same priority, pick the **lowest J-number** first (earlier tasks set up context for later ones). If a task has `Depends on` that aren't complete, skip it and pick the next one.

### When you finish a task

1. Mark its status as `Complete` with the date and commit hash
2. Update `docs/coordination/handoff-joe.md` with what you did, what's next, and any blockers
3. Run all EditMode tests -- they must pass before you push
3b. Check for shared file changes. If you modified ANY of these, note it prominently in handoff-joe.md:
    - Slopworks.Runtime.asmdef or any .asmdef file
    - Anything in ProjectSettings/
    - Anything in Scripts/Core/
    - Any ScriptableObject definition (fields, not assets)
    - Any new package dependency
4. Commit and push to `joe/main`
5. Check this file again for the next task (repeat the priority rules)
6. If no pending tasks remain, use the `slopworks-handoff-joe` skill to run the full handoff process

### When you finish ALL tasks

When no pending tasks remain:
1. Use the `slopworks-handoff-joe` skill to run the full handoff process
2. This ensures compilation verification, test reporting, and shared-file change tracking
3. Do not just write "all tasks complete" -- run the skill

### When you hit a blocker

If you can't proceed because of an architectural question or a dependency on Kevin's code:
1. Write the question in `docs/coordination/contradictions.md` following the C-NNN format
2. Note the blocker in `handoff-joe.md`
3. Skip to the next available task (don't wait)

---

## Completed tasks

### TASK J-001: FPS Character Controller + Camera Toggle

**Status:** Complete (2026-02-28)
**Commits:** `c251da1`, `ac55db6`

### TASK J-002: Clean up TMP extras and merge to master

**Status:** Complete (2026-02-28)
**Commits:** `d5dde89`, `c45d3be`

### TASK J-003: Health and damage system

**Status:** Complete (2026-02-28)
**Commits:** `70ffac3`
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Built HealthComponent (plain C#), HealthBehaviour (thin wrapper), DamageData struct, DamageType enum. Tests in `HealthComponentTests.cs`.

### TASK J-004: Basic hitscan weapon

**Status:** Complete (2026-02-28)
**Commits:** `7699c61`
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Built WeaponController (plain C#), WeaponBehaviour (wrapper with raycast), WeaponDefinitionSO. Tests in `WeaponControllerTests.cs`.

### TASK J-005: Enemy AI with NPBehave

**Status:** Complete (2026-02-28)
**Commits:** `d2b7281`
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

**Notes:**
- NPBehave vendored from github.com/meniku/NPBehave (not snozbot -- reference doc URL was wrong)
- Custom asmdef created for NPBehave to resolve assembly boundaries with Slopworks.Runtime
- `UnityEngine.Random` must be fully qualified due to NPBehave.Random name collision
- Enemy_Basic exists in Dev_Test scene with all components; prefab asset needs manual drag-to-project
- PlayMode tests noted but not written -- behavior tree + NavMesh require running game loop

### TASK J-006: Wave defense system

**Status:** Complete (2026-02-28)
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Built WaveController (plain C#), WaveControllerBehaviour (wrapper), ThreatMeter, WaveDefinition. Tests in `WaveControllerTests.cs`.

---

## Code review fixes (2026-02-28)

Lead developer reviewed J-003 through J-006 commits on master. Issues logged in `contradictions.md` as C-004 through C-007. **All four fixed.**

### TASK J-007: Fix server authority violations (C-004)

**Status:** Complete (2026-02-28)
**Priority:** Critical
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Added `if (!IsServerInitialized) return;` guards to EnemySpawner, WaveControllerBehaviour, FaunaController. Converted all four combat MonoBehaviours to NetworkBehaviour (required adding FishNet.Runtime to Slopworks.Runtime.asmdef). WeaponBehaviour now validates damage server-side via `[ServerRpc] ServerFireWeapon(Vector3 origin, Vector3 direction)` — client does raycast for visual feedback only, server re-validates and applies damage.

### TASK J-008: Extract FaunaAI from FaunaController (C-005)

**Status:** Complete (2026-02-28)
**Priority:** High
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Created `FaunaAI.cs` (plain C#) with attack timing, threat evaluation, pack coordination, alert evaluation, aggression management, and strafe direction. FaunaController is now a thin wrapper delegating to FaunaAI. 23 EditMode tests in `FaunaAITests.cs` cover all simulation logic.

### TASK J-009: Remove GameObject.Find usage (C-006)

**Status:** Complete (2026-02-28)
**Priority:** Medium
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`, `Scripts/UI/`

PlayerHUD now uses `[SerializeField]` references for HealthBehaviour, WeaponBehaviour, WaveControllerBehaviour, CameraShake (wired at editor time via PlaytestSetup). WeaponBehaviour's `_hitMarker` is now a `[SerializeField]`. EnemyKnockback uses `damage.sourcePosition` (new field on DamageData) instead of `GameObject.Find(sourceId)`. Zero Find calls remain in combat or UI code.

### TASK J-010: Cache GetComponent in FaunaController (C-007)

**Status:** Complete (2026-02-28)
**Priority:** Low
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Added `_cachedTargetHealth` field. UpdatePerception caches HealthBehaviour once on target change, clears on target loss. MeleeAttack uses the cached reference. GetComponent called once per target acquisition, not per attack.

---

## asmdef reference fixes

### TASK J-011: Add NPBehave reference to Slopworks.Runtime.asmdef

**Status:** Pending
**Priority:** Critical
**Branch:** `joe/main`
**Ownership:** `Scripts/` (shared file)

J-005 vendored NPBehave and J-007 converted combat scripts to NetworkBehaviour, but the NPBehave GUID reference (`b23d0b8134b59074db4ef602bb53a3c5`) was not added to `Slopworks.Runtime.asmdef` on joe/main. This causes compilation failures when merging to other branches.

**Acceptance criteria:**
- `Slopworks.Runtime.asmdef` includes `GUID:b23d0b8134b59074db4ef602bb53a3c5` in its references array
- Zero compilation errors after recompile
- Note this in handoff as a shared file change

### TASK J-012: Add FishNet and NPBehave references to Slopworks.Tests.EditMode.asmdef

**Status:** Pending
**Priority:** Critical
**Branch:** `joe/main`
**Ownership:** `Tests/` (shared file)

`PackCoordinatorTests.cs` uses `FaunaController` (which extends `NetworkBehaviour`) and `PackCoordinator` (which uses `NPBehave.Blackboard`). The test assembly needs direct references to both transitive dependencies to compile.

**Acceptance criteria:**
- `Slopworks.Tests.EditMode.asmdef` includes `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (FishNet.Runtime) and `GUID:b23d0b8134b59074db4ef602bb53a3c5` (NPBehave) in its references array
- All 666 EditMode tests pass
- Zero compilation errors after recompile
- Note both asmdef changes in handoff as shared file changes

**Depends on:** J-011

---

## Phase 3 completion notes

**All tasks (J-003 through J-006) complete and merged to master** (2026-02-28, commit `7bdb704`).
**Code review fixes (J-007 through J-010) complete** (2026-02-28, commit `9b71b82`). Merged to master.

### Additional work beyond task specs

**Pack-coordinated group AI** — extends J-005's FaunaController with three new systems:
- `PackCoordinator.cs` — singleton-per-fauna-type, shared NPBehave blackboard for alert propagation, confidence-based morale, distributed flank angles
- `CombatMovement.cs` — static utility for strafe, flank, and cover-seeking position calculations
- Behavior tree expanded from 4 to 6 priority branches: hurt > ally death reaction > flee (cover-seeking) > combat (melee/strafe/flank/chase) > alert investigation > wander
- 24 EditMode tests across `PackCoordinatorTests.cs` and `CombatMovementTests.cs`

**Combat effects** — CameraRecoil, CameraShake, MuzzleFlash, ProjectileTracer, EnemyHitFlash, EnemyKnockback, HitMarkerUI

**Dev_Test arena** — playable test scene with cover structures, spawn points, environment materials, and EnvironmentSetup editor tool

### Interfaces available for Phase 4

Phase 4 is now assigned to Joe. Use these existing systems:
- `HealthComponent` / `HealthBehaviour` -- attach to anything that takes damage
- `DamageData` / `DamageType` -- pass to `HealthComponent.TakeDamage()`
- `FaunaDefinitionSO` -- define new enemy types with pack behavior fields
- `GameEventSO` (EnemyDied) -- listen for enemy deaths
- `PhysicsLayers` constants -- layer masks for raycasts
- `BuildingPlacementService` -- place turrets on factory grid (like machines)
- `PortNode` system -- turrets can have input ports for ammo delivery via belts
- `PowerNetwork` / `PowerNetworkManager` -- turrets check `GetSatisfaction()` to operate
- `StorageContainer` -- turret internal ammo inventory
- `MachineDefinitionSO` / `MachinePort` -- reference pattern for turret SO + port definitions

---

## Phase 4: Turret Defenses

### TASK J-013: Auto-turret simulation layer

**Status:** Pending
**Priority:** High
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Build the turret as a plain C# simulation object (D-004 pattern). The turret detects enemies, rotates toward them, and fires on interval. It consumes ammo from an internal `StorageContainer` and requires power to operate.

**Files to create:**
- `Scripts/Combat/TurretController.cs` -- plain C# turret logic
- `Scripts/Combat/TurretDefinitionSO.cs` -- read-only turret stats (range, fire rate, damage, ammo type, power consumption)
- `Tests/Editor/EditMode/TurretControllerTests.cs` -- EditMode tests

**Implementation:**
1. `TurretDefinitionSO`: range, fireInterval, damagePerShot, damageType, ammoItemId, powerConsumption, size, ports (MachinePort[] for ammo input)
2. `TurretController`: plain C# class, takes TurretDefinitionSO
   - `Tick(float deltaTime)` -- called by simulation
   - Internally owns a `StorageContainer` for ammo
   - Tracks current target, rotation toward target, fire cooldown
   - Target selection: nearest enemy within range (pass candidate list, don't use Physics)
   - Fire: consume 1 ammo from internal storage, return `TurretFireEvent` struct (target, damage)
   - Power check: takes satisfaction float (0-1), won't fire if satisfaction < threshold (e.g. 0.5)
   - Does NOT do raycasting or Unity physics -- pure simulation logic
3. Tests: fire rate timing, ammo consumption, no-ammo stops firing, no-power stops firing, target selection (nearest), no target when out of range

**Acceptance criteria:**
- TurretController is pure C# with no MonoBehaviour dependency
- All tests pass
- Follows D-004 pattern (simulation object, not MonoBehaviour)

### TASK J-014: Turret MonoBehaviour wrapper and placement

**Status:** Pending
**Priority:** High
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`
**Depends on:** J-013

Build the thin MonoBehaviour wrapper and integrate turret placement into the playtest scene.

**Files to create:**
- `Scripts/Combat/TurretBehaviour.cs` -- thin wrapper
- Modify `Scripts/Building/StructuralPlaytestSetup.cs` -- add tool [8] for turret placement

**Implementation:**
1. `TurretBehaviour`: thin wrapper around TurretController
   - `FixedUpdate`: call `TurretController.Tick()` with nearby enemies
   - Uses `OverlapSphere` to find enemies in range, passes to controller
   - When controller returns a fire event, apply `DamageData` to target's `HealthComponent`
   - Visual: rotate barrel transform toward current target
2. Add turret as tool [8] in StructuralPlaytestSetup:
   - Create `TurretDefinitionSO` at runtime (like smelter/storage)
   - Place via `BuildingPlacementService.PlaceMachine()` (turrets are machines that consume ammo)
   - Port definition: one input port for ammo belt connection
   - Turret visual: cylinder base + elongated cube barrel
   - Port indicators like machines (blue input for ammo)
3. Add turret stats to OnGUI overlay

**Acceptance criteria:**
- Turret placeable on foundations via tool [8]
- Turret auto-targets and kills enemies in range
- Turret consumes ammo from internal storage
- Belt can deliver ammo to turret via port connection
- Turret stops firing when out of ammo

### TASK J-015: Turret playtest scene

**Status:** Pending
**Priority:** Medium
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`, `Scenes/`
**Depends on:** J-014

Create a combined turret defense playtest that verifies the full loop: place turrets, feed them ammo via belts, trigger enemy waves, watch turrets defend.

**Implementation:**
1. Either extend StructuralPlaytest scene or create a new `TurretPlaytest.unity`
2. Pre-seed option: storage with ammo -> belt -> turret, enemy spawner nearby
3. Wave trigger key (e.g. W) to spawn a wave of enemies
4. Verify: ammo flows on belt -> inserter loads turret -> turret fires at enemies -> enemies die
5. Verify: turret stops when ammo runs out, stops when power disconnected (if power network wired)

**Acceptance criteria:**
- Playable end-to-end: build turret, connect ammo supply, trigger wave, watch defense
- Turret kills enemies, consumes ammo, logs activity
- Enemies path toward base and get intercepted
