# Game bible implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a structured game design bible in `docs/bible/` with 24 YAML-in-markdown category files, aligned to existing codebase data structures, with a validation script for referential integrity.

**Architecture:** Markdown files with embedded YAML data blocks organized in 5 subdirectories (items, buildables, systems, world, characters). Schemas match existing ScriptableObject field names and types exactly. A Python validation script checks schema compliance and cross-references. Adopts CDDA-style `copy_from` inheritance for item variants.

**Tech Stack:** Markdown + YAML (data), Python (validation), Unity C# (future SO generator)

---

## Research digest

These findings from web research and codebase audit inform every schema decision below.

### Best practices adopted

1. **String IDs as primary keys** (Factorio, CDDA, Mindustry pattern). All IDs are `snake_case`. No numeric IDs, no enums-as-IDs. IDs are prefixed by context when ambiguous across categories.

2. **`copy_from` inheritance** (CDDA pattern). Item variants inherit from a base entry and override specific fields. Keeps 50 sword variants DRY. We use `copy_from` (underscore) not `copy-from` (hyphen) for YAML key consistency.

3. **Tags for cross-cutting concerns** (Mindustry/CDDA pattern). Every entry gets a `tags` array. Recipes can require tags instead of specific items (e.g., "any item tagged `fuel`"). Tags replace rigid category hierarchies.

4. **Two-layer validation** (industry standard). Layer 1: schema validation (correct types, required fields). Layer 2: referential integrity (every cross-reference resolves to an existing ID). JSON Schema handles layer 1; a custom Python script handles layer 2.

5. **Schema matches code exactly.** Every field name in the bible uses the same `camelCase` or `snake_case` as the corresponding SO field. The bible is the design authority; the code is the implementation. Drift between them is the most expensive bug class.

### Pitfalls we're avoiding

- **Enum explosion** — using string IDs with registry lookup, not C# enums for item identity
- **Orphaned references** — validation script catches refs to nonexistent IDs
- **Schema drift** — schemas document exact SO field names; future SO generator reads from bible directly
- **Balance spreadsheet hell** — structured YAML with inheritance beats copy-paste spreadsheets

### Codebase alignment map

Each bible category maps to existing code. Schemas must include ALL fields from the SO type, using the same names.

| Bible file | SO type | Registry | ID field |
|---|---|---|---|
| `items/raw-materials.md` | `ItemDefinitionSO` | `ItemRegistry` | `itemId` |
| `items/weapons.md` | `ItemDefinitionSO` + `WeaponDefinitionSO` | `ItemRegistry` | `itemId` / `weaponId` |
| `items/wearables.md` | `ItemDefinitionSO` | `ItemRegistry` | `itemId` |
| `items/equipment.md` | `ItemDefinitionSO` | `ItemRegistry` | `itemId` |
| `items/consumables.md` | `ItemDefinitionSO` | `ItemRegistry` | `itemId` |
| `items/tower-loot.md` | `ItemDefinitionSO` + `LootDropDefinition` | `ItemRegistry` / `TowerLootTable` | `itemId` |
| `items/lore-items.md` | `ItemDefinitionSO` | `ItemRegistry` | `itemId` |
| `buildables/machines.md` | `MachineDefinitionSO` | — | `machineId` |
| `buildables/defenses.md` | `TurretDefinitionSO` | — | `turretId` |
| `buildables/structural.md` | `FoundationDefinitionSO` / `WallDefinitionSO` / `RampDefinitionSO` | — | `foundationId` / `wallId` / `rampId` |
| `buildables/scenery.md` | (no SO yet) | — | `sceneryId` |
| `systems/recipes.md` | `RecipeSO` | `RecipeRegistry` | `recipeId` |
| `systems/research-tree.md` | (no SO yet) | — | `nodeId` |
| `systems/upgrades.md` | (no SO yet) | — | `upgradeId` |
| `systems/status-effects.md` | (no SO yet) | — | `effectId` |
| `systems/wave-events.md` | `WaveDefinition` (struct) | — | `waveId` |
| `world/biomes.md` | `OverworldBiomeType` (enum) | — | `biomeId` |
| `world/reclaimed-buildings.md` | `BuildingDefinitionSO` | — | `buildingId` |
| `world/environmental-hazards.md` | (no SO yet) | — | `hazardId` |
| `world/supply-lines.md` | `SupplyLine` (class) | — | `lineTypeId` |
| `characters/fauna.md` | `FaunaDefinitionSO` | — | `faunaId` |
| `characters/npcs.md` | (no SO yet) | — | `npcId` |
| `characters/slop-dialogue.md` | (no SO yet) | — | `lineId` |
| `characters/narrative-progression.md` | (no SO yet) | — | `chapterId` |

### Existing enums the schemas must reference

These enum values are already in code. Bible entries that use them must use the exact same value strings.

- **ItemCategory:** None, RawMaterial, Component, Tool, Building, Consumable, Ammo
- **DamageType:** Kinetic, Explosive, Fire, Toxic
- **TargetingMode:** Closest, LowestHealth, HighestThreat
- **MachineStatus:** Idle, Working, Blocked
- **PortType:** Input, Output
- **PortOwnerType:** Machine, Storage, Belt, Turret
- **MEPSystemType:** Electrical, Plumbing, Mechanical, HVAC
- **OverworldBiomeType:** Grassland, Forest, Wasteland, Swamp, Ruins, OvergrownRuins
- **OverworldNodeType:** HomeBase, Building, Tower
- **LootRarity:** Common, Uncommon, Rare, Epic, Legendary
- **SnapPointType:** FoundationEdge, FoundationCorner, WallEnd, RampBase, RampTop

### Existing item IDs (from PlaytestContext)

These items already exist in code and must appear in the bible with matching IDs:

- `iron_ore`, `iron_ingot`, `iron_scrap`, `turret_ammo`
- `power_cell`, `signal_decoder`, `reinforced_plating`
- `key_fragment`, `boss_blueprint`

### Existing recipe IDs

- `smelt_iron`, `craft_turret_ammo`

---

## Task 1: Create directory structure and README

**Files:**
- Create: `docs/bible/README.md`
- Create: directories `docs/bible/items/`, `docs/bible/buildables/`, `docs/bible/systems/`, `docs/bible/world/`, `docs/bible/characters/`

**Step 1: Create directories**

```bash
mkdir -p docs/bible/items docs/bible/buildables docs/bible/systems docs/bible/world docs/bible/characters
```

**Step 2: Write README.md**

The README is the index page. It links to every category file, explains the file format, documents the `copy_from` inheritance pattern, and lists all shared enums that entries can reference. Include the codebase alignment table from the research digest above.

Content should cover:
- Purpose of the bible
- File format (markdown header + YAML schema block + YAML entries block)
- ID conventions (snake_case, unique within category)
- Cross-reference format (string IDs referencing entries in other files)
- `copy_from` inheritance rules
- `tags` array conventions
- All enum values (copied from the enums list above)
- Links to every category file

**Step 3: Commit**

```bash
git add docs/bible/
git commit -m "Add game bible directory structure and README index"
```

---

## Task 2: Items — raw materials

**Files:**
- Create: `docs/bible/items/raw-materials.md`

**Step 1: Write the file**

Schema must match `ItemDefinitionSO` fields exactly:
- `itemId` (string) — maps to `ItemDefinitionSO.itemId`
- `displayName` (string) — maps to `ItemDefinitionSO.displayName`
- `description` (string) — maps to `ItemDefinitionSO.description`
- `slopCommentary` (string) — SLOP's in-character comment (not in SO, design-only)
- `category` (enum) — maps to `ItemDefinitionSO.category`, must be `RawMaterial`
- `isStackable` (bool) — maps to `ItemDefinitionSO.isStackable`
- `maxStackSize` (int) — maps to `ItemDefinitionSO.maxStackSize`
- `hasDurability` (bool) — maps to `ItemDefinitionSO.hasDurability`
- `maxDurability` (float) — maps to `ItemDefinitionSO.maxDurability`
- `tags` (list of strings) — cross-cutting tags (e.g., `metal`, `organic`, `fuel`)
- `rarity` (enum) — LootRarity value
- `tier` (int) — progression tier (1-3)
- `obtainedFrom` (list) — acquisition sources with details
- `modelStyle` (string) — art direction note

Entries must include all existing IDs: `iron_ore`, `iron_ingot`, `iron_scrap`. Add additional raw materials that the game design doc implies: copper_ore, copper_ingot, steel_ingot, scrap_metal, chemicals, organic_matter, fuel_cell.

**Step 2: Commit**

```bash
git add docs/bible/items/raw-materials.md
git commit -m "Add raw materials bible entry with schema and initial entries"
```

---

## Task 3: Items — weapons

**Files:**
- Create: `docs/bible/items/weapons.md`

**Step 1: Write the file**

Schema combines `ItemDefinitionSO` fields + `WeaponDefinitionSO` fields:

From ItemDefinitionSO: `itemId`, `displayName`, `description`, `category` (must be `Tool` or new Weapon value), `isStackable` (false for weapons), `maxStackSize` (1), `hasDurability` (true), `maxDurability`

From WeaponDefinitionSO: `weaponId`, `damage`, `fireRate`, `range`, `damageType` (Kinetic/Explosive/Fire/Toxic), `magazineSize`, `reloadTime`

Additional design fields (not yet in SO, but needed for full bible):
- `slopCommentary`, `rarity`, `tier`
- `weaponType` (enum: rifle, shotgun, pistol, smg, melee_blunt, melee_blade)
- `projectileSpeed` (float, 0 for hitscan)
- `recoil` (float, 0.0-1.0)
- `headshotMultiplier` (float)
- `penetration` (int)
- `aoeRadius` (float)
- `statusEffects` (list of effect_id + chance + duration)
- `ammoType` (string, item ID)
- `upgradeSlots` (int)
- `upgradePath` (string, weapon ID of next tier)
- `researchRequired` (string, research node ID)
- `craftingRecipe` (string, recipe ID)
- `obtainedFrom`, `tags`, `modelStyle`, `fireSoundEvent`, `reloadSoundEvent`

Entry: `test_rifle` (matches existing WeaponDefinitionSO asset).

**Step 2: Commit**

```bash
git add docs/bible/items/weapons.md
git commit -m "Add weapons bible entry with schema and initial entries"
```

---

## Task 4: Items — wearables, equipment, consumables

**Files:**
- Create: `docs/bible/items/wearables.md`
- Create: `docs/bible/items/equipment.md`
- Create: `docs/bible/items/consumables.md`

**Step 1: Write all three files**

All three use `ItemDefinitionSO` as the base schema. Each adds category-specific fields:

**Wearables** (armor, helmets, boots):
- Base ItemDefinitionSO fields with `category: Component` (or future Armor value)
- `armorSlot` (enum: head, chest, legs, feet, hands)
- `damageReduction` (float, flat reduction)
- `damageResistance` (dict of DamageType -> float percentage)
- `movementModifier` (float, multiplier on move speed)
- `specialEffect` (string, description of passive effect)
- Design fields: `slopCommentary`, `rarity`, `tier`, `tags`, `researchRequired`, `craftingRecipe`, `obtainedFrom`, `modelStyle`

**Equipment** (tools, scanners, repair kits):
- Base ItemDefinitionSO fields with `category: Tool`
- `equipSlot` (enum: primary, secondary, utility)
- `useAction` (string, what happens on use)
- `cooldown` (float, seconds between uses)
- Design fields same as above

**Consumables** (med kits, stim packs, temp buffs):
- Base ItemDefinitionSO fields with `category: Consumable`
- `useEffect` (string, effect ID applied on use)
- `effectDuration` (float, seconds, 0 for instant)
- `effectMagnitude` (float)
- `maxCarry` (int, inventory limit independent of stack size)
- Design fields same as above

**Step 2: Commit**

```bash
git add docs/bible/items/
git commit -m "Add wearables, equipment, and consumables bible entries"
```

---

## Task 5: Items — tower loot and lore items

**Files:**
- Create: `docs/bible/items/tower-loot.md`
- Create: `docs/bible/items/lore-items.md`

**Step 1: Write both files**

**Tower loot** schema combines `ItemDefinitionSO` + `LootDropDefinition` fields:
- Base ItemDefinitionSO fields
- From LootDropDefinition: `rarity` (LootRarity), `dropWeight` (float), `minAmount`/`maxAmount` (int), `minFloorElevation`/`maxFloorElevation` (int), `tierRequirement` (int)
- `towerExclusive` (bool, true for all tower loot)
- `lostOnDeath` (bool, true for carried loot, false for equipped gear)

Entries must include existing IDs: `power_cell`, `signal_decoder`, `reinforced_plating`, `key_fragment`, `boss_blueprint`

**Lore items** — lighter schema since these are primarily narrative:
- `itemId`, `displayName`, `description`, `slopCommentary`
- `loreType` (enum: slop_log, safety_poster, data_fragment, environmental, audio_log)
- `narrativeChapter` (string, which progression chapter this relates to)
- `locationHint` (string, where this is typically found)
- `discoveryTrigger` (string, what reveals this item)
- `fullText` (string, the actual lore content)
- `tags`

**Step 2: Commit**

```bash
git add docs/bible/items/
git commit -m "Add tower loot and lore items bible entries"
```

---

## Task 6: Buildables — machines

**Files:**
- Create: `docs/bible/buildables/machines.md`

**Step 1: Write the file**

Schema matches `MachineDefinitionSO` fields exactly:
- `machineId` (string) — maps to `MachineDefinitionSO.machineId`
- `displayName` (string)
- `size` (array [width, depth]) — maps to `MachineDefinitionSO.size` (Vector2Int)
- `machineType` (string) — maps to `MachineDefinitionSO.machineType`, matches `RecipeSO.requiredMachineType`
- `inputBufferSize` (int) — maps to `MachineDefinitionSO.inputBufferSize`
- `outputBufferSize` (int) — maps to `MachineDefinitionSO.outputBufferSize`
- `processingSpeed` (float) — maps to `MachineDefinitionSO.processingSpeed`
- `powerConsumption` (float) — maps to `MachineDefinitionSO.powerConsumption`
- `ports` (list) — maps to `MachineDefinitionSO.ports` (MachinePort[])
  - Each port: `localOffset` [x,y], `direction` [x,y], `type` (Input/Output)

Additional design fields:
- `slopCommentary`, `description`, `tier`, `tags`
- `category` (enum: production, logistics, power, storage, utility)
- `defaultRecipe` (string, recipe ID or null)
- `availableRecipes` (list of recipe IDs)
- `powerGeneration` (float, kW, for generators)
- `health` (float, can be damaged during raids)
- `repairMaterial` (string, item ID)
- `researchRequired`, `craftingRecipe`, `upgradePath`
- `portOwnerType`: always `Machine`
- `modelStyle`, `workingSound`, `idleSound`

Entries: smelter_t1 (matches existing `machineType: "smelter"`), plus planned machines from design doc (assembler, splitter, merger, inserter, generator).

**Step 2: Commit**

```bash
git add docs/bible/buildables/machines.md
git commit -m "Add machines bible entry with schema and initial entries"
```

---

## Task 7: Buildables — defenses, structural, scenery

**Files:**
- Create: `docs/bible/buildables/defenses.md`
- Create: `docs/bible/buildables/structural.md`
- Create: `docs/bible/buildables/scenery.md`

**Step 1: Write all three files**

**Defenses** schema matches `TurretDefinitionSO`:
- `turretId`, `displayName`, `range`, `fireInterval`, `damagePerShot`, `damageType` (DamageType enum)
- `ammoItemId` (string, item ID), `powerConsumption`, `size` [w,d], `ports` (MachinePort[])
- `powerThreshold`, `ammoSlotCount`, `ammoMaxStackSize`, `targetingMode` (TargetingMode enum)
- Design fields: `slopCommentary`, `description`, `tier`, `tags`, `researchRequired`, `craftingRecipe`, `health`, `modelStyle`
- Non-turret defenses (spike walls, barbed wire, mines, spotlights) use a simpler schema since they have no SO yet — document the fields they'll need

**Structural** schema covers three SO types:
- Foundation section: matches `FoundationDefinitionSO` — `foundationId`, `displayName`, `size`, `generatesSnapPoints`
- Wall section: matches `WallDefinitionSO` — `wallId`, `displayName`
- Ramp section: matches `RampDefinitionSO` — `rampId`, `displayName`, `footprintLength`
- All share: `tier`, `tags`, `craftingRecipe`, `health`, `modelStyle`

**Scenery** — no existing SO, so define the schema from scratch:
- `sceneryId`, `displayName`, `description`, `slopCommentary`
- `sceneryType` (enum: prop, signage, decoration, vegetation, debris)
- `placementMode` (enum: foundation_snap, freeform, wall_mount)
- `size` [w,d]
- `isDestructible` (bool)
- `biomeAffinity` (list of OverworldBiomeType values where this spawns naturally)
- `tags`, `modelStyle`

**Step 2: Commit**

```bash
git add docs/bible/buildables/
git commit -m "Add defenses, structural, and scenery bible entries"
```

---

## Task 8: Systems — recipes

**Files:**
- Create: `docs/bible/systems/recipes.md`

**Step 1: Write the file**

Schema matches `RecipeSO` fields exactly:
- `recipeId` (string) — maps to `RecipeSO.recipeId`
- `displayName` (string)
- `inputs` (list) — each entry: `itemId` (string), `count` (int). Maps to `RecipeSO.inputs` (RecipeIngredient[])
- `outputs` (list) — each entry: `itemId` (string), `count` (int). Maps to `RecipeSO.outputs`
- `craftDuration` (float, seconds) — maps to `RecipeSO.craftDuration`
- `requiredMachineType` (string or null) — maps to `RecipeSO.requiredMachineType`, null = hand-craftable

Additional design fields:
- `slopCommentary`, `description`, `tier`, `tags`
- `researchRequired` (string, research node ID)
- `discoveredByDefault` (bool)
- `category` (enum: smelting, crafting, construction, assembly, chemical)
- `powerRequired` (bool)
- `byproducts` (list, same format as outputs — waste products)

Entries must include existing: `smelt_iron`, `craft_turret_ammo`. Add recipes implied by design doc: smelt_copper, smelt_steel, craft_iron_plate, craft_mechanical_component, craft_circuit_board.

**Step 2: Commit**

```bash
git add docs/bible/systems/recipes.md
git commit -m "Add recipes bible entry with schema and initial entries"
```

---

## Task 9: Systems — research tree, upgrades, status effects, wave events

**Files:**
- Create: `docs/bible/systems/research-tree.md`
- Create: `docs/bible/systems/upgrades.md`
- Create: `docs/bible/systems/status-effects.md`
- Create: `docs/bible/systems/wave-events.md`

**Step 1: Write all four files**

**Research tree** — no existing SO, new schema:
- `nodeId`, `displayName`, `description`, `slopCommentary`
- `prerequisites` (list of nodeId strings)
- `cost` (list of itemId + count pairs)
- `researchTime` (float, seconds)
- `unlocks` (list) — each entry: `type` (enum: recipe, machine, weapon, building, upgrade), `targetId` (string)
- `tier`, `tags`

**Upgrades** — no existing SO, new schema:
- `upgradeId`, `displayName`, `description`, `slopCommentary`
- `targetType` (enum: machine, weapon, turret, building, supply_line, player)
- `targetId` (string, ID of thing being upgraded, or null for category-wide)
- `effect` (string, what changes)
- `magnitude` (float)
- `tier`, `researchRequired`, `craftingRecipe`, `tags`

**Status effects** — no existing SO, new schema:
- `effectId`, `displayName`, `description`, `slopCommentary`
- `effectType` (enum: damage_over_time, slow, damage_resistance, speed_boost, heal_over_time, stun)
- `magnitude` (float)
- `duration` (float, seconds, 0 for permanent until removed)
- `tickInterval` (float, seconds between ticks for DoT/HoT)
- `stackable` (bool)
- `maxStacks` (int)
- `source` (enum: fauna_attack, environmental, consumable, equipment)
- `visualIndicator` (string, art direction note)
- `tags`

**Wave events** — matches `WaveDefinition` struct:
- `waveId`, `displayName`, `description`
- `threatTier` (int) — minimum threat tier to trigger
- `enemyCount` (int) — maps to `WaveDefinition.enemyCount`
- `spawnDelay` (float) — maps to `WaveDefinition.spawnDelay`
- `timeBetweenWaves` (float) — maps to `WaveDefinition.timeBetweenWaves`
- `faunaIds` (list of strings) — maps to `WaveDefinition.faunaIds`
- `spawnDirections` (list of enum: north, south, east, west, multi)
- `bossWave` (bool)
- `tags`

**Step 2: Commit**

```bash
git add docs/bible/systems/
git commit -m "Add research tree, upgrades, status effects, and wave events bible entries"
```

---

## Task 10: World — biomes, buildings, hazards, supply lines

**Files:**
- Create: `docs/bible/world/biomes.md`
- Create: `docs/bible/world/reclaimed-buildings.md`
- Create: `docs/bible/world/environmental-hazards.md`
- Create: `docs/bible/world/supply-lines.md`

**Step 1: Write all four files**

**Biomes** — matches `OverworldBiomeType` enum:
- `biomeId` (string, must match enum value: Grassland, Forest, Wasteland, Swamp, Ruins, OvergrownRuins)
- `displayName`, `description`, `slopCommentary`
- `vertexColor` (RGB array) — from overworld terrain design doc
- `temperatureRange` (enum: warm, cool)
- `moistureRange` (enum: dry, medium, wet)
- `nativeFauna` (list of faunaId strings)
- `nativeHazards` (list of hazardId strings)
- `resourceNodes` (list of itemId strings — what raw materials spawn here)
- `ruinDensity` (float, 0.0-1.0)
- `ambientSoundscape` (string, FMOD event)
- `tags`

**Reclaimed buildings** — matches `BuildingDefinitionSO`:
- `buildingId` — maps to `BuildingDefinitionSO.buildingId`
- `displayName`
- `description`, `slopCommentary`
- `buildingType` (enum: power_plant, foundry, warehouse, machine_shop, water_treatment, electronics_lab, hospital, office)
- `requiredMEPCount` (int) — maps to `BuildingDefinitionSO.requiredMEPCount`
- `mepSystems` (list of MEPSystemType values present)
- `producedItemIds` (list of strings) — maps to `BuildingDefinitionSO.producedItemIds`
- `producedAmounts` (list of ints) — maps to `BuildingDefinitionSO.producedAmounts`
- `productionInterval` (float) — maps to `BuildingDefinitionSO.productionInterval`
- `biomeAffinity` (list of biomeId strings — where this building type appears)
- `difficultyTier` (int)
- `faunaEcosystem` (string, description of what fauna types nest here)
- `faunaIds` (list of faunaId strings)
- `overworldNodeType`: always `Building`
- `tags`

**Environmental hazards** — no existing SO:
- `hazardId`, `displayName`, `description`, `slopCommentary`
- `hazardType` (enum: toxic_leak, structural_collapse, spore_cloud, electrical, radiation, flooding)
- `damageType` (DamageType enum)
- `damagePerSecond` (float)
- `aoeRadius` (float)
- `duration` (float, 0 for permanent)
- `statusEffectApplied` (string, effectId or null)
- `biomeAffinity` (list of biomeId strings)
- `triggerCondition` (string, what activates the hazard)
- `visualIndicator`, `soundEvent`
- `tags`

**Supply lines** — based on `SupplyLine` class:
- `lineTypeId`, `displayName`, `description`, `slopCommentary`
- `tier` (int)
- `throughputRate` (float, items per minute)
- `maxDistance` (int, hex count)
- `vulnerabilityMultiplier` (float, how much fauna targets this)
- `constructionCost` (list of itemId + count)
- `upgradePath` (string, next tier lineTypeId)
- `researchRequired`
- `tags`

**Step 2: Commit**

```bash
git add docs/bible/world/
git commit -m "Add biomes, reclaimed buildings, environmental hazards, and supply lines bible entries"
```

---

## Task 11: Characters — fauna

**Files:**
- Create: `docs/bible/characters/fauna.md`

**Step 1: Write the file**

Schema matches `FaunaDefinitionSO` fields exactly:
- `faunaId` — maps to `FaunaDefinitionSO.faunaId`
- `displayName`, `description`, `slopCommentary`
- `maxHealth` (float) — maps to `FaunaDefinitionSO.maxHealth`
- `moveSpeed` (float) — maps to `FaunaDefinitionSO.moveSpeed`
- `attackDamage` (float) — maps to `FaunaDefinitionSO.attackDamage`
- `attackRange` (float) — maps to `FaunaDefinitionSO.attackRange`
- `attackCooldown` (float) — maps to `FaunaDefinitionSO.attackCooldown`
- `sightRange` (float) — maps to `FaunaDefinitionSO.sightRange`
- `sightAngle` (float) — maps to `FaunaDefinitionSO.sightAngle`
- `hearingRange` (float) — maps to `FaunaDefinitionSO.hearingRange`
- `attackDamageType` (DamageType enum) — maps to `FaunaDefinitionSO.attackDamageType`
- `alertRange` (float) — pack behavior
- `strafeSpeed` (float) — pack behavior
- `strafeRadius` (float) — pack behavior
- `baseBravery` (float) — morale
- `fleeConfidenceThreshold` (float) — morale, default 0.3
- `coverSearchRadius` (float)

Additional design fields:
- `faunaType` (enum: surface, interior, boss, pack, apex)
- `biomeAffinity` (list of biomeId strings)
- `tier` (int)
- `rarity` (LootRarity)
- `lootDrops` (list) — itemId + dropChance + minAmount + maxAmount
- `statusEffectsApplied` (list) — effectId + chance on hit
- `packSize` (int, typical group count, 1 for solo)
- `spawnConditions` (string, where/when this fauna appears)
- `copy_from` (string, parent faunaId for variants)
- `tags`, `modelStyle`, `soundEvents`

Entries: existing fauna IDs from PlaytestContext bootstrappers (grunt, stalker, tower_boss). Add fauna types from lore design: spore_crawler (chemical buildings), biomech_hybrid (heavy manufacturing), pack_runner (warehouse), apex_predator (power generation).

**Step 2: Commit**

```bash
git add docs/bible/characters/fauna.md
git commit -m "Add fauna bible entry with schema and initial entries"
```

---

## Task 12: Characters — NPCs, SLOP dialogue, narrative progression

**Files:**
- Create: `docs/bible/characters/npcs.md`
- Create: `docs/bible/characters/slop-dialogue.md`
- Create: `docs/bible/characters/narrative-progression.md`

**Step 1: Write all three files**

**NPCs** — no existing SO, new schema:
- `npcId`, `displayName`, `description`
- `npcType` (enum: trader, quest_giver, radio_contact, ambient)
- `location` (string, where they're found)
- `faction` (string, group affiliation)
- `inventory` (list of itemId strings, for traders)
- `questsOffered` (list of quest/objective IDs)
- `dialogueTreeId` (string, reference to dialogue system)
- `tags`, `modelStyle`, `voiceStyle`

**SLOP dialogue** — unique schema for AI character lines:
- `lineId`, `category` (enum: greeting, warning, advice, commentary, lie, deflection, honest_moment, passive_aggressive)
- `triggerContext` (enum: enter_building, examine_machine, pick_up_item, low_health, high_threat, idle, building_restored, player_death, boss_encounter)
- `moodState` (enum: cheerful_corporate, passive_aggressive, paranoid, almost_honest)
- `text` (string, the actual line)
- `reliability` (enum: accurate, misleading, false, partially_true)
- `relatedBuildingType` (string, buildingId or null)
- `relatedItemId` (string or null)
- `minNarrativeChapter` (string, chapterId — earliest chapter this line can appear)
- `tags`

**Narrative progression** — the chapter/milestone structure:
- `chapterId`, `displayName`, `description`
- `chapterNumber` (int)
- `triggerCondition` (string, what advances the player to this chapter)
- `buildingsRequired` (int, number of reclaimed buildings)
- `towerTierRequired` (int)
- `slopMoodUnlocked` (string, new mood state available)
- `loreItemsAvailable` (list of lore item IDs revealed in this chapter)
- `mechanicsUnlocked` (list of strings, game systems that become available)
- `endgameRelevance` (string, how this chapter connects to the SLOP-caused-collapse revelation)
- `tags`

**Step 2: Commit**

```bash
git add docs/bible/characters/
git commit -m "Add NPCs, SLOP dialogue, and narrative progression bible entries"
```

---

## Task 13: Validation script

**Files:**
- Create: `tools/validate-bible.py`

**Step 1: Write the validation script**

A Python script that:

1. Finds all `.md` files in `docs/bible/`
2. Extracts YAML code blocks (regex: `` ```yaml\n(.*?)\n``` `` with `re.DOTALL`)
3. Parses each YAML block with `yaml.safe_load()`
4. For schema blocks (detected by presence of type annotations in values): skip, these are documentation
5. For entry blocks (detected by list of dicts with `id`-suffixed keys): validate
   - Required field check (every entry must have its category's ID field)
   - Enum validation (category, damageType, etc. must be valid values)
   - Cross-reference validation: collect all defined IDs, then check every field that references another ID (ammoType, craftingRecipe, researchRequired, faunaIds, etc.) resolves to a defined ID
6. Report: all errors grouped by file, with line hints

The script should be runnable as:
```bash
python3 tools/validate-bible.py
```

Output on success: `bible validation passed: X entries, Y cross-references, 0 errors`
Output on failure: list of errors with file path and entry ID

**Step 2: Commit**

```bash
git add tools/validate-bible.py
git commit -m "Add bible validation script for schema and referential integrity"
```

---

## Task 14: Run validation and fix errors

**Step 1: Run the validator**

```bash
python3 tools/validate-bible.py
```

**Step 2: Fix any errors found**

Common issues to expect:
- Typos in cross-reference IDs
- Missing entries for items referenced in recipes
- Enum value mismatches (e.g., `kinetic` vs `Kinetic`)

**Step 3: Run validation again until clean**

**Step 4: Final commit**

```bash
git add -A
git commit -m "Game bible: 24 category files with schemas, entries, and passing validation

- docs/bible/ with items/, buildables/, systems/, world/, characters/
- Schemas aligned to existing SO types (ItemDefinitionSO, RecipeSO, etc.)
- All existing item/recipe/fauna IDs from codebase included
- CDDA-style copy_from inheritance for variants
- Tags for cross-cutting queries
- Python validation script checks structure and referential integrity
- Research digest in implementation plan documents all design decisions"
```

---

Plan complete and saved to `docs/plans/2026-03-07-game-bible-plan.md`. Two execution options:

**1. Subagent-driven (this session)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Parallel session (separate)** — Open new session with executing-plans, batch execution with checkpoints

Which approach?
