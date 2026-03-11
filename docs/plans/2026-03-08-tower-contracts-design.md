# Tower contracts system design

**Goal:** Define the full roster of 15 tower contracts (4 spine + 11 branches), their progression tree, and the contract definition format for the game bible and website.

**Constraints:**
- 4 BIM models available now (30 Rock, MetLife, Woolworth, One World Trade Center) from Kevin's Elite CAD work
- Exteriors are real NYC buildings; interiors take design liberties
- 3 future buildings planned (Empire State, Chrysler) — branches only, not required for progression
- Fully branching tree with a linear spine: main path is fixed, optional branches reward mastery

---

## Progression structure

**Linear spine with branches.** The spine is 4 required contracts that drive the narrative. Branch contracts split off each spine node and are always optional — they give specialized rewards, extra lore, and build mastery but never gate spine progression.

### Rules

1. Spine contracts must be completed in order (30 Rock → MetLife → Woolworth → One World)
2. Each spine completion unlocks 2-3 branch contracts
3. Branch contracts can't gate spine progression
4. Some branches gate other branches (mini side-chains, not designed yet)
5. Branch contracts reuse existing BIM models (new floors/objectives) or use planned future models
6. Future-model contracts are designed now, built when models are ready

---

## The contract tree

```
SPINE                              BRANCHES
─────                              ────────

30 Rock (T1, required)        ──→  30 Rock: Night shift (T1)
    │                               MetLife: Loading docks (T1)
    │
    ▼
MetLife (T1, required)        ──→  MetLife: Penthouse (T1)
    │                               30 Rock: Sublevel B (T2)
    │                               [Empire State: Lobby] (T1, future)
    │
    ▼
Woolworth (T2, required)      ──→  Woolworth: Clock tower (T2)
    │                               MetLife: Server farm (T2)
    │                               [Chrysler: Observatory] (T2, future)
    │
    ▼
One World (T2→3, required)    ──→  Woolworth: The crypt (T3)
                                    One World: Sublevel zero (T3)
                                    [Empire State: Antenna] (T3, future)
```

**Total: 15 contracts** (12 using existing models, 3 requiring future models)

---

## Contract roster

### Spine contracts (required, narrative-driven)

| ID | Name | Building | Tier | Threat | Rec. players | Floors | Checkpoints | Boss floors | Fauna | Teaches | Narrative beat |
|----|------|----------|------|--------|-------------|--------|-------------|------------|-------|---------|----------------|
| 01 | 30 Rockefeller Plaza | 30 Rock | 1 | Low | 1 | 8 | 1, 4, 7 | 8 | grunt, pack_runner | Basic tower loop, elevator checkpoints, key fragments | First contact. SLOP is helpful, almost too helpful. |
| 02 | MetLife Building | MetLife | 1 | Moderate | 2 | 12 | 1, 4, 8, 11 | 12 | grunt, pack_runner, spitter | Multi-direction fauna, co-op scaling, checkpoints matter | SLOP's intel gets things wrong. First "that's not what the map says" moment. |
| 03 | Woolworth Building | Woolworth | 2 | High | 2 | 15 | 1, 5, 10, 13 | 15 | grunt, pack_runner, spitter, spore_crawler | Interior fauna, environmental hazards, vertical complexity | SLOP decision logs start appearing. Safety margins were removed. |
| 04 | One World Trade Center | One World | 2→3 | Extreme | 4 | 20 | 1, 5, 10, 14, 18 | 10, 20 | grunt, pack_runner, spitter, spore_crawler, biomech_hybrid | Multi-boss thresholds, full fauna roster, endgame gauntlet | The Warden. SLOP caused the collapse. Player choice on SLOP's fate. |

### Tier 1 branches (unlocked by completing 30 Rock spine)

| ID | Name | Building | Threat | Rec. players | Floors | Boss? | Fauna | Hazards | Unique reward |
|----|------|----------|--------|-------------|--------|-------|-------|---------|---------------|
| 05 | Night shift | 30 Rock | Low | 1 | 6 | No | grunt, stalker | Darkness (power out, flashlight only) | Tower map fragments (reveal hidden caches in future runs) |
| 06 | Loading docks | MetLife | Low | 1 | 5 | No | pack_runner, grunt | None | Reinforced plating (tier 2 armor component, available early) |

### Tier 1-2 branches (unlocked by completing MetLife spine)

| ID | Name | Building | Threat | Rec. players | Floors | Boss? | Fauna | Hazards | Unique reward |
|----|------|----------|--------|-------------|--------|-------|-------|---------|---------------|
| 07 | Penthouse | MetLife | Moderate | 2 | 8 | No | spitter, grunt | Wind exposure (roof access, exposed positions) | Capacitor banks (advanced generator components) |
| 08 | Sublevel B | 30 Rock | Moderate | 2 | 10 | No | biomech_hybrid, grunt, stalker | EM interference | Neural processors (SLOP-adjacent tech, lore-heavy) |
| 09 | [Empire State: Lobby] | Empire State | Low | 1 | 7 | No | grunt, pack_runner | None | Signal decoders (encrypted SLOP comms) |

### Tier 2 branches (unlocked by completing Woolworth spine)

| ID | Name | Building | Threat | Rec. players | Floors | Boss? | Fauna | Hazards | Unique reward |
|----|------|----------|--------|-------------|--------|-------|-------|---------|---------------|
| 10 | Clock tower | Woolworth | High | 2 | 8 | No | spore_crawler, stalker | Structural instability (clock mechanism) | Boss blueprint (unique weapon schematic) |
| 11 | Server farm | MetLife | High | 2 | 10 | No | stalker, biomech_hybrid | EM interference, darkness | 2x narrative fragments (accelerates chapter 4-5) |
| 12 | [Chrysler: Observatory] | Chrysler | Moderate | 2 | 12 | No | grunt, spitter, pack_runner | Wind exposure | Modifier shards (factory machine overclock) |

### Tier 3 branches (unlocked by completing One World spine)

| ID | Name | Building | Threat | Rec. players | Floors | Boss? | Fauna | Hazards | Unique reward |
|----|------|----------|--------|-------------|--------|-------|-------|---------|---------------|
| 13 | The crypt | Woolworth | Extreme | 3 | 12 | Yes (hive queen) | spore_crawler, biomech_hybrid, hive_queen | Spore clouds, darkness | Legendary blueprint (endgame weapon/armor) |
| 14 | Sublevel zero | One World | Extreme | 3 | 15 | No | biomech_hybrid, stalker | EM interference, structural instability | Lore completion (full casualty list, original SLOP directives) |
| 15 | [Empire State: Antenna] | Empire State | Extreme | 4 | 10 | Yes (optional boss) | biomech_hybrid, spitter, hive_queen | Wind exposure, structural instability | Cosmetic + title reward |

---

## Contract definition format

Extends the existing `tower-contracts.md` bible format with new fields.

```yaml
contractId: tower_contract_05
displayName: "Night shift"
buildingName: "30 Rockefeller Plaza"
contractType: branch              # spine | branch
tier: 1
prerequisiteContracts: [tower_contract_01]
unlocksTier: null                 # only spine contracts advance tiers

estimatedThreatLevel: low
recommendedPlayers: 1
scalingMode: linear
floorCount: 6
checkpointFloors: [1, 3, 5]
bossFloors: []

suspectedMobTypes: [grunt, stalker]
environmentalHazards: [darkness]  # darkness, em_interference, spore_clouds,
                                  # wind_exposure, structural_instability

guaranteedLoot: [tower_map_fragment]
lootTable: night_shift_loot
narrativeFragments: [lore_maintenance_log_3]
uniqueReward: "Tower map fragments reveal hidden caches in future runs"

slopCommentary: >
  SLOP notes that the building's lighting system is functioning within
  acceptable parameters. The parameters have been adjusted to include
  'no light whatsoever.'

slopIntel: >
  Thermal signatures detected on floors 3 and 5. They are probably
  equipment. Probably.
```

### New fields (vs. existing format)

| Field | Type | Purpose |
|-------|------|---------|
| `contractType` | `spine` or `branch` | Distinguishes required from optional contracts |
| `environmentalHazards` | string[] | Darkness, EM interference, spore clouds, wind, structural instability |
| `slopIntel` | string | Pre-mission briefing quip (separate from general commentary) |
| `uniqueReward` | string | Human-readable description of what makes this branch worth doing |

---

## Environmental hazards

| Hazard | Effect | Buildings |
|--------|--------|-----------|
| Darkness | Power out, flashlight only. Reduced visibility, stalkers more dangerous. | 30 Rock: Night shift, MetLife: Server farm |
| EM interference | HUD glitches, compass unreliable, SLOP communications distorted. Biomech hybrids cause it. | 30 Rock: Sublevel B, MetLife: Server farm |
| Spore clouds | Damage over time in affected areas. Spore crawlers emit them. Gas mask required for extended exposure. | Woolworth: The crypt |
| Wind exposure | Knockback near edges, reduced accuracy at range. Roof and antenna combat. | MetLife: Penthouse, Chrysler: Observatory, Empire State: Antenna |
| Structural instability | Floor sections collapse under weight or after time. Environmental timer pressure. | Woolworth: Clock tower, One World: Sublevel zero, Empire State: Antenna |

---

## Website deliverable

Update `tower.html` with:
- Full contract tree visualization (interactive or static)
- Contract cards for all 14 contracts (future-model contracts shown as "classified" placeholders)
- SLOP commentary for each contract
- The progression path shown visually

---

## Open questions

1. Should any branches gate other branches? (e.g., must complete "Night shift" before "Sublevel B" since both are 30 Rock?)
2. Exact loot tables per branch contract (item IDs, drop rates, floor minimums)
3. Checkpoint floor assignments for branch contracts (listed above are initial estimates)
4. Whether future-model contracts (Empire State, Chrysler) should appear on the website now as teasers or stay hidden
