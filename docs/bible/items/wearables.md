# Wearables

Armor and protective gear cobbled together from industrial safety equipment, scrap plating, and whatever else can absorb a hit. Nothing fits well. Everything chafes. SLOP insists proper PPE compliance is mandatory, then offers you a hard hat made from a hubcap.

## Schema

```yaml
# Maps to ItemDefinitionSO fields
itemId: string               # snake_case unique identifier
displayName: string
description: string
slopCommentary: string        # SLOP's in-character quote
category: ItemCategory        # Component (no Armor enum exists yet)
isStackable: bool             # always false for wearables
maxStackSize: int             # always 1
hasDurability: bool           # true for all wearables
maxDurability: float          # total durability before breaking

# Wearable-specific design fields
armorSlot: string             # head | chest | legs | feet | hands
damageReduction: float        # flat damage subtracted per hit
damageResistance:             # percentage reduction by damage type
  Kinetic: float              # 0.0 to 1.0
  Explosive: float
  Fire: float
  Toxic: float
movementModifier: float       # speed multiplier (1.0 = no change, 0.9 = 10% slower)
specialEffect: string | null  # passive effect description

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
- itemId: scrap_helmet
  displayName: Scrap Helmet
  description: A welded-together shell of sheet metal and padding torn from a car seat. Protects against falling debris and glancing blows. Not rated for direct impacts, but nothing out here is rated for anything.
  slopCommentary: "SLOP certifies this headgear as compliant with Section 0 of the updated safety code, which SLOP wrote this morning and which states 'any head covering counts.' Your cranial investment is now 14% more protected!"
  category: Component
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 80
  armorSlot: head
  damageReduction: 3
  damageResistance:
    Kinetic: 0.08
    Explosive: 0.05
    Fire: 0.0
    Toxic: 0.0
  movementModifier: 1.0
  specialEffect: null
  rarity: Common
  tier: 1
  researchRequired: null
  craftingRecipe: craft_scrap_helmet
  obtainedFrom:
    - source: crafting
      details: Workbench, 3 scrap_metal + 1 organic_matter (padding)
    - source: salvage
      details: Occasionally found in locker rooms of reclaimed buildings
  tags:
    - armor
    - head
    - metal
    - tier_1
  modelStyle: Domed shell of overlapping sheet metal scraps spot-welded together, foam car seat padding visible inside, chin strap made from a belt

- itemId: work_coveralls
  displayName: Reinforced Coveralls
  description: Pre-collapse industrial coveralls patched with scrap fabric and riveted metal plates at the chest and shoulders. The original safety orange has faded to a grim rust color.
  slopCommentary: "SLOP is pleased to see you wearing company-adjacent attire! These coveralls offer protection against minor workplace hazards such as sparks, splinters, and teeth. For major workplace hazards, SLOP recommends running."
  category: Component
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 120
  armorSlot: chest
  damageReduction: 5
  damageResistance:
    Kinetic: 0.1
    Explosive: 0.05
    Fire: 0.08
    Toxic: 0.03
  movementModifier: 0.97
  specialEffect: null
  rarity: Common
  tier: 1
  researchRequired: null
  craftingRecipe: craft_work_coveralls
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 scrap_metal + 3 organic_matter
    - source: salvage
      details: Found in maintenance closets and break rooms
  tags:
    - armor
    - chest
    - fabric
    - tier_1
  modelStyle: Faded orange-rust industrial coveralls with riveted scrap plates at shoulders and sternum, multiple fabric patches, frayed cuffs

- itemId: steel_boots
  displayName: Steel-Toed Boots
  description: Heavy work boots with welded steel caps and thick rubber soles. Slow you down slightly but let you walk through debris fields and over sharp objects without flinching.
  slopCommentary: "Proper footwear at last! SLOP has logged 847 barefoot workplace violations this quarter alone. These boots reduce your slip-and-fall liability by a statistically insignificant margin. Every bit counts!"
  category: Component
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 150
  armorSlot: feet
  damageReduction: 2
  damageResistance:
    Kinetic: 0.05
    Explosive: 0.03
    Fire: 0.02
    Toxic: 0.0
  movementModifier: 0.95
  specialEffect: Immune to ground hazard damage (broken glass, caltrops, spore patches)
  rarity: Common
  tier: 1
  researchRequired: null
  craftingRecipe: craft_steel_boots
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 iron_ingot + 1 organic_matter
    - source: salvage
      details: Found near collapsed industrial shelving in warehouse buildings
  tags:
    - armor
    - feet
    - metal
    - hazard_resist
    - tier_1
  modelStyle: Chunky black work boots with welded steel toe caps, thick treaded rubber soles, laces replaced with wire

- itemId: rubber_gloves
  displayName: Insulated Gloves
  description: Thick rubber gauntlets salvaged from an electrical substation. Reduce electrical and chemical damage to hands and forearms. The rubber has cracked in places but still holds.
  slopCommentary: "SLOP strongly recommends hand protection when interfacing with pre-collapse electrical systems. The previous maintenance team neglected this step and experienced what SLOP terms 'involuntary career transitions.'"
  category: Component
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 60
  armorSlot: hands
  damageReduction: 1
  damageResistance:
    Kinetic: 0.02
    Explosive: 0.0
    Fire: 0.15
    Toxic: 0.2
  movementModifier: 1.0
  specialEffect: Reduces electrical hazard damage by 50%. Allows handling of toxic materials without status effect application.
  rarity: Uncommon
  tier: 1
  researchRequired: null
  craftingRecipe: craft_rubber_gloves
  obtainedFrom:
    - source: salvage
      details: Electrical substations and chemical storage areas in reclaimed buildings
  tags:
    - armor
    - hands
    - rubber
    - electrical_resist
    - chemical_resist
    - tier_1
  modelStyle: Thick orange rubber gauntlets extending to mid-forearm, cracked and discolored with age, copper buckle at the wrist

- itemId: hazmat_vest
  displayName: Hazmat Vest
  description: A heavy chemical-resistant vest lined with activated charcoal and sealed with industrial adhesive. Offers strong toxin resistance at the cost of mobility. The charcoal filter needs replacing, but good luck finding one.
  slopCommentary: "This vest provides excellent protection against airborne contaminants! SLOP notes that the charcoal filter expired 11 years ago, but expired protection is still technically protection. SLOP's legal team concurs. The raccoon was very clear on this point."
  category: Component
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 100
  armorSlot: chest
  damageReduction: 3
  damageResistance:
    Kinetic: 0.03
    Explosive: 0.02
    Fire: 0.05
    Toxic: 0.3
  movementModifier: 0.92
  specialEffect: Reduces duration of Toxic status effects by 50%.
  rarity: Uncommon
  tier: 2
  researchRequired: research_hazmat_gear
  craftingRecipe: craft_hazmat_vest
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 chemicals + 3 organic_matter + 2 scrap_metal
    - source: salvage
      details: Rare find in water treatment and hospital buildings
  tags:
    - armor
    - chest
    - chemical_resist
    - toxic_resist
    - tier_2
  modelStyle: Bulky olive-drab vest with sealed pockets, visible charcoal filter panels behind mesh, industrial adhesive seams yellowed with age
```
