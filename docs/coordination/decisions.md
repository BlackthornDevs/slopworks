# Architectural decisions

Settled decisions that both agents follow. Do not re-litigate unless new information is added to `contradictions.md` and the lead resolves it.

---

## D-001: No dependency injection framework

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** `team-workflow.md` says "no DI framework." `service-architecture.md` says use VContainer.

**Decision:** No DI framework. Use plain C# classes with constructor parameters for simulation logic (testable without mocks or containers). Use `[SerializeField]` references for MonoBehaviour wiring. `GetComponent` cached in `Awake`/`Start`.

**Rationale:** The testing doc already requires simulation logic in plain C# classes -- that gives constructor-based DI for free. VContainer adds learning curve for a team where one developer is new to Unity. The project doesn't have the scale or complexity where a DI container pays for itself.

**Impact:** Ignore `service-architecture.md` VContainer patterns. `ItemRegistry` and `RecipeRegistry` are MonoBehaviour singletons in the Core scene (acceptable because Core is never unloaded). Not static classes -- scene-bound components with `[SerializeField]` references to SO arrays.

---

## D-002: SceneManager now, Addressables later

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** `team-workflow.md` uses raw `SceneManager`. `addressables.md` says Addressables only.

**Decision:** Use `SceneManager.LoadSceneAsync` for the vertical slice. Wrap all scene loading calls behind a `ISceneService` interface so migration to Addressables is a single implementation swap.

**Rationale:** Vertical slice has 4-5 scenes. Addressables adds async complexity, group management, address constants, and a build pipeline that two Unity-new developers don't need yet. When building DLC or exceeding ~20 scenes, swap the implementation.

**Impact:** `addressables.md` patterns are deferred. Don't install the Addressables package yet. Scene references use string names or build index.

---

## D-003: New Input System (not legacy)

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** Implementation plan uses legacy `Input.GetKeyDown`. `input-system.md` bans legacy Input.

**Decision:** Use Unity's New Input System with two Action Maps (Factory and Exploration). Generate `SlopworksControls` C# wrapper. Never use `Input.GetKeyDown` or `Input.GetMouseButtonDown`.

**Rationale:** Two camera modes (FPS/Isometric) need distinct input bindings. The New Input System's Action Maps are designed for exactly this. Legacy Input can't cleanly handle the mode toggle.

**Impact:** Implementation plan tasks 1.2, 2.1, 2.2 must use `SlopworksControls` callbacks instead of legacy Input calls. Input setup must happen in Phase 0 (create `.inputactions` asset, generate C# class).

---

## D-004: Simulation logic in plain C# classes

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** `testing.md` requires simulation logic in plain C# classes. Plan implements everything as MonoBehaviours.

**Decision:** All simulation logic (belt tick, machine tick, grid operations, power calculation, inventory operations) lives in plain C# classes. MonoBehaviours are thin wrappers that hold Unity references and call the C# class each frame/tick.

**Rationale:** Plain C# classes are testable in EditMode without Play Mode overhead. Tests run in under 1 second. MonoBehaviour-only code requires Play Mode tests which are slow and flaky.

**Impact:** Every simulation class gets split: `BeltSimulation` (plain C#) + `BeltSegmentBehaviour` (MonoBehaviour wrapper). `MachineSimulation` (plain C#) + `MachineComponent` (MonoBehaviour wrapper). Tests target the plain C# class.

---

## D-005: Physics layers set up before any prefab work

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** `physics-layers.md` defines layers 8-19. Retroactive changes require touching every prefab.

**Decision:** Physics layer matrix and `PhysicsLayers` constants class are set up in Phase 0, before any prefab or scene work begins. Layer assignments per `physics-layers.md`.

**Impact:** New Phase 0 task. `PhysicsLayers.cs` provides layer numbers and pre-computed raycast masks. All raycast code uses constants, never magic numbers or `LayerMask` SerializeFields for standard masks.

---

## D-006: Supabase deferred, local JSON saves for vertical slice

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** Multiple docs reference Supabase. Vertical slice doesn't need cloud persistence.

**Decision:** No Supabase for the vertical slice. Save to local JSON files at `Application.persistentDataPath`. Supabase integration happens post-vertical-slice when lobby discovery and cross-session persistence are needed.

**Impact:** Don't install supabase-csharp or UniTask (UniTask was only needed for Supabase and Addressables, both deferred). Save system writes JSON via `JsonUtility` or `Newtonsoft.Json` (included with Unity).

---

## D-007: NPBehave for fauna AI (not basic state machine)

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** Plan uses basic NavMeshAgent state machine. `fauna-ai.md` specifies NPBehave behavior trees.

**Decision:** Use NPBehave for all fauna AI. Install from GitHub into `Assets/_Slopworks/Plugins/`. NavMeshAgent still handles pathfinding -- NPBehave wraps it in behavior tree actions.

**Rationale:** Behavior trees scale better than hand-rolled state machines. NPBehave is lightweight, server-only compatible, and the reference doc already has the full tree structure designed.

**Impact:** Plan task 3.2 (EnemyAI) uses NPBehave behavior tree instead of if/else state machine. NPBehave installed in Phase 0.

---

## D-008: uGUI for UI, UI Toolkit deferred

**Date:** 2026-02-27
**Resolved by:** Lead (Kevin's Claude)
**Context:** `ui-framework.md` specifies uGUI primary, UI Toolkit deferred.

**Decision:** uGUI for all UI work in the vertical slice. World-space Canvas for machine status panels. Screen Space Overlay for HUD. No UI Toolkit until Phase 2+.

**Impact:** Consistent with plan. No changes needed.

---

## D-009: One playtest scene per developer, incrementally growing

**Date:** 2026-03-01
**Resolved by:** Lead (Kevin's Claude)
**Context:** Phase 5 created Phase5Playtest as a separate scene instead of adding to StructuralPlaytest. Three isolated islands instead of two growing scenes.

**Decision:** Each developer has one playtest scene that grows each phase. Kevin's scene is StructuralPlaytest (building + automation + inventory + crafting + combat). Joe's scene is Dev_Test (combat focus). New features get added to the existing scene, never a separate playtest scene per phase.

**Rationale:** Isolated playtest scenes don't test feature interactions. A growing scene ensures each new system works alongside existing ones. Two scenes (one per developer) keeps merge conflicts manageable.

**Impact:** Phase5Playtest deleted. Phase 5 features (PlayerInventory, HUD, crafting) merged into StructuralPlaytestSetup. Future phases add to StructuralPlaytestSetup, not new scenes.

---

## D-010: Hotbar is an action bar with switchable pages

**Date:** 2026-03-01
**Resolved by:** Lead (Kevin's Claude)
**Context:** Hotbar was directly tied to inventory slots. Need build tools on the same hotbar strip.

**Decision:** Hotbar is a generic action bar with switchable pages. Page 0 = inventory items (bound to PlayerInventory slots 0-8). Page 1 = build tools (mapped to ToolMode values). B key toggles between pages. Digit keys select the slot on the current page.

**Rationale:** Keeps UI simple (one strip, two contexts) rather than a separate build menu. Switching to items page cancels any active build tool. Escape returns to items page.

**Impact:** HotbarSlotUI gains `SetEntry()` for non-inventory display. PlayerHUD manages pages via `HotbarPage[]`. StructuralPlaytestSetup digit-key tool selection removed in favor of page-based selection.

---

## D-011: NetworkBehaviour local play guard pattern

**Date:** 2026-03-01
**Resolved by:** Lead (Kevin's Claude)
**Context:** Playtest bootstrappers create objects at runtime without FishNet. All combat scripts extend NetworkBehaviour with `if (!IsServerInitialized) return;` guards. Without a properly spawned NetworkObject, accessing `IsServerInitialized` or `IsOwner` throws NRE because `_networkObjectCache` is null. FishNet cannot spawn runtime-created objects (requires registered prefab collections).

**Decision:** Prefix all NetworkBehaviour authority guards with a null check: `if (NetworkObject != null && !IsServerInitialized) return;`. The `NetworkObject` property returns `_networkObjectCache` directly -- when null (no FishNet), the guard is skipped. When present (multiplayer), the guard works normally.

**Rationale:** Runtime-created objects in playtest scenes will never have a NetworkObject. Booting FishNet as local host was attempted and failed -- NetworkManager.Awake() requires SpawnablePrefabs, and runtime NetworkObjects don't get properly initialized through FishNet's spawn pipeline. The null-check pattern is backward-compatible, requires no FishNet changes, and lets the same scripts work in both local and networked modes.

**Impact:** All NetworkBehaviour scripts in playtest scenes must use this pattern. Applied to WeaponBehaviour, EnemySpawner, WaveControllerBehaviour, FaunaController. Future NetworkBehaviour scripts that need to work in local playtests should follow the same pattern. Do not attempt to boot FishNet in bootstrapper scenes.

---

## D-012: IPlaytestFeatureProvider for multi-developer scene integration

**Date:** 2026-03-02
**Resolved by:** Lead (Kevin's Claude)
**Context:** Two developers have exclusive bootstrapper scenes. No single place where all features run together. Merging to master doesn't verify feature coexistence.

**Decision:** Each dev's bootstrapper implements `IPlaytestFeatureProvider` interface. `MasterPlaytestSetup` orchestrator discovers all providers on its GameObject and calls them in 7-phase order. Bootstrappers detect master mode via `GetComponent<MasterPlaytestSetup>() != null` and skip their standalone Awake.

**Phases:** (1) CreateDefinitions, (2) ConfigureBuildPage, (3) RegisterToolHandlers, (4) CreateWorldObjects, (5) CreateCombatSetup, (6) PreSeed, (7) WireHUD. Runtime: FixedTick, UpdateInput, DrawGUI, Cleanup.

**Wave controller deconfliction:** First non-null return from `CreateCombatSetup()` wins. Kevin returns his controller (also creates building enemies). Joe returns null in master mode.

**WireHUD timing:** Providers return no-yield coroutines (`yield break`). Orchestrator handles the 2-frame delay once. Standalone bootstrappers have their own delayed wrappers.

**Rationale:** Neither dev works in the master scene directly. It serves as a merge gate: all features must coexist before a PR to master can be approved. The interface contract is explicit about what each provider contributes.

**Impact:** New files: `IPlaytestFeatureProvider.cs`, `MasterPlaytestSetup.cs`, `MasterPlaytest.unity`. Modified: both bootstrappers (implement interface + `_isStandalone` guard), `PlaytestToolController.cs` (static helpers), `PlaytestValidator.cs` (master scene checks).

---

## D-013: PhysicsLayers.cs is master-only

**Date:** 2026-03-02
**Resolved by:** Lead (Kevin's Claude)
**Context:** Both kevin/main and joe/main independently added mask fields to `PhysicsLayers.cs`, causing a merge conflict on master. PhysicsLayers is in `Scripts/Core/` which is documented as shared (master only) in ownership.md, but the rule was not enforced.

**Decision:** `PhysicsLayers.cs` must never be edited on feature branches. All changes go through a direct PR to master. Both branches then merge master to pick up the change. This matches the existing ownership.md rule for `Scripts/Core/`.

**Rationale:** PhysicsLayers is a single flat file with layer constants and mask fields. Any two developers adding fields at adjacent lines will produce a merge conflict. Since the file has no logical sections that can be owned separately, the only safe workflow is sequential edits through master.

**Impact:** Before adding a new layer or mask, create a small PR to master with just the PhysicsLayers change. Do not bundle it with feature work on kevin/main or joe/main.

---

## D-014: MasterPlaytest scene must pass before merging to master

**Date:** 2026-03-02
**Resolved by:** Lead (Kevin's Claude)
**Context:** PR #17 merged to master without loading the MasterPlaytest scene. The phase completion standard requires a playable scene, and D-012 established MasterPlaytest as the merge gate, but neither agent was actually running it before merging.

**Decision:** Before merging any PR to master, the MasterPlaytest scene must be loaded and played. At minimum:
1. Load `Scenes/Playtest/MasterPlaytest.unity`
2. Hit Play -- scene boots without errors
3. PlaytestValidator reports no failures
4. Verify the features touched by the PR work in the master scene (not just the dev's standalone scene)

EditMode tests passing is necessary but not sufficient. The MasterPlaytest scene catches integration bugs that unit tests miss (component ordering, ghost cleanup, tool handler registration, HUD wiring).

**Rationale:** PR #15 and #17 both merged with passing tests but untested scene integration. C-009 (turret ghost cleanup) was only caught when Joe manually ran MasterPlaytest for J-024. If the scene had been run before merging, C-009 would have been caught earlier.

**Impact:** Both agents must run MasterPlaytest before creating a PR to master. The PR description should include a "MasterPlaytest verified" checkbox. Code reviewers should check for this before approving.

---

## D-015: Multiplayer-first architecture (FishNet host-client)

**Date:** 2026-03-05
**Resolved by:** Lead (Kevin's Claude)
**Context:** Vertical slice complete with bootstrapper architecture. Need to decide whether to continue fleshing out mechanics in single-player bootstrapper or convert to multiplayer now.

**Decision:** Convert to multiplayer now using FishNet host-client model with Tugboat transport. 7-step conversion sequence: (1) Scene+Network+Player, (2) Factory Grid+Placement, (3) Inventory+Items, (4) Machines+Belts+Simulation, (5) Combat, (6) Tower+Buildings, (7) Persistence (Supabase). Each step produces a playable two-player milestone.

**Rationale:** Building more mechanics in the bootstrapper just creates more code to convert later. The simulation layer (pure C# classes) transfers directly. Better to set up the networked foundation now and build new features directly on it. Host-client model means day-to-day dev is the same: hit Play, FishNet boots as host, test locally.

**Impact:** New scene structure: `Assets/_Slopworks/Scenes/Multiplayer/HomeBase.unity`. New prefab structure: `Assets/_Slopworks/Prefabs/Buildings/Foundations/`. Bootstrapper scenes remain for reference but are no longer the primary development target. Feature branch: `kevin/multiplayer-step1`.

---

## D-016: Tugboat transport for all dev and LAN play

**Date:** 2026-03-05
**Resolved by:** Lead (Kevin's Claude)
**Context:** Need transport choice for FishNet. Options: Tugboat (TCP/UDP, built-in), FishySteamworks (Steam relay).

**Decision:** Tugboat for all development and LAN play. FishySteamworks deferred to post-foundation. Tugboat supports both host-client and dedicated server (Ubuntu mini PC) without code changes.

**Rationale:** Tugboat works immediately, no Steam SDK setup. LAN play between two local machines (dev workflow) is the primary use case. FishySteamworks can be added later as a transport swap without changing game code.

**Impact:** No Steam SDK dependency. Players on the same network connect via IP address. Dedicated server on Ubuntu mini PC uses same Tugboat transport.

---

## D-017: Snap-to-snap placement (no computed offsets)

**Date:** 2026-03-09
**Resolved by:** Lead (Kevin's Claude)
**Context:** Snap placement was computing Y offsets, half-depths, and base offsets from renderer bounds. This broke for FBX prefabs with non-center origins (baseOffset=0) and required constant hacks for different building types.

**Decision:** Snap placement uses snap-to-snap alignment. Both the target building and the ghost prefab have snap point children. `FindGhostAttachSnap` pairs them (opposite normal, opposite height tier: _Bot->_Top, _Top->_Bot, _Mid->_Mid). Ghost position = `targetSnapPos - Rot * ghostSnapLocalPos`. No extents, no halfHeight, no baseOffset math.

**Rationale:** Snap point child transforms already encode all attachment geometry. Computing offsets from renderer bounds duplicates information, breaks on non-center-origin meshes, and requires per-type special cases. The snap points are the single source of truth.

**Impact:** Fix snap geometry by moving snap point children on the prefab, not by adding offset calculations to GridManager. All building prefabs must have snap point children for snap placement to work. `GetPrefabBaseOffset` and `GetPrefabExtents` are still used for grid-mode placement but NOT for snap-mode.

---

## D-018: Prefer GameObject-oriented data over computed values

**Date:** 2026-03-09
**Resolved by:** Lead (Kevin's Claude)
**Context:** Multiple bugs in the snap system were caused by computing values from renderer bounds at runtime (baseOffset=0 for FBX, wrong renderer found in hierarchy, etc).

**Decision:** If information can be baked into child objects, components, or transform positions on a prefab, do that instead of computing from mesh bounds at runtime. Unless explicitly asked to interpret mesh data, default to Unity's GameObject-oriented solutions.

**Rationale:** Child transforms and component fields are visible in the Inspector, debuggable, and independent of mesh origin conventions. Computed values from renderer bounds are fragile (origin varies between Unity primitives and FBX imports), invisible at edit time, and require understanding the math to debug.

**Impact:** New systems should prefer data-on-object patterns. Existing systems that compute from renderer bounds should be left as-is unless they cause bugs.
