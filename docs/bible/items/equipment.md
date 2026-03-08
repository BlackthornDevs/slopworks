# Equipment

Tools and utility devices that don't deal damage directly but keep you alive, informed, or productive. Most of these were standard issue before the collapse. Now they're luxuries held together with solder and optimism.

## Schema

```yaml
# Maps to ItemDefinitionSO fields
itemId: string               # snake_case unique identifier
displayName: string
description: string
slopCommentary: string        # SLOP's in-character quote
category: ItemCategory        # Tool for equipment
isStackable: bool             # false for most equipment
maxStackSize: int             # 1 for non-consumable equipment
hasDurability: bool           # true for equipment with limited uses
maxDurability: float          # total uses or time-based durability

# Equipment-specific design fields
equipSlot: string             # primary | secondary | utility
useAction: string             # description of what happens on activation
cooldown: float               # seconds between uses (0 = no cooldown)

# Shared design fields
rarity: LootRarity
tier: int
researchRequired: string | null
craftingRecipe: string | null
obtainedFrom:
  - source: string
    details: string
tags: list[string]
modelStyle: string
```

## Entries

```yaml
- itemId: repair_kit
  displayName: Repair Kit
  description: A pouch of scrap patches, wire, adhesive, and a hand riveter. Restores durability to weapons, armor, and machines. Each use consumes materials from the kit until it's empty.
  slopCommentary: "Preventive maintenance is the cornerstone of operational excellence! SLOP recommends a repair schedule of every 200 operating hours. SLOP also recommends ignoring the fact that nothing here has operated correctly in over a decade."
  category: Tool
  isStackable: true
  maxStackSize: 5
  hasDurability: true
  maxDurability: 3
  equipSlot: utility
  useAction: Restores 40 durability to targeted item or machine. Consumes 1 charge per use.
  cooldown: 1.5
  rarity: Common
  tier: 1
  researchRequired: null
  craftingRecipe: craft_repair_kit
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 scrap_metal + 1 iron_ingot
    - source: salvage
      details: Maintenance closets in reclaimed buildings
  tags:
    - utility
    - repair
    - consumable_equipment
    - tier_1
  modelStyle: Canvas roll-up pouch with loops holding a hand riveter, wire spools, and adhesive tubes, stained with grease

- itemId: slop_scanner
  displayName: SLOP Scanner
  description: A handheld terminal that taps into SLOP's local sensor network. Displays a map overlay with resource markers, hazard zones, and fauna density. The data is approximately 60% accurate, which SLOP considers excellent.
  slopCommentary: "Welcome to the SLOP Situational Awareness Module! All readings are provided on a best-effort basis. If the scanner indicates 'safe,' there is only a moderate chance of encountering unauthorized biological occupants. SLOP accepts no liability for moderate chances."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 500
  equipSlot: secondary
  useAction: Activates a 30-second scan overlay showing nearby resources, hazards, and fauna within 50m. Data accuracy varies. Occasional false positives and missing contacts.
  cooldown: 45
  rarity: Uncommon
  tier: 1
  researchRequired: null
  craftingRecipe: craft_slop_scanner
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 copper_ingot + 1 quartz + 1 iron_ingot
    - source: salvage
      details: Control rooms in reclaimed buildings, rare
  tags:
    - utility
    - scanner
    - slop_interface
    - information
    - tier_1
  modelStyle: Chunky handheld terminal with a cracked green-tint LCD screen, exposed circuit board on one side, antenna made from a bent coathanger

- itemId: salvage_cutter
  displayName: Salvage Cutter
  description: A motorized cutting tool powered by a small power cell. Speeds up salvage operations on metal objects and can cut through locked doors and sealed containers. Loud enough to attract attention.
  slopCommentary: "This cutting tool dramatically improves your material reclamation throughput! SLOP notes that the noise output may attract nearby workforce participants who did not receive their termination notices. Because they are animals. Literal animals."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 200
  equipSlot: primary
  useAction: Hold to cut through salvage nodes 3x faster. Can open locked containers and sealed doors. Generates noise that increases fauna aggro radius by 2x while active.
  cooldown: 0
  rarity: Uncommon
  tier: 1
  researchRequired: research_salvage_tools
  craftingRecipe: craft_salvage_cutter
  obtainedFrom:
    - source: crafting
      details: Workbench, 3 iron_ingot + 1 copper_ingot + 1 power_cell
  tags:
    - utility
    - salvage
    - powered
    - noisy
    - tier_1
  modelStyle: Angular handheld disc cutter with an exposed spinning blade guard, vibrating power cell housing on the side, worn rubber grip

- itemId: power_meter
  displayName: Power Meter
  description: A diagnostic tool that reads the power output and consumption of machines and grid segments. Essential for debugging factory layouts and finding bottlenecks before they cause a cascade failure.
  slopCommentary: "Knowledge is power! And power is also power. This device measures the second kind. SLOP recommends maintaining grid load below 80% capacity. SLOP also maintained grid load below 80% capacity right up until the moment it didn't."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: false
  maxDurability: 0
  equipSlot: utility
  useAction: Point at a machine or power conduit to display its current power draw, output, and grid status in a floating panel. Passive use, no charges consumed.
  cooldown: 0
  rarity: Common
  tier: 1
  researchRequired: null
  craftingRecipe: craft_power_meter
  obtainedFrom:
    - source: crafting
      details: Workbench, 1 copper_ingot + 1 quartz
    - source: salvage
      details: Found in electrical rooms of power plant buildings
  tags:
    - utility
    - diagnostic
    - factory
    - electrical
    - tier_1
  modelStyle: Small handheld device resembling a multimeter with two probes on cables, analog needle gauge on the face, cracked plastic housing

- itemId: signal_booster
  displayName: Signal Booster
  description: An antenna amplifier that extends the range of SLOP's sensor network in a local area. Place it to create a temporary zone of improved scanner accuracy and faster data refresh. Also picks up fragments of pre-collapse radio broadcasts, which is either useful or deeply unsettling.
  slopCommentary: "By deploying this booster, you are extending SLOP's awareness radius. SLOP appreciates your voluntary contribution to the surveillance — sorry, 'safety monitoring' — network. SLOP will use this data responsibly. SLOP promises."
  category: Tool
  isStackable: true
  maxStackSize: 3
  hasDurability: true
  maxDurability: 1
  equipSlot: utility
  useAction: Place on the ground to create a 30m radius zone for 120 seconds. Inside the zone, SLOP Scanner accuracy increases to 90% and scan cooldown is halved. Consumed on use.
  cooldown: 5
  rarity: Uncommon
  tier: 2
  researchRequired: research_signal_processing
  craftingRecipe: craft_signal_booster
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 copper_ingot + 1 quartz + 1 signal_decoder
  tags:
    - utility
    - scanner
    - slop_interface
    - placeable
    - consumable_equipment
    - tier_2
  modelStyle: Tripod-mounted antenna dish the size of a dinner plate with blinking LED indicators, tangled coax cables, and a jury-rigged signal amplifier box
```
