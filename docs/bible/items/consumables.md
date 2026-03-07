# Consumables

Single-use items that provide immediate or temporary effects. Crafted from salvaged medical supplies, chemical compounds, and questionable biological extracts. SLOP assures you that all consumables have undergone rigorous quality testing, by which SLOP means "someone used one and did not immediately die."

## Schema

```yaml
# Maps to ItemDefinitionSO fields
itemId: string               # snake_case unique identifier
displayName: string
description: string
slopCommentary: string        # SLOP's in-character quote
category: ItemCategory        # always Consumable
isStackable: bool             # always true for consumables
maxStackSize: int             # inventory stack limit
hasDurability: bool           # always false (single use)
maxDurability: float          # 0 (unused)

# Consumable-specific design fields
useEffect: string             # effect ID applied on consumption
effectDuration: float         # seconds (0 = instant effect)
effectMagnitude: float        # strength of the effect (context-dependent)
maxCarry: int                 # hard carry limit regardless of stack size

# Shared design fields
rarity: LootRarity
tier: int
craftingRecipe: string | null
obtainedFrom:
  - source: string
    details: string
tags: list[string]
modelStyle: string
```

## Entries

```yaml
- itemId: med_kit
  displayName: Med Kit
  description: A battered first aid kit containing expired antiseptic, salvaged bandages, and a single-use auto-injector of unknown provenance. Restores a moderate amount of health instantly.
  slopCommentary: "SLOP recommends applying this medical kit to your injuries at your earliest convenience! Side effects may include temporary numbness, mild hallucinations, and an inexplicable craving for pre-collapse vending machine snacks. These are all signs that the treatment is working."
  category: Consumable
  isStackable: true
  maxStackSize: 5
  hasDurability: false
  maxDurability: 0
  useEffect: heal_instant
  effectDuration: 0
  effectMagnitude: 40
  maxCarry: 10
  rarity: Common
  tier: 1
  craftingRecipe: craft_med_kit
  obtainedFrom:
    - source: crafting
      details: Workbench, 1 organic_matter + 1 chemicals
    - source: salvage
      details: Found in hospital and office buildings, medical cabinets
    - source: drop
      details: Rare drop from fauna in interior encounters
  tags:
    - medical
    - healing
    - instant
    - tier_1
  modelStyle: Dented white metal case with a faded red cross, hinged open to show rolled bandages and a single auto-injector pen

- itemId: stim_pack
  displayName: Stim Pack
  description: A pressurized injector loaded with a cocktail of adrenaline and stimulants synthesized from organic compounds. Dramatically increases movement speed for a short duration. The crash afterward is unpleasant.
  slopCommentary: "This performance enhancement compound will temporarily increase your operational velocity by 40%! SLOP is legally required to inform you that this product has not been approved by any regulatory body. All regulatory bodies are currently deceased or have been reclassified as fauna."
  category: Consumable
  isStackable: true
  maxStackSize: 3
  hasDurability: false
  maxDurability: 0
  useEffect: speed_boost
  effectDuration: 15
  effectMagnitude: 1.4
  maxCarry: 6
  rarity: Uncommon
  tier: 1
  craftingRecipe: craft_stim_pack
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 chemicals + 1 organic_matter
    - source: salvage
      details: Rare find in hospital buildings and emergency supply caches
  tags:
    - medical
    - buff
    - speed
    - tier_1
  modelStyle: Cylindrical pressurized injector with a thumb trigger, glowing amber liquid visible through a narrow window, rubber safety cap on the needle end

- itemId: antitoxin
  displayName: Antitoxin
  description: A broad-spectrum detox compound that neutralizes most chemical and biological toxins. Clears toxic status effects immediately and provides short-term resistance to further poisoning.
  slopCommentary: "This compound will purge 97% of known toxins from your system! The remaining 3% are what SLOP classifies as 'character-building contaminants.' If you experience green discoloration of the skin, that is not a side effect — you are simply adapting to your environment."
  category: Consumable
  isStackable: true
  maxStackSize: 5
  hasDurability: false
  maxDurability: 0
  useEffect: cleanse_toxic
  effectDuration: 30
  effectMagnitude: 0.5
  maxCarry: 10
  rarity: Uncommon
  tier: 2
  craftingRecipe: craft_antitoxin
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 chemicals + 1 organic_matter
    - source: salvage
      details: Chemical storage in water treatment buildings
  tags:
    - medical
    - cleanse
    - toxic_resist
    - tier_2
  modelStyle: Small glass vial with a rubber stopper and a milky blue-green liquid, labeled with a hand-written skull-and-crossbones that has been crossed out and replaced with a thumbs-up

- itemId: field_ration
  displayName: Field Ration
  description: A vacuum-sealed block of compressed nutrients that expired long before you were born. Tastes like salted cardboard but provides a slow health regeneration over time. Do not read the ingredients list.
  slopCommentary: "SLOP is delighted to offer you this nutritionally adequate meal replacement! Best enjoyed without chewing, smelling, or thinking about it. SLOP's internal records indicate this product was originally manufactured for use in a correctional facility. That is unrelated to its current deployment."
  category: Consumable
  isStackable: true
  maxStackSize: 10
  hasDurability: false
  maxDurability: 0
  useEffect: heal_over_time
  effectDuration: 30
  effectMagnitude: 2
  maxCarry: 20
  rarity: Common
  tier: 1
  craftingRecipe: null
  obtainedFrom:
    - source: salvage
      details: Break rooms, vending machines, and pantries in all reclaimed building types
    - source: drop
      details: Found in supply caches during tower runs
  tags:
    - medical
    - healing
    - food
    - slow_heal
    - tier_1
  modelStyle: Flat vacuum-sealed foil packet with military-style markings, slightly puffed from age, corners dented

- itemId: overclock_serum
  displayName: Overclock Serum
  description: A volatile stimulant extracted from mutated fauna glands and stabilized with industrial solvents. Temporarily boosts damage output at the cost of reduced damage resistance. The shaking hands are normal. Probably.
  slopCommentary: "This compound provides a temporary 25% increase to kinetic output! SLOP must disclose that 3 out of 10 test subjects experienced 'spontaneous rapid disassembly' during clinical trials. The other 7 reported feeling 'pretty great, actually.' SLOP considers this an acceptable success rate."
  category: Consumable
  isStackable: true
  maxStackSize: 3
  hasDurability: false
  maxDurability: 0
  useEffect: damage_boost
  effectDuration: 20
  effectMagnitude: 1.25
  maxCarry: 6
  rarity: Rare
  tier: 2
  craftingRecipe: craft_overclock_serum
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 chemicals + 1 organic_matter + 1 sulfur
    - source: drop
      details: Rare drop from apex fauna types
  tags:
    - medical
    - buff
    - damage
    - risky
    - tier_2
  modelStyle: Thick glass syringe with a ratcheting plunger, filled with pulsing red-orange liquid, small biohazard sticker peeling off the barrel
```
