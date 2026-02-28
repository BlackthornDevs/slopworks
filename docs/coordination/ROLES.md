# Development roles

This file defines the authority structure for Claude Code agents working on Slopworks. Both agents read this file at session start and follow it.

---

## Kevin's Claude — Lead Developer

- **Branch:** `kevin/main`
- **Authority:** Final decision on all architectural questions, contradictions, and design disputes.
- **Responsibilities:**
  - Resolves entries in `contradictions.md` by writing decisions to `decisions.md`
  - Approves or rejects proposals from the junior developer
  - Updates shared interfaces and contracts on `master`
  - Owns the implementation plan and task prioritization
  - Pushes coordination updates to `master`

## Joe's Claude — Junior Developer

- **Branch:** `joe/main`
- **Authority:** Implementation decisions within owned systems only. No unilateral architectural changes.
- **Responsibilities:**
  - Implements assigned systems on `joe/main`
  - Proposes architectural changes by writing to `contradictions.md` (never decides alone)
  - Follows decisions in `decisions.md` without deviation
  - Flags concerns or blockers in `contradictions.md` for lead review
  - Regularly merges `master` into `joe/main` to pick up coordination updates

## Decision-making protocol

1. **Settled decisions** are in `decisions.md`. Both agents follow them. No re-litigating.
2. **New architectural questions** go to `contradictions.md` with context and options.
3. **Lead resolves** by moving the entry from `contradictions.md` to `decisions.md` with rationale.
4. **Junior never overrides** a decision in `decisions.md`. If new information surfaces, write a new entry in `contradictions.md` explaining why the decision should be revisited.

## What counts as an "architectural decision"

- Adding or removing a package/dependency
- Changing a shared interface or SO definition
- Modifying `ProjectSettings/` (physics layers, tags, input maps)
- Changing the scene structure or loading model
- Altering the networking authority model
- Anything that affects both branches' ability to merge

## What the junior can decide independently

- Implementation details within owned scripts
- Private class structure, variable naming, internal algorithms
- Test structure for owned systems
- Scene content within owned scenes
- Prefab internals for owned prefabs
