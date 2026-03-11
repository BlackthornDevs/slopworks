# Tower contracts system implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the full 14-contract tower system (4 spine + 10 branches) in both the game bible and the website.

**Architecture:** Extend the existing YAML contract entries in `tower-contracts.md` with 4 new schema fields and 10 new branch contracts. Update `tower.html` with a visual progression tree, expanded contract cards, and an environmental hazards section.

**Tech Stack:** Markdown/YAML (bible), HTML/CSS/JS (website), existing Slopworks design system (Oswald, IBM Plex Sans, Space Mono, amber/teal palette)

**Design doc:** `docs/plans/2026-03-08-tower-contracts-design.md`

---

### Task 1: Update schema and spine contracts with new fields

**Files:**
- Modify: `docs/bible/systems/tower-contracts.md`

**Step 1: Add new fields to the schema block**

Add these 4 fields to the YAML schema comment block (after `tags`):

```yaml
contractType: string             # spine | branch — required vs optional contract
environmentalHazards: list[string] | null  # darkness, em_interference, spore_clouds,
                                           # wind_exposure, structural_instability
slopIntel: string | null         # pre-mission briefing quip (separate from commentary)
uniqueReward: string | null      # human-readable description of what makes this contract worth doing
```

**Step 2: Add new fields to all 4 existing spine contracts**

For each of `tower_contract_01` through `tower_contract_04`, add:

```yaml
  contractType: spine
  environmentalHazards: null
  slopIntel: "<unique briefing quip per contract>"
  uniqueReward: null
```

`slopIntel` values for each spine contract:

- **01 (30 Rock):** `"Thermal signatures detected on floors 3 and 5. They are probably equipment. Probably."`
- **02 (MetLife):** `"Upper-floor fauna density exceeds baseline projections by 340%. SLOP has adjusted the baseline."`
- **03 (Woolworth):** `"Interior acoustics suggest significant biological mass on floors 10 through 14. SLOP recommends proceeding quietly. SLOP will not be proceeding at all."`
- **04 (One World):** `"This building contains answers to questions Management has not authorized you to ask. Complete the contract. Do not read the walls."`

Also update existing spine contracts with narrative fields from the design doc:

- **01:** Add tags `spine`, `first_contact`. Add to description: "First contact. SLOP is helpful, almost too helpful."
- **02:** Add tag `spine`. Update `unlocksTier` context in description to note "SLOP's intel gets things wrong."
- **03:** Add tag `spine`. Note in description: "SLOP decision logs start appearing."
- **04:** Add tag `spine`. Note in description: "The Warden. SLOP caused the collapse."

**Step 3: Verify formatting**

Visually check that the YAML is valid — consistent indentation (2 spaces), proper quoting on multi-line strings, no trailing whitespace.

**Step 4: Commit**

```bash
git add docs/bible/systems/tower-contracts.md
git commit -m "feat(bible): add contractType, environmentalHazards, slopIntel, uniqueReward to schema and spine contracts"
```

---

### Task 2: Write tier 1 branch contracts (Night shift, Loading docks)

**Files:**
- Modify: `docs/bible/systems/tower-contracts.md`

**Step 1: Add tower_contract_05 (Night shift)**

Append to the entries block, after tower_contract_04:

```yaml
- contractId: tower_contract_05
  displayName: "Night shift"
  description: >
    Branch contract. 30 Rock after dark — the building's power grid failed during a routine
    SLOP-managed maintenance cycle. Six floors of pitch-black corridors populated by stalkers
    that thrive in low-visibility environments. Flashlight only. Teaches resource management
    under sensory deprivation.
  slopCommentary: "SLOP notes that the building's lighting system is functioning within acceptable parameters. The parameters have been adjusted to include 'no light whatsoever.'"
  slopIntel: "Thermal signatures detected on floors 3 and 5. They are probably equipment. Probably."
  buildingName: 30 Rockefeller Plaza
  contractType: branch
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 6
  checkpointFloors: [1, 3, 5]
  bossFloors: []
  suspectedMobTypes:
    - grunt
    - stalker
  environmentalHazards:
    - darkness
  guaranteedLoot:
    - tower_map_fragment
  lootTable: night_shift_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_01
  scalingMode: linear
  narrativeFragments:
    - lore_maintenance_log_3
  uniqueReward: "Tower map fragments reveal hidden caches in future runs"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - darkness
```

**Step 2: Add tower_contract_06 (Loading docks)**

```yaml
- contractId: tower_contract_06
  displayName: "Loading docks"
  description: >
    Branch contract. MetLife's ground-level freight infrastructure — loading bays, cargo lifts,
    and service tunnels. Pack runners use the open floor plans for coordinated flanking.
    Short contract with an outsized reward: reinforced plating, a tier 2 armor component
    available early to players who seek it out.
  slopCommentary: "This area was previously used for receiving shipments. SLOP cannot confirm what is being shipped now, but it is arriving very aggressively."
  slopIntel: "Motion sensors indicate 12 to 15 fast-moving contacts on the loading floor. They do not appear to be forklift operators."
  buildingName: MetLife Building
  contractType: branch
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 5
  checkpointFloors: [1, 3]
  bossFloors: []
  suspectedMobTypes:
    - pack_runner
    - grunt
  environmentalHazards: null
  guaranteedLoot:
    - reinforced_plating
  lootTable: loading_docks_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_01
  scalingMode: linear
  narrativeFragments:
    - lore_shipping_manifest
  uniqueReward: "Reinforced plating — tier 2 armor component, available early"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - early_armor
```

**Step 3: Commit**

```bash
git add docs/bible/systems/tower-contracts.md
git commit -m "feat(bible): add tier 1 branch contracts — Night shift, Loading docks"
```

---

### Task 3: Write tier 1-2 branch contracts (Penthouse, Sublevel B, Empire State: Lobby)

**Files:**
- Modify: `docs/bible/systems/tower-contracts.md`

**Step 1: Add tower_contract_07 (Penthouse)**

```yaml
- contractId: tower_contract_07
  displayName: "Penthouse"
  description: >
    Branch contract. MetLife's upper executive floors and rooftop access. Wind exposure on
    the open roof sections creates knockback near edges and reduced accuracy at range.
    Spitters exploit the elevation advantage. Reward: capacitor banks, advanced generator
    components that unlock higher-tier factory power systems.
  slopCommentary: "The penthouse level previously housed executive offices with exceptional views. The views remain exceptional. The executives do not."
  slopIntel: "Wind speed at roof level exceeds safe operational thresholds. SLOP has redefined 'safe' to accommodate current conditions."
  buildingName: MetLife Building
  contractType: branch
  tier: 1
  recommendedPlayers: 2
  estimatedThreatLevel: moderate
  floorCount: 8
  checkpointFloors: [1, 4, 7]
  bossFloors: []
  suspectedMobTypes:
    - spitter
    - grunt
  environmentalHazards:
    - wind_exposure
  guaranteedLoot:
    - capacitor_bank
  lootTable: penthouse_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_02
  scalingMode: linear
  narrativeFragments:
    - lore_executive_memo
  uniqueReward: "Capacitor banks — advanced generator components for factory power"
  tags:
    - tier_1
    - branch
    - moderate_threat
    - wind_exposure
    - co_op_recommended
```

**Step 2: Add tower_contract_08 (Sublevel B)**

```yaml
- contractId: tower_contract_08
  displayName: "Sublevel B"
  description: >
    Branch contract. Below 30 Rock — sub-basement levels that don't appear on any official
    blueprint. EM interference from biomech hybrid activity distorts HUD readouts and scrambles
    compass navigation. The neural processors found here are SLOP-adjacent tech, dense with
    lore fragments about SLOP's original architecture.
  slopCommentary: "There are no sub-basement levels beneath this building. You are not currently in a sub-basement level. Please disregard any sub-basement levels you may encounter."
  slopIntel: "Electromagnetic readings below floor level are consistent with industrial equipment. SLOP is 14% confident in this assessment."
  buildingName: 30 Rockefeller Plaza
  contractType: branch
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: moderate
  floorCount: 10
  checkpointFloors: [1, 4, 7, 9]
  bossFloors: []
  suspectedMobTypes:
    - biomech_hybrid
    - grunt
    - stalker
  environmentalHazards:
    - em_interference
  guaranteedLoot:
    - neural_processor
  lootTable: sublevel_b_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_02
  scalingMode: linear
  narrativeFragments:
    - lore_slop_architecture_notes
    - lore_sublevel_access_log
  uniqueReward: "Neural processors — SLOP-adjacent tech with dense lore fragments"
  tags:
    - tier_2
    - branch
    - moderate_threat
    - em_interference
    - lore_heavy
    - co_op_recommended
```

**Step 3: Add tower_contract_09 (Empire State: Lobby) — future model placeholder**

```yaml
- contractId: tower_contract_09
  displayName: "Empire State: Lobby"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Empire State Building's
    grand lobby and lower floors. A relatively safe early-game branch that rewards exploration
    with signal decoders — devices that decrypt SLOP's internal communications and reveal
    hidden narrative threads in other contracts.
  slopCommentary: "SLOP has no record of any contract associated with this building. If such a contract existed, it would certainly have been filed correctly. SLOP's filing system is impeccable."
  slopIntel: "Signal traffic from this location suggests active SLOP subsystems operating independently. This is not unusual. This is fine."
  buildingName: Empire State Building
  contractType: branch
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 7
  checkpointFloors: [1, 4, 6]
  bossFloors: []
  suspectedMobTypes:
    - grunt
    - pack_runner
  environmentalHazards: null
  guaranteedLoot:
    - signal_decoder
  lootTable: empire_lobby_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_02
  scalingMode: linear
  narrativeFragments:
    - lore_slop_encrypted_comm_1
  uniqueReward: "Signal decoders — decrypt SLOP's internal communications"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - future_model
    - lore_heavy
```

**Step 4: Commit**

```bash
git add docs/bible/systems/tower-contracts.md
git commit -m "feat(bible): add tier 1-2 branch contracts — Penthouse, Sublevel B, Empire State: Lobby"
```

---

### Task 4: Write tier 2 branch contracts (Clock tower, Server farm, Chrysler: Observatory)

**Files:**
- Modify: `docs/bible/systems/tower-contracts.md`

**Step 1: Add tower_contract_10 (Clock tower)**

```yaml
- contractId: tower_contract_10
  displayName: "Clock tower"
  description: >
    Branch contract. The Woolworth Building's ornamental clock tower. Structural instability
    from the degraded clock mechanism means floor sections collapse under sustained weight
    or after timed intervals. Spore crawlers nest in the gears. Reward: a boss blueprint,
    a unique weapon schematic not available through any other contract.
  slopCommentary: "The clock tower's timekeeping function has degraded to an accuracy of plus or minus several decades. SLOP still uses it as a reference. This explains some things."
  slopIntel: "Structural analysis indicates load-bearing capacity of 60% on floors 4 through 7. SLOP rounded up."
  buildingName: Woolworth Building
  contractType: branch
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: high
  floorCount: 8
  checkpointFloors: [1, 4, 7]
  bossFloors: []
  suspectedMobTypes:
    - spore_crawler
    - stalker
  environmentalHazards:
    - structural_instability
  guaranteedLoot:
    - boss_blueprint
  lootTable: clock_tower_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: aggressive
  narrativeFragments:
    - lore_clock_maintenance_record
  uniqueReward: "Boss blueprint — unique weapon schematic exclusive to this contract"
  tags:
    - tier_2
    - branch
    - high_threat
    - structural_instability
    - co_op_recommended
    - unique_schematic
```

**Step 2: Add tower_contract_11 (Server farm)**

```yaml
- contractId: tower_contract_11
  displayName: "Server farm"
  description: >
    Branch contract. MetLife's converted data center floors. Dual hazards: EM interference
    from the still-active server racks combined with complete darkness from the power reroute.
    Stalkers and biomech hybrids occupy the server rows. Reward: double narrative fragments,
    accelerating the chapter 4-5 story content.
  slopCommentary: "These servers contain backup copies of SLOP's decision logs. SLOP strongly recommends not reading them. For security purposes. Not because they are embarrassing."
  slopIntel: "Thermal output from this floor is consistent with active computing equipment. Or a very large number of warm bodies. Further analysis is unnecessary."
  buildingName: MetLife Building
  contractType: branch
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: high
  floorCount: 10
  checkpointFloors: [1, 4, 7, 9]
  bossFloors: []
  suspectedMobTypes:
    - stalker
    - biomech_hybrid
  environmentalHazards:
    - em_interference
    - darkness
  guaranteedLoot:
    - narrative_fragment_batch
  lootTable: server_farm_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: aggressive
  narrativeFragments:
    - lore_slop_decision_log_2
    - lore_slop_decision_log_3
    - lore_safety_override_sequence
    - lore_server_access_log
  uniqueReward: "2x narrative fragments — accelerates chapter 4-5 story"
  tags:
    - tier_2
    - branch
    - high_threat
    - em_interference
    - darkness
    - co_op_recommended
    - lore_heavy
```

**Step 3: Add tower_contract_12 (Chrysler: Observatory) — future model placeholder**

```yaml
- contractId: tower_contract_12
  displayName: "Chrysler: Observatory"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Chrysler Building's
    art deco observation deck and upper spire. Wind exposure at extreme altitude creates
    constant knockback pressure near the building's iconic eagle gargoyles. Reward: modifier
    shards that overclock factory machines beyond their rated specifications.
  slopCommentary: "The Chrysler Building's architectural ornamentation remains structurally sound. SLOP bases this assessment on the fact that it has not fallen on anyone yet."
  slopIntel: "Wind patterns at this altitude are categorized as 'inadvisable.' SLOP does not recall who categorized them. It may have been SLOP."
  buildingName: Chrysler Building
  contractType: branch
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: moderate
  floorCount: 12
  checkpointFloors: [1, 4, 7, 10]
  bossFloors: []
  suspectedMobTypes:
    - grunt
    - spitter
    - pack_runner
  environmentalHazards:
    - wind_exposure
  guaranteedLoot:
    - modifier_shard
  lootTable: chrysler_observatory_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: linear
  narrativeFragments:
    - lore_chrysler_broadcast_log
  uniqueReward: "Modifier shards — overclock factory machines beyond rated specs"
  tags:
    - tier_2
    - branch
    - moderate_threat
    - wind_exposure
    - future_model
    - co_op_recommended
```

**Step 4: Commit**

```bash
git add docs/bible/systems/tower-contracts.md
git commit -m "feat(bible): add tier 2 branch contracts — Clock tower, Server farm, Chrysler: Observatory"
```

---

### Task 5: Write tier 3 branch contracts (The crypt, Sublevel zero, Empire State: Antenna)

**Files:**
- Modify: `docs/bible/systems/tower-contracts.md`

**Step 1: Add tower_contract_13 (The crypt)**

```yaml
- contractId: tower_contract_13
  displayName: "The crypt"
  description: >
    Branch contract. Deep beneath the Woolworth Building — a sealed sub-level where spore
    crawler colonies have established a hive. Spore clouds deal damage over time in affected
    areas; gas masks are required for extended exposure. Darkness compounds the threat.
    A hive queen boss guards the deepest chamber. Reward: a legendary blueprint for
    endgame-tier weapons or armor.
  slopCommentary: "SLOP's geological surveys do not indicate the presence of any crypt beneath this building. However, SLOP's geological surveys were conducted by SLOP, so their reliability is open to interpretation."
  slopIntel: "Atmospheric composition below floor level includes several compounds SLOP cannot identify. SLOP recommends breathing as little as possible. SLOP will be holding its breath from up here."
  buildingName: Woolworth Building
  contractType: branch
  tier: 3
  recommendedPlayers: 3
  estimatedThreatLevel: extreme
  floorCount: 12
  checkpointFloors: [1, 4, 7, 10]
  bossFloors: [12]
  suspectedMobTypes:
    - spore_crawler
    - biomech_hybrid
    - hive_queen
  environmentalHazards:
    - spore_clouds
    - darkness
  guaranteedLoot:
    - legendary_blueprint
  lootTable: crypt_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_04
  scalingMode: aggressive
  narrativeFragments:
    - lore_hive_origin
    - lore_spore_research_notes
  uniqueReward: "Legendary blueprint — endgame weapon or armor schematic"
  tags:
    - tier_3
    - branch
    - extreme_threat
    - spore_clouds
    - darkness
    - boss_encounter
    - endgame
    - co_op_recommended
```

**Step 2: Add tower_contract_14 (Sublevel zero)**

```yaml
- contractId: tower_contract_14
  displayName: "Sublevel zero"
  description: >
    Branch contract. The deepest accessible level of One World Trade Center — a maintenance
    infrastructure layer that predates SLOP's records. EM interference and structural
    instability make this the most disorienting contract in the game. No boss, but 15 floors
    of relentless biomech hybrid and stalker pressure. Reward: lore completion — the full
    casualty list and SLOP's original operational directives.
  slopCommentary: "This level does not exist in SLOP's building schematics. SLOP has checked twice. It continues to not exist. You are not currently standing in it."
  slopIntel: "Sensor readings from this depth return data that SLOP cannot parse. SLOP has filed this under 'not a problem' and moved on."
  buildingName: One World Trade Center
  contractType: branch
  tier: 3
  recommendedPlayers: 3
  estimatedThreatLevel: extreme
  floorCount: 15
  checkpointFloors: [1, 4, 7, 10, 13]
  bossFloors: []
  suspectedMobTypes:
    - biomech_hybrid
    - stalker
  environmentalHazards:
    - em_interference
    - structural_instability
  guaranteedLoot:
    - lore_casualty_list
    - lore_slop_original_directives
  lootTable: sublevel_zero_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_04
  scalingMode: aggressive
  narrativeFragments:
    - lore_casualty_list
    - lore_original_directives
    - lore_pre_slop_maintenance_log
    - lore_foundation_depth_survey
  uniqueReward: "Lore completion — full casualty list and original SLOP directives"
  tags:
    - tier_3
    - branch
    - extreme_threat
    - em_interference
    - structural_instability
    - lore_heavy
    - endgame
    - co_op_recommended
```

**Step 3: Add tower_contract_15 (Empire State: Antenna) — future model placeholder**

```yaml
- contractId: tower_contract_15
  displayName: "Empire State: Antenna"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Empire State Building's
    antenna spire — a vertical gauntlet of wind-blasted platforms and unstable scaffolding.
    An optional boss encounter (the only one among the future-model contracts). Wind exposure
    and structural instability combine for the most environmentally punishing contract in
    the game. Reward: cosmetic gear and a unique player title.
  slopCommentary: "The antenna was originally designed to dock airships. SLOP has repurposed it for a different kind of docking. SLOP will not elaborate on what kind."
  slopIntel: "Readings from the antenna spire are intermittent. Either the sensors are failing or something very large is periodically blocking them. SLOP prefers the sensor explanation."
  buildingName: Empire State Building
  contractType: branch
  tier: 3
  recommendedPlayers: 4
  estimatedThreatLevel: extreme
  floorCount: 10
  checkpointFloors: [1, 4, 7]
  bossFloors: [10]
  suspectedMobTypes:
    - biomech_hybrid
    - spitter
    - hive_queen
  environmentalHazards:
    - wind_exposure
    - structural_instability
  guaranteedLoot:
    - cosmetic_antenna_climber
    - title_spire_runner
  lootTable: empire_antenna_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_04
  scalingMode: aggressive
  narrativeFragments:
    - lore_antenna_broadcast_log
  uniqueReward: "Cosmetic gear + 'Spire runner' player title"
  tags:
    - tier_3
    - branch
    - extreme_threat
    - wind_exposure
    - structural_instability
    - future_model
    - boss_encounter
    - endgame
    - full_squad
```

**Step 4: Commit**

```bash
git add docs/bible/systems/tower-contracts.md
git commit -m "feat(bible): add tier 3 branch contracts — The crypt, Sublevel zero, Empire State: Antenna"
```

---

### Task 6: Update tower.html — contract progression tree

**Files:**
- Modify: `docs/tower.html`

**Step 1: Add a new section after the building cards section (line ~172)**

Insert a new `<section>` between the building cards and the elevator section. This shows the full contract tree as a styled HTML diagram (not SVG — avoiding the animation issues from earlier).

The progression tree uses the existing `.terminal` styling for a CRT-terminal aesthetic, with a monospaced text layout showing the spine/branch structure:

```html
<div class="caution-divider"></div>

<!-- Contract progression tree -->
<section class="section">
    <div class="container">
        <h2 class="text-accent" style="text-align:center; margin-bottom: 1rem;">Contract progression</h2>
        <p class="text-dim" style="text-align:center; margin-bottom: 2rem;">Spine contracts drive the narrative. Branches reward mastery.</p>
        <div class="terminal">
            <div class="terminal-header">S.L.O.P.://CONTRACT_TREE [PROGRESSION MAP]</div>
            <div style="padding: 1.5rem; font-family: 'Space Mono', monospace; font-size: 0.8rem; line-height: 1.6; color: var(--text-dim); overflow-x: auto;">
<pre style="margin:0; color: inherit; font: inherit;">
<span style="color:#E8A031;">SPINE (required)</span>                      <span style="color:#5CCFE6;">BRANCHES (optional)</span>
<span style="color:#E8A031;">──────────────</span>                      <span style="color:#5CCFE6;">──────────────────</span>

<span style="color:#E8A031;">30 Rock</span> <span style="color:var(--text-dim);">T1 // Low</span>            ──→  <span style="color:#5CCFE6;">Night shift</span> <span style="color:var(--text-dim);">(30 Rock, T1)</span>
    │                               <span style="color:#5CCFE6;">Loading docks</span> <span style="color:var(--text-dim);">(MetLife, T1)</span>
    ▼
<span style="color:#E8A031;">MetLife</span> <span style="color:var(--text-dim);">T1 // Moderate</span>       ──→  <span style="color:#5CCFE6;">Penthouse</span> <span style="color:var(--text-dim);">(MetLife, T1)</span>
    │                               <span style="color:#5CCFE6;">Sublevel B</span> <span style="color:var(--text-dim);">(30 Rock, T2)</span>
    │                               <span style="color:#5CCFE6;opacity:0.4;">Empire State: Lobby</span> <span style="color:var(--text-dim);opacity:0.4;">(future)</span>
    ▼
<span style="color:#E8A031;">Woolworth</span> <span style="color:var(--text-dim);">T2 // High</span>         ──→  <span style="color:#5CCFE6;">Clock tower</span> <span style="color:var(--text-dim);">(Woolworth, T2)</span>
    │                               <span style="color:#5CCFE6;">Server farm</span> <span style="color:var(--text-dim);">(MetLife, T2)</span>
    │                               <span style="color:#5CCFE6;opacity:0.4;">Chrysler: Observatory</span> <span style="color:var(--text-dim);opacity:0.4;">(future)</span>
    ▼
<span style="color:#E8A031;">One World</span> <span style="color:var(--text-dim);">T2→3 // Extreme</span>    ──→  <span style="color:#5CCFE6;">The crypt</span> <span style="color:var(--text-dim);">(Woolworth, T3)</span>
                                    <span style="color:#5CCFE6;">Sublevel zero</span> <span style="color:var(--text-dim);">(One World, T3)</span>
                                    <span style="color:#5CCFE6;opacity:0.4;">Empire State: Antenna</span> <span style="color:var(--text-dim);opacity:0.4;">(future)</span>
</pre>
            </div>
        </div>
        <div class="slop-says" style="margin-top: 1.5rem;">
            SLOP has organized these contracts in order of increasing regret. Complete them sequentially for the optimal despair curve. Branch contracts are optional but SLOP recommends them for personnel who enjoy unnecessary risk.
        </div>
    </div>
</section>
```

**Step 2: Commit**

```bash
git add docs/tower.html
git commit -m "feat(site): add contract progression tree to tower page"
```

---

### Task 7: Update tower.html — expand building cards with branch contracts

**Files:**
- Modify: `docs/tower.html`

**Step 1: Rename the "Active contracts" heading to "Contract roster"**

Change:
```html
<h2 class="text-accent" style="text-align:center; margin-bottom: 2rem;">Active contracts</h2>
```
To:
```html
<h2 class="text-accent" style="text-align:center; margin-bottom: 0.5rem;">Contract roster</h2>
<p class="text-dim" style="text-align:center; margin-bottom: 2rem;">14 contracts across 4 buildings — 11 active, 3 classified.</p>
```

**Step 2: Add branch contract cards after the existing 4 spine cards**

Add a subheading and new cards inside the existing `.fauna-grid` (or create a second grid). Each branch card follows the same `.fauna-card` structure. Use the same building images as the parent spine contract (reusing existing assets).

Add cards for all 11 current-model contracts. The 3 future-model contracts get a "classified" treatment — dimmed card with `opacity: 0.4` and a `[CLASSIFIED]` overlay.

Branch cards to add (8 current-model branches):
- Night shift (30 Rock image)
- Loading docks (MetLife image)
- Penthouse (MetLife image)
- Sublevel B (30 Rock image)
- Clock tower (Woolworth image)
- Server farm (MetLife image)
- The crypt (Woolworth image)
- Sublevel zero (One World image)

Classified cards (3 future-model, dimmed):
- Empire State: Lobby
- Chrysler: Observatory
- Empire State: Antenna

Each branch card shows: name, building, tier, threat, floor count, and the SLOP commentary snippet.

**Step 3: Commit**

```bash
git add docs/tower.html
git commit -m "feat(site): add branch contract cards and classified placeholders to tower page"
```

---

### Task 8: Update tower.html — add environmental hazards section

**Files:**
- Modify: `docs/tower.html`

**Step 1: Add a hazards section after the contract progression tree**

Insert a new section showing the 5 environmental hazard types. Use a grid of styled cards (same `.fauna-card` pattern or a new minimal card style).

```html
<div class="caution-divider"></div>

<section class="section" data-slop-interjection="SLOP has classified all environmental hazards as 'character-building opportunities.' Insurance claims will not be processed.">
    <div class="container">
        <h2 class="text-accent" style="text-align:center; margin-bottom: 0.5rem;">Environmental hazards</h2>
        <p class="text-dim" style="text-align:center; margin-bottom: 2rem;">Branch contracts introduce conditions that spine contracts don't prepare you for.</p>

        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 1.5rem;">

            <!-- Darkness -->
            <div style="border: 1px solid rgba(232,160,49,0.2); border-radius: 4px; padding: 1.25rem; background: rgba(232,160,49,0.02);">
                <h3 style="color: #E8A031; font-family: 'Oswald', sans-serif; font-size: 1rem; text-transform: uppercase; margin-bottom: 0.5rem;">Darkness</h3>
                <p style="color: var(--text-dim); font-size: 0.85rem; line-height: 1.5;">Power out, flashlight only. Reduced visibility makes stalkers more dangerous. Found in: Night shift, Server farm.</p>
            </div>

            <!-- EM interference -->
            <div style="border: 1px solid rgba(92,207,230,0.2); border-radius: 4px; padding: 1.25rem; background: rgba(92,207,230,0.02);">
                <h3 style="color: #5CCFE6; font-family: 'Oswald', sans-serif; font-size: 1rem; text-transform: uppercase; margin-bottom: 0.5rem;">EM interference</h3>
                <p style="color: var(--text-dim); font-size: 0.85rem; line-height: 1.5;">HUD glitches, compass unreliable, SLOP communications distorted. Biomech hybrids cause it. Found in: Sublevel B, Server farm.</p>
            </div>

            <!-- Spore clouds -->
            <div style="border: 1px solid rgba(232,160,49,0.2); border-radius: 4px; padding: 1.25rem; background: rgba(232,160,49,0.02);">
                <h3 style="color: #E8A031; font-family: 'Oswald', sans-serif; font-size: 1rem; text-transform: uppercase; margin-bottom: 0.5rem;">Spore clouds</h3>
                <p style="color: var(--text-dim); font-size: 0.85rem; line-height: 1.5;">Damage over time in affected areas. Gas mask required for extended exposure. Spore crawlers emit them. Found in: The crypt.</p>
            </div>

            <!-- Wind exposure -->
            <div style="border: 1px solid rgba(92,207,230,0.2); border-radius: 4px; padding: 1.25rem; background: rgba(92,207,230,0.02);">
                <h3 style="color: #5CCFE6; font-family: 'Oswald', sans-serif; font-size: 1rem; text-transform: uppercase; margin-bottom: 0.5rem;">Wind exposure</h3>
                <p style="color: var(--text-dim); font-size: 0.85rem; line-height: 1.5;">Knockback near edges, reduced accuracy at range. Roof and antenna combat. Found in: Penthouse, Chrysler: Observatory, Empire State: Antenna.</p>
            </div>

            <!-- Structural instability -->
            <div style="border: 1px solid rgba(232,160,49,0.2); border-radius: 4px; padding: 1.25rem; background: rgba(232,160,49,0.02);">
                <h3 style="color: #E8A031; font-family: 'Oswald', sans-serif; font-size: 1rem; text-transform: uppercase; margin-bottom: 0.5rem;">Structural instability</h3>
                <p style="color: var(--text-dim); font-size: 0.85rem; line-height: 1.5;">Floor sections collapse under weight or after time. Environmental timer pressure. Found in: Clock tower, Sublevel zero, Empire State: Antenna.</p>
            </div>

        </div>
    </div>
</section>
```

**Step 2: Commit**

```bash
git add docs/tower.html
git commit -m "feat(site): add environmental hazards section to tower page"
```

---

### Task 9: Final review and cleanup

**Files:**
- Review: `docs/bible/systems/tower-contracts.md`
- Review: `docs/tower.html`

**Step 1: Validate the bible file**

- Count entries — should be 15 total (4 spine + 11 branches, IDs 01-15)
- Check all `prerequisiteContracts` references point to valid contract IDs
- Check all `contractType` values are either `spine` or `branch`
- Verify `future_model` tag appears on exactly 3 contracts (09, 12, 15)
- Verify YAML formatting consistency

**Step 2: Validate tower.html**

- Open in a browser and verify:
  - Contract progression tree renders in the terminal box
  - All 14 contract cards display (11 normal, 3 classified/dimmed)
  - Environmental hazards grid renders
  - Mobile responsiveness (cards stack properly)
  - Existing sections (header, skyline, contract board, elevator, loop) still work

**Step 3: Final commit**

If any fixes were needed:
```bash
git add docs/bible/systems/tower-contracts.md docs/tower.html
git commit -m "fix(tower): cleanup from final review pass"
```
