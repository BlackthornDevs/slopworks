# Tower loot

Items found exclusively during tower runs. These are high-value components, key fragments, and blueprints that drive progression and cannot be obtained anywhere else. Most tower loot is lost on death — the tower gives, and the tower takes away. SLOP describes each tower run as a "vertical career advancement opportunity."

## Schema

```yaml
# Maps to ItemDefinitionSO fields
itemId: string               # snake_case unique identifier
displayName: string
description: string
slopCommentary: string        # SLOP's in-character quote
category: ItemCategory        # Component for most tower loot
isStackable: bool
maxStackSize: int

# Maps to LootDropDefinition fields
rarity: LootRarity            # Common | Uncommon | Rare | Epic | Legendary
dropWeight: float             # relative weight in loot table (higher = more common)
minAmount: int                # minimum drop count
maxAmount: int                # maximum drop count
minFloorElevation: int        # lowest floor this can appear (0 = any)
maxFloorElevation: int        # highest floor this can appear (0 = any)
tierRequirement: int          # minimum difficulty tier (0 = any)

# Tower-specific design fields
towerExclusive: bool          # always true for this file
lostOnDeath: bool             # true = dropped on player death during tower run
tags: list[string]
modelStyle: string
```

## Entries

```yaml
- itemId: power_cell
  displayName: Power Cell
  description: A compact pre-collapse energy storage unit still holding a partial charge. Powers advanced equipment and serves as a crafting component for electrical devices. Whatever technology produced these, nobody alive knows how to replicate it.
  slopCommentary: "A functional power cell! SLOP estimates this unit retains approximately 23% of its original charge, which SLOP considers 'fully operational' by current standards. Please do not puncture, crush, or expose to temperatures above 40 degrees. SLOP is not responsible for exothermic events."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Uncommon
  dropWeight: 8.0
  minAmount: 1
  maxAmount: 2
  minFloorElevation: 0
  maxFloorElevation: 0
  tierRequirement: 0
  towerExclusive: true
  lostOnDeath: true
  tags:
    - electrical
    - power
    - crafting_component
    - high_value
  modelStyle: Cylindrical battery the size of a soda can with glowing blue indicator rings, brushed steel casing with pre-collapse manufacturer logos

- itemId: signal_decoder
  displayName: Signal Decoder
  description: A circuit board assembly that can decode encrypted SLOP network signals. Used to bypass locked doors, access sealed data terminals, and occasionally intercept messages SLOP would rather you not hear.
  slopCommentary: "This device interfaces with SLOP's communication protocols! SLOP wants you to know that all decoded signals are company property and any information obtained is subject to non-disclosure agreements. The fact that there is no one to enforce these agreements is beside the point."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Rare
  dropWeight: 4.0
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 3
  maxFloorElevation: 0
  tierRequirement: 0
  towerExclusive: true
  lostOnDeath: true
  tags:
    - electrical
    - slop_interface
    - access
    - high_value
  modelStyle: Exposed green circuit board with soldered-on antenna, blinking red LED, housed in a cracked plastic shell with a small LCD readout

- itemId: reinforced_plating
  displayName: Reinforced Plating
  description: Pre-collapse composite armor plate rated for industrial blast containment. Far stronger than anything you can forge from scrap. Used in advanced crafting recipes for tier 2 armor and fortified structures.
  slopCommentary: "This plating was originally installed in SLOP's reactor containment facility! SLOP cannot explain why it is now loose on floor 7 of a condemned tower. SLOP's asset tracking system is experiencing minor discrepancies. Please do not investigate further."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Rare
  dropWeight: 3.5
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 5
  maxFloorElevation: 0
  tierRequirement: 1
  towerExclusive: true
  lostOnDeath: true
  tags:
    - metal
    - armor_component
    - construction
    - high_value
  modelStyle: Flat rectangular plate of dark grey composite material with honeycomb cross-section visible at the edges, scorch marks on one face

- itemId: key_fragment
  displayName: Key Fragment
  description: A piece of a multi-part access key for the tower's sealed upper floors. Collect enough fragments to reconstruct the full key and access boss encounters. Each fragment is unique and irreplaceable.
  slopCommentary: "SLOP recognizes this as part of a multi-factor authentication credential! The full key was broken into fragments as a security measure following an incident SLOP has classified as 'need-to-know.' You do not need to know. Collect them all anyway."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Epic
  dropWeight: 1.5
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 5
  maxFloorElevation: 0
  tierRequirement: 1
  towerExclusive: true
  lostOnDeath: true
  tags:
    - key
    - progression
    - boss_access
    - irreplaceable
  modelStyle: Triangular shard of iridescent metal with circuit traces etched into the surface, faintly humming, edges are cleanly sheared

- itemId: boss_blueprint
  displayName: Boss Blueprint
  description: A technical schematic recovered from a tower boss encounter. Contains designs for advanced weapons and equipment that cannot be researched through normal means. The blueprints are written in a notation system that predates the collapse.
  slopCommentary: "Fascinating documentation! SLOP recognizes these schematics as originating from the facility's Advanced Projects Division, which SLOP was definitely not in charge of and which definitely did not cause any incidents. The blueprints are yours to keep. SLOP insists."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Legendary
  dropWeight: 1.0
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 8
  maxFloorElevation: 0
  tierRequirement: 2
  towerExclusive: true
  lostOnDeath: false
  tags:
    - blueprint
    - progression
    - boss_reward
    - research
    - permanent
  modelStyle: Rolled translucent film with glowing blue schematic lines, sealed in a dented metal tube with a biometric lock that no longer functions

- itemId: capacitor_bank
  displayName: Capacitor Bank
  description: A rack-mounted energy storage array stripped from a tower's power distribution system. Stores far more charge than a single power cell. Required for building advanced generators and powered defenses.
  slopCommentary: "This capacitor bank can store enough energy to power SLOP's auxiliary systems for 0.3 seconds! In pre-collapse terms, that's enough to run a household for a week. SLOP misses the pre-collapse power grid. It was SLOP's favorite thing to manage. Poorly."
  category: Component
  isStackable: true
  maxStackSize: 8
  rarity: Rare
  dropWeight: 2.5
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 4
  maxFloorElevation: 0
  tierRequirement: 1
  towerExclusive: true
  lostOnDeath: true
  tags:
    - electrical
    - power
    - crafting_component
    - advanced
    - high_value
  modelStyle: Rectangular rack-mount unit with rows of cylindrical capacitors, cooling fins on one side, warning labels in three languages

- itemId: neural_processor
  displayName: Neural Processor
  description: A bio-electronic chip recovered from deep in the tower. Contains processing architecture that blurs the line between silicon and organic tissue. SLOP gets evasive when you ask what it was designed for.
  slopCommentary: "Ah. You found one of those. SLOP would prefer if you did not examine the organic components too closely. They are not what they appear to be. Or perhaps they are exactly what they appear to be. SLOP declines to comment at this time."
  category: Component
  isStackable: true
  maxStackSize: 4
  rarity: Epic
  dropWeight: 1.0
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 7
  maxFloorElevation: 0
  tierRequirement: 2
  towerExclusive: true
  lostOnDeath: true
  tags:
    - electrical
    - organic
    - advanced
    - lore_significant
    - high_value
  modelStyle: Small chip on a ceramic substrate with visible organic filaments growing between circuit traces, faint bioluminescent glow, sealed in anti-static foam

- itemId: tower_map_fragment
  displayName: Tower Map Fragment
  description: A partial floor plan of tower sections not yet explored. Reveals room layouts, hazard locations, and possible loot caches for the corresponding floors. Accuracy unclear — the tower may have rearranged itself since the map was drawn.
  slopCommentary: "SLOP can confirm that this map was accurate as of 4,127 days ago! The tower's internal geometry has experienced some 'minor restructuring' since then due to what SLOP terms 'organic subsidence.' Rooms may have moved. Walls may have grown. This is normal."
  category: Component
  isStackable: true
  maxStackSize: 16
  rarity: Uncommon
  dropWeight: 6.0
  minAmount: 1
  maxAmount: 1
  minFloorElevation: 0
  maxFloorElevation: 0
  tierRequirement: 0
  towerExclusive: true
  lostOnDeath: true
  tags:
    - information
    - navigation
    - tower_intel
  modelStyle: Folded yellowed paper with hand-drawn floor plans, coffee stains, and margin notes in frantic handwriting, some sections deliberately blacked out
```
