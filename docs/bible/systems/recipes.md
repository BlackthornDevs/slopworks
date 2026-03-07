# Recipes

Crafting and processing recipes that define how items transform into other items. Each recipe specifies inputs, outputs, duration, and which machine type can run it. Maps to `RecipeSO` in code. The recipe system is the backbone of factory automation -- every production chain is a sequence of recipes feeding into each other.

## Schema

```yaml
# Core fields (match RecipeSO)
recipeId: string                          # unique snake_case identifier
displayName: string                       # player-facing name
inputs:                                   # list of RecipeIngredient
  - itemId: string                        # item ID consumed
    count: int                            # quantity consumed per craft
outputs:                                  # list of RecipeIngredient
  - itemId: string                        # item ID produced
    count: int                            # quantity produced per craft
craftDuration: float                      # seconds to complete one craft cycle
requiredMachineType: string | null        # machine type string, null = hand-craftable

# Design fields (bible-only)
slopCommentary: string                    # in-character SLOP quote
description: string                       # design description
tier: int                                 # tech tier (1 = starter, 2 = mid, 3 = late)
tags: list                                # lowercase string tags
researchRequired: string | null           # research node ID, null = known from start
discoveredByDefault: bool                 # true = available without research
recipeCategory: enum [smelting, crafting, construction, assembly, chemical]
powerRequired: bool                       # whether the machine needs power to run this
byproducts:                               # optional secondary outputs
  - itemId: string
    count: int
```

## Entries

```yaml
# --- Tier 1: smelting ---

- recipeId: smelt_iron
  displayName: "Smelt iron"
  inputs:
    - itemId: iron_ore
      count: 2
  outputs:
    - itemId: iron_ingot
      count: 1
  craftDuration: 4.0
  requiredMachineType: "smelter"
  slopCommentary: "The Thermal Mineral Refinement Process converts raw geological samples into standardized metal units. SLOP reminds you that ingots are company property until signed out on form 7-B."
  description: "The most basic smelting recipe. Two iron ore in, one iron ingot out. Available from game start with no research. This is the first link in every production chain."
  tier: 1
  tags: [smelting, iron, starter, basic, ore_processing]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: smelting
  powerRequired: true
  byproducts: []

- recipeId: smelt_copper
  displayName: "Smelt copper"
  inputs:
    - itemId: copper_ore
      count: 2
  outputs:
    - itemId: copper_ingot
      count: 1
  craftDuration: 3.5
  requiredMachineType: "smelter"
  slopCommentary: "Copper: the metal that made civilization possible. SLOP is confident it can do the same for your little operation here. Results may vary."
  description: "Smelts copper ore into copper ingots. Slightly faster than iron smelting. Copper is used in circuit boards and electrical components, making it critical for mid-game electronics."
  tier: 1
  tags: [smelting, copper, starter, ore_processing, electronics_chain]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: smelting
  powerRequired: true
  byproducts: []

- recipeId: smelt_steel
  displayName: "Smelt steel"
  inputs:
    - itemId: iron_ingot
      count: 2
    - itemId: coal
      count: 1
  outputs:
    - itemId: steel_ingot
      count: 1
  craftDuration: 8.0
  requiredMachineType: "smelter"
  slopCommentary: "The Advanced Alloy Synthesis Program combines iron with carbon-enriched fuel to produce a superior material. SLOP metallurgists describe the process as 'hot and loud.' Further details are classified."
  description: "Second-tier smelting recipe. Combines iron ingots with coal to produce steel. Longer craft time and requires already-processed iron, making it a second-step recipe. Steel is needed for reinforced walls, advanced machines, and weapons."
  tier: 2
  tags: [smelting, steel, advanced, alloy, mid_game]
  researchRequired: advanced_metallurgy
  discoveredByDefault: false
  recipeCategory: smelting
  powerRequired: true
  byproducts: []

# --- Tier 1: crafting ---

- recipeId: craft_iron_plate
  displayName: "Craft iron plate"
  inputs:
    - itemId: iron_ingot
      count: 1
  outputs:
    - itemId: iron_plate
      count: 2
  craftDuration: 2.0
  requiredMachineType: "assembler"
  slopCommentary: "The Planar Metal Formation Process flattens perfectly good ingots into sheets. Progress requires sacrifice, and in this case the sacrifice is thickness."
  description: "Presses an iron ingot into two iron plates. Plates are the most common intermediate component -- used in walls, machines, storage crates, and repairs. High throughput recipe, keep assemblers stocked."
  tier: 1
  tags: [crafting, iron, plate, component, high_demand, starter]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: crafting
  powerRequired: true
  byproducts: []

- recipeId: craft_mechanical_component
  displayName: "Craft mechanical component"
  inputs:
    - itemId: iron_plate
      count: 2
    - itemId: scrap_metal
      count: 1
  outputs:
    - itemId: mechanical_component
      count: 1
  craftDuration: 5.0
  requiredMachineType: "assembler"
  slopCommentary: "The Precision Apparatus Assembly combines flat metal with recycled material to create a functional mechanism. SLOP quality assurance rates these components as 'probably fine.'"
  description: "Assembles iron plates and scrap into a mechanical component. Used in machines, weapons, turrets, and vehicles. Mid-tier intermediate that gates access to most advanced buildables."
  tier: 2
  tags: [crafting, mechanical, component, intermediate, mid_game]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: assembly
  powerRequired: true
  byproducts: []

- recipeId: craft_circuit_board
  displayName: "Craft circuit board"
  inputs:
    - itemId: copper_ingot
      count: 2
    - itemId: iron_plate
      count: 1
  outputs:
    - itemId: circuit_board
      count: 1
  craftDuration: 6.0
  requiredMachineType: "assembler"
  slopCommentary: "The Electronic Logic Substrate is assembled from copper traces on a ferrous backing. SLOP engineers assure us it meets all pre-collapse safety standards, which no longer apply."
  description: "Combines copper ingots and an iron plate into a circuit board. Required for turrets, spotlights, advanced machines, and the research bench. The electronics bottleneck -- copper supply limits circuit board production."
  tier: 2
  tags: [crafting, electronics, circuit, component, copper_dependent, mid_game]
  researchRequired: basic_electronics
  discoveredByDefault: false
  recipeCategory: assembly
  powerRequired: true
  byproducts: []

# --- Tier 1: consumables ---

- recipeId: craft_med_kit
  displayName: "Craft med kit"
  inputs:
    - itemId: cloth
      count: 2
    - itemId: chemical_compound
      count: 1
  outputs:
    - itemId: med_kit
      count: 1
  craftDuration: 4.0
  requiredMachineType: "assembler"
  slopCommentary: "The Personal Wellness Restoration Package contains everything you need to address minor injuries. For major injuries, SLOP recommends filing an incident report and hoping for the best."
  description: "Assembles cloth and chemical compound into a med kit. Heals a significant amount of HP when used. Keep a steady supply flowing -- wave events burn through med kits fast."
  tier: 1
  tags: [crafting, consumable, healing, med_kit, survival]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: chemical
  powerRequired: true
  byproducts: []

# --- Tier 1: ammo ---

- recipeId: craft_turret_ammo
  displayName: "Craft turret ammo"
  inputs:
    - itemId: iron_ingot
      count: 1
    - itemId: chemical_compound
      count: 1
  outputs:
    - itemId: turret_ammo
      count: 16
  craftDuration: 3.0
  requiredMachineType: "assembler"
  slopCommentary: "The Kinetic Compliance Cartridge is manufactured to SLOP ballistic standards. Each round carries a small note reading 'please vacate the premises' for legal purposes."
  description: "Produces a stack of 16 turret ammo rounds from iron and chemical compound. Turrets consume ammo fast during wave events. Dedicate at least one assembler to continuous ammo production and belt it directly to turret input ports."
  tier: 1
  tags: [crafting, ammo, turret, defense, high_demand]
  researchRequired: defensive_systems
  discoveredByDefault: false
  recipeCategory: crafting
  powerRequired: true
  byproducts: []

# --- Tier 1: construction ---

- recipeId: craft_foundation
  displayName: "Craft foundation"
  inputs:
    - itemId: scrap_metal
      count: 4
    - itemId: iron_plate
      count: 2
  outputs:
    - itemId: foundation_item
      count: 1
  craftDuration: 3.0
  requiredMachineType: null
  slopCommentary: "The Workspace Allocation Unit can be assembled by hand using salvaged materials. SLOP recommends placing it on level ground, but acknowledges that level ground is a luxury."
  description: "Hand-craftable recipe for a 1x1 foundation slab. No machine required -- players can craft this from the inventory screen. The entry point for all base construction."
  tier: 1
  tags: [construction, foundation, hand_craft, structural, starter]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: construction
  powerRequired: false
  byproducts: []

- recipeId: craft_wall
  displayName: "Craft wall"
  inputs:
    - itemId: iron_plate
      count: 2
  outputs:
    - itemId: wall_item
      count: 1
  craftDuration: 2.0
  requiredMachineType: null
  slopCommentary: "Every great civilization was built on walls. SLOP is confident yours will be no exception. Please ensure wall placement does not block designated emergency exits that no longer exist."
  description: "Hand-craftable wall section. Place on foundation edges to enclose the factory. Cheap and fast -- two iron plates per wall. The first line of defense before turrets come online."
  tier: 1
  tags: [construction, wall, hand_craft, structural, defense, starter]
  researchRequired: null
  discoveredByDefault: true
  recipeCategory: construction
  powerRequired: false
  byproducts: []

- recipeId: craft_rifle
  displayName: "Craft rifle"
  inputs:
    - itemId: steel_ingot
      count: 3
    - itemId: mechanical_component
      count: 2
    - itemId: iron_plate
      count: 4
  outputs:
    - itemId: rifle
      count: 1
  craftDuration: 12.0
  requiredMachineType: "assembler"
  slopCommentary: "The SLOP Personal Security Instrument is issued to employees who have completed Workplace Defense Training Module 9. Training Module 9 has been indefinitely postponed. Here's your rifle anyway."
  description: "Produces a kinetic rifle -- the primary ranged weapon for players. Expensive in both materials and time. Steel ingots mean this requires the advanced metallurgy research chain. One rifle per craft."
  tier: 2
  tags: [crafting, weapon, rifle, combat, steel_dependent, mid_game]
  researchRequired: weapons_research
  discoveredByDefault: false
  recipeCategory: assembly
  powerRequired: true
  byproducts: []
```
