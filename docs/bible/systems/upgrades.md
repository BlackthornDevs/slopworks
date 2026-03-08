# Upgrades

Persistent improvements applied to machines, weapons, turrets, buildings, and supply lines. Each upgrade is a physical item crafted and installed on a target. Upgrades modify a single stat by a fixed magnitude. No existing SO -- these definitions will drive a future `UpgradeDefinitionSO`.

## Schema

```yaml
upgradeId: string                         # unique snake_case identifier
displayName: string                       # player-facing name
description: string                       # design description
slopCommentary: string                    # in-character SLOP quote
targetType: enum [machine, weapon, turret, building, supply_line, player]
targetId: string | null                   # specific target ID, null = applies to all of targetType
effect: string                            # stat being modified (matches code field names)
magnitude: float                          # modification value (additive or multiplicative, noted in description)
tier: int                                 # tech tier
researchRequired: string | null           # research node ID
craftingRecipe:                           # inline recipe to produce this upgrade
  inputs:
    - itemId: string
      count: int
  craftDuration: float
  requiredMachineType: string | null
tags: list                                # lowercase string tags
```

## Entries

```yaml
- upgradeId: overclock_module
  displayName: "Overclock module"
  description: "Installs a frequency booster on a machine's processing unit, increasing its processingSpeed by 25%. Also increases powerConsumption by 50% -- faster output at the cost of grid capacity. One module per machine. Applies to any machine with a processingSpeed field."
  slopCommentary: "The SLOP Productivity Acceleration Device makes your machines work harder so you don't have to. Side effects include increased power draw, elevated operating temperature, and a voided warranty that was already void."
  targetType: machine
  targetId: null
  effect: "processingSpeed"
  magnitude: 0.25
  tier: 3
  researchRequired: factory_efficiency
  craftingRecipe:
    inputs:
      - itemId: circuit_board
        count: 4
      - itemId: mechanical_component
        count: 2
      - itemId: steel_ingot
        count: 2
    craftDuration: 10.0
    requiredMachineType: "assembler"
  tags: [upgrade, machine, speed, power_hungry, late_game]

- upgradeId: reinforced_barrel
  displayName: "Reinforced barrel"
  description: "Replaces the barrel on a kinetic weapon with a heat-treated steel variant. Increases weapon durability by 50% (reduces degradation rate). Does not affect damage or fire rate. One per weapon."
  slopCommentary: "SLOP's Armament Longevity Program extends the service life of your Personal Security Instrument. A well-maintained weapon is a productive weapon. A broken weapon is a conversation starter with an unauthorized biological occupant."
  targetType: weapon
  targetId: null
  effect: "durability"
  magnitude: 0.5
  tier: 2
  researchRequired: field_medicine
  craftingRecipe:
    inputs:
      - itemId: steel_ingot
        count: 3
      - itemId: iron_plate
        count: 2
    craftDuration: 8.0
    requiredMachineType: "assembler"
  tags: [upgrade, weapon, durability, steel_dependent, mid_game]

- upgradeId: extended_magazine
  displayName: "Extended magazine"
  description: "Increases ammo capacity on a turret by doubling ammoMaxStackSize from 64 to 128 per slot. Turrets reload less often, sustaining fire longer during wave events. One per turret."
  slopCommentary: "The Expanded Cartridge Retention Module allows your Automated Perimeter Compliance Officer to file more complaints before needing a refill. Efficiency meets enthusiasm."
  targetType: turret
  targetId: null
  effect: "ammoMaxStackSize"
  magnitude: 64.0
  tier: 3
  researchRequired: advanced_defense
  craftingRecipe:
    inputs:
      - itemId: steel_ingot
        count: 2
      - itemId: mechanical_component
        count: 3
      - itemId: iron_plate
        count: 4
    craftDuration: 8.0
    requiredMachineType: "assembler"
  tags: [upgrade, turret, ammo_capacity, defense, late_game]

- upgradeId: turret_tracking_upgrade
  displayName: "Turret tracking upgrade"
  description: "Replaces the turret's targeting servo with a faster motor, reducing the time to acquire and switch targets. Decreases fireInterval by 20% (multiplicative). Stacks with overclock if the turret has both slots. One per turret."
  slopCommentary: "The Enhanced Target Acquisition System allows your turret to identify and address unauthorized visitors with greater promptness. First impressions matter, especially at 600 rounds per minute."
  targetType: turret
  targetId: null
  effect: "fireInterval"
  magnitude: -0.2
  tier: 3
  researchRequired: advanced_defense
  craftingRecipe:
    inputs:
      - itemId: circuit_board
        count: 4
      - itemId: mechanical_component
        count: 2
      - itemId: copper_ingot
        count: 3
    craftDuration: 12.0
    requiredMachineType: "assembler"
  tags: [upgrade, turret, fire_rate, tracking, electronics_dependent, late_game]

- upgradeId: supply_line_armor
  displayName: "Supply line armor"
  description: "Reinforces a supply line connection between buildings with steel plating. Increases supply line health by 100% (doubled). Supply lines under attack during wave events are the most common point of failure in distributed factory layouts."
  slopCommentary: "The Logistics Corridor Protection Package ensures your inter-facility material transfers proceed without interruption from environmental factors, unauthorized biological occupants, or budget cuts."
  targetType: supply_line
  targetId: null
  effect: "health"
  magnitude: 1.0
  tier: 3
  researchRequired: factory_efficiency
  craftingRecipe:
    inputs:
      - itemId: steel_ingot
        count: 6
      - itemId: iron_plate
        count: 8
    craftDuration: 15.0
    requiredMachineType: "assembler"
  tags: [upgrade, supply_line, durability, infrastructure, late_game]

- upgradeId: insulated_casing
  displayName: "Insulated casing"
  description: "Wraps a machine in thermal insulation, reducing heat buildup. Allows the machine to run an overclock module without the 50% power penalty -- power consumption increase is reduced to 15%. Must be installed before or alongside the overclock module."
  slopCommentary: "The Thermal Management Solution keeps your overclocked equipment within acceptable temperature ranges. SLOP defines 'acceptable' as 'not currently on fire.' Smoldering is a gray area."
  targetType: machine
  targetId: null
  effect: "powerConsumption"
  magnitude: -0.35
  tier: 3
  researchRequired: factory_efficiency
  craftingRecipe:
    inputs:
      - itemId: chemical_compound
        count: 4
      - itemId: iron_plate
        count: 6
      - itemId: cloth
        count: 4
    craftDuration: 10.0
    requiredMachineType: "assembler"
  tags: [upgrade, machine, power_efficiency, thermal, late_game, overclock_companion]
```
