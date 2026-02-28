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

## Task order

All code review fixes (J-007 through J-010) complete. Ready to merge `joe/main` to `master`.
