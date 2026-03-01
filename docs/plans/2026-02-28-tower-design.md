# The Tower -- Design Document

> Repeatable FPS combat gauntlet for progression, rare materials, and factory upgrades.

## Overview

The Tower is a home-base feature where the player explores a real BIM building floor by floor, fighting enemies and collecting loot. Each run loads a random building from a pool. The elevator is the primary navigation tool between floor chunks. Dying means losing all carried loot. Successfully extracting banks your finds permanently.

The tower is the only source of rare materials, efficiency modifiers, and blueprints -- none of which can be automated. It gives the player a reason to pick up the gun instead of optimizing belts.

## Core Loop

1. Enter the tower (random building selected from pool)
2. Ride elevator to any floor
3. Explore in FPS -- fight enemies, collect loot (shards, modifiers, blueprints, rare materials, key fragments)
4. Choose when to extract -- return to lobby and exit to bank everything carried
5. Die = lose all carried loot (shards, fragments, materials, unlearned blueprints). Keep equipped gear.
6. Key fragments accumulate across successful extractions toward unlocking the boss floor
7. With all 4 fragments banked, boss floor unlocks on next run
8. Beat the boss: major reward (new machine tier, weapon tier), difficulty tier increases, fragment cycle resets

## What Makes It Evergreen

- Random building each run = different layouts to learn
- Tiered difficulty scales with player progression
- Rare materials only available here (manual extraction only, cannot be automated)
- Boss kills gate major factory and combat upgrades
- Full loot drop on death means every run has real stakes

## Floor Structure

The building is divided into **floor chunks** -- logical groups of 1-3 floors loaded as a unit.

### Elevator panel

Shows all available floors. Player selects a floor, brief transition (elevator door close, short ride, door open), new chunk loads. Current chunk is destroyed, selected chunk is instantiated.

### Floor chunk types

- **Single floor** -- one level of geometry. Stairwell doors blocked by debris. Most common.
- **Multi-floor section** -- 2-3 floors connected by internal stairs. Loaded together as one chunk. Used for atriums, lobbies, double-height mechanical rooms.
- **Boss floor** -- locked on elevator panel until all 4 key fragments are banked. Hand-designed arena. Always the top floor.

### Per-building configuration

- A building definition specifies how its floors are grouped into chunks
- Each chunk has spawn points (enemies), loot nodes (items), and optionally a fragment location
- Fragment locations are randomized across non-boss chunks each run
- Enemy composition and count scale with current difficulty tier

## Enemy Population

### Scaling by elevation

- **Lower floors:** Surface fauna -- same enemy types from the overworld. Familiar, manageable.
- **Upper floors:** Interior fauna -- new enemy types designed for close-quarters. Fast, flanking, ambush-oriented. Tighter corridors change combat dynamics.

### Scaling by difficulty tier

- **Tier 1:** Mostly surface fauna, a few interior types on upper floors. Low enemy counts.
- **Tier 2+:** More interior fauna, higher stats, larger packs, new enemy variants per tier.

### Boss

One per tier cycle. Top floor. Hand-designed encounter in a large room (mechanical room, rooftop, lobby -- whatever the BIM model offers). Boss kill triggers tier completion.

### Spawn behavior

- Enemies spawn when the floor chunk loads (not dynamically mid-combat)
- Cleared floors stay cleared for the duration of the run
- Leaving and re-entering resets everything (new building, fresh spawns)

## Rewards and Loot

All carried as loot. Lost on death.

### 1. Rare materials

- Tower-exclusive items. Cannot be found on the overworld or in supply chains.
- Used in high-tier crafting recipes at home base.
- Drop from enemies and loot nodes on floors.

### 2. Modifiers / power shards

- Factory machine boosters: overclock, speed, yield increase.
- Weapon/turret upgrades: damage, fire rate, range.
- Found as shard fragments on floors -- collect enough to assemble a full modifier.
- Rarer on lower floors, more common on upper floors.

### 3. Blueprints

- Unlock new machine types, recipes, building types, weapon tiers.
- Guaranteed drop from boss kills.
- Rare random drops from loot nodes on upper floors.
- Once learned (back at base after extraction), permanent. Unlearned blueprints carried as loot are lost on death.

### Key fragments

- 4 required per boss cycle.
- Placed randomly across non-boss floor chunks each run (not guaranteed to find all 4 in one run).
- Carried as loot -- must extract to bank them.
- Banked fragments persist across runs.
- Once all 4 banked, boss floor unlocks on next entry.

## Tower Access and Overworld Integration

### Overworld node

"The Tower" is a node on the overworld map. Requires a power connection from the player's network to activate.

### Before activation

Player can enter the tower lobby (floor 1 only). Fight some enemies, preview the space. Elevator is unpowered.

### After activation

Elevator powers on. All floors accessible. Full tower loop available.

### Not a supply chain node

The tower does not produce resources passively or connect to supply lines. Purely manual, go-there-and-fight. Intentionally non-automatable.

## Technical Architecture

### Scene structure

- `Tower_Core.unity` -- persistent scene. Elevator, lobby geometry, tower state manager, UI. Always loaded while in the tower.
- Floor chunks are **prefabs** instantiated at runtime, not separate scenes. Elevator transition: destroy current chunk, instantiate new chunk at floor origin.
- Building definitions are ScriptableObjects (`TowerBuildingDefinitionSO`) referencing floor chunk prefabs with spawn/loot/fragment configuration.

### Simulation layer (D-004 pattern)

| Class | Type | Responsibility |
|-------|------|----------------|
| `TowerController` | Plain C# | Run state: building, cleared chunks, carried loot, banked fragments, current tier |
| `FloorChunkDefinition` | Data class | Spawn points, loot nodes, stair connections for multi-floor sections |
| `TowerLootTable` | Plain C# | Resolves floor loot based on tier, elevation, and randomization |
| `TowerBehaviour` | MonoBehaviour wrapper | Scene management, chunk instantiation, elevator transitions |
| `TowerBuildingDefinitionSO` | ScriptableObject | Read-only building config: chunk list, floor groupings, boss floor setup |

### Integration with existing systems

| System | Integration |
|--------|-------------|
| Combat | Reuses HealthComponent, FaunaController, FaunaAI, WeaponBehaviour directly |
| Items | Tower-exclusive items are ItemDefinitionSO assets with a tower-only category |
| Inventory | Player carries loot in existing inventory. Die = tower loot cleared, equipped gear stays |
| Overworld | Tower node uses same node system as buildings. Power connection enables elevator |
| Threat/waves | Tower does not affect threat meter. Separate from base defense |

## Death and Extraction

### On death

- All carried tower loot is destroyed (shards, fragments, materials, unlearned blueprints)
- Equipped gear (weapons, armor, tools brought into the tower) is kept
- Player respawns at home base
- No corpse run (building changes each run, recovery is impossible)

### On extraction

- Player walks to lobby and exits the tower
- All carried loot is banked to persistent storage
- Key fragments move to the banked fragment counter
- Building resets on next entry (new random building, fresh enemies/loot)

## Vertical Slice Scope

- 1 building (6-7 floor chunks from one BIM model)
- 1 boss (top floor arena)
- 1 difficulty tier
- 4 key fragments per cycle
- 3-4 tower-exclusive item types (1 rare material, 1 modifier type, 1 blueprint)
- Surface fauna on lower floors, 1 new interior enemy type on upper floors
- Elevator transitions with brief fade/animation
- Lobby accessible before overworld node activation (floor 1 only)

## Phase Placement

The Tower should be implemented after:
- Core UI + Player Inventory (player needs to carry and manage loot)
- Building Exploration (establishes the BIM building interior pattern)

The Tower should be implemented before or alongside:
- Supply Chain Network (tower node integrates with overworld, but doesn't depend on supply lines)

Suggested phase: between Building Exploration and Supply Chain Network.
