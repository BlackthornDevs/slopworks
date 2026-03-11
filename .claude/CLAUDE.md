# Slopworks — Claude rules

Post-apocalyptic co-op factory/survival game built in Unity + FishNet. Two-person team: Joe (jamditis) + Kevin (kamditis) at BlackthornDevs. Both developers run parallel builds from the same design doc and merge the best parts — so both Claude instances working in this repo must follow identical rules.

## Engineering principles

Always adhere to the general principles of engineering:

1. Make it work.
2. Make it simple.
3. Make it efficient/fast.
4. Make it secure.

**Prefer GameObject-oriented data over computed values.** If information can be baked into child objects, components, or transform positions on a prefab, do that instead of computing it from mesh bounds, extents, or offsets at runtime. Unity's scene graph is the source of truth. Snap points encode attachment geometry as child transforms -- placement reads those positions directly instead of deriving them from renderer bounds. This principle applies broadly: if you find yourself writing `GetComponent<Renderer>().bounds.extents` to derive a value that could instead be a field on a component or a child object's position, you're overcomplicating it. Unless explicitly asked to interpret mesh data, default to Unity's GameObject-oriented solutions first.

---

## Agent hierarchy

**Kevin's Claude is the lead developer. Joe's Claude is the junior developer.**

Read `docs/coordination/ROLES.md` for full authority rules. The short version:
- Architectural decisions are made by the lead and recorded in `docs/coordination/decisions.md`
- The junior proposes changes via `docs/coordination/contradictions.md`, never decides unilaterally
- Both agents check `decisions.md` before making any architectural choice
- Shared code (`Scripts/Core/`, `ScriptableObjects/`, `ProjectSettings/`) changes go through `master` only

Before starting any session, read:
1. `docs/coordination/decisions.md` -- settled architectural decisions
2. `docs/coordination/ownership.md` -- who owns what
3. `docs/coordination/contradictions.md` -- open questions needing resolution

### Joe's session workflow (auto-pickup)

Joe's Claude follows the auto-pickup protocol in `docs/coordination/tasks-joe.md`. Every session:

1. Merge master: `git fetch origin master && git merge origin/master`
2. Read `docs/coordination/handoff-joe.md` for context from the last session
3. Read `docs/coordination/tasks-joe.md` and pick the next task by priority rules (Critical > High > Medium > Low, lowest J-number first within same priority)
4. Work the task to completion, mark it `Complete` with date and commit hash
5. **Report shared-file changes** -- if you touched asmdef, ProjectSettings, Core scripts,
   or added packages, flag this in `handoff-joe.md` under "Shared file changes"
6. Update `handoff-joe.md` with what was done, what's next, and any blockers
7. Run all EditMode tests. Report exact counts. They must pass before you push.
8. Commit, push to `joe/main`
9. Pick up the next task (repeat from step 3)
10. **When all tasks are done:** use the `slopworks-handoff-joe` skill for session end

No need to wait for Kevin to assign work. Tasks are pre-assigned in `tasks-joe.md`.
Kevin adds new tasks via PR to master. Joe picks them up on the next merge.

**To merge to master:** Push to `joe/main`, then `gh pr create --base master --head joe/main`. Do NOT push directly to master.

### Kevin's session workflow

Kevin reads `docs/coordination/handoff-kevin.md` at session start. Uses the `slopworks-handoff` skill at session end to write handoff notes.

**To merge to master:** Push to `kevin/main`, then `gh pr create --base master --head kevin/main`. Run the `code-review` skill on the PR before merging. Use `gh pr merge` to merge, not `git push origin master`.

---

## Hard rules

These are non-negotiable. Violating any of them creates bugs that are hard to find and expensive to fix.

**Never mutate ScriptableObjects at runtime.** SOs are read-only static definitions shared across all instances. Mutating one corrupts every object of that type. Write per-instance state to `ItemInstance` / `ItemSlot` structs, not the SO.

**Never spawn a NetworkObject per belt item.** At factory scale this kills performance. Belt contents are a `SyncList<BeltItem>` on the segment entity. One NetworkObject per belt segment, full stop.

**Factory simulation runs server-side only.** Belt tick, machine tick, power calculation, and crafting progress run under `if (!IsServerInitialized) return;`. Clients display state synced via SyncVar/SyncList — they never simulate.

**Never use direct cross-scene references.** Unity doesn't allow them, and the three-scene structure means objects in one scene can be unloaded at any time. All cross-scene communication goes through the `GameEventSO` ScriptableObject event bus.

**Never use RPCs for persistent state.** Late-joining players don't receive RPCs fired before they connected. Anything a new client needs on join — machine status, craft progress, inventory, belt contents — goes in SyncVar or SyncList.

**No direct LLM API calls.** Use `claude -p` or `gemini -p` via subprocess. This applies to all tooling and automation code in this repo.

**Never push directly to master.** All changes to master go through a pull request. Push to your branch (`kevin/main` or `joe/main`), then create a PR with `gh pr create`. The other agent or the lead developer reviews before merging. This applies to both Kevin's and Joe's Claude agents -- no exceptions, even for "small" changes like coordination docs. Use `gh pr merge` after review, not `git push origin master`.

**Never cache GetComponent, FindObjectOfType, or FindObjectsOfType results inside Update or FixedUpdate.** Cache in Awake or Start. One allocation per frame across thousands of objects adds up.

**When adding a new PortOwnerType, update ConnectionResolver.** `CreateSource` and `CreateDestination` in `ConnectionResolver.cs` have switch statements over `PortOwnerType`. Adding a new enum value without adding corresponding cases causes exceptions at runtime when ports try to wire up. This is an integration seam that unit tests don't catch — it only fails when you place buildings adjacent to each other.

**Never duplicate placement math, physics constants, or derived values.** Every building type's world position, rotation, and scale must come from a single source of truth -- currently `GridManager`'s universal placement methods. Ghost previews and server spawns call the same method. If you're writing `new Vector3(x, someOffset, z)` for placement anywhere outside GridManager, you're doing it wrong. This rule extends beyond placement: any logic that could apply to multiple building/item/enemy types belongs in a shared method parameterized by the type's data (prefab, definition SO, etc.), not copy-pasted per type.

**Snap placement is snap-to-snap, not computed.** `GetSnapPlacementPosition` finds the ghost prefab's matching snap point (opposite normal, opposite height tier) and positions the ghost so the two snap points meet: `ghostPos = targetSnapPos - Rot * ghostSnapLocalPos`. No extents, no halfHeight, no baseOffset math. Snap point child transforms on the prefab encode all attachment geometry. If a building snaps wrong, fix the snap point positions on the prefab, don't add offset calculations to GridManager.

---

## Before writing C# code

Load the `slopworks-patterns` skill. It has the full `ItemDefinitionSO`/`ItemInstance` code, the NetworkVariable vs RPC decision tree, the belt sync pattern, server authority guard, cross-scene event bus pattern, and a pitfall table.

Load the `slopworks-architecture` skill for design decisions about where a system belongs, what owns what, and how FishNet and Supabase divide responsibility.

---

## Bug-fixing workflow

1. Write a failing test that reproduces the bug.
2. Fix the bug.
3. Verify the test passes. A passing test is the proof.

Don't skip step 1. Server-side factory simulation logic is pure C# with no MonoBehaviour dependencies — it's unusually testable. Use that.

---

## Phase completion standard

**A phase is not done until it is playable.** Every implementation phase must produce a self-contained playtest scene that lets a human verify the system works end-to-end. The pattern:

1. **Simulation layer** — pure C# classes with EditMode tests (D-004 pattern). All tests must pass.
2. **MonoBehaviour wrappers** — thin wrappers that own the simulation objects and spawn placeholder visuals.
3. **Playtest scene** — a `[SystemName]Playtest` scene with a single bootstrapper component. Drop it on an empty GameObject, hit Play, and exercise every feature the phase adds. No prefabs or asset dependencies required -- runtime primitives and `ScriptableObject.CreateInstance` only.
4. **Human verification** — the developer plays the scene and confirms behavior matches intent before the phase is marked complete.

**Playtest scenes must log extensively.** Every user action, placement, removal, validation failure, and state change must produce a `Debug.Log` message. These logs are the primary verification tool -- if something goes wrong, the console should make it obvious what happened and why. Log messages should be short and factual: `"foundation placed at (5,3) level 0"`, `"ramp blocked: cell (5,7) occupied by non-structural building"`, `"wall removed at (5,5) edge north"`.

Existing playtest scenes to reference:
- `PortNodePlaytestSetup.cs` — factory automation chain (belts, machines, inserters)
- `StructuralPlaytestSetup.cs` — structural building, automation, combat, turrets (the main playtest bootstrapper)
- `PlaytestEnvironment.cs` — reusable post-apocalyptic arena generator (procedural ground, ruins, props, lighting, fog). Call `Generate()` from any bootstrapper.

**Pre-seed factory** (`P` key or `_preSeedFactory` checkbox): lays a foundation slab, places a storage->belt->smelter->belt->storage chain and an ammo storage->belt->turret defense chain. Use this to skip manual placement when testing.

---

## Testing requirements

**Unit tests are necessary but not sufficient.** Phase 5 shipped 675 passing EditMode tests and still had 5 bugs that only appeared during manual playtest. Every bug was at an integration seam: component ordering, Unity lifecycle timing, UI hierarchy, input system wiring. Pure C# simulation tests don't catch these.

Every phase must include tests at two levels:

1. **Simulation tests** — pure C# logic (Inventory.AddItem returns correct count, Machine.Tick advances state). These are the existing EditMode tests. Keep writing them.

2. **Integration tests** — verify that systems actually work together the way a player experiences them. These catch the bugs that simulation tests miss:
   - **Component dependency chains** — if B.Awake() calls GetComponent<A>(), test what happens when A is missing or added in the wrong order
   - **Bootstrapper ordering** — test that setup sequences produce working references, not just that individual systems work in isolation
   - **UI structure** — test that runtime-created UI elements have correct parents, nonzero dimensions, and expected component configurations
   - **End-to-end flows** — test the full user action (player walks over item -> item enters inventory -> hotbar updates), not just each step alone

**Do not mark a phase complete based on passing unit tests alone.** If the playtest scene reveals bugs, the test suite had gaps. Write the missing integration test before (or alongside) fixing the bug so the same class of problem is caught automatically next time.

---

## Writing style

Sentence case everywhere — code comments, log messages, UI text, commit messages, variable names where naming allows it. No emojis in source code or logs.

Short, factual log messages. `Debug.Log("belt tick: {0} items", count)` not `Debug.Log("🏭 Belt Tick Initiated Successfully!")`.

---

## Project structure

```
Assets/_Slopworks/
  Scripts/
    Automation/    — belt, machine, grid, power
    Combat/        — weapons, damage, health, fauna AI, pack coordination, wave defense, turrets
    Building/      — structural placement, playtest bootstrappers, PlaytestEnvironment
    Network/       — FishNet setup, Supabase client, save system
    Player/        — character controller, camera rig, input
    World/         — terrain gen, BIM import, chunk loading
    UI/            — HUD, menus, world-space machine panels
    Core/          — game manager, scene loader, item/recipe registry
  ScriptableObjects/
    Items/         — ItemDefinitionSO assets
    Recipes/       — RecipeSO assets
    Events/        — GameEventSO assets (cross-scene event bus)
    Buildings/     — building type definitions
  Materials/
    BIM_Standard.mat  — base material for all Revit-imported geometry
    Decals/           — wear, damage, rust decal materials
    Environment/      — arena materials (ground, wall, crate, barrel, etc.)
  Prefabs/
    Machines/
    Belt/
    Player/
    Enemies/          — Enemy_Basic prefab (capsule + NavMeshAgent + FaunaController)
    UI/
```

Underscore prefix on `_Slopworks/` sorts it to the top of the Project window and separates game code from Unity built-ins and third-party assets.

---

## Scene structure

```
Scenes/
  Core/
    Core_Network.unity       — NetworkManager, FishNet (ALWAYS loaded, never unloaded)
    Core_GameManager.unity   — session state, threat level, wave controller
  HomeBase/
    HomeBase_Terrain.unity   — ground, resource nodes
    HomeBase_Grid.unity      — factory grid, belt network, machines
    HomeBase_UI.unity        — HUD, build menu, inventory
    HomeBase_Lighting.unity  — baked GI
  Buildings/
    Building_Template.unity  — base for all reclaimed buildings
    [BuildingName].unity     — one scene per building
  Overworld/
    Overworld_Map.unity      — territory, supply lines
    Overworld_UI.unity       — overworld HUD
```

`Core_Network.unity` is always loaded first and never unloaded. The NetworkManager lives there. `ItemRegistry` and `RecipeRegistry` live in Core — loaded once at startup.

Scene loading is host-initiated: `NetworkManager.SceneManager.LoadScene` loads the same scene for all connected clients simultaneously.

---

## FishNet authority model

| Object type | Owner | Sync mechanism |
|-------------|-------|----------------|
| Player character | Client | ClientRpc prediction + server validation |
| Factory machines | Server | SyncVar for status, ServerRpc for config |
| Belt items | Server | SyncList on segment entity |
| Inventory | Server | SyncList for slots, ServerRpc for operations |
| Building placement | Server | Client requests, server validates + spawns |
| Turrets | Server | Local playtest only for now, no NetworkBehaviour yet |
| World chunks | Server | Generated server-side, sent to clients on demand |

---

## Supabase integration

The project URL and anon key are in `Assets/StreamingAssets/supabase-config.json` (gitignored). Copy from `supabase-config.template.json` and set `"buildVersion"` to `"joe"` or `"kevin"` depending on whose machine this is.

Supabase and FishNet are separate systems that only touch at discrete events:

| Event | FishNet | Supabase |
|-------|---------|----------|
| Player creates session | StartServer | Insert game_sessions |
| Player joins | StartClient | Insert session_players |
| Player disconnects | OnClientDisconnect | Update session_players.status |
| Autosave | — | Upsert world_state + player_saves |
| Session ends | StopServer | Update game_sessions.status |

If the data must survive a server crash and be available to a new session, it goes to Supabase. If it only matters while the session is live, it stays in FishNet.

---

## Git and branches

Joe works on `joe/main`, Kevin on `kevin/main`. Short-lived feature branches off your own main — don't let branches run more than a day or two. Merge back frequently.

Unity YAML files (`.unity`, `.prefab`, `.asset`, `.mat`) use UnityYAMLMerge — configured in `.gitattributes`. Binary assets (textures, audio, meshes) use Git LFS.

Render pipeline assets (URP Pipeline Asset, Renderer Asset) are binary ScriptableObjects that don't merge cleanly. Joe owns render configuration. Request changes in `docs/render-requests.md`.

---

## Reference docs

All major architectural decisions are documented in `docs/reference/`. Check here before searching the web.

| File | Covers |
|------|--------|
| `multiplayer.md` | FishNet + FishySteamworks, NetworkVariable vs RPC, belt sync pattern, ParrelSync |
| `factory-automation.md` | Belt data structure (distance-offset), machine state machine, simulation tick |
| `crafting-inventory.md` | ItemDefinitionSO/ItemInstance split, RecipeSO, ItemRegistry, serialization |
| `world-generation.md` | Three world spaces, noise stack, BIM pipeline overview |
| `team-workflow.md` | gitattributes, UnityYAMLMerge, multi-scene merge strategy, MCP Unity |
| `render-pipeline.md` | URP setup, camera stacking, SRP Batcher for BIM, belt item GPU instancing |
| `input-system.md` | New Input System, two Action Maps (Factory/Exploration), generated C# class, camera toggle |
| `audio.md` | FMOD Studio, adaptive factory audio parameters, machine loops, two-dev ownership |
| `addressables.md` | Group structure, async scene loading, address constants, remote catalog for DLC |
| `fauna-ai.md` | NPBehave behavior trees, server-only AI, perception system, wave controller, pack coordination |
| `physics-layers.md` | Layer assignments (slots 8–19), collision matrix, raycast mask constants |
| `testing.md` | EditMode for simulation logic, PlayMode for FishNet, testable C# patterns |
| `service-architecture.md` | VContainer DI, scope hierarchy, registration, MonoBehaviour injection |
| `ui-framework.md` | uGUI primary, world-space machine panels, FishNet SyncVar binding, UI Toolkit future |
| `supabase-unity-sdk.md` | supabase-csharp, UniTask, JSONB upsert, authentication, thread safety |

Additional reference docs are added as architectural decisions are made. Check for new files before assuming a decision hasn't been made yet.

---

## Adding a new automation building type

Follow this checklist every time. Missing any step causes silent runtime failures.

1. **DefinitionSO** — create `[BuildingName]DefinitionSO : ScriptableObject` implementing `IPlaceableDefinition` (size, ports, ID)
2. **Simulation class** — pure C# following D-004 pattern (e.g. `TurretController`). EditMode tests.
3. **PortOwnerType** — add new value to `PortOwnerType` enum
4. **ConnectionResolver** — add cases for the new type in both `CreateSource` and `CreateDestination`. If the port owner is a `StorageContainer`, fall through to the Storage case.
5. **BuildingPlacementService** — add `Place[BuildingName]` method following `PlaceMachine`/`PlaceStorage`/`PlaceTurret` pattern
6. **GridManager placement method** — add a `Get[BuildingName]WorldPos()` method to `GridManager`'s universal placement block. Derive Y offset from the prefab via `GetPrefabHalfHeight()`. Both ghost preview (NetworkBuildController) and server spawn (GridManager.CmdPlace*) must call this single method. Never hardcode offsets inline.
7. **Prefab** — add a `_[buildingName]Prefab` serialized field to `GridManager`, a public getter, and wire it in `FactoryPrefabSetup.cs`. Ghost preview uses the prefab via `EnsurePrefabGhost()`.
8. **MonoBehaviour wrapper** — thin wrapper (e.g. `TurretBehaviour`) using inactive-then-activate pattern
9. **StructuralPlaytestSetup** — add tool mode, build page slot, input handler, visual spawner, OnGUI stats
10. **PreSeedFactory** (optional) — add a pre-built chain to verify the full automation loop

Reference implementation: turret system (J-013 through J-015). Files: `TurretController.cs`, `TurretDefinitionSO.cs`, `TurretBehaviour.cs`, plus modifications to `PortOwnerType.cs`, `ConnectionResolver.cs`, `BuildingPlacementService.cs`, `StructuralPlaytestSetup.cs`.

---

## Current phase status

- **Phase 3** (Combat): Complete — weapons, enemies, AI, waves
- **Phase 4** (Turret Defenses): Complete — J-013, J-014, J-015
- **Phase 5** (Core UI + Inventory): Complete (Kevin)
- **Phase 7** (The Tower): Next for Joe — starts at J-016. Tower contracts bible complete (15 contracts, 6 buildings). Website tower page updated with progression tree, contract cards, and environmental hazards.
- **Phase 6** (Building Exploration): Next for Kevin
