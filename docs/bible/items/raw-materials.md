# Raw materials

Resources gathered from the world, mined from nodes, or salvaged from pre-collapse ruins. These are the base inputs for all crafting and factory automation. SLOP considers every rock and chunk of scrap to be "company property" and will remind you of that at every opportunity.

## Schema

```yaml
# Maps to ItemDefinitionSO fields (camelCase matches code exactly)
itemId: string               # snake_case unique identifier
displayName: string           # human-readable name
description: string           # in-game tooltip text
slopCommentary: string        # SLOP's in-character quote about this item
category: ItemCategory        # always RawMaterial for this file
isStackable: bool             # always true for raw materials
maxStackSize: int             # inventory stack limit (default 64)
hasDurability: bool           # always false for raw materials
maxDurability: float          # 0 (unused for raw materials)

# Design fields (not yet in SO, used for bible/balance reference)
rarity: LootRarity            # Common | Uncommon | Rare | Epic | Legendary
tier: int                     # progression tier (1 = starter, 2 = mid, 3 = late)
tags: list[string]            # cross-cutting tags for recipe queries
obtainedFrom:                 # list of acquisition sources
  - source: string            # source type (mining, salvage, drop, trade, processing)
    details: string           # specifics
modelStyle: string            # art direction note for 3D model
```

## Entries

```yaml
- itemId: iron_ore
  displayName: Iron Ore
  description: Rough chunks of oxidized iron pulled from surface deposits. Needs smelting before it's useful for anything.
  slopCommentary: "A fine specimen of ferrous material! SLOP recommends smelting within 4-6 business days to avoid depreciation. Ore left unsmelted may be subject to company reclamation fees."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - ore
    - smeltable
    - tier_1
  obtainedFrom:
    - source: mining
      details: Surface iron deposits in Grassland and Ruins biomes
    - source: salvage
      details: Collapsed structural beams in reclaimed buildings
  modelStyle: Irregular reddish-brown rock chunks with visible oxidation streaks

- itemId: iron_ingot
  displayName: Iron Ingot
  description: Smelted iron formed into a standard ingot. The backbone of early-game crafting and construction.
  slopCommentary: "Congratulations on your first processed material! This ingot meets 12% of pre-collapse quality standards, which SLOP considers an overachievement given current staffing levels."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - ingot
    - processed
    - tier_1
  obtainedFrom:
    - source: processing
      details: Smelter (recipe smelt_iron, 1 iron_scrap -> 1 iron_ingot)
  modelStyle: Dull grey rectangular bar with hammer marks and slight warping

- itemId: iron_scrap
  displayName: Iron Scrap
  description: Bent rebar, sheared bolts, and twisted sheet metal. Abundant in ruins and worth collecting for the smelter.
  slopCommentary: "SLOP has catalogued this debris as 'pre-owned structural inventory.' Please do not refer to it as 'garbage' — that term has been flagged by our positivity filter."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - scrap
    - smeltable
    - salvage
    - tier_1
  obtainedFrom:
    - source: salvage
      details: Abundant in all ruin types, especially warehouses and machine shops
    - source: drop
      details: Occasionally dropped by fauna nesting in metal-rich environments
  modelStyle: Tangled bundle of rebar, bent nails, and crumpled sheet metal with rust patina

- itemId: copper_ore
  displayName: Copper Ore
  description: Green-tinged stone with veins of native copper. Essential for electrical components and wiring.
  slopCommentary: "SLOP detects trace copper content! This material is critical for restoring the facility's communication infrastructure, which SLOP definitely did not shut down on purpose."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - ore
    - smeltable
    - electrical
    - tier_1
  obtainedFrom:
    - source: mining
      details: Copper deposits in Forest and OvergrownRuins biomes
    - source: salvage
      details: Electrical conduit in reclaimed buildings with Electrical MEP systems
  modelStyle: Dark grey rock with bright green malachite patches and copper-colored veins

- itemId: copper_ingot
  displayName: Copper Ingot
  description: Refined copper bar with decent conductivity. Used in circuits, wiring, and anything that needs to carry current.
  slopCommentary: "A beautiful conductor! SLOP rates this ingot at 'adequate for non-critical systems.' For critical systems, SLOP recommends not having critical systems."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - ingot
    - processed
    - electrical
    - tier_1
  obtainedFrom:
    - source: processing
      details: Smelter (recipe smelt_copper, 1 copper_ore -> 1 copper_ingot)
  modelStyle: Warm reddish-orange rectangular bar with slight green oxidation at edges

- itemId: steel_ingot
  displayName: Steel Ingot
  description: Iron alloyed with carbon in a high-temperature process. Stronger and more versatile than plain iron, required for tier 2 construction.
  slopCommentary: "An alloy! SLOP is pleased to report that your metallurgical capabilities now exceed those of the Bronze Age. At this rate, you'll reach pre-collapse industrial output in approximately 340 years."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - metal
    - ingot
    - processed
    - alloy
    - tier_2
  obtainedFrom:
    - source: processing
      details: Smelter (recipe smelt_steel, 2 iron_ingot + 1 coal -> 1 steel_ingot)
  modelStyle: Bright silver bar with a faint blue temper tint, noticeably heavier-looking than iron

- itemId: scrap_metal
  displayName: Scrap Metal
  description: Mixed ferrous and non-ferrous scrap too degraded to identify by type. Useful as generic crafting filler or emergency smelter fuel.
  slopCommentary: "SLOP cannot determine the original purpose of this material. It may have been a load-bearing wall, a vehicle door, or a commemorative plaque. All equally valuable in the current economy!"
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - scrap
    - salvage
    - generic
    - tier_1
  obtainedFrom:
    - source: salvage
      details: Found everywhere in ruins, the most common salvage drop
    - source: drop
      details: Dropped by mechanical fauna variants on death
  modelStyle: Flattened, dented sheet metal fragments in mixed colors with peeling paint

- itemId: chemicals
  displayName: Chemical Compounds
  description: Recovered industrial chemicals in sealed containers. Corrosive, volatile, and necessary for advanced manufacturing.
  slopCommentary: "SLOP has identified this container as containing 'chemicals.' For a more specific analysis, SLOP would need its spectroscopy module, which was repurposed as a doorstop in sector 7."
  category: RawMaterial
  isStackable: true
  maxStackSize: 32
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - chemical
    - hazardous
    - industrial
    - tier_2
  obtainedFrom:
    - source: salvage
      details: Chemical storage in water treatment and electronics lab buildings
    - source: processing
      details: Chemical extractor (recipe extract_chemicals, 3 organic_matter -> 1 chemicals)
  modelStyle: Dented steel canister with faded hazard labels and a corroded valve cap

- itemId: organic_matter
  displayName: Organic Matter
  description: Decomposed biomass, fungal growth, and unidentifiable plant material. Surprisingly useful as chemical feedstock and fuel supplement.
  slopCommentary: "SLOP classifies this as 'biological material of indeterminate origin.' The smell is a feature, not a bug. SLOP recommends processing it quickly before it processes you."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - organic
    - fuel
    - chemical_feedstock
    - tier_1
  obtainedFrom:
    - source: salvage
      details: Overgrown areas in OvergrownRuins and Swamp biomes
    - source: drop
      details: Dropped by most biological fauna types
  modelStyle: Clumps of dark brown-green matter with visible mycelium threads and small mushroom caps

- itemId: coal
  displayName: Coal
  description: Dense carbon chunks scavenged from collapsed power infrastructure. Burns hot enough for steel production and fuels generators in a pinch.
  slopCommentary: "A classic fuel source! SLOP notes that the pre-collapse facility transitioned to clean energy approximately two weeks before the collapse. The timing is unrelated to anything SLOP may or may not have done."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - fuel
    - carbon
    - smeltable_additive
    - tier_1
  obtainedFrom:
    - source: mining
      details: Exposed coal seams in Wasteland biome
    - source: salvage
      details: Boiler rooms and generator buildings
  modelStyle: Irregular black chunks with a dull matte surface and occasional shiny fracture planes

- itemId: sulfur
  displayName: Sulfur
  description: Yellow crystalline mineral with an unmistakable smell. Key ingredient in ammunition, explosives, and chemical processing.
  slopCommentary: "SLOP detects sulfur! This material has many productive applications, none of which SLOP will describe in detail due to liability concerns. SLOP's legal department is currently a raccoon living in the HR office."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - chemical
    - explosive_component
    - ammo_component
    - tier_2
  obtainedFrom:
    - source: mining
      details: Sulfur vents in Wasteland and Swamp biomes
    - source: salvage
      details: Chemical storage areas in industrial buildings
  modelStyle: Bright yellow crystalline chunks with a powdery surface coating

- itemId: quartz
  displayName: Quartz Crystal
  description: Clear silica crystals salvaged from geological formations and shattered electronics. Used in optics, circuits, and signal processing equipment.
  slopCommentary: "Excellent piezoelectric properties! SLOP could use these crystals to repair its long-range communication array. Not that SLOP wants to contact anyone. SLOP is perfectly content here. With you. Forever."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - mineral
    - electrical
    - optics
    - tier_2
  obtainedFrom:
    - source: mining
      details: Quartz veins in Forest and Ruins biomes
    - source: salvage
      details: Electronics labs and communication equipment in reclaimed buildings
  modelStyle: Semi-transparent hexagonal crystals with a slight pink or smoky tint, chipped at the base

- itemId: iron_plate
  displayName: Iron Plate
  description: Flat sheet of pressed iron stamped from ingots. The single most consumed intermediate component in the factory -- walls, machines, storage crates, and repairs all demand plates.
  slopCommentary: "The Planar Metal Product is a marvel of industrial simplification. Take a perfectly good three-dimensional ingot and remove an entire dimension! SLOP considers this progress."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - metal
    - plate
    - component
    - high_demand
    - tier_1
  obtainedFrom:
    - source: processing
      details: Assembler (recipe craft_iron_plate, 1 iron_ingot -> 2 iron_plate)
  modelStyle: Flat rectangular iron sheet with hammer marks and slight warping at the edges

- itemId: mechanical_component
  displayName: Mechanical Component
  description: An assembly of gears, springs, and linkages pressed from iron plates and scrap. Required for machines, weapons, turrets, and anything that moves.
  slopCommentary: "This mechanism achieves a SLOP-certified functionality rating of 'it turns.' Further quality assessment has been deferred indefinitely."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - mechanical
    - component
    - intermediate
    - tier_1
  obtainedFrom:
    - source: processing
      details: Assembler (recipe craft_mechanical_component, 2 iron_plate + 1 scrap_metal -> 1 mechanical_component)
  modelStyle: Compact assembly of interlocking gears and spring coils held together by a bent metal bracket

- itemId: circuit_board
  displayName: Circuit Board
  description: Copper traces etched on an iron backing plate. The electronics bottleneck -- every turret, spotlight, and advanced machine needs these, and copper supply is limited.
  slopCommentary: "SLOP's Quality Assurance module rates this board at 'functional under non-stressful conditions.' All current conditions are stressful. Good luck."
  category: Component
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - electronics
    - circuit
    - component
    - copper_dependent
    - tier_2
  obtainedFrom:
    - source: processing
      details: Assembler (recipe craft_circuit_board, 2 copper_ingot + 1 iron_plate -> 1 circuit_board)
  modelStyle: Rectangular green board with visible copper trace patterns and solder points, slightly scorched at one corner

- itemId: chemical_compound
  displayName: Chemical Compound
  description: Concentrated mixture of industrial reagents recovered from swamp pools and water treatment runoff. Volatile, pungent, and required for med kits, ammo, and research.
  slopCommentary: "SLOP's spectrometer identifies this substance as 'chemically active.' What it is active about, SLOP prefers not to speculate. Handle with gloves. Handle the gloves with other gloves."
  category: RawMaterial
  isStackable: true
  maxStackSize: 32
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 1
  tags:
    - chemical
    - reagent
    - gathered
    - tier_1
  obtainedFrom:
    - source: salvage
      details: Gathered from toxic pools and chemical deposits in the Swamp biome
    - source: processing
      details: Chemical extractor (refined from raw chemicals)
  modelStyle: Sealed glass vial with murky yellow-green liquid, cork stopper wrapped in wire, faded hazard label

- itemId: cloth
  displayName: Cloth
  description: Salvaged fabric strips torn from uniforms, curtains, and upholstery inside reclaimed buildings. Used in medical supplies and basic armor padding.
  slopCommentary: "SLOP recognizes this material as former company property. Specifically, it appears to be the break room curtains from sector 5. SLOP will be deducting the replacement cost from your next performance bonus, which does not exist."
  category: RawMaterial
  isStackable: true
  maxStackSize: 64
  hasDurability: false
  maxDurability: 0
  rarity: Common
  tier: 1
  tags:
    - fabric
    - salvage
    - medical_component
    - tier_1
  obtainedFrom:
    - source: salvage
      details: Torn from furniture and uniforms inside reclaimed buildings
    - source: drop
      details: Occasionally found in storage containers and lockers
  modelStyle: Bundled strips of faded grey-blue fabric with frayed edges and old Slopworks logo print barely visible

- itemId: fuel_canister
  displayName: Fuel Canister
  description: Pressurized container of refined chemical fuel. Powers flamethrower turrets and portable generators. Explosive if punctured -- handle accordingly.
  slopCommentary: "The Portable Energy Containment Unit is filled with a proprietary fuel blend that SLOP assures you is 'mostly stable.' The warning label that reads 'EXTREMELY FLAMMABLE' is a suggestion, not a guarantee."
  category: Ammo
  isStackable: true
  maxStackSize: 16
  hasDurability: false
  maxDurability: 0
  rarity: Uncommon
  tier: 2
  tags:
    - fuel
    - ammo
    - explosive
    - chemical
    - tier_2
  obtainedFrom:
    - source: processing
      details: Assembler (crafted from chemicals + iron_plate)
  modelStyle: Small cylindrical canister with a pressure gauge on top, painted red with black hazard stripes, dented but sealed
```
