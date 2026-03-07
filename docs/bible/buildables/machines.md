# Machines

Production buildings that process items, generate power, store materials, and route logistics. Each machine occupies a grid footprint and exposes I/O ports for belt connections. Machine definitions map to `MachineDefinitionSO` in code, with design-only fields tracked here for future tooling and balancing passes.

## Schema

```yaml
# Core fields (match MachineDefinitionSO)
machineId: string                         # unique snake_case identifier
displayName: string                       # player-facing name
size: [int, int]                          # grid footprint [width, depth]
machineType: string                       # matches RecipeSO.requiredMachineType
inputBufferSize: int                      # input item slots
outputBufferSize: int                     # output item slots
processingSpeed: float                    # multiplier on recipe craftDuration (higher = faster)
powerConsumption: float                   # watts consumed while working

# Port definitions (match MachinePort struct)
ports:
  - localOffset: [int, int]               # position relative to machine origin cell
    direction: enum [north, south, east, west]  # mapped to Vector2Int in code
    type: enum [Input, Output]            # PortType enum
    filter: string | null                 # item ID filter, null = accept all
    throughput: float                     # items per second capacity

# Design fields (bible-only, not yet in SO)
slopCommentary: string                    # in-character SLOP quote
description: string                       # design description
tier: int                                 # tech tier (1 = starter, 2 = mid, 3 = late)
tags: list                                # lowercase string tags for queries
category: enum [production, logistics, power, storage, utility]
defaultRecipe: string | null              # recipe ID loaded by default
availableRecipes: list                    # recipe IDs this machine can run
powerGeneration: float                    # watts generated (for generators, 0 otherwise)
health: float                             # hit points before destruction
repairMaterial: string                    # item ID used for repairs
researchRequired: string | null           # research node ID, null = available from start
craftingRecipe: string | null             # recipe ID to craft this machine
upgradePath: string | null                # machine ID this upgrades into
portOwnerType: enum [Machine, Storage, Belt, Turret]  # always Machine for this file
modelStyle: string                        # visual style description
workingSound: string                      # FMOD event path
idleSound: string                         # FMOD event path
```

## Entries

```yaml
- machineId: smelter_t1
  displayName: "Smelter mk.I"
  size: [2, 2]
  machineType: "smelter"
  inputBufferSize: 2
  outputBufferSize: 2
  processingSpeed: 1.0
  powerConsumption: 100.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [1, 0]
      direction: south
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [0, 1]
      direction: north
      type: Output
      filter: null
      throughput: 1.0
    - localOffset: [1, 1]
      direction: north
      type: Output
      filter: null
      throughput: 1.0
  slopCommentary: "The SLOP-certified Personal Thermal Reclamation Unit turns unprocessed geological samples into shiny productivity tokens. Side effects may include localized heat advisories."
  description: "Starter ore processor. Converts raw ores into ingots. Slow but reliable. Two input ports accept ore from belts; two output ports push ingots downstream."
  tier: 1
  tags: [production, smelting, starter, ore_processing]
  category: production
  defaultRecipe: smelt_iron
  availableRecipes:
    - smelt_iron
    - smelt_copper
    - smelt_steel
  powerGeneration: 0.0
  health: 500.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_smelter
  upgradePath: smelter_t2
  portOwnerType: Machine
  modelStyle: "boxy industrial furnace with exposed pipes, orange glow from intake vents, scorch marks on casing"
  workingSound: "event:/machines/smelter/working"
  idleSound: "event:/machines/smelter/idle"

- machineId: assembler_t1
  displayName: "Assembler mk.I"
  size: [2, 2]
  machineType: "assembler"
  inputBufferSize: 3
  outputBufferSize: 1
  processingSpeed: 1.0
  powerConsumption: 150.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [1, 0]
      direction: east
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [0, 1]
      direction: west
      type: Input
      filter: null
      throughput: 0.5
    - localOffset: [1, 1]
      direction: north
      type: Output
      filter: null
      throughput: 1.0
  slopCommentary: "The Multi-Purpose Assembly Station combines simple components into complex ones through the miracle of approved mechanical processes. Fingers are not covered under warranty."
  description: "General-purpose fabrication machine. Takes up to three different input materials and produces assembled components. The workhorse of mid-game production chains."
  tier: 1
  tags: [production, assembly, crafting, multi_input]
  category: production
  defaultRecipe: craft_iron_plate
  availableRecipes:
    - craft_iron_plate
    - craft_mechanical_component
    - craft_circuit_board
    - craft_med_kit
  powerGeneration: 0.0
  health: 400.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_assembler
  upgradePath: assembler_t2
  portOwnerType: Machine
  modelStyle: "wide workbench with robotic arm, sparking welding tip, parts bin on one side"
  workingSound: "event:/machines/assembler/working"
  idleSound: "event:/machines/assembler/idle"

- machineId: splitter_t1
  displayName: "Splitter"
  size: [1, 1]
  machineType: "splitter"
  inputBufferSize: 1
  outputBufferSize: 2
  processingSpeed: 2.0
  powerConsumption: 20.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: null
      throughput: 2.0
    - localOffset: [0, 0]
      direction: east
      type: Output
      filter: null
      throughput: 1.0
    - localOffset: [0, 0]
      direction: west
      type: Output
      filter: null
      throughput: 1.0
  slopCommentary: "The Item Redistribution Node ensures equitable allocation of resources across your production network. Everyone gets their fair share. SLOP guarantees it."
  description: "Logistics node that takes one input belt and splits items evenly across two output belts. Alternates items round-robin. Small footprint, low power draw."
  tier: 1
  tags: [logistics, splitter, routing, belt_management]
  category: logistics
  defaultRecipe: null
  availableRecipes: []
  powerGeneration: 0.0
  health: 200.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_splitter
  upgradePath: null
  portOwnerType: Machine
  modelStyle: "compact junction box with y-shaped chute, painted yellow caution stripes"
  workingSound: "event:/machines/splitter/working"
  idleSound: "event:/machines/splitter/idle"

- machineId: merger_t1
  displayName: "Merger"
  size: [1, 1]
  machineType: "merger"
  inputBufferSize: 2
  outputBufferSize: 1
  processingSpeed: 2.0
  powerConsumption: 20.0
  ports:
    - localOffset: [0, 0]
      direction: east
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [0, 0]
      direction: west
      type: Input
      filter: null
      throughput: 1.0
    - localOffset: [0, 0]
      direction: north
      type: Output
      filter: null
      throughput: 2.0
  slopCommentary: "The Material Convergence Hub brings two streams together in perfect harmony. Like a team-building exercise, but for inanimate objects."
  description: "Logistics node that combines two input belts into a single output belt. Items interleave in arrival order. Inverse of the splitter."
  tier: 1
  tags: [logistics, merger, routing, belt_management]
  category: logistics
  defaultRecipe: null
  availableRecipes: []
  powerGeneration: 0.0
  health: 200.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_merger
  upgradePath: null
  portOwnerType: Machine
  modelStyle: "compact junction box with funnel-shaped intake, painted yellow caution stripes"
  workingSound: "event:/machines/merger/working"
  idleSound: "event:/machines/merger/idle"

- machineId: inserter_t1
  displayName: "Inserter"
  size: [1, 1]
  machineType: "inserter"
  inputBufferSize: 1
  outputBufferSize: 1
  processingSpeed: 1.5
  powerConsumption: 10.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: null
      throughput: 1.5
    - localOffset: [0, 0]
      direction: north
      type: Output
      filter: null
      throughput: 1.5
  slopCommentary: "The Automated Material Transfer Appendage moves items from Point A to Point B so you don't have to. Your hands were meant for filling out productivity reports."
  description: "Robotic arm that picks items from one belt or machine port and places them on another. Essential bridge between belt segments and machine buffers. Fast cycle time, minimal power."
  tier: 1
  tags: [logistics, inserter, transfer, belt_machine_bridge]
  category: logistics
  defaultRecipe: null
  availableRecipes: []
  powerGeneration: 0.0
  health: 150.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_inserter
  upgradePath: inserter_t2
  portOwnerType: Machine
  modelStyle: "rotating arm on a post, grabber claw at end, yellow and black safety striping"
  workingSound: "event:/machines/inserter/working"
  idleSound: "event:/machines/inserter/idle"

- machineId: generator_t1
  displayName: "Generator mk.I"
  size: [2, 2]
  machineType: "generator"
  inputBufferSize: 1
  outputBufferSize: 0
  processingSpeed: 1.0
  powerConsumption: 0.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: coal
      throughput: 0.5
  slopCommentary: "The SLOP Portable Energy Solution converts combustible materials into clean, approved electricity. Smoke output has been classified as an atmospheric feature, not a pollutant."
  description: "Burns fuel items (coal, wood) to produce electrical power for the factory grid. Single input port accepts fuel from a belt. No output port -- power is distributed via the power network, not belts."
  tier: 1
  tags: [power, generator, fuel, starter]
  category: power
  defaultRecipe: null
  availableRecipes: []
  powerGeneration: 200.0
  health: 600.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_generator
  upgradePath: generator_t2
  portOwnerType: Machine
  modelStyle: "squat diesel generator with exhaust stack, rumbling chassis, fuel tank visible on side"
  workingSound: "event:/machines/generator/working"
  idleSound: "event:/machines/generator/idle"

- machineId: storage_t1
  displayName: "Storage crate"
  size: [1, 1]
  machineType: "storage"
  inputBufferSize: 20
  outputBufferSize: 20
  processingSpeed: 0.0
  powerConsumption: 0.0
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: null
      throughput: 2.0
    - localOffset: [0, 0]
      direction: north
      type: Output
      filter: null
      throughput: 2.0
  slopCommentary: "The SLOP-Approved Material Retention Facility keeps your items safe, organized, and ready for redistribution. Contents may shift during unauthorized biological occupant events."
  description: "Basic storage container with 20 slots. Accepts items from a belt on the input side and feeds them out on the output side. Also accessible directly by players for manual inventory management. Maps to StorageDefinitionSO in code."
  tier: 1
  tags: [storage, container, starter, inventory]
  category: storage
  defaultRecipe: null
  availableRecipes: []
  powerGeneration: 0.0
  health: 300.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_storage_crate
  upgradePath: storage_t2
  portOwnerType: Storage
  modelStyle: "reinforced wooden crate with metal corner braces, stenciled SLOP logo on side"
  workingSound: null
  idleSound: null
```
