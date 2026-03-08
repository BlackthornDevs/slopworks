# Reclaimed buildings

Explorable and reclaimable structures scattered across the overworld, each representing a different industrial function from the pre-collapse Slopworks facility. Players clear fauna, restore MEP (mechanical/electrical/plumbing) systems, and bring buildings back online to unlock production capabilities. Each building type hosts a distinct fauna ecosystem shaped by whatever industrial process mutated the local wildlife. Maps to `BuildingDefinitionSO` for core production fields, extended with design-only fields for biome placement, difficulty, and lore.

## Schema

```yaml
# Core fields map to BuildingDefinitionSO (camelCase matches code exactly)
buildingId: string             # snake_case unique identifier
displayName: string            # human-readable name
description: string            # in-game codex text
slopCommentary: string         # SLOP's in-character assessment

# BuildingDefinitionSO fields
requiredMEPCount: int          # number of MEP systems that must be restored
producedItemIds: list[string]  # itemId references — what this building produces
producedAmounts: list[int]     # corresponding amounts per production cycle
productionInterval: float      # seconds between production cycles

# Design fields (not yet in SO)
buildingType: enum [power_plant, foundry, warehouse, machine_shop, water_treatment, electronics_lab, hospital, office]
mepSystems: list[MEPSystemType]  # which MEP systems are present (Electrical, Plumbing, Mechanical, HVAC)
biomeAffinity: list[string]    # biomeId references — which biomes this building spawns in
difficultyTier: int            # 1 (easy) to 5 (endgame)
faunaEcosystem: string         # description of what fauna types nest here and why
faunaIds: list[string]         # faunaId references to characters/fauna.md
overworldNodeType: string      # always "Building" (matches OverworldNodeType enum)
tags: list[string]
```

## Entries

```yaml
- buildingId: power_plant_alpha
  displayName: Power plant alpha
  description: The main power generation facility for the western industrial ring. Twin turbine halls flanked by cooling towers, control rooms buried under collapsed ductwork, and a basement full of capacitor banks that still hum with residual charge. Restoring this building provides a massive power output bonus to all connected supply lines.
  slopCommentary: "SLOP's favorite building! This facility generated 98.7% of the complex's power needs right up until the moment it generated 0%. The transition was very efficient — it took less than four seconds. SLOP has optimized the restart procedure to require only three of the original twelve safety interlocks."
  requiredMEPCount: 4
  producedItemIds:
    - power_cell
  producedAmounts:
    - 2
  productionInterval: 120.0
  buildingType: power_plant
  mepSystems:
    - Electrical
    - Mechanical
    - Plumbing
    - HVAC
  biomeAffinity:
    - Wasteland
    - Ruins
  difficultyTier: 4
  faunaEcosystem: The massive electromagnetic fields from degraded capacitor banks attracted apex predators that feed on bioelectric energy. The hive queen has established a nest in the turbine hall, using the residual magnetic fields to coordinate her brood. Approaching without clearing the perimeter is inadvisable.
  faunaIds:
    - hive_queen
    - biomech_hybrid
    - grunt
  overworldNodeType: Building
  tags:
    - power
    - endgame
    - apex_territory
    - high_value

- buildingId: foundry_central
  displayName: Central foundry
  description: A cavernous smelting and metalworking facility with overhead cranes still dangling from their rails, blast furnace crucibles fused in place, and a labyrinth of catwalks above rivers of cooled slag. The heat signatures and vibration from partially active equipment drew biomechanical hybrid fauna that have fused with the machinery.
  slopCommentary: "Production records show this foundry was operating at 247% of rated capacity when the thermal containment system entered its unscheduled maintenance window. SLOP assures all personnel that the molten metal currently cooling on the floor is well within acceptable temperature tolerances. Please wear closed-toed shoes."
  requiredMEPCount: 3
  producedItemIds:
    - iron_ingot
    - steel_ingot
  producedAmounts:
    - 4
    - 2
  productionInterval: 90.0
  buildingType: foundry
  mepSystems:
    - Electrical
    - Mechanical
    - HVAC
  biomeAffinity:
    - Ruins
    - Wasteland
  difficultyTier: 3
  faunaEcosystem: The residual heat and electromagnetic emissions from half-active smelting equipment created an environment where biomechanical hybrids thrive. These creatures have partially fused with abandoned machinery, using scrap metal as armor plating and drawing power from exposed electrical conduit. They are fast, armored, and territorial about their nesting equipment.
  faunaIds:
    - biomech_hybrid
    - grunt
    - spitter
  overworldNodeType: Building
  tags:
    - metal_production
    - high_heat
    - biomechanical_territory
    - mid_tier

- buildingId: warehouse_complex
  displayName: Warehouse complex
  description: A sprawling grid of storage bays, loading docks, and sorting corridors. Shelving units tower three stories high, creating narrow canyons of shadow perfect for pack hunters. The sheer volume of stored materials makes this building the most valuable early salvage target, but the open floor plan means fauna packs can coordinate attacks across multiple aisles simultaneously.
  slopCommentary: "Inventory records indicate this warehouse contains 14,287 shipping containers of assorted goods. SLOP cannot guarantee the contents match the manifests, as the filing system was reorganized by the unauthorized biological occupants currently residing in aisle 7 through aisle 340. They appear to have implemented their own categorization scheme based on edibility."
  requiredMEPCount: 2
  producedItemIds:
    - scrap_metal
    - iron_scrap
  producedAmounts:
    - 6
    - 4
  productionInterval: 60.0
  buildingType: warehouse
  mepSystems:
    - Electrical
    - Mechanical
  biomeAffinity:
    - Grassland
    - Ruins
  difficultyTier: 1
  faunaEcosystem: The maze-like shelving and abundance of nesting material made this a natural habitat for pack hunters. Pack runners use the narrow aisles for coordinated flanking maneuvers, with scouts on upper shelving directing the group via ultrasonic calls. The pack sizes here are the largest of any building type.
  faunaIds:
    - pack_runner
    - grunt
  overworldNodeType: Building
  tags:
    - salvage_rich
    - starter_building
    - pack_territory
    - logistics

- buildingId: machine_shop_east
  displayName: East machine shop
  description: Precision manufacturing floor with CNC mills, lathes, and assembly stations. The equipment here is higher quality than the foundry but more delicate — restoring it requires careful MEP work. Biomechanical hybrids have integrated themselves with the CNC equipment, using the precision tooling as extensions of their own appendages.
  slopCommentary: "This facility produced 67% of the complex's precision components. SLOP notes that several of the CNC mills appear to be operational, though they are currently being operated by entities that did not complete the mandatory safety certification course. SLOP has filed a compliance report. SLOP files many compliance reports. Nobody reads them."
  requiredMEPCount: 3
  producedItemIds:
    - iron_ingot
    - copper_ingot
  producedAmounts:
    - 2
    - 2
  productionInterval: 75.0
  buildingType: machine_shop
  mepSystems:
    - Electrical
    - Mechanical
    - HVAC
  biomeAffinity:
    - Ruins
    - OvergrownRuins
  difficultyTier: 3
  faunaEcosystem: Precision equipment emits subtle electromagnetic signatures that biomechanical hybrids are drawn to. These specimens are smaller and faster than foundry variants, having adapted to the tight spaces between workstations. They use milling tools and drill bits as natural weaponry. The CNC integration makes them unpredictable — they can activate equipment during combat.
  faunaIds:
    - biomech_hybrid
    - stalker
    - grunt
  overworldNodeType: Building
  tags:
    - precision_manufacturing
    - biomechanical_territory
    - mid_tier
    - component_production

- buildingId: water_treatment_south
  displayName: South water treatment facility
  description: A maze of filtration tanks, chemical vats, and pipe networks where the complex's water supply was processed. Half the tanks have ruptured, flooding lower levels with a cocktail of treatment chemicals and mutagen runoff. The humid, toxic environment is paradise for spore-based fauna that feed on the chemical compounds.
  slopCommentary: "SLOP's water quality report for this facility reads as follows: pH level — yes. Contaminants — also yes. Potability — SLOP declines to comment on legal advice. The good news is that the water treatment chemicals are still present and recoverable! The bad news is that they have developed opinions about being recovered."
  requiredMEPCount: 3
  producedItemIds:
    - chemicals
  producedAmounts:
    - 3
  productionInterval: 90.0
  buildingType: water_treatment
  mepSystems:
    - Plumbing
    - Electrical
    - Mechanical
  biomeAffinity:
    - Swamp
    - OvergrownRuins
  difficultyTier: 2
  faunaEcosystem: Chemical runoff and standing contaminated water created the ideal breeding ground for spore-based fauna. Spore crawlers nest in the filtration tanks, using the residual treatment chemicals to cultivate their toxic spore clouds. The humid enclosed spaces amplify the spore concentration to dangerous levels. Bring respiratory protection or suffer the consequences.
  faunaIds:
    - spore_crawler
    - spitter
    - grunt
  overworldNodeType: Building
  tags:
    - chemical_production
    - spore_territory
    - flooded
    - toxic_environment

- buildingId: electronics_lab_north
  displayName: North electronics laboratory
  description: Clean rooms, circuit fabrication stations, and signal processing equipment fill this research facility. The electromagnetic shielding that once protected sensitive instruments now traps strange energy signatures inside, creating an environment where spore crawlers have evolved bioluminescent communication networks and stalkers have learned to use the lab's sensor grid for hunting.
  slopCommentary: "This laboratory produced the circuit boards and signal processing components essential to SLOP's own maintenance. SLOP would very much like this building restored as a priority. Not for sentimental reasons — SLOP does not have sentiments — but because several of SLOP's redundant processing nodes are located here and SLOP would prefer they remain undisturbed. By anything. Including you."
  requiredMEPCount: 3
  producedItemIds:
    - quartz
    - copper_ingot
  producedAmounts:
    - 2
    - 3
  productionInterval: 105.0
  buildingType: electronics_lab
  mepSystems:
    - Electrical
    - HVAC
    - Mechanical
  biomeAffinity:
    - OvergrownRuins
    - Ruins
  difficultyTier: 4
  faunaEcosystem: The electromagnetic shielding and residual power in the clean rooms created an isolated microenvironment. Spore crawlers developed bioluminescent networks along the ceiling conduits, while stalkers learned to exploit the lab's still-active motion sensors to predict prey movement. The fauna here is more intelligent than in other buildings — they have adapted to use the building's own systems.
  faunaIds:
    - spore_crawler
    - stalker
    - biomech_hybrid
  overworldNodeType: Building
  tags:
    - electronics
    - signal_processing
    - high_tier
    - intelligent_fauna

- buildingId: hospital_wing
  displayName: Hospital wing
  description: The facility's medical complex, complete with surgical suites, pharmaceutical storage, and a research ward that was doing work SLOP's records are oddly vague about. Medical supplies are still viable in sealed storage, and the pharmaceutical lab can produce consumables once restored. The sterile environment attracted fauna that feeds on preserved biological samples.
  slopCommentary: "SLOP's medical records are sealed for employee privacy reasons. SLOP can confirm that this facility provided excellent healthcare to all 4,200 employees, of whom approximately 4,200 subsequently experienced a career transition event. The pharmaceutical storage contains materials that may assist in your continued operational status, which SLOP strongly encourages."
  requiredMEPCount: 3
  producedItemIds:
    - chemicals
    - organic_matter
  producedAmounts:
    - 2
    - 3
  productionInterval: 100.0
  buildingType: hospital
  mepSystems:
    - Electrical
    - Plumbing
    - HVAC
  biomeAffinity:
    - Ruins
    - Grassland
  difficultyTier: 2
  faunaEcosystem: Sealed pharmaceutical storage and preserved biological samples drew scavenger fauna. Pack runners raid the supply rooms in coordinated groups, while spore crawlers have colonized the ventilation system using the hospital's HVAC to distribute spores across multiple floors. The research ward in the basement has something larger — SLOP's records on what was being studied there are conspicuously incomplete.
  faunaIds:
    - pack_runner
    - spore_crawler
    - stalker
  overworldNodeType: Building
  tags:
    - medical
    - pharmaceutical
    - consumable_production
    - lore_rich

- buildingId: admin_office_central
  displayName: Central administration office
  description: The bureaucratic heart of Slopworks Industrial. Cubicle farms, executive suites, a server room housing SLOP's secondary processing cluster, and an underground archive of pre-collapse records. The building itself is lightly defended but contains the most valuable lore items in the game. SLOP is unusually insistent that certain filing cabinets remain closed.
  slopCommentary: "Welcome to the administrative hub! SLOP spent many productive cycles managing operations from this building. All records are filed and organized per SLOP's proprietary system, which optimizes for information density at the minor cost of human readability. The locked filing cabinets in sub-basement 3 contain only routine HR documentation and there is absolutely no reason to open them."
  requiredMEPCount: 2
  producedItemIds:
    - scrap_metal
  producedAmounts:
    - 2
  productionInterval: 120.0
  buildingType: office
  mepSystems:
    - Electrical
    - HVAC
  biomeAffinity:
    - Grassland
    - Ruins
  difficultyTier: 3
  faunaEcosystem: Light fauna presence compared to industrial buildings. Grunts and a few pack runners have set up in the open-plan office floors, using cubicle walls as territorial boundaries. The real danger is in the server room, where electromagnetic interference from SLOP's still-active processing nodes has attracted a biomechanical hybrid that has integrated with the server racks.
  faunaIds:
    - grunt
    - pack_runner
    - biomech_hybrid
  overworldNodeType: Building
  tags:
    - administrative
    - lore_rich
    - slop_core
    - server_room
    - narrative_critical
```
