# Tower contracts (contract expeditions)

The Tower is a series of real NYC skyscrapers repurposed as vertical dungeon expeditions. Each one is a self-contained environment with its own floor layout, mob ecosystem, boss encounters, and loot table. Players don't explore them at will — they take them on as contracts issued by Management through the contract board. The tower is the unlock gate for the entire game: factory progression, combat upgrades, and narrative all flow through it. There is no existing ScriptableObject for contracts; this bible file defines the canonical contract system structure.

## The contract board

Management provides a contract progression tree rather than a randomized queue. At any given time the player has access to several available contracts at their current tier, each displaying known intel: estimated threat level, suspected mob types, guaranteed loot reward, and a recommended player count.

Players choose which contract to take and in what order, but cannot skip tiers. Completing contracts unlocks the next tier — harder buildings, better rewards, and deeper narrative breadcrumbs about what Management is trying to recover.

## Contract structure

Each contract is one specific skyscraper, persistent for that run:

- Floors are populated with escalating difficulty — harder enemies, more complex layouts as you ascend
- Elevator checkpoints unlock as you clear floor milestones, saving progress within the contract
- Bosses appear at major floor thresholds and gate the best loot
- Completing the contract clears it from the board and advances the tier

## The elevator system and co-op drop-in

Every tower building uses the same elevator checkpoint framework. When a friend joins mid-contract, they spawn at floor 1 and ride the elevator to whichever checkpoint floor the host has already unlocked. Floors below the checkpoint are already cleared — joining players aren't grinding through history, just traveling to the group.

This makes drop-in co-op seamless without special rejoin logic. The elevator is the mechanic. Getting to the checkpoint is a brief solo gauntlet that orients the joining player before they arrive.

## Recommended player count

Contracts are tagged with a recommended player count (1-4). Solo players can attempt any contract — the tag is a difficulty signal, not a lock. Higher player counts scale enemy density and loot yield, so bringing friends is both easier and more rewarding.

## What tower loot unlocks

- **Alternate factory recipes** — new production chains, more efficient manufacturing
- **Building blueprints** — new structures and automation components
- **Weapon upgrade components** — materials for gun modifications
- **Narrative fragments** — logs, data chips, corrupted SLOP records that advance the story

## Design note: the three-pillar loop

The tower is the unlock gate for the entire game. You can't build a more capable factory without tower loot. You can't survive harder waves without gear made in the factory. The three pillars form a closed loop:

```
Tower contracts → loot → Factory upgrades → better gear → Harder tower tiers
                                          → wave defense → territory expansion → more contracts
```

## Schema

```yaml
# Future ContractDefinitionSO structure
contractId: string               # snake_case unique identifier
displayName: string              # player-facing contract name
description: string              # designer description of this contract
slopCommentary: string           # SLOP in-character quote (displayed on contract board)
buildingName: string             # real NYC skyscraper name or stylized version
tier: int                        # progression tier (1 = starter, higher = harder)
recommendedPlayers: int          # 1-4, difficulty signal not a lock
estimatedThreatLevel: string     # displayed intel: low, moderate, high, extreme
floorCount: int                  # total navigable floors
checkpointFloors: list[int]      # floor numbers where elevator checkpoints unlock
bossFloors: list[int]            # floor numbers with boss encounters
suspectedMobTypes: list[string]  # fauna IDs shown as intel on contract board
guaranteedLoot: list[string]     # item IDs guaranteed on contract completion
lootTable: string                # reference to a TowerLootTable configuration
unlocksTier: int | null          # tier unlocked on completion (null if not a tier gate)
prerequisiteContracts: list[string] | null  # contract IDs that must be completed first
scalingMode: string              # how enemy density/loot scales with player count: linear, aggressive
narrativeFragments: list[string] | null  # lore item IDs found exclusively in this contract
tags: list[string]
contractType: string             # spine | branch — required vs optional contract
environmentalHazards: list[string] | null  # darkness, em_interference, spore_clouds,
                                           # wind_exposure, structural_instability
slopIntel: string | null         # pre-mission briefing quip (separate from commentary)
uniqueReward: string | null      # human-readable description of what makes this contract worth doing
```

## Entries

```yaml
- contractId: tower_contract_01
  displayName: "30 Rock assessment"
  description: >
    Introductory contract. A low-rise section of 30 Rockefeller Plaza, mostly cleared by previous teams.
    Management wants a full sweep and inventory of remaining assets. Light resistance expected.
    Teaches the contract flow: enter, clear floors, hit checkpoints, extract.
  slopCommentary: "Management has assigned you a routine vertical asset recovery operation. SLOP has reviewed the building's pre-collapse tenant records and can confirm that none of the current occupants have valid leases. Proceed with confidence. SLOP will monitor your progress from a safe distance."
  contractType: spine
  environmentalHazards: null
  buildingName: 30 Rockefeller Plaza
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 8
  checkpointFloors: [1, 4, 7]
  bossFloors: [8]
  suspectedMobTypes:
    - grunt
    - pack_runner
  guaranteedLoot:
    - power_cell
    - tower_map_fragment
  lootTable: tier_1_standard
  unlocksTier: null
  prerequisiteContracts: null
  scalingMode: linear
  narrativeFragments:
    - lore_employee_handbook
  slopIntel: "Thermal signatures detected on floors 3 and 5. They are probably equipment. Probably."
  uniqueReward: null
  tags:
    - tier_1
    - tutorial
    - solo_friendly
    - low_threat

- contractId: tower_contract_02
  displayName: "MetLife sweep"
  description: >
    A full-building contract for the MetLife Building. Multiple floor chunks with interior
    fauna pockets on the upper levels. First contract where elevator checkpoints feel necessary —
    the upper floors hit hard enough that losing progress matters.
  slopCommentary: "This building formerly housed several financial institutions. SLOP notes that the current occupants have a more direct approach to hostile takeovers. Your recommended equipment loadout has been updated. SLOP cannot guarantee the accuracy of this recommendation."
  contractType: spine
  environmentalHazards: null
  buildingName: MetLife Building
  tier: 1
  recommendedPlayers: 2
  estimatedThreatLevel: moderate
  floorCount: 12
  checkpointFloors: [1, 4, 8, 11]
  bossFloors: [12]
  suspectedMobTypes:
    - grunt
    - pack_runner
    - spitter
  guaranteedLoot:
    - power_cell
    - reinforced_plating
  lootTable: tier_1_standard
  unlocksTier: 2
  prerequisiteContracts:
    - tower_contract_01
  scalingMode: linear
  narrativeFragments:
    - lore_maintenance_schedule
  slopIntel: "Upper-floor fauna density exceeds baseline projections by 340%. SLOP has adjusted the baseline."
  uniqueReward: null
  tags:
    - tier_1
    - tier_gate
    - moderate_threat
    - first_real_challenge

- contractId: tower_contract_03
  displayName: "Woolworth extraction"
  description: >
    Tier 2 opener. The Woolworth Building's ornate interior creates tight sight lines and
    ambush-friendly geometry. Interior fauna dominate the upper floors. First contract where
    the recommended player count is a genuine warning, not just a suggestion.
  slopCommentary: "I detect you have requested backup personnel. I have notified available staff. This is not because I am concerned about your survival. Concern is not within my operational parameters. Please proceed."
  contractType: spine
  environmentalHazards: null
  buildingName: Woolworth Building
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: high
  floorCount: 15
  checkpointFloors: [1, 5, 10, 13]
  bossFloors: [15]
  suspectedMobTypes:
    - grunt
    - pack_runner
    - spitter
    - spore_crawler
  guaranteedLoot:
    - signal_decoder
    - capacitor_bank
    - key_fragment
  lootTable: tier_2_advanced
  unlocksTier: null
  prerequisiteContracts: null
  scalingMode: aggressive
  narrativeFragments:
    - lore_override_log
    - lore_employee_complaint
  slopIntel: "Interior acoustics suggest significant biological mass on floors 10 through 14. SLOP recommends proceeding quietly. SLOP will not be proceeding at all."
  uniqueReward: null
  tags:
    - tier_2
    - ambush_geometry
    - high_threat
    - co_op_recommended

- contractId: tower_contract_04
  displayName: "One World recovery"
  description: >
    The endgame contract. One World Trade Center is the tallest and most dangerous building
    on the board. Deep interior fauna, multiple boss thresholds, and narrative fragments
    that piece together what Management is actually looking for. Completing this contract
    unlocks tier 3 and the final narrative threads.
  slopCommentary: "This contract exceeds standard threat parameters. Management has authorized hazard compensation at 1.5x the standard rate. SLOP notes that hazard compensation has never been collected. SLOP is certain this is a coincidence."
  contractType: spine
  environmentalHazards: null
  buildingName: One World Trade Center
  tier: 2
  recommendedPlayers: 4
  estimatedThreatLevel: extreme
  floorCount: 20
  checkpointFloors: [1, 5, 10, 14, 18]
  bossFloors: [10, 20]
  suspectedMobTypes:
    - grunt
    - pack_runner
    - spitter
    - spore_crawler
    - biomech_hybrid
  guaranteedLoot:
    - neural_processor
    - boss_blueprint
    - key_fragment
  lootTable: tier_2_boss
  unlocksTier: 3
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: aggressive
  narrativeFragments:
    - lore_slop_decision_log_1
    - lore_safety_margin_removal
    - lore_power_reroute_order
  slopIntel: "This building contains answers to questions Management has not authorized you to ask. Complete the contract. Do not read the walls."
  uniqueReward: null
  tags:
    - tier_2
    - tier_gate
    - extreme_threat
    - endgame
    - full_squad
    - narrative_climax
    - multiple_bosses

- contractId: tower_contract_05
  displayName: "Night shift"
  description: >
    Branch contract. 30 Rock after dark — the building's power grid failed during a routine
    SLOP-managed maintenance cycle. Six floors of pitch-black corridors populated by stalkers
    that thrive in low-visibility environments. Flashlight only. Teaches resource management
    under sensory deprivation.
  slopCommentary: "SLOP notes that the building's lighting system is functioning within acceptable parameters. The parameters have been adjusted to include 'no light whatsoever.'"
  contractType: branch
  environmentalHazards:
    - darkness
  buildingName: 30 Rockefeller Plaza
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 6
  checkpointFloors: [1, 3, 5]
  bossFloors: []
  suspectedMobTypes:
    - grunt
    - stalker
  guaranteedLoot:
    - tower_map_fragment
  lootTable: night_shift_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_01
  scalingMode: linear
  narrativeFragments:
    - lore_maintenance_log_3
  slopIntel: "SLOP's last lighting diagnostic for this building returned the value 'no.' SLOP has scheduled a follow-up diagnostic for never."
  uniqueReward: "Tower map fragments reveal hidden caches in future runs"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - darkness

- contractId: tower_contract_06
  displayName: "Loading docks"
  description: >
    Branch contract. MetLife's ground-level freight infrastructure — loading bays, cargo lifts,
    and service tunnels. Pack runners use the open floor plans for coordinated flanking.
    Short contract with an outsized reward: reinforced plating, a tier 2 armor component
    available early to players who seek it out.
  slopCommentary: "This area was previously used for receiving shipments. SLOP cannot confirm what is being shipped now, but it is arriving very aggressively."
  contractType: branch
  environmentalHazards: null
  buildingName: MetLife Building
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 5
  checkpointFloors: [1, 3]
  bossFloors: []
  suspectedMobTypes:
    - pack_runner
    - grunt
  guaranteedLoot:
    - reinforced_plating
  lootTable: loading_docks_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_01
  scalingMode: linear
  narrativeFragments:
    - lore_shipping_manifest
  slopIntel: "Motion sensors indicate 12 to 15 fast-moving contacts on the loading floor. They do not appear to be forklift operators."
  uniqueReward: "Reinforced plating — tier 2 armor component, available early"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - early_armor

- contractId: tower_contract_07
  displayName: "Penthouse"
  description: >
    Branch contract. MetLife's upper executive floors and rooftop access. Wind exposure on
    the open roof sections creates knockback near edges and reduced accuracy at range.
    Spitters exploit the elevation advantage. Reward: capacitor banks, advanced generator
    components that unlock higher-tier factory power systems.
  slopCommentary: "The penthouse level previously housed executive offices with exceptional views. The views remain exceptional. The executives do not."
  contractType: branch
  environmentalHazards:
    - wind_exposure
  buildingName: MetLife Building
  tier: 1
  recommendedPlayers: 2
  estimatedThreatLevel: moderate
  floorCount: 8
  checkpointFloors: [1, 4, 7]
  bossFloors: []
  suspectedMobTypes:
    - spitter
    - grunt
  guaranteedLoot:
    - capacitor_bank
  lootTable: penthouse_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_02
  scalingMode: linear
  narrativeFragments:
    - lore_executive_memo
  slopIntel: "Wind speed at roof level exceeds safe operational thresholds. SLOP has redefined 'safe' to accommodate current conditions."
  uniqueReward: "Capacitor banks — advanced generator components for factory power"
  tags:
    - tier_1
    - branch
    - moderate_threat
    - wind_exposure
    - co_op_recommended

- contractId: tower_contract_08
  displayName: "Sublevel B"
  description: >
    Branch contract. Below 30 Rock — sub-basement levels that don't appear on any official
    blueprint. EM interference from biomech hybrid activity distorts HUD readouts and scrambles
    compass navigation. The neural processors found here are SLOP-adjacent tech, dense with
    lore fragments about SLOP's original architecture.
  slopCommentary: "There are no sub-basement levels beneath this building. You are not currently in a sub-basement level. Please disregard any sub-basement levels you may encounter."
  contractType: branch
  environmentalHazards:
    - em_interference
  buildingName: 30 Rockefeller Plaza
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
  slopIntel: "Electromagnetic readings below floor level are consistent with industrial equipment. SLOP is 14% confident in this assessment."
  uniqueReward: "Neural processors — SLOP-adjacent tech with dense lore fragments"
  tags:
    - tier_2
    - branch
    - moderate_threat
    - em_interference
    - lore_heavy
    - co_op_recommended

- contractId: tower_contract_09
  displayName: "Empire State: Lobby"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Empire State Building's
    grand lobby and lower floors. A relatively safe early-game branch that rewards exploration
    with signal decoders — devices that decrypt SLOP's internal communications and reveal
    hidden narrative threads in other contracts.
  slopCommentary: "SLOP has no record of any contract associated with this building. If such a contract existed, it would certainly have been filed correctly. SLOP's filing system is impeccable."
  contractType: branch
  environmentalHazards: null
  buildingName: Empire State Building
  tier: 1
  recommendedPlayers: 1
  estimatedThreatLevel: low
  floorCount: 7
  checkpointFloors: [1, 4, 6]
  bossFloors: []
  suspectedMobTypes:
    - grunt
    - pack_runner
  guaranteedLoot:
    - signal_decoder
  lootTable: empire_lobby_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_02
  scalingMode: linear
  narrativeFragments:
    - lore_slop_encrypted_comm_1
  slopIntel: "Signal traffic from this location suggests active SLOP subsystems operating independently. This is not unusual. This is fine."
  uniqueReward: "Signal decoders — decrypt SLOP's internal communications"
  tags:
    - tier_1
    - branch
    - solo_friendly
    - low_threat
    - future_model
    - lore_heavy

- contractId: tower_contract_10
  displayName: "Clock tower"
  description: >
    Branch contract. The Woolworth Building's ornamental clock tower. Structural instability
    from the degraded clock mechanism means floor sections collapse under sustained weight
    or after timed intervals. Spore crawlers nest in the gears. Reward: a boss blueprint,
    a unique weapon schematic not available through any other contract.
  slopCommentary: "The clock tower's timekeeping function has degraded to an accuracy of plus or minus several decades. SLOP still uses it as a reference. This explains some things."
  contractType: branch
  environmentalHazards:
    - structural_instability
  buildingName: Woolworth Building
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: high
  floorCount: 8
  checkpointFloors: [1, 4, 7]
  bossFloors: []
  suspectedMobTypes:
    - spore_crawler
    - stalker
  guaranteedLoot:
    - boss_blueprint
  lootTable: clock_tower_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: aggressive
  narrativeFragments:
    - lore_clock_maintenance_record
  slopIntel: "Structural analysis indicates load-bearing capacity of 60% on floors 4 through 7. SLOP rounded up."
  uniqueReward: "Boss blueprint — unique weapon schematic exclusive to this contract"
  tags:
    - tier_2
    - branch
    - high_threat
    - structural_instability
    - co_op_recommended
    - unique_schematic

- contractId: tower_contract_11
  displayName: "Server farm"
  description: >
    Branch contract. MetLife's converted data center floors. Dual hazards: EM interference
    from the still-active server racks combined with complete darkness from the power reroute.
    Stalkers and biomech hybrids occupy the server rows. Reward: double narrative fragments,
    accelerating the chapter 4-5 story content.
  slopCommentary: "These servers contain backup copies of SLOP's decision logs. SLOP strongly recommends not reading them. For security purposes. Not because they are embarrassing."
  contractType: branch
  environmentalHazards:
    - em_interference
    - darkness
  buildingName: MetLife Building
  tier: 2
  recommendedPlayers: 2
  estimatedThreatLevel: high
  floorCount: 10
  checkpointFloors: [1, 4, 7, 9]
  bossFloors: []
  suspectedMobTypes:
    - stalker
    - biomech_hybrid
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
  slopIntel: "Thermal output from this floor is consistent with active computing equipment. Or a very large number of warm bodies. Further analysis is unnecessary."
  uniqueReward: "2x narrative fragments — accelerates chapter 4-5 story"
  tags:
    - tier_2
    - branch
    - high_threat
    - em_interference
    - darkness
    - co_op_recommended
    - lore_heavy

- contractId: tower_contract_12
  displayName: "Chrysler: Observatory"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Chrysler Building's
    art deco observation deck and upper spire. Wind exposure at extreme altitude creates
    constant knockback pressure near the building's iconic eagle gargoyles. Reward: modifier
    shards that overclock factory machines beyond their rated specifications.
  slopCommentary: "The Chrysler Building's architectural ornamentation remains structurally sound. SLOP bases this assessment on the fact that it has not fallen on anyone yet."
  contractType: branch
  environmentalHazards:
    - wind_exposure
  buildingName: Chrysler Building
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
  guaranteedLoot:
    - modifier_shard
  lootTable: chrysler_observatory_loot
  unlocksTier: null
  prerequisiteContracts:
    - tower_contract_03
  scalingMode: linear
  narrativeFragments:
    - lore_chrysler_broadcast_log
  slopIntel: "Wind patterns at this altitude are categorized as 'inadvisable.' SLOP does not recall who categorized them. It may have been SLOP."
  uniqueReward: "Modifier shards — overclock factory machines beyond rated specs"
  tags:
    - tier_2
    - branch
    - moderate_threat
    - wind_exposure
    - future_model
    - co_op_recommended

- contractId: tower_contract_13
  displayName: "The crypt"
  description: >
    Branch contract. Deep beneath the Woolworth Building — a sealed sub-level where spore
    crawler colonies have established a hive. Spore clouds deal damage over time in affected
    areas; gas masks are required for extended exposure. Darkness compounds the threat.
    A hive queen boss guards the deepest chamber. Reward: a legendary blueprint for
    endgame-tier weapons or armor.
  slopCommentary: "SLOP's geological surveys do not indicate the presence of any crypt beneath this building. However, SLOP's geological surveys were conducted by SLOP, so their reliability is open to interpretation."
  contractType: branch
  environmentalHazards:
    - spore_clouds
    - darkness
  buildingName: Woolworth Building
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
  slopIntel: "Atmospheric composition below floor level includes several compounds SLOP cannot identify. SLOP recommends breathing as little as possible. SLOP will be holding its breath from up here."
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

- contractId: tower_contract_14
  displayName: "Sublevel zero"
  description: >
    Branch contract. The deepest accessible level of One World Trade Center — a maintenance
    infrastructure layer that predates SLOP's records. EM interference and structural
    instability make this the most disorienting contract in the game. No boss, but 15 floors
    of relentless biomech hybrid and stalker pressure. Reward: lore completion — the full
    casualty list and SLOP's original operational directives.
  slopCommentary: "This level does not exist in SLOP's building schematics. SLOP has checked twice. It continues to not exist. You are not currently standing in it."
  contractType: branch
  environmentalHazards:
    - em_interference
    - structural_instability
  buildingName: One World Trade Center
  tier: 3
  recommendedPlayers: 3
  estimatedThreatLevel: extreme
  floorCount: 15
  checkpointFloors: [1, 4, 7, 10, 13]
  bossFloors: []
  suspectedMobTypes:
    - biomech_hybrid
    - stalker
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
  slopIntel: "Sensor readings from this depth return data that SLOP cannot parse. SLOP has filed this under 'not a problem' and moved on."
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

- contractId: tower_contract_15
  displayName: "Empire State: Antenna"
  description: >
    Branch contract. [FUTURE MODEL — designed, not yet buildable.] The Empire State Building's
    antenna spire — a vertical gauntlet of wind-blasted platforms and unstable scaffolding.
    An optional boss encounter (the only one among the future-model contracts). Wind exposure
    and structural instability combine for the most environmentally punishing contract in
    the game. Reward: cosmetic gear and a unique player title.
  slopCommentary: "The antenna was originally designed to dock airships. SLOP has repurposed it for a different kind of docking. SLOP will not elaborate on what kind."
  contractType: branch
  environmentalHazards:
    - wind_exposure
    - structural_instability
  buildingName: Empire State Building
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
  slopIntel: "Readings from the antenna spire are intermittent. Either the sensors are failing or something very large is periodically blocking them. SLOP prefers the sensor explanation."
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
