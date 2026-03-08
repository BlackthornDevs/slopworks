# Weapons

Improvised and salvaged armaments built from scrap, rebar, and whatever mechanical parts still function. Nothing here passed a safety inspection even before the collapse. SLOP insists these are "productivity enhancement tools" and reminds you that workplace violence is still against company policy, even if the workplace is a crumbling ruin full of mutated fauna.

## Schema

```yaml
# Maps to ItemDefinitionSO fields
itemId: string               # snake_case unique identifier (used in inventory system)
displayName: string
description: string
slopCommentary: string        # SLOP's in-character quote
category: ItemCategory        # Tool for weapons (no Weapon enum value exists yet)
isStackable: bool             # always false for weapons
maxStackSize: int             # always 1 for weapons
hasDurability: bool           # true for all weapons
maxDurability: float          # total durability before breaking

# Maps to WeaponDefinitionSO fields
weaponId: string              # matches itemId by convention
damage: float                 # base damage per hit
fireRate: float               # rounds per second (0 for melee swing speed)
range: float                  # effective range in units
damageType: DamageType        # Kinetic | Explosive | Fire | Toxic
magazineSize: int             # rounds before reload (0 for melee)
reloadTime: float             # seconds to reload (0 for melee)

# Design fields (not yet in SO)
rarity: LootRarity            # Common | Uncommon | Rare | Epic | Legendary
tier: int                     # progression tier (1-3)
weaponType: string            # rifle | shotgun | pistol | smg | melee_blunt | melee_blade
projectileSpeed: float        # m/s, 0 for hitscan
recoil: float                 # 0.0 to 1.0 screen kick intensity
headshotMultiplier: float     # damage multiplier for headshots
penetration: int              # number of targets projectile passes through (0 = none)
aoeRadius: float              # area of effect radius (0 = single target)
statusEffects:                # effects applied on hit
  - effectId: string
    chance: float             # 0.0 to 1.0
    duration: float           # seconds
ammoType: string              # item ID of required ammo (null for melee)
upgradeSlots: int             # number of modification slots
upgradePath: string | null    # weapon ID of next tier version
researchRequired: string | null  # research node ID needed to craft
craftingRecipe: string | null    # recipe ID to craft this weapon
obtainedFrom:
  - source: string
    details: string
tags: list[string]
modelStyle: string            # art direction note
fireSoundEvent: string        # FMOD event path
reloadSoundEvent: string      # FMOD event path
```

## Entries

```yaml
- itemId: test_rifle
  displayName: Salvage Rifle
  description: A bolt-action rifle assembled from scavenged barrel stock and a filing cabinet spring. Accurate enough at range if you ignore the duct tape holding the stock together.
  slopCommentary: "SLOP recognizes this as a 'manual projectile delivery system.' Safety reminder: always point the loud end away from coworkers. SLOP cannot process workers' compensation claims at this time."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 200
  weaponId: test_rifle
  damage: 25
  fireRate: 2
  range: 50
  damageType: Kinetic
  magazineSize: 12
  reloadTime: 1.5
  rarity: Common
  tier: 1
  weaponType: rifle
  projectileSpeed: 0
  recoil: 0.4
  headshotMultiplier: 2.0
  penetration: 0
  aoeRadius: 0
  statusEffects: []
  ammoType: turret_ammo
  upgradeSlots: 1
  upgradePath: assault_rifle
  researchRequired: null
  craftingRecipe: null
  obtainedFrom:
    - source: starter
      details: Provided at game start during tutorial sequence
  tags:
    - ranged
    - kinetic
    - starter
    - tier_1
  modelStyle: Bolt-action rifle made from pipe stock with a bent filing cabinet spring, wrapped in electrical tape at the grip
  fireSoundEvent: event:/weapons/rifle_fire
  reloadSoundEvent: event:/weapons/rifle_reload

- itemId: salvage_pistol
  displayName: Salvage Pistol
  description: A compact sidearm built around a car door hinge mechanism. Low damage but fast to draw and light enough to carry alongside anything else.
  slopCommentary: "A sidearm! SLOP appreciates your commitment to personal safety. This device is rated for 'light deterrence' against unauthorized biological occupants up to 30 kilograms."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 150
  weaponId: salvage_pistol
  damage: 12
  fireRate: 4
  range: 25
  damageType: Kinetic
  magazineSize: 8
  reloadTime: 1.0
  rarity: Common
  tier: 1
  weaponType: pistol
  projectileSpeed: 0
  recoil: 0.2
  headshotMultiplier: 1.8
  penetration: 0
  aoeRadius: 0
  statusEffects: []
  ammoType: turret_ammo
  upgradeSlots: 1
  upgradePath: null
  researchRequired: null
  craftingRecipe: craft_salvage_pistol
  obtainedFrom:
    - source: crafting
      details: Workbench, 3 iron_ingot + 1 scrap_metal
    - source: salvage
      details: Rare find in security offices within reclaimed buildings
  tags:
    - ranged
    - kinetic
    - sidearm
    - tier_1
  modelStyle: Snub-nosed pistol built from car door hinge and pipe, wooden grip wrapped in paracord
  fireSoundEvent: event:/weapons/pistol_fire
  reloadSoundEvent: event:/weapons/pistol_reload

- itemId: pipe_shotgun
  displayName: Pipe Shotgun
  description: Two lengths of steel pipe, a nail, and a prayer. Devastating at close range, useless beyond spitting distance. Reload after every shot.
  slopCommentary: "SLOP has flagged this device as 'high-risk, high-reward productivity equipment.' Effective operating radius: 8 meters. Effective regret radius: 9 meters."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 120
  weaponId: pipe_shotgun
  damage: 45
  fireRate: 0.8
  range: 12
  damageType: Kinetic
  magazineSize: 1
  reloadTime: 2.5
  rarity: Common
  tier: 1
  weaponType: shotgun
  projectileSpeed: 0
  recoil: 0.85
  headshotMultiplier: 1.3
  penetration: 0
  aoeRadius: 0
  statusEffects: []
  ammoType: turret_ammo
  upgradeSlots: 0
  upgradePath: null
  researchRequired: null
  craftingRecipe: craft_pipe_shotgun
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 iron_ingot + 2 scrap_metal
  tags:
    - ranged
    - kinetic
    - close_range
    - tier_1
  modelStyle: Two nested pipes with a nail firing pin, no stock, wrapped in wire at the breach
  fireSoundEvent: event:/weapons/shotgun_fire
  reloadSoundEvent: event:/weapons/shotgun_reload

- itemId: rebar_club
  displayName: Rebar Club
  description: A length of rebar bent into a handle with a chunk of concrete still attached to the business end. Simple, brutal, and it never runs out of ammo.
  slopCommentary: "SLOP classifies this as an 'ergonomic impact resolution tool.' Please note that repeated percussive contact with unauthorized biological occupants may void your health benefits. Which no longer exist."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 300
  weaponId: rebar_club
  damage: 18
  fireRate: 1.5
  range: 2.5
  damageType: Kinetic
  magazineSize: 0
  reloadTime: 0
  rarity: Common
  tier: 1
  weaponType: melee_blunt
  projectileSpeed: 0
  recoil: 0
  headshotMultiplier: 1.5
  penetration: 0
  aoeRadius: 0
  statusEffects: []
  ammoType: null
  upgradeSlots: 1
  upgradePath: null
  researchRequired: null
  craftingRecipe: craft_rebar_club
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 iron_scrap
    - source: salvage
      details: Common find in construction debris
  tags:
    - melee
    - kinetic
    - blunt
    - starter
    - tier_1
  modelStyle: Bent rebar shaft with a fist-sized concrete chunk still bonded to the striking end, rust-colored
  fireSoundEvent: event:/weapons/melee_swing
  reloadSoundEvent: null

- itemId: arc_welder
  displayName: Arc Welder
  description: A repurposed industrial welding torch that delivers a short-range electrical arc. Drains power cells fast but staggers most fauna on contact.
  slopCommentary: "This tool was originally designed for joining metal seams! SLOP does not endorse its current application, but does note that unauthorized biological occupants are, technically, conductors."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 180
  weaponId: arc_welder
  damage: 22
  fireRate: 2.0
  range: 3.0
  damageType: Fire
  magazineSize: 0
  reloadTime: 0
  rarity: Uncommon
  tier: 1
  weaponType: melee_blade
  projectileSpeed: 0
  recoil: 0
  headshotMultiplier: 1.2
  penetration: 0
  aoeRadius: 0
  statusEffects:
    - effectId: burn
      chance: 0.3
      duration: 4.0
  ammoType: power_cell
  upgradeSlots: 1
  upgradePath: null
  researchRequired: research_electrical_salvage
  craftingRecipe: craft_arc_welder
  obtainedFrom:
    - source: crafting
      details: Workbench, 2 copper_ingot + 1 iron_ingot + 1 power_cell
    - source: salvage
      details: Rare find in machine shop buildings
  tags:
    - melee
    - fire
    - electrical
    - stagger
    - tier_1
  modelStyle: Heavy industrial welding torch with exposed coils and a crackling blue-white arc tip, insulated grip
  fireSoundEvent: event:/weapons/arc_strike
  reloadSoundEvent: null

- itemId: assault_rifle
  displayName: Assault Rifle
  description: A proper semi-automatic rifle assembled from salvaged military parts. Higher fire rate and bigger magazine than the salvage rifle, but harder to build and maintain.
  slopCommentary: "SLOP has upgraded your threat response classification from 'mild inconvenience' to 'moderate concern.' Please continue directing this enhanced productivity toward authorized targets only."
  category: Tool
  isStackable: false
  maxStackSize: 1
  hasDurability: true
  maxDurability: 250
  weaponId: assault_rifle
  damage: 20
  fireRate: 6
  range: 45
  damageType: Kinetic
  magazineSize: 24
  reloadTime: 2.0
  rarity: Uncommon
  tier: 2
  weaponType: rifle
  projectileSpeed: 0
  recoil: 0.35
  headshotMultiplier: 2.0
  penetration: 1
  aoeRadius: 0
  statusEffects: []
  ammoType: turret_ammo
  upgradeSlots: 2
  upgradePath: null
  researchRequired: research_advanced_weapons
  craftingRecipe: craft_assault_rifle
  obtainedFrom:
    - source: crafting
      details: Workbench, 4 steel_ingot + 2 copper_ingot + 1 scrap_metal
  tags:
    - ranged
    - kinetic
    - automatic
    - tier_2
  modelStyle: Boxy semi-auto rifle with stamped steel receiver, mismatched military parts, and a makeshift rail on top
  fireSoundEvent: event:/weapons/assault_fire
  reloadSoundEvent: event:/weapons/assault_reload
```
