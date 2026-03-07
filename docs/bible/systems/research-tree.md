# Research tree

Tech progression nodes that gate access to recipes, machines, weapons, and upgrades. Players spend materials and time at a research bench to unlock nodes. The tree forms a directed acyclic graph where later nodes require earlier ones as prerequisites. No existing SO -- these definitions will drive a future `ResearchNodeSO`.

## Schema

```yaml
nodeId: string                            # unique snake_case identifier
displayName: string                       # player-facing name
description: string                       # design description
slopCommentary: string                    # in-character SLOP quote
prerequisites: list                       # nodeId strings, empty for root nodes
cost:                                     # materials consumed to begin research
  - itemId: string
    count: int
researchTime: float                       # seconds to complete
unlocks:                                  # what completing this node grants
  - type: enum [recipe, machine, weapon, building, upgrade]
    targetId: string                      # ID of the unlocked entry
tier: int                                 # tech tier (1 = early, 2 = mid, 3 = late)
tags: list                                # lowercase string tags
```

## Entries

```yaml
# --- Tier 1: root nodes (no prerequisites) ---

- nodeId: basic_smelting
  displayName: "Basic smelting"
  description: "Unlocks the smelter machine and base ore-to-ingot recipes. The foundation of all material processing. Without this, raw ore is just heavy gravel."
  slopCommentary: "Congratulations on discovering that heat changes things. SLOP's Research Division filed this patent centuries ago, but we're generously sharing the knowledge with you. You're welcome."
  prerequisites: []
  cost:
    - itemId: iron_ore
      count: 20
    - itemId: scrap_metal
      count: 10
  researchTime: 60.0
  unlocks:
    - type: machine
      targetId: smelter_t1
    - type: recipe
      targetId: smelt_iron
    - type: recipe
      targetId: smelt_copper
  tier: 1
  tags: [research, smelting, starter, production, root]

- nodeId: basic_electronics
  displayName: "Basic electronics"
  description: "Unlocks circuit board crafting. Opens the path to turrets, spotlights, and advanced machinery that require electronic components."
  slopCommentary: "The flow of electrons through copper traces is one of nature's great miracles. SLOP has harnessed this miracle and packaged it into a convenient research module. Please do not attempt to harness miracles without SLOP approval."
  prerequisites: []
  cost:
    - itemId: copper_ore
      count: 20
    - itemId: iron_plate
      count: 10
  researchTime: 90.0
  unlocks:
    - type: recipe
      targetId: craft_circuit_board
  tier: 1
  tags: [research, electronics, copper, starter, root]

# --- Tier 2: mid-game branches ---

- nodeId: advanced_metallurgy
  displayName: "Advanced metallurgy"
  description: "Unlocks steel smelting and steel-dependent recipes. Steel is required for reinforced structures, advanced weapons, and tier 2 machines. The single most important mid-game unlock."
  slopCommentary: "By combining iron with carbon under controlled conditions, you can produce a material that is stronger, harder, and more suitable for keeping unauthorized biological occupants at a distance. SLOP approves of distance."
  prerequisites:
    - basic_smelting
  cost:
    - itemId: iron_ingot
      count: 30
    - itemId: coal
      count: 20
  researchTime: 180.0
  unlocks:
    - type: recipe
      targetId: smelt_steel
    - type: building
      targetId: reinforced_wall
  tier: 2
  tags: [research, metallurgy, steel, mid_game, production]

- nodeId: weapons_research
  displayName: "Weapons research"
  description: "Unlocks the rifle, flamethrower turret, and landmine recipes. Requires basic electronics because modern weapons need fire control circuits. Opens the offensive branch of the tech tree."
  slopCommentary: "SLOP's Workplace Security Division has declassified several personnel defense blueprints for your use. These designs were originally intended for competitive corporate negotiations. They work equally well on fauna."
  prerequisites:
    - basic_electronics
  cost:
    - itemId: iron_ingot
      count: 20
    - itemId: circuit_board
      count: 5
    - itemId: chemical_compound
      count: 10
  researchTime: 240.0
  unlocks:
    - type: weapon
      targetId: rifle
    - type: recipe
      targetId: craft_rifle
    - type: building
      targetId: flamethrower_turret_t1
    - type: recipe
      targetId: craft_landmine
  tier: 2
  tags: [research, weapons, combat, offensive, mid_game]

- nodeId: defensive_systems
  displayName: "Defensive systems"
  description: "Unlocks the auto-turret, turret ammo recipe, and spotlight. The core defensive research node. Requires basic electronics for turret targeting circuits."
  slopCommentary: "The best offense is a good defense. The best defense is an automated system that doesn't require bathroom breaks, health insurance, or motivational posters. SLOP presents: turrets."
  prerequisites:
    - basic_electronics
  cost:
    - itemId: iron_plate
      count: 20
    - itemId: circuit_board
      count: 5
    - itemId: mechanical_component
      count: 5
  researchTime: 200.0
  unlocks:
    - type: building
      targetId: auto_turret_t1
    - type: recipe
      targetId: craft_turret_ammo
    - type: building
      targetId: spotlight
  tier: 2
  tags: [research, defense, turret, automated, mid_game]

# --- Tier 3: late-game specialization ---

- nodeId: factory_efficiency
  displayName: "Factory efficiency"
  description: "Unlocks machine overclock and supply line upgrades. Requires both advanced metallurgy (for durable components) and basic electronics (for control circuits). The production optimization endgame."
  slopCommentary: "SLOP productivity consultants have identified several opportunities to extract more output from your existing infrastructure. The consultants are no longer available, but their findings live on in this research module."
  prerequisites:
    - advanced_metallurgy
    - basic_electronics
  cost:
    - itemId: steel_ingot
      count: 20
    - itemId: circuit_board
      count: 10
    - itemId: mechanical_component
      count: 10
  researchTime: 360.0
  unlocks:
    - type: upgrade
      targetId: overclock_module
    - type: upgrade
      targetId: supply_line_armor
  tier: 3
  tags: [research, efficiency, optimization, production, late_game]

- nodeId: advanced_defense
  displayName: "Advanced defense"
  description: "Unlocks turret tracking upgrades and tier 2 turret variants. Requires both defensive systems and weapons research -- full mastery of both offense and defense converges here."
  slopCommentary: "Your perimeter compliance systems have been performing adequately, but 'adequate' is not a word that appears in SLOP's mission statement. This module upgrades your defenses from 'adequate' to 'enthusiastic.'"
  prerequisites:
    - defensive_systems
    - weapons_research
  cost:
    - itemId: steel_ingot
      count: 15
    - itemId: circuit_board
      count: 10
    - itemId: chemical_compound
      count: 10
  researchTime: 300.0
  unlocks:
    - type: upgrade
      targetId: turret_tracking_upgrade
    - type: upgrade
      targetId: extended_magazine
  tier: 3
  tags: [research, defense, advanced, turret_upgrade, late_game]

- nodeId: field_medicine
  displayName: "Field medicine"
  description: "Unlocks the regeneration consumable recipe and improved med kit variants. A survival-focused branch for players who take more hits than their turrets can prevent."
  slopCommentary: "SLOP's Occupational Health Division has released an updated first aid protocol. Step one: stop bleeding. Step two: resume working. Steps three through seven have been redacted for morale purposes."
  prerequisites:
    - basic_smelting
  cost:
    - itemId: chemical_compound
      count: 15
    - itemId: cloth
      count: 20
  researchTime: 120.0
  unlocks:
    - type: recipe
      targetId: craft_med_kit
    - type: upgrade
      targetId: reinforced_barrel
  tier: 2
  tags: [research, medical, survival, consumable, healing]

# --- Logistics research chain ---

- nodeId: research_reinforced_logistics
  displayName: "Reinforced logistics"
  description: "Unlocks the reinforced supply line, which adds steel plating and fauna deterrence to overworld transport routes. Doubles throughput over basic lines and reduces cargo loss."
  slopCommentary: "SLOP has identified that your supply lines are experiencing an unacceptable level of unauthorized biological interference. This research module adds metal plating to your logistics infrastructure, which SLOP considers the minimum acceptable standard for cargo transport. The previous standard was 'hope.'"
  prerequisites:
    - advanced_metallurgy
  cost:
    - itemId: steel_ingot
      count: 10
    - itemId: iron_plate
      count: 15
    - itemId: mechanical_component
      count: 5
  researchTime: 180.0
  unlocks:
    - type: upgrade
      targetId: reinforced_supply_line
  tier: 2
  tags: [research, logistics, supply_line, mid_game, transport]

- nodeId: research_express_logistics
  displayName: "Express logistics"
  description: "Unlocks the motorized express supply line. Requires power cells to operate but achieves the highest surface throughput rate. For players running multiple production buildings into a single hub."
  slopCommentary: "Motorized transport! SLOP is experiencing a 12% increase in operational optimism. This system moves cargo at speeds approaching 'acceptable,' which is the highest rating SLOP's logistics division has ever awarded to a post-collapse operation."
  prerequisites:
    - research_reinforced_logistics
    - factory_efficiency
  cost:
    - itemId: steel_ingot
      count: 15
    - itemId: circuit_board
      count: 8
    - itemId: power_cell
      count: 2
  researchTime: 300.0
  unlocks:
    - type: upgrade
      targetId: express_supply_line
  tier: 3
  tags: [research, logistics, supply_line, late_game, motorized, power_dependent]

- nodeId: research_underground_logistics
  displayName: "Underground logistics"
  description: "Unlocks tunneled supply lines that run beneath the surface, immune to all fauna attacks and environmental hazards. Expensive to build but nothing touches your cargo underground."
  slopCommentary: "Subterranean logistics represent the pinnacle of supply chain philosophy: if they can't see it, they can't eat it. SLOP has been applying this principle to its own data storage for years. The excavation costs are significant, but SLOP assures you that the feeling of invulnerability is worth every ingot."
  prerequisites:
    - research_express_logistics
  cost:
    - itemId: steel_ingot
      count: 20
    - itemId: copper_ingot
      count: 15
    - itemId: sulfur
      count: 10
    - itemId: power_cell
      count: 3
  researchTime: 420.0
  unlocks:
    - type: upgrade
      targetId: underground_supply_line
  tier: 3
  tags: [research, logistics, supply_line, endgame, tunneled, immune_to_surface]
```
