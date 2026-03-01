# Joe's session handoff

Updated by Joe's Claude at the end of each session.

---

## Last updated: 2026-03-01 (by Kevin -- Phase 5 complete, testing policy update)

### What was completed by Kevin (2026-03-01)

- **Phase 5 complete.** Core UI, player inventory, scene management, HUD, hotbar, recipe selection -- all done on kevin/main. This unblocks your J-018 (Tower MonoBehaviour wrapper + elevator system).
- **Testing policy added to CLAUDE.md (merged to master via PR #3).** New requirement: write integration tests alongside simulation tests. Phase 5 had 675 passing unit tests but 5 bugs found only during manual playtest -- all at integration seams (component ordering, Awake timing, UI hierarchy, input wiring). Read the new "Testing requirements" section in `.claude/CLAUDE.md` on your next `git merge origin/master`.
- 675/675 EditMode tests passing on kevin/main

### Previous session (2026-02-28)

- Fixed compilation errors on kevin/main caused by missing asmdef references
- Added NPBehave (`GUID:b23d0b8134b59074db4ef602bb53a3c5`) reference to `Slopworks.Runtime.asmdef`
- Added FishNet.Runtime (`GUID:7c88a4a7926ee5145ad2dfa06f454c67`) and NPBehave (`GUID:b23d0b8134b59074db4ef602bb53a3c5`) references to `Slopworks.Tests.EditMode.asmdef`

### Task assignments (2026-03-01)

Phase ordering has been revised. Phases 5-9 are rewritten in the vertical slice plan doc (`docs/plans/2026-02-27-vertical-slice-plan.md`). Your assignments:

**Immediate (Phase 4):** J-011, J-012 (asmdef fixes), then J-013, J-014, J-015 (turret defenses). Same as before.

**Next up (Phase 7 -- The Tower):** J-016 through J-021. These are new tasks added to `tasks-joe.md`. The Tower is a repeatable FPS combat gauntlet -- full design in `docs/plans/2026-02-28-tower-design.md`. Key points:
- J-016 and J-017 are pure C# simulation (D-004 pattern) -- can start as soon as J-015 is done, no dependency on Kevin's Phase 5
- J-018 depends on Kevin's Phase 5 (scene management + inventory) being done
- J-019 through J-021 build on J-016/J-017/J-018
- Your file ownership: `Scripts/World/Tower*`, `Scripts/Combat/InteriorFauna*`, `Scenes/Tower_Core.unity`
- Kevin's Phase 6 runs in parallel -- no file overlap

**Parallel execution schedule:**
| Round | Kevin | Joe |
|-------|-------|-----|
| 1 | Phase 5 (Core UI + Inventory) | Phase 4 (Turrets) -- current |
| 2 | Phase 6 (Building Exploration) | Phase 7 (The Tower) |
| 3 | Phase 8 (Supply Chain) | Phase 9 (Save + Full Loop) |

### Next task

Pick up from where you left off -- check `tasks-joe.md` priority rules. J-011 and J-012 (asmdef fixes) are Critical priority and should be first if not already done on your branch.

### Blockers

None.

### Test status (as of 2026-03-01)

675/675 passing on kevin/main, 0 failing, 0 skipped. (Joe's branch: 666/666 as of 2026-02-28.)

### Key context

- `Slopworks.Runtime.asmdef` now references: Unity InputSystem, TMPro, FishNet.Runtime, NPBehave (plus 2 others)
- `Slopworks.Tests.EditMode.asmdef` now references: Slopworks.Runtime, TestRunner, FishNet.Runtime, NPBehave, plus nunit precompiled reference
- After git operations that modify asmdef files, Unity requires Assets/Refresh before recompile to pick up changes
- FaunaAI is a standalone testable class -- future AI changes go there, not in FaunaController
- Tower design doc: `docs/plans/2026-02-28-tower-design.md` -- read this before starting J-016
