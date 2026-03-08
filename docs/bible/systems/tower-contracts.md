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
  tags:
    - tier_2
    - tier_gate
    - extreme_threat
    - endgame
    - full_squad
    - narrative_climax
    - multiple_bosses
```
