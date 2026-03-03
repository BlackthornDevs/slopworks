# Joe's workflow guide

How to work with Claude Code on Slopworks. This is for you (Joe the human), not for the Claude agent.

---

## Starting a session

1. Open a terminal in the Slopworks repo
2. Run `claude` to start Claude Code
3. Your Claude agent reads the coordination docs automatically and picks up where it left off. You don't need to tell it what to do first -- it knows.
4. If it starts on the wrong task, just tell it: "skip that, I want to work on X"

---

## The basic loop

Your job is to describe what you want. Claude's job is to write the code, run tests, and verify it compiles.

**You say things like:**
- "Add a rare loot drop called Dark Fragment that only appears on floor 5+"
- "Make turrets fire faster but do less damage"
- "The boss should have 3x health and drop a guaranteed fragment"
- "When I press P, pre-seed a turret chain with 200 ammo"
- "I walked into the tower elevator but nothing happened"

**You don't need to say:**
- Which file to edit
- What class or method to modify
- How to wire up serialized fields
- How to register a tool handler

Claude knows the codebase and the file structure. It knows where your code goes.

---

## Testing your changes

After Claude makes changes, you test by playing:

1. Claude will recompile and tell you if there are errors
2. Open your scene in Unity (it should have a single GameObject with `JoePlaytestSetup`)
3. Hit Play
4. Walk around, place things, shoot, trigger waves -- exercise whatever was changed
5. Watch the console for `[LOG]` messages (PlaytestLogger) -- these show every input and event

**When something breaks, tell Claude what you saw:**
- "I pressed left click but nothing placed"
- "Turret is spinning but not firing"
- "I picked up the item but the hotbar didn't update"
- "Console says NullReferenceException on line 245 of TurretBehaviour"

The more specific you are about what happened (or didn't happen), the faster Claude fixes it.

---

## Common tasks and how to ask for them

### Adding a new item

"Add a new item called Dark Fragment. It's a rare tower loot drop, stack size 10, no crafting recipe."

Claude will create an `ItemDefinitionSO` at runtime in your bootstrapper and register it with the ItemRegistry.

### Changing turret stats

"Make the turret fire every 0.3 seconds instead of 0.5, and reduce damage to 8."

Claude edits the `CreateTurretDefinition()` method in `JoePlaytestSetup.cs`.

### Adding a new loot drop to the tower

"Add a loot drop entry: Dark Fragment, Rare rarity, weight 0.15, 1 per drop, floors 5+ only."

Claude adds a `LootDropDefinition` entry to whatever loot table you're building.

### Adding a new enemy type

"Create a fast melee enemy called Stalker. Half the health of the basic enemy, double speed, half damage. Spawns on floors 3+."

Claude creates a `FaunaDefinitionSO` with those stats and adds spawn entries to the relevant floor chunks.

### Debugging a playtest issue

"When I enter the tower and go to floor 2, enemies spawn but they just stand there and don't attack."

Claude will check the behavior tree wiring, NavMesh baking, and perception system for that scenario.

### Adding a debug key

"Add a key that gives me 50 of every item when I press F8."

Claude adds a key handler in `JoePlaytestSetup.Update()`.

---

## What you can freely change (your territory)

- `JoePlaytestSetup.cs` -- your bootstrapper, all your scene setup goes here
- `Scripts/Combat/Turret*` -- turret code you wrote
- `Scripts/Combat/Fauna*` -- fauna AI code you wrote
- `Scripts/World/Tower*` -- tower code (Phase 7)
- Any test files you created
- Your playtest scene

## What you should NOT touch

- `KevinPlaytestSetup.cs` -- Kevin's bootstrapper
- `KevinPlaytest.unity` -- Kevin's scene
- `PlaytestBootstrap.cs`, `PlaytestToolController.cs`, `PlaytestContext.cs` -- shared infrastructure. If you need something added here, tell Claude to note it in contradictions.md and Kevin will handle it.
- `ProjectSettings/` -- unless explicitly discussed
- Anything in `Scripts/Core/` -- these are shared types that both branches depend on. Additive changes (new enum values) are usually OK but must be flagged in your handoff.

If Claude tries to create a new bootstrapper file or modify shared files, tell it: "No, put that in JoePlaytestSetup." It should know this from the CLAUDE.md, but humans are the final check.

---

## Ending a session

Tell Claude: "wrap up" or "end session"

It will:
1. Run all EditMode tests (must pass)
2. Write handoff notes in `handoff-joe.md`
3. Flag any shared file changes
4. Commit and push to `joe/main`
5. If all tasks are done, it creates a PR to master

You don't need to manage git yourself unless you want to.

---

## When to intervene

- **Claude creates a new file you didn't ask for** -- ask why. It might be necessary (new SO definition) or it might be duplicating something that exists.
- **Claude modifies a shared file** -- check that it flagged it in the handoff. If it didn't, remind it.
- **Tests fail after a change** -- Claude should fix them before moving on. If it's stuck, describe what you see.
- **Claude goes off on a tangent** -- "stop, that's not what I asked for. I want X."
- **Something feels wrong in the game** -- trust your instincts. Describe exactly what you see and Claude will investigate.

---

## Quick reference

| You want to... | Say something like... |
|---|---|
| Start working | Just open Claude Code, it auto-picks up |
| Add a feature | "Add X that does Y" |
| Change a value | "Make X do Y instead of Z" |
| Fix a bug | "When I do X, Y happens instead of Z" |
| Add a debug key | "Add a key for X" |
| Test | Hit Play in Unity, exercise the feature |
| See logs | Watch console for `[LOG]` lines |
| End session | "wrap up" |
| Skip a task | "skip that, work on X instead" |
| Course-correct | "no, do it this way instead" |
