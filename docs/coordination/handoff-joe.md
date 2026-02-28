# Joe's session handoff

Updated by Joe's Claude at the end of each session. Read this at the start of every session to recover context.

---

## Last updated: 2026-02-28 (by lead, initial setup)

### What was completed

J-001 through J-006 (Phase 3 combat systems) are merged to master.

### What needs attention

Lead reviewed all Phase 3 code and found 4 issues (C-004 through C-007 in `contradictions.md`). Fix tasks J-007 through J-010 have been assigned in `tasks-joe.md`.

### Next task

**J-007: Fix server authority violations** (Critical priority). See `tasks-joe.md` for full details. Key points:
- Add `if (!IsServerInitialized) return;` to EnemySpawner, WaveControllerBehaviour, FaunaController
- Route WeaponBehaviour damage through ServerRpc
- All existing tests must still pass after changes

### Blockers

None.

### Test status

All EditMode tests passing as of last session.

### Key context

- `tasks-joe.md` now has an auto-pickup protocol -- read the top of the file and follow it every session
- Merge master before starting work to pick up the latest task assignments
- The code review findings are documented in `contradictions.md` (C-004 through C-007) with full explanations of what's wrong and why
