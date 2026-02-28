# Contradictions and open questions

When either agent finds a conflict between docs, or needs an architectural decision made, write it here. The lead (Kevin's Claude) resolves entries by moving them to `decisions.md`.

Format:
```
## C-NNN: Short title

**Found by:** [joe/kevin]
**Date:** YYYY-MM-DD
**Doc A says:** ...
**Doc B says:** ...
**Options:**
1. ...
2. ...
**Recommendation:** ...
```

---

## Resolved

- C-001 through C-003: VContainer vs no DI, Addressables vs SceneManager, Legacy vs New Input System. See `decisions.md` D-001 through D-003.

---

## Open

## C-004: Missing server authority guards in combat systems

**Found by:** kevin (code review of J-003 through J-006 on master)
**Date:** 2026-02-28
**CLAUDE.md says:** "Factory simulation runs server-side only. Belt tick, machine tick, power calculation, and crafting progress run under `if (!IsServerInitialized) return;`. Clients display state synced via SyncVar/SyncList -- they never simulate."
**Joe's code does:** `EnemySpawner`, `WaveControllerBehaviour`, and `FaunaController` all spawn/destroy NetworkObjects without `IsServerInitialized` guards. `WeaponBehaviour` calls `healthComponent.TakeDamage()` directly on the client instead of through a `ServerRpc`.
**Impact:** Clients will crash or duplicate spawns in multiplayer. Damage can be applied by any client without server validation.
**Fix required:** Add `if (!IsServerInitialized) return;` guards to all spawning, wave, and AI tick methods. Route damage through a `ServerRpc`.

## C-005: FaunaController violates D-004 pattern

**Found by:** kevin (code review of J-005 on master)
**Date:** 2026-02-28
**decisions.md D-004 says:** Pure C# simulation classes + thin MonoBehaviour wrappers. Simulation logic must be testable in EditMode without MonoBehaviour dependencies.
**Joe's code does:** `FaunaController` is a single MonoBehaviour mixing simulation logic (threat evaluation, attack timing, state transitions, pack coordination) with Unity lifecycle. Not testable in EditMode.
**Impact:** AI logic is untestable without PlayMode. Bugs will be harder to catch and reproduce.
**Fix required:** Extract simulation logic into `FaunaAI` (pure C#), keep `FaunaController` as thin wrapper.

## C-006: GameObject.Find and FindAnyObjectByType in combat code

**Found by:** kevin (code review of J-003/J-004 on master)
**Date:** 2026-02-28
**CLAUDE.md says:** "Never use direct cross-scene references. Unity doesn't allow them, and the three-scene structure means objects in one scene can be unloaded at any time."
**Joe's code does:** `PlayerHUD` uses `FindAnyObjectByType<HealthComponent>()`. `WeaponBehaviour` uses `GameObject.Find` patterns. These assume single-scene and break in multi-scene.
**Impact:** Will return null or wrong objects when scenes load/unload independently.
**Fix required:** Use dependency injection or `GameEventSO` event bus to wire references. Player should receive its own `HealthComponent` reference through its spawn setup, not global search.

## C-007: GetComponent called per melee attack

**Found by:** kevin (code review of J-005 on master)
**Date:** 2026-02-28
**CLAUDE.md says:** "Never cache GetComponent, FindObjectOfType, or FindObjectsOfType results inside Update or FixedUpdate. Cache in Awake or Start."
**Joe's code does:** `FaunaController` calls `GetComponent<HealthComponent>()` on the target every melee attack instead of caching.
**Impact:** Unnecessary allocation per attack. At scale with many fauna this adds up.
**Fix required:** Cache the target's `HealthComponent` when target is acquired, clear on target change.
