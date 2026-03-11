# Slopworks — Copilot context

## Project overview

Post-apocalyptic co-op factory/survival game (1-4 players). Players explore abandoned buildings modeled from real BIM data, restore mechanical systems, and build Satisfactory-style automation networks at their home base. Two developers: Joe (jamditis) and Kevin (kamditis) at BlackthornDevs. Both push to their own branches (`joe/main`, `kevin/main`) and merge to `master` via PRs.

## Tech stack

| Component | Technology |
|-----------|-----------|
| Engine | Unity 6 (URP 17.3) |
| Language | C# |
| Networking | FishNet (server-authoritative), FishySteamworks |
| DI | VContainer |
| AI behavior | NPBehave behavior trees (server-side only) |
| Input | Unity New Input System |
| Audio | FMOD Studio |
| Persistence | Supabase (supabase-csharp + UniTask) |
| Asset loading | Addressables |

## Coding guidelines

**C# / Unity rules:**
- Never mutate ScriptableObjects at runtime. They are read-only static definitions. Per-instance state goes in `ItemInstance` / `ItemSlot` structs.
- Never spawn a NetworkObject per belt item — belt contents are a `SyncList<BeltItem>` on the segment entity.
- Factory simulation (belt tick, machine tick, power, crafting) runs server-side only — guard with `if (!IsServerInitialized) return;`.
- Never use direct cross-scene references. All cross-scene communication goes through the `GameEventSO` ScriptableObject event bus.
- Never use RPCs for persistent state — late-joining clients won't receive RPCs. Use SyncVar or SyncList for anything a new client needs on join.
- Never cache `GetComponent`, `FindObjectOfType`, or `FindObjectsOfType` results inside `Update` or `FixedUpdate`. Cache in `Awake` or `Start`.
- Prefer GameObject-oriented data over computed values — snap points encode attachment geometry as child transforms, not as computed extents.
- Snap placement is snap-to-snap: `ghostPos = targetSnapPos - Rot * ghostSnapLocalPos`. No extents math in GridManager.
- When adding a new `PortOwnerType`, update both switch cases in `ConnectionResolver.CreateSource` and `ConnectionResolver.CreateDestination`.
- Never duplicate placement math or physics constants — all world position/rotation/scale comes from `GridManager` universal placement methods.

**General:**
- No direct LLM API calls in tooling or automation code. Use `claude -p` or `gemini -p` via subprocess.
- No emojis in source code, log messages, or comments.
- Sentence case in all UI text, headings, and variable names.
- Keep comments factual and brief. No filler phrases.

**Branch rules:**
- Never push directly to `master`. All changes go through PRs from `joe/main` or `kevin/main`.
- Both developers run EditMode tests before pushing. Tests must pass.

## Project structure

```
Assets/
  Scripts/
    Core/          # Shared — changes go through master only
    Buildings/     # Building placement, snap system, GridManager
    Networking/    # FishNet setup, lobby, connection management
    Factory/       # Belt, machine, power, crafting simulation (server-side)
    UI/            # UGUI components
    Enemies/       # NPBehave behavior trees (server-side)
    Player/        # Movement, interaction, inventory
  ScriptableObjects/   # ItemDefinitionSO, BuildingDefinitionSO, etc.
ProjectSettings/       # Shared — changes go through master only
Packages/              # Unity package manifest
docs/                  # GitHub Pages site
supabase/              # Database schema and edge functions
```

## Available resources

- `.claude/CLAUDE.md` — full engineering rules, agent hierarchy, session workflow
- `docs/coordination/decisions.md` — settled architectural decisions (read before making architectural choices)
- `docs/coordination/ownership.md` — which developer owns which systems
- `.claude/skills/slopworks-patterns` — `ItemDefinitionSO`/`ItemInstance` code, NetworkVariable vs RPC decision tree, belt sync pattern
- `.claude/skills/slopworks-architecture` — system ownership, FishNet/Supabase responsibility split
