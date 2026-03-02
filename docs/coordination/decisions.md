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
