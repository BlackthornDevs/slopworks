# Joe's session handoff

Updated by Joe's Claude at the end of each session. Read this at the start of every session to recover context.

---

## Last updated: 2026-02-28

### What was completed

J-007 through J-010 — all four code review fixes from Kevin's review of Phase 3.

**J-007 (Critical):** Converted EnemySpawner, WaveControllerBehaviour, FaunaController, WeaponBehaviour from MonoBehaviour to NetworkBehaviour. Added `IsServerInitialized` guards to all server-only methods. WeaponBehaviour now validates damage server-side via `[ServerRpc] ServerFireWeapon(Vector3 origin, Vector3 direction)` — client does client-prediction raycast for visuals, server re-validates. Added FishNet.Runtime reference to Slopworks.Runtime.asmdef.

**J-008 (High):** Extracted `FaunaAI.cs` (plain C#) from FaunaController following D-004 pattern. Covers attack timing, threat evaluation, pack coordination, alert evaluation, aggression management, strafe direction. FaunaController is now a thin wrapper. 23 EditMode tests in FaunaAITests.cs.

**J-009 (Medium):** Eliminated all `GameObject.Find` and `FindAnyObjectByType` calls from combat and UI code. PlayerHUD uses `[SerializeField]` references wired by PlaytestSetup. WeaponBehaviour's `_hitMarker` is a SerializeField. Added `sourcePosition` field to DamageData so EnemyKnockback resolves knockback direction from damage data instead of `GameObject.Find(sourceId)`.

**J-010 (Low):** Added `_cachedTargetHealth` field to FaunaController. Cached on target change in UpdatePerception, used in MeleeAttack. GetComponent called once per target, not per attack.

### What needs attention

All tasks complete. Ready to merge to master — Phase 3 code review fixes are done.

### Next task

All tasks complete, awaiting new assignments.

### Blockers

None.

### Test status

FaunaAITests.cs added (23 tests). Existing tests unaffected — no signature changes to HealthComponent, WeaponController, or WaveController.

### Key context

- `Slopworks.Runtime.asmdef` now references FishNet.Runtime — all combat scripts can use NetworkBehaviour
- DamageData has a new `sourcePosition` field (Vector3) with backward-compatible constructors
- PlaytestSetup.SetupHUD() now wires all serialized references via SerializedObject
- FaunaAI is a standalone testable class — future AI changes should go there, not in FaunaController
