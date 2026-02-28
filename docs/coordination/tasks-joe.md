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
4. Commit and push to `joe/main`
5. Check this file again for the next task (repeat the priority rules)
6. If no pending tasks remain, write a note in `handoff-joe.md` saying "all tasks complete, awaiting new assignments" and stop

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

## Pending tasks

### Code review fixes (2026-02-28)

Lead developer reviewed J-003 through J-006 commits on master. Issues logged in `contradictions.md` as C-004 through C-007. **These must be fixed before Phase 3 is considered complete.**

### TASK J-007: Fix server authority violations (C-004)

**Status:** Pending
**Priority:** Critical
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Add `if (!IsServerInitialized) return;` guards to:
- `EnemySpawner` -- all spawn/destroy methods
- `WaveControllerBehaviour` -- wave start, enemy tracking
- `FaunaController` -- AI tick, attack execution

Route damage through a `ServerRpc` in `WeaponBehaviour` instead of calling `TakeDamage()` directly on the client.

**Acceptance criteria:** No NetworkObject spawning, destroying, or state mutation happens without a server authority check. WeaponBehaviour damage goes through ServerRpc. All existing tests still pass.

### TASK J-008: Extract FaunaAI from FaunaController (C-005)

**Status:** Pending
**Priority:** High
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

`FaunaController` violates D-004. Split into:
- `FaunaAI` (plain C#) -- threat evaluation, attack timing, state transitions, pack coordination
- `FaunaController` (thin MonoBehaviour) -- owns `FaunaAI`, feeds it perception data, executes movement

**Acceptance criteria:** `FaunaAI` is testable in EditMode. New EditMode tests cover threat evaluation and attack timing without MonoBehaviour dependencies. `FaunaController` is a thin wrapper only. All existing tests still pass.

### TASK J-009: Remove GameObject.Find usage (C-006)

**Status:** Pending
**Priority:** Medium
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`, `Scripts/UI/`

Replace `FindAnyObjectByType<HealthComponent>()` in `PlayerHUD` and `GameObject.Find` patterns in `WeaponBehaviour` with proper dependency injection or `GameEventSO` event bus wiring. Player should receive its own references through spawn setup, not global search.

**Acceptance criteria:** Zero `GameObject.Find`, `FindObjectOfType`, or `FindAnyObjectByType` calls in combat or UI code. References wired through spawn setup or event bus.

### TASK J-010: Cache GetComponent in FaunaController (C-007)

**Status:** Pending
**Priority:** Low
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

Cache the target's `HealthComponent` when target is acquired, clear on target change. Currently calls `GetComponent<HealthComponent>()` on every melee attack.

**Acceptance criteria:** `GetComponent` called once per target acquisition, not per attack. Cached reference cleared when target changes or dies.

---

## Task order

Current priority sequence (auto-pickup follows this):

1. **J-007** (Critical) -- server authority guards
2. **J-008** (High) -- FaunaAI extraction (D-004)
3. **J-009** (Medium) -- remove GameObject.Find
4. **J-010** (Low) -- cache GetComponent

After all fixes: merge `joe/main` to `master`. Phase 3 is not complete until these are resolved.
