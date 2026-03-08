# Game bible design

**Date:** 2026-03-07
**Author:** Joe (brainstormed with Claude)
**Status:** Approved

---

## Summary

A structured game design bible for Slopworks: 24 category files organized in `docs/bible/` with markdown headers and YAML data blocks. Each file defines a schema (property names, types, constraints) and concrete entries. Machine-readable for future SO generation tooling.

---

## Directory structure

```
docs/bible/
  README.md                      -- index with links to all files
  items/
    raw-materials.md             -- ores, scrap, organic matter, chemicals
    weapons.md                   -- ranged + melee, full combat stats
    wearables.md                 -- armor, helmets, boots
    equipment.md                 -- tools, scanners, repair kits
    consumables.md               -- med kits, stim packs, temp buffs
    tower-loot.md                -- key fragments, power shards, blueprints, rare mats
    lore-items.md                -- SLOP logs, safety posters, data fragments
  buildables/
    machines.md                  -- smelters, assemblers, splitters, inserters, generators
    defenses.md                  -- turrets, spike walls, barbed wire, gates, mines, lights
    structural.md                -- foundations, walls, ramps, doors, walkways
    scenery.md                   -- decorative objects, props, signage
  systems/
    recipes.md                   -- crafting chains: inputs -> outputs -> time -> workstation
    research-tree.md             -- tech nodes: prerequisites, unlock targets, costs
    upgrades.md                  -- tiers for machines, weapons, buildings, supply lines
    status-effects.md            -- corrosion, toxicity, bleeding, buffs, debuffs
    wave-events.md               -- wave compositions, threat tiers, spawn configs
  world/
    biomes.md                    -- wasteland, grassland, forest, swamp, ruins, overgrown
    reclaimed-buildings.md       -- power plant, foundry, warehouse, machine shop, etc.
    environmental-hazards.md     -- toxic leaks, collapses, spore clouds, electrical
    supply-lines.md              -- inter-building logistics: throughput, vulnerability
  characters/
    fauna.md                     -- surface, interior, boss, pack -- stats, behaviors, biomes
    npcs.md                      -- friendly/neutral characters, traders, quest givers
    slop-dialogue.md             -- SLOP lines, mood triggers, unreliability patterns
    narrative-progression.md     -- chapters, milestones, endgame reveal beats
```

## File format

Each file follows this template:

1. H1 title
2. Brief description of the category, which SO type it maps to, which registry it belongs to
3. `## Schema` section with a YAML code block defining all properties with types and constraints
4. `## Entries` section with a YAML code block containing concrete game elements

Schemas include five property groups:
- **Identity** -- id, display_name, description, slop_commentary, rarity, tier
- **Game stats** -- category-specific mechanical properties
- **Item properties** -- weight, durability, stackability (where applicable)
- **Progression / cross-references** -- research_required, crafting_recipe, upgrade_path, etc.
- **Acquisition** -- all sources where the item can be obtained
- **Visual / audio** -- model style, animations, FMOD event paths

Cross-reference fields use string IDs that must match an `id` field in another bible file. A future validation script can check referential integrity.

## Conventions

- All IDs are `snake_case`
- All enums are `snake_case`
- Cross-references use the target entry's `id` field
- `null` means "not applicable" (e.g., melee weapon has no ammo_type)
- Empty list `[]` means "none" (e.g., no status effects)
- Entries are ordered by tier, then alphabetically within tier
- SLOP commentary is always in quotes, always in character

## Future tooling

The YAML-in-markdown format supports a Unity editor script that:
1. Parses ```` ```yaml ```` blocks from each file
2. Reads schema to determine SO field mapping
3. Creates/updates `ScriptableObject.CreateInstance<T>()` assets in `Assets/_Slopworks/ScriptableObjects/`
4. Validates cross-references across all bible files

This tooling is not part of the initial scaffold. The bible is useful as a design reference before any automation exists.
