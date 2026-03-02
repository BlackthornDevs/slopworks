# Slopworks

Post-apocalyptic co-op factory/survival game. Reclaim real buildings by restoring their mechanical systems, build Satisfactory-style automation networks, and defend your territory from hostile fauna.

**Engine:** Unity 6 (6000.3.10f1)
**Networking:** FishNet + FishySteamworks
**Players:** 1-4 (solo + drop-in/drop-out co-op)
**Platform:** Desktop

## What makes it different

Every reclaimable building is modeled from real BIM (Building Information Modeling) data -- actual duct layouts, pipe routing, and mechanical rooms exported from Revit/Navisworks. No other indie game offers this level of environmental authenticity.

## Core loop

```
SCOUT -> BREACH -> CLEAR -> RESTORE -> CONNECT -> AUTOMATE -> DEFEND -> EXPAND (repeat)
```

Players alternate between first-person exploration/combat in abandoned buildings and isometric factory building at their home base. Connecting buildings to your network increases your production capacity but also raises the global threat level, bringing tougher fauna waves.

## World structure

The game is split across three scene groups:

- **Home base** -- persistent factory and fortress. Flat buildable terrain with a Satisfactory-style foundation grid for freeform factory building. Supports multi-story stacking, integrated defenses (turrets, walls, gates, landmines), and isometric + first-person camera toggle.
- **Reclaimed buildings** -- BIM-sourced explorable levels. Each building is a separate scene with its own fauna, hazards, and MEP systems to restore. Fauna types and difficulty scale with distance from your hub.
- **Overworld map** -- isometric territory visualization showing supply lines, connected buildings, and threat levels. Used for scouting, logistics planning, and network management.

## Automation

Two layers of factory automation:

**Building-level (distributed network):** Each reclaimed building becomes a configurable production node. Set recipes, assign output destinations, and manage throughput from the overworld UI. Supply lines connect buildings and carry products at configurable rates -- longer lines are more vulnerable to fauna attacks.

**Hub-level (home base factory):** Full Satisfactory-style automation with belts, splitters, mergers, and inserters. Player-placed machines snap to the foundation grid. Receives intermediate products from the building network and handles final assembly for weapons, tools, armor, turrets, and advanced MEP parts.

The factory simulation runs server-side only at a fixed tick rate, decoupled from rendering. Belt items use a distance-offset data structure for O(1) steady-state updates, with one NetworkObject per belt segment (not per item).

## Defense and threat

A global threat meter drives wave intensity. Connecting buildings, increasing throughput, and expanding territory all raise threat. Higher threat means more frequent waves, multi-directional assaults, and boss spawns. Baseline pressure keeps waves coming even at low threat -- you can't turtle indefinitely.

Defensive structures include auto-turrets (power + ammo dependent), spike walls, reinforced gates, landmines, and spotlights.

## Tech stack

| Component | Technology |
|-----------|-----------|
| Engine | Unity 6 (URP 17.3) |
| Networking | FishNet (server-authoritative) |
| Transport | FishySteamworks (Steam lobbies + NAT punchthrough) |
| AI | NPBehave behavior trees (server-side only) |
| Input | Unity New Input System 1.18 |
| DI | VContainer |
| Audio | FMOD Studio (adaptive) |
| Persistence | Supabase (supabase-csharp + UniTask) |
| Asset loading | Addressables |
| Dev testing | ParrelSync (dual editor instances) |

### FishNet authority model

| Object | Owner | Sync mechanism |
|--------|-------|----------------|
| Player character | Client | Client prediction + server validation |
| Factory machines | Server | SyncVar status, ServerRpc config |
| Belt items | Server | SyncList per segment |
| Inventory | Server | SyncList for slots, ServerRpc operations |
| Building placement | Server | Client requests, server validates + spawns |
| World chunks | Server | Generated server-side, sent on demand |
| Fauna AI | Server | NPBehave trees, SyncVar position/state |

## Project structure

```
Assets/_Slopworks/
  Scripts/
    Automation/     belt, machine, grid, power systems
    Building/       structural building (foundations, walls, ramps)
    Combat/         weapons, damage, health, fauna AI, wave defense
    Core/           game manager, scene loader, item/recipe registry
    Input/          input action maps and bindings
    Network/        FishNet setup, Supabase client, save system
    Player/         character controller, camera rig
    UI/             HUD, menus, world-space machine panels
    World/          terrain gen, BIM import, chunk loading
  ScriptableObjects/
    Items/          ItemDefinitionSO assets
    Recipes/        RecipeSO assets
    Events/         GameEventSO event bus (cross-scene communication)
    Buildings/      building type definitions
  Materials/
  Prefabs/
  Scenes/
    Core/           Core_Network (always loaded), Core_GameManager
    HomeBase/       terrain, grid, UI, lighting
    Buildings/      one scene per reclaimed building
    Overworld/      map, overworld UI
  Tests/
    Editor/         EditMode tests for simulation logic
    PlayMode/       integration tests for networking and lifecycle
```

## Scene architecture

`Core_Network.unity` loads first and never unloads -- the NetworkManager and registries live there. All other scenes load additively. Scene loading is host-initiated: `NetworkManager.SceneManager.LoadScene` loads the same scene for all connected clients simultaneously.

Cross-scene communication uses a `GameEventSO` ScriptableObject event bus. Direct cross-scene references are never used.

## Getting started

### Prerequisites

- Unity 6 (version 6000.3.10f1)
- Git + Git LFS (binary assets are tracked via LFS)
- Steam SDK (for FishySteamworks transport)

### Setup

1. Clone the repo and ensure Git LFS pulls binary assets
2. Open in Unity 6 -- packages will resolve automatically from `manifest.json`
3. Copy `Assets/StreamingAssets/supabase-config.template.json` to `supabase-config.json` and fill in your Supabase project URL and anon key
4. Open `Core_Network.unity` as the starting scene

### Multiplayer testing

Use [ParrelSync](https://github.com/VeriorPies/ParrelSync) to run two editor instances on the same machine. One hosts, the other joins. All factory simulation and AI runs server-side, so the host instance is where you debug game logic.

## Development workflow

Two-person team: Joe (jamditis) and Kevin (kamditis) at BlackthornDevs. Both run parallel builds from the same design doc and merge the best parts.

### Branching

- Kevin works on `kevin/main`, Joe on `joe/main`
- All changes to `master` go through pull requests -- no direct pushes
- Short-lived feature branches off your personal main (keep under two days)
- Shared code (`Scripts/Core/`, `ScriptableObjects/`, `ProjectSettings/`) changes go through `master` only

Unity YAML files use UnityYAMLMerge (configured in `.gitattributes`). Binary assets use Git LFS.

### Testing

Tests live in `Assets/_Slopworks/Tests/`. Two levels:

- **EditMode tests** -- pure C# simulation logic (inventory operations, machine state, belt ticking). All must pass before pushing.
- **PlayMode tests** -- integration tests for FishNet networking, MonoBehaviour lifecycle, and UI wiring.

Every implementation phase must produce a playtest scene that lets a human verify the system works end-to-end before the phase is marked complete.

## Key influences

| Game | What we take | What we change |
|------|-------------|----------------|
| Satisfactory | Factory building, belts, foundation grid, co-op | Post-apocalyptic setting, combat, real buildings |
| Riftbreaker | Base defense waves, turrets, action combat | Persistent progression, expansion-driven threat |
| Stardew Valley | Relaxing progression loop | Industrial scale, combat layer |
| Halo | FPS combat feel, co-op campaign | Survival context, base building |
| V Rising | Territory expansion, conquest feel | Satisfactory-style building instead of rooms |

## Docs

- [Game design document](docs/plans/2026-02-27-game-design.md)
- [Vertical slice plan](docs/plans/2026-02-27-vertical-slice-plan.md)

### Reference architecture

Detailed architecture docs live in `docs/reference/`:

| Document | Covers |
|----------|--------|
| [Multiplayer](docs/reference/multiplayer.md) | FishNet + FishySteamworks, SyncVar vs RPC, belt sync, ParrelSync |
| [Factory automation](docs/reference/factory-automation.md) | Belt data structure, machine state machine, simulation tick, power |
| [Crafting and inventory](docs/reference/crafting-inventory.md) | ItemDefinitionSO/ItemInstance, RecipeSO, registry, serialization |
| [World generation](docs/reference/world-generation.md) | Three world spaces, noise stack, BIM pipeline, chunk system |
| [Fauna AI](docs/reference/fauna-ai.md) | NPBehave trees, perception, wave controller, pack coordination |
| [Input system](docs/reference/input-system.md) | Action maps, camera toggle, generated C# class |
| [Render pipeline](docs/reference/render-pipeline.md) | URP, camera stacking, SRP Batcher, GPU instancing |
| [Audio](docs/reference/audio.md) | FMOD Studio, adaptive factory audio, machine loops |
| [Service architecture](docs/reference/service-architecture.md) | VContainer DI, scope hierarchy, registration |
| [UI framework](docs/reference/ui-framework.md) | uGUI, world-space panels, SyncVar binding |
| [Testing](docs/reference/testing.md) | EditMode/PlayMode patterns, testable C# |
| [Physics layers](docs/reference/physics-layers.md) | Layer assignments, collision matrix, raycast masks |
| [Addressables](docs/reference/addressables.md) | Group structure, async loading, DLC catalog |
| [Supabase SDK](docs/reference/supabase-unity-sdk.md) | supabase-csharp, UniTask, JSONB upsert, auth |
| [Team workflow](docs/reference/team-workflow.md) | gitattributes, UnityYAMLMerge, multi-scene merge |
