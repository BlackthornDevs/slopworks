---
name: slopworks-handoff
description: End a Slopworks dev session - write handoff notes, update coordination docs, commit, push, and update memory so the next session picks up seamlessly
user_invocable: true
---

# Slopworks Session Handoff

When the user says "handoff", "end session", or "wrap up", perform ALL of the following steps. This is a Slopworks-specific session end that preserves context for the next Claude session AND for Joe's Claude working in parallel.

## Steps

### 1. Write session handoff file

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

### 2. Update tasks-joe.md and check for new assignments

**Always check whether Joe needs new tasks.** Review:
- Did this session complete work that unblocks Joe?
- Are there new fix tasks from code review findings?
- Did the plan produce new implementation tasks Joe should pick up?
- Are all of Joe's pending tasks still accurate (priorities, acceptance criteria)?

If yes to any: update `docs/coordination/tasks-joe.md` with new tasks following the standard format (Status: Pending, Priority, Branch, Ownership, Acceptance criteria). Use the next available J-number.

Also update `docs/coordination/handoff-joe.md` so Joe's next session has context on what changed.

### 3. Update decisions.md (if architectural decisions were made)

If any architectural decisions were made during the session, add them to `docs/coordination/decisions.md` following the existing format.

### 4. Update auto-memory

Update `C:\Users\KevinAmditis\.claude\projects\C--Users-KevinAmditis-source-repos\memory\MEMORY.md` with:
- Any new patterns or conventions discovered
- Key file paths for new systems
- Solutions to problems encountered
- Current phase and progress

Keep it concise. Don't duplicate what's in CLAUDE.md or the handoff file.

### 5. Commit all changes

- Run `git status` to see what's outstanding
- Stage all relevant files (exclude `.claude/settings.local.json`)
- If there are uncommitted code changes, commit them first with a descriptive message
- Then commit the handoff/coordination updates separately:
  ```
  Update session handoff and coordination docs
  ```
- NEVER include Co-Authored-By lines

### 6. Push to kevin/main

- Push to `kevin/main`
- Report the final commit hash

### 6b. Push coordination docs to master (if Joe's tasks changed)

If `tasks-joe.md`, `handoff-joe.md`, `contradictions.md`, or `decisions.md` were updated:

1. Switch to master: `git checkout master && git pull origin master`
2. Cherry-pick only the coordination files: `git checkout kevin/main -- .claude/CLAUDE.md docs/coordination/tasks-joe.md docs/coordination/handoff-joe.md docs/coordination/contradictions.md docs/coordination/decisions.md` (only files that changed)
3. Commit: "Update coordination docs for Joe"
4. Push to master: `git push origin master`
5. Switch back: `git checkout kevin/main && git merge origin/master --no-edit && git push origin kevin/main`

This ensures Joe picks up new tasks on his next `git merge origin/master`. Do NOT push any Phase implementation code to master -- only coordination and CLAUDE.md files.

### 7. Summary to user

Print a brief summary:
- What was accomplished
- Where to pick up next session
- Whether jawn has new instructions waiting
- Final test count

## Important Notes

- NEVER add Co-Authored-By lines to commits
- The handoff file is the primary way the next session recovers context -- make it thorough
- Joe's Claude auto-picks tasks from `tasks-joe.md` on master. Step 6b ensures he gets them.
- Only push coordination files to master (step 6b), never Phase implementation code
- If you made changes to shared code (Scripts/Core/, ScriptableObjects/, ProjectSettings/), note this prominently -- it needs a separate merge to master for jawn to pick up
