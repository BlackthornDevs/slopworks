# Slopworks game bible

Canonical catalog of every game element in Slopworks. Each file defines a schema (property names, types, constraints) and concrete entries in YAML. Machine-readable for future ScriptableObject generation tooling.

**Both developers (Joe and Kevin) reference this bible when adding content.** If it's not in the bible, it doesn't exist. If the bible disagrees with code, the bible is the design authority and code should be updated.

---

## Directory index

### Items (player-facing objects)

| File | Contents |
|------|----------|
| [items/raw-materials.md](items/raw-materials.md) | Ores, scrap, organic matter, chemicals, ingots |
| [items/weapons.md](items/weapons.md) | Ranged and melee weapons with full combat stats |
| [items/wearables.md](items/wearables.md) | Armor, helmets, boots — defense and passive effects |
| [items/equipment.md](items/equipment.md) | Tools, scanners, repair kits — utility gear |
| [items/consumables.md](items/consumables.md) | Med kits, stim packs, temporary buffs |
| [items/tower-loot.md](items/tower-loot.md) | Key fragments, power shards, blueprints, rare materials |
| [items/lore-items.md](items/lore-items.md) | SLOP logs, safety posters, data fragments |

### Buildables (placed in the world)

| File | Contents |
|------|----------|
| [buildables/machines.md](buildables/machines.md) | Smelters, assemblers, splitters, inserters, generators |
| [buildables/defenses.md](buildables/defenses.md) | Turrets, spike walls, barbed wire, gates, mines, spotlights |
| [buildables/structural.md](buildables/structural.md) | Foundations, walls, ramps, doors, walkways |
| [buildables/scenery.md](buildables/scenery.md) | Decorative objects, props, signage |

### Systems (rules and progression)

| File | Contents |
|------|----------|
| [systems/recipes.md](systems/recipes.md) | Crafting chains: inputs, outputs, time, workstation |
| [systems/research-tree.md](systems/research-tree.md) | Tech nodes: prerequisites, costs, unlocks |
| [systems/upgrades.md](systems/upgrades.md) | Tier progression for machines, weapons, buildings |
| [systems/status-effects.md](systems/status-effects.md) | Corrosion, toxicity, bleeding, buffs, debuffs |
| [systems/tower-contracts.md](systems/tower-contracts.md) | 15 contracts (4 spine + 11 branch), tier progression, co-op drop-in, environmental hazards, SLOP intel |
| [systems/wave-events.md](systems/wave-events.md) | Wave compositions, threat tiers, spawn configs |

### World (exploration and territory)

| File | Contents |
|------|----------|
| [world/biomes.md](world/biomes.md) | Wasteland, grassland, forest, swamp, ruins |
| [world/reclaimed-buildings.md](world/reclaimed-buildings.md) | Power plant, foundry, warehouse, machine shop, etc. |
| [world/environmental-hazards.md](world/environmental-hazards.md) | Toxic leaks, collapses, spore clouds, electrical |
| [world/supply-lines.md](world/supply-lines.md) | Inter-building logistics: throughput, vulnerability |

### Characters (NPCs, enemies, narrative)

| File | Contents |
|------|----------|
| [characters/fauna.md](characters/fauna.md) | Surface, interior, boss, pack — stats, behaviors, biomes |
| [characters/npcs.md](characters/npcs.md) | Friendly/neutral characters, traders, quest givers |
| [characters/slop-dialogue.md](characters/slop-dialogue.md) | SLOP lines, mood triggers, unreliability patterns |
| [characters/narrative-progression.md](characters/narrative-progression.md) | Chapters, milestones, endgame reveal beats |

---

## File format

Each file follows this structure:

```
# Category name

Brief description of what this category covers and which SO type it maps to.

## Schema

(YAML code block defining all properties with types and constraints)

## Entries

(YAML code block with concrete game elements)
```

### Schema conventions

- Property names match existing C# ScriptableObject field names exactly (camelCase)
- Types use: `string`, `int`, `float`, `bool`, `list`, `enum`
- Enums list allowed values in brackets: `enum [Kinetic, Explosive, Fire, Toxic]`
- Cross-references use the target entry's ID field as a string
- `| null` means the field is optional/nullable

### Entry conventions

- All IDs are `snake_case`, unique within their category
- `copy_from` inherits all fields from a parent entry, then overrides listed fields (CDDA pattern)
- `tags` is a list of lowercase strings for cross-cutting queries
- `slopCommentary` is always in-character, always in quotes
- Entries are ordered by tier, then alphabetically within tier

---

## Inheritance with copy_from

To avoid duplicating fields across similar entries:

```yaml
- id: grunt_base
  abstract: true          # abstract entries don't appear in-game
  maxHealth: 100
  moveSpeed: 3.5
  attackDamage: 10

- id: grunt_forest
  copy_from: grunt_base
  moveSpeed: 4.0          # override: faster in forest
  tags: [surface, forest]

- id: grunt_ruins
  copy_from: grunt_base
  maxHealth: 120           # override: tougher in ruins
  tags: [surface, ruins]
```

Rules:
- `copy_from` references another entry's ID in the same file
- `abstract: true` entries define templates but aren't real game objects
- Only one level of inheritance (no chains)
- Overridden fields replace the parent value entirely
- Use `extend_tags` to add to the parent's tags without replacing them

---

## Shared enums

These values are defined in C# code. Bible entries must use these exact strings.

### ItemCategory
`None`, `RawMaterial`, `Component`, `Tool`, `Building`, `Consumable`, `Ammo`

### DamageType
`Kinetic`, `Explosive`, `Fire`, `Toxic`

### TargetingMode
`Closest`, `LowestHealth`, `HighestThreat`

### MachineStatus
`Idle`, `Working`, `Blocked`

### PortType
`Input`, `Output`

### PortOwnerType
`Machine`, `Storage`, `Belt`, `Turret`

### MEPSystemType
`Electrical`, `Plumbing`, `Mechanical`, `HVAC`

### OverworldBiomeType
`Grassland`, `Forest`, `Wasteland`, `Swamp`, `Ruins`, `OvergrownRuins`

### OverworldNodeType
`HomeBase`, `Building`, `Tower`

### LootRarity
`Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`

### SnapPointType
`FoundationEdge`, `FoundationCorner`, `WallEnd`, `RampBase`, `RampTop`

---

## Cross-reference format

Fields that reference entries in other files use the target's ID as a plain string:

```yaml
- id: smelt_iron
  inputs:
    - itemId: iron_ore       # references raw-materials.md entry
      count: 2
  outputs:
    - itemId: iron_ingot     # references raw-materials.md entry
      count: 1
  requiredMachineType: smelter  # references machines.md machineType field
```

The validation script (`tools/validate-bible.py`) checks that every cross-reference resolves to an existing ID.

---

## Validation

Run the validation script to check all bible files:

```bash
python3 tools/validate-bible.py
```

Checks performed:
- Every entry has a required ID field
- Enum fields use valid values
- Cross-references resolve to existing entries
- No duplicate IDs within a category
- `copy_from` targets exist and aren't circular

---

## Adding new entries

1. Find the correct category file
2. Add the entry in the `## Entries` YAML block, following the schema
3. If the entry references items/recipes/fauna in other files, verify those exist
4. Run `python3 tools/validate-bible.py`
5. Commit
