---
name: slopworks-handoff
description: End a Slopworks dev session - write handoff notes, update coordination docs, commit, push, and update memory so the next session picks up seamlessly
user_invocable: true
---

# Slopworks Session Handoff

When the user says "handoff", "end session", or "wrap up", perform ALL of the following steps. This is a Slopworks-specific session end that preserves context for the next Claude session AND for Joe's Claude working in parallel.

## Steps

### 1. Review Joe's handoff

Read `docs/coordination/handoff-joe.md`. Check:
- Did Joe report any shared file changes? If so, note merge risks in your own handoff.
- Did Joe flag any contradictions? If so, resolve them in `decisions.md`.
- Are Joe's test counts consistent with expectations?
- Did Joe add any packages or asmdef references that need attention?

If Joe's handoff has a "Shared file changes" section with entries, merge those changes
carefully. asmdef reference additions in particular can cause compilation failures if
the referenced package isn't set up identically on both branches.

### 2. Write session handoff file

Create or overwrite `docs/coordination/handoff-kevin.md` with:

```markdown
# Kevin's Claude -- Session Handoff

Last updated: [YYYY-MM-DD HH:MM]
Branch: kevin/main
Last commit: [hash] [message]

## What was completed this session
- [Bullet list of everything done, with file paths]

## What's in progress (not yet committed)
- [Any uncommitted work, or "None -- all committed"]

## Next task to pick up
- [Specific next step with enough detail to start immediately]
- [Reference the plan phase/task number if applicable]

## Blockers or decisions needed
- [Any open questions, or "None"]

## Test status
- [X/Y passing, any known failures]

## Key context the next session needs
- [Anything non-obvious: workarounds, gotchas, partially built systems]
```

### 3. Update tasks-joe.md and check for new assignments

**Always check whether Joe needs new tasks.** Review:
- Did this session complete work that unblocks Joe?
- Are there new fix tasks from code review findings?
- Did the plan produce new implementation tasks Joe should pick up?
- Are all of Joe's pending tasks still accurate (priorities, acceptance criteria)?

If yes to any: update `docs/coordination/tasks-joe.md` with new tasks following the standard format (Status: Pending, Priority, Branch, Ownership, Acceptance criteria). Use the next available J-number.

Also update `docs/coordination/handoff-joe.md` so Joe's next session has context on what changed.

### 4. Update decisions.md (if architectural decisions were made)

If any architectural decisions were made during the session, add them to `docs/coordination/decisions.md` following the existing format.

### 5. Update auto-memory

Update `C:\Users\KevinAmditis\.claude\projects\C--Users-KevinAmditis-source-repos\memory\MEMORY.md` with:
- Any new patterns or conventions discovered
- Key file paths for new systems
- Solutions to problems encountered
- Current phase and progress

Keep it concise. Don't duplicate what's in CLAUDE.md or the handoff file.

### 6. Commit all changes

- Run `git status` to see what's outstanding
- Stage all relevant files (exclude `.claude/settings.local.json`)
- If there are uncommitted code changes, commit them first with a descriptive message
- Then commit the handoff/coordination updates separately:
  ```
  Update session handoff and coordination docs
  ```
- NEVER include Co-Authored-By lines

### 7. Push to kevin/main

- Push to `kevin/main`
- Report the final commit hash

### 7b. Create PR to master (if Joe's tasks or coordination docs changed)

**Never push directly to master.** If `tasks-joe.md`, `handoff-joe.md`, `contradictions.md`, `decisions.md`, or `.claude/CLAUDE.md` were updated:

1. Push to `kevin/main` first (step 7)
2. Create a PR: `gh pr create --base master --head kevin/main --title "Update coordination docs" --body "Updated tasks/handoff/decisions for Joe"`
3. If the user approves, merge: `gh pr merge --merge`
4. Pull master back: `git fetch origin master && git merge origin/master --no-edit`

This ensures Joe picks up new tasks on his next `git merge origin/master`. All changes to master go through PRs -- no exceptions.

### 8. Summary to user

Print a brief summary:
- What was accomplished
- Where to pick up next session
- Whether jawn has new instructions waiting
- Final test count

## Important Notes

- NEVER add Co-Authored-By lines to commits
- The handoff file is the primary way the next session recovers context -- make it thorough
- **NEVER push directly to master.** Always create a PR via `gh pr create`. This is a hard rule.
- Joe's Claude auto-picks tasks from `tasks-joe.md` on master. Step 7b ensures he gets them.
- Only merge coordination files to master (step 7b), never Phase implementation code
- If you made changes to shared code (Scripts/Core/, ScriptableObjects/, ProjectSettings/), note this prominently -- it needs a separate PR to master for jawn to pick up
