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
- C-009: Turret ghost not cleaned up on tool switch. Fixed by Kevin — added `RegisterToolCleanup(Action)` to PlaytestToolController. Both KevinPlaytestSetup and JoePlaytestSetup register `DestroyTurretGhost` during `RegisterToolHandlers`. Commit `255f2a7`.
- C-010: Redefine Joe's scope. Resolved — see D-019. Joe focuses on terrain, environment art, UI visuals, asset sourcing, lore. Kevin handles mechanics, multiplayer, simulation. Joe's code tasks are limited to UI scripts (VisorHUD, VisorBuildAdapter) and terrain/environment scripts directly related to his visual work.

---

## Open

(none)
