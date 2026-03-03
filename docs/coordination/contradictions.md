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
- C-004: Missing server authority guards. Fixed in J-007 — all combat classes converted to NetworkBehaviour with IsServerInitialized guards, WeaponBehaviour damage routed through ServerRpc.
- C-005: FaunaController violates D-004. Fixed in J-008 — FaunaAI extracted as plain C#, 23 EditMode tests.
- C-006: GameObject.Find in combat code. Fixed in J-009 — all references wired via SerializeField, zero Find calls remain.
- C-007: GetComponent per melee attack. Fixed in J-010 — cached on target acquisition.
- C-008: Dev_Test bootstrapper question. Moot — StructuralPlaytestSetup no longer exists. JoePlaytestSetup replaces it per D-012 bootstrapper refactoring.

---

## Open

## C-009: Turret ghost not cleaned up on tool switch

**Found by:** joe
**Date:** 2026-03-02
**Issue:** `CancelAllPending()` in PlaytestToolController cleans up shared ghosts (foundation, wall, ramp, belt, machine, storage) but has no way to notify custom tool handlers (like TurretPlace) to destroy their private ghosts. When switching away from turret tool or closing the build menu, the turret ghost cube stays at its last position.
**Symptoms:** Turret appears to "move" when switching tools. Ghost stays visible after closing build menu.
**Root cause:** `_turretGhost` and `_turretGhostPorts` are private to JoePlaytestSetup. CancelAllPending doesn't know about them.
**Recommended fix:** Add `RegisterToolCleanup(Action)` to PlaytestToolController. Call all registered cleanups inside `CancelAllPending()`. JoePlaytestSetup registers `DestroyTurretGhost` during `RegisterToolHandlers`.
**Assigned to:** Kevin (lead, owns PlaytestToolController as shared infrastructure)
