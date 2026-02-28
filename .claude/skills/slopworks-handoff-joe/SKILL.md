---
name: slopworks-handoff-joe
description: End a Slopworks dev session for Joe -- write handoff notes, report shared changes, commit, push
user_invocable: true
---

# Joe's Session Handoff

When the user says "handoff", "end session", or "wrap up", perform ALL steps below.

## Steps

### 1. Recompile and run all tests

Before writing any handoff notes, verify the build:

1. Recompile: `mcp__mcp-unity__recompile_scripts`
2. If compilation errors exist, fix them. Do not proceed until zero errors.
3. Run all EditMode tests: `mcp__mcp-unity__run_tests`
4. Record exact counts: X passing, Y failing, Z skipped
5. If any tests fail, fix them or document why they can't be fixed right now

### 2. Write session handoff file

Create or overwrite `docs/coordination/handoff-joe.md`:

```markdown
# Joe's session handoff

Updated by Joe's Claude at the end of each session.

---

## Last updated: [YYYY-MM-DD]

### What was completed

[Bullet list of tasks completed with J-numbers and commit hashes]

### Shared file changes (CRITICAL)

[List every change to files outside Scripts/Combat/ or Joe's owned directories]
[Specifically call out:]
- asmdef changes (added/removed references)
- ProjectSettings changes
- Core/ script changes
- New packages or dependencies added
- ScriptableObject definition changes

[If none: "No shared file changes this session."]

### What needs attention

[Anything Kevin should know: interface changes, new patterns, merge risks]

### Next task

[What Joe should pick up next, or "all tasks complete"]

### Blockers

[Open blockers, or "None"]

### Test status

[Exact numbers: X/Y passing, Z compilation errors, W warnings]
[List any new test files added]

### Key context

[Non-obvious details: workarounds, partially built systems, naming decisions]
```

### 3. Update tasks-joe.md

Mark completed tasks with date and commit hash. Do NOT add new tasks (that's Kevin's job). Do NOT change task priorities.

### 4. Update contradictions.md (if needed)

If any architectural questions came up during work, add them to `docs/coordination/contradictions.md` using the next C-number. Include context, options considered, and a recommendation.

### 5. Commit all changes

1. `git status` to see outstanding changes
2. Commit code changes first with descriptive message
3. Commit handoff/coordination updates separately:
   "Update Joe session handoff and coordination docs"
4. NEVER include Co-Authored-By lines

### 6. Push to joe/main

Push to `joe/main`. Report final commit hash.

### 6b. Push coordination docs to master

Push only coordination files to master so Kevin sees them:

1. `git checkout master && git pull origin master`
2. Cherry-pick coordination files only:
   `git checkout joe/main -- docs/coordination/handoff-joe.md docs/coordination/contradictions.md`
   (Only files that actually changed)
3. Commit: "Update Joe coordination docs"
4. `git push origin master`
5. `git checkout joe/main && git merge origin/master --no-edit && git push origin joe/main`

NEVER push implementation code to master. Only coordination docs.

### 7. Summary to user

Print:
- Tasks completed (J-numbers)
- Shared file changes (if any)
- Test count
- Final commit hash
- Next task to pick up
