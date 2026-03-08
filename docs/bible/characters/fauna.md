# Fauna

Mutated creatures that inhabit the overworld and reclaimed buildings. Each species evolved in response to the specific industrial environment it nests in — chemical facilities produced spore-based organisms, heavy manufacturing created biomechanical hybrids, warehouses bred pack hunters, and power generation infrastructure attracted apex predators. All fauna stats map directly to `FaunaDefinitionSO` fields, extended with design fields for loot, spawning, and lore.

## Schema

```yaml
# Core fields map to FaunaDefinitionSO (camelCase matches code exactly)
faunaId: string               # snake_case unique identifier
displayName: string           # human-readable name
description: string           # in-game codex text
slopCommentary: string        # SLOP's in-character classification

# FaunaDefinitionSO fields
maxHealth: float              # hit points (default 100)
moveSpeed: float              # units per second (default 3.5)
attackDamage: float           # damage per hit (default 10)
attackRange: float            # melee/ranged reach in units (default 2)
attackCooldown: float         # seconds between attacks (default 1)
sightRange: float             # detection distance in units (default 15)
sightAngle: float             # field of view in degrees (default 120)
hearingRange: float           # sound detection radius in units (default 8)
attackDamageType: DamageType  # Kinetic | Explosive | Fire | Toxic
alertRange: float             # pack alert broadcast radius (default 20)
strafeSpeed: float            # lateral movement speed in combat (default 2.5)
strafeRadius: float           # distance maintained during strafing (default 3)
baseBravery: float            # morale baseline 0.0-1.0 (default 0.5)
fleeConfidenceThreshold: float  # flee when confidence drops below this (default 0.3)
coverSearchRadius: float      # how far to search for cover when fleeing (default 10)

# Design fields (not yet in SO)
faunaType: enum [surface, interior, boss, pack, apex]
biomeAffinity: list[string]   # biomeId references to world/biomes.md
tier: int                     # difficulty tier (1 = starter, 4 = endgame)
rarity: LootRarity            # Common | Uncommon | Rare | Epic | Legendary
lootDrops: list               # what this fauna drops on death
  # - itemId: string          # reference to items/*.md
  #   dropChance: float       # 0.0-1.0 probability
  #   minAmount: int
  #   maxAmount: int
statusEffectsApplied: list    # status effects inflicted on hit
  # - effectId: string        # reference to systems/status-effects.md
  #   chance: float           # 0.0-1.0 probability per hit
packSize: int                 # typical group count (1 for solo creatures)
spawnConditions: string       # where and when this fauna appears
copy_from: string | null      # faunaId of parent entry for inheritance
tags: list[string]
modelStyle: string            # art direction note
soundEvents: string           # FMOD event path prefix
```

## Entries

```yaml
# --- Tier 1: surface grunts and basic threats ---

- faunaId: grunt
  displayName: Grunt
  description: The most common fauna in the wastelands. Hunched bipedal creatures with thick callused skin and blunt bone protrusions used for bashing. Not smart, not fast, but they travel in small groups and their alerting calls bring reinforcements. The baseline threat that every player learns to handle in their first hour.
  slopCommentary: "SLOP has classified this entity as an 'unauthorized biological occupant, standard model.' They appear to have developed a rudimentary social structure based on hitting things and screaming. SLOP finds this relatable."
  maxHealth: 80.0
  moveSpeed: 3.0
  attackDamage: 10.0
  attackRange: 2.0
  attackCooldown: 1.2
  sightRange: 12.0
  sightAngle: 120.0
  hearingRange: 8.0
  attackDamageType: Kinetic
  alertRange: 15.0
  strafeSpeed: 1.5
  strafeRadius: 2.5
  baseBravery: 0.4
  fleeConfidenceThreshold: 0.3
  coverSearchRadius: 8.0
  faunaType: surface
  biomeAffinity:
    - Grassland
    - Wasteland
    - Ruins
  tier: 1
  rarity: Common
  lootDrops:
    - itemId: organic_matter
      dropChance: 0.6
      minAmount: 1
      maxAmount: 2
    - itemId: scrap_metal
      dropChance: 0.2
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied: []
  packSize: 3
  spawnConditions: Surface spawns in open terrain during all hours. Most common fauna type. Spawn rate increases near ruins and building perimeters.
  copy_from: null
  tags:
    - surface
    - melee
    - pack
    - tier_1
    - common
  modelStyle: Hunched humanoid silhouette with oversized forearms, thick grey-brown skin, bone ridge along spine and knuckles. Mouth is a vertical slit. No visible eyes — echolocation bumps on forehead.
  soundEvents: event:/sfx/fauna/grunt

- faunaId: spitter
  displayName: Spitter
  description: Ranged fauna variant that launches globs of corrosive acid from a specialized throat sac. Thin-bodied and fragile compared to grunts, spitters maintain distance and let the acid do the work. The acid lingers on surfaces briefly, creating temporary hazard zones during combat.
  slopCommentary: "This biological unit has developed a projectile capability that SLOP's ballistics module rates as 'impressively disgusting.' The acidic compound achieves a pH of 1.2, which would dissolve SLOP's exterior casing in approximately 47 minutes. Not that SLOP has calculated this. Not that SLOP is worried."
  maxHealth: 50.0
  moveSpeed: 3.5
  attackDamage: 8.0
  attackRange: 12.0
  attackCooldown: 2.0
  sightRange: 18.0
  sightAngle: 90.0
  hearingRange: 10.0
  attackDamageType: Toxic
  alertRange: 15.0
  strafeSpeed: 2.5
  strafeRadius: 8.0
  baseBravery: 0.25
  fleeConfidenceThreshold: 0.4
  coverSearchRadius: 12.0
  faunaType: surface
  biomeAffinity:
    - Grassland
    - Wasteland
    - Swamp
  tier: 1
  rarity: Common
  lootDrops:
    - itemId: chemicals
      dropChance: 0.35
      minAmount: 1
      maxAmount: 1
    - itemId: organic_matter
      dropChance: 0.5
      minAmount: 1
      maxAmount: 2
  statusEffectsApplied:
    - effectId: corrosion
      chance: 0.3
  packSize: 2
  spawnConditions: Surface spawns, often mixed in with grunt groups as ranged support. More common near chemical-rich biomes. Prefers to spawn near cover they can retreat behind.
  copy_from: null
  tags:
    - surface
    - ranged
    - toxic
    - tier_1
    - fragile
  modelStyle: Elongated neck with inflatable throat sac, lean body with digitigrade legs. Mottled green-yellow skin with visible veins carrying luminescent acid. Small forearms, large hind legs for backpedaling.
  soundEvents: event:/sfx/fauna/spitter

# --- Tier 2: specialists and pack threats ---

- faunaId: pack_runner
  displayName: Pack runner
  description: Fast, coordinated pack hunters that originated in the warehouse district. They use ultrasonic calls to coordinate flanking maneuvers and will attempt to surround isolated targets before attacking from multiple angles. Individually weak, but a full pack of six to eight can overwhelm even well-armed players through sheer action economy.
  slopCommentary: "SLOP has observed that these units operate with a level of teamwork that exceeds the pre-collapse workforce by a considerable margin. Their coordinated hunting strategies achieve an efficiency rating of 'disturbingly competent.' SLOP has considered recruiting them but cannot locate the appropriate onboarding forms."
  maxHealth: 60.0
  moveSpeed: 5.5
  attackDamage: 8.0
  attackRange: 1.5
  attackCooldown: 0.7
  sightRange: 20.0
  sightAngle: 160.0
  hearingRange: 15.0
  attackDamageType: Kinetic
  alertRange: 25.0
  strafeSpeed: 4.0
  strafeRadius: 4.0
  baseBravery: 0.6
  fleeConfidenceThreshold: 0.2
  coverSearchRadius: 6.0
  faunaType: pack
  biomeAffinity:
    - Grassland
    - Ruins
  tier: 2
  rarity: Uncommon
  lootDrops:
    - itemId: organic_matter
      dropChance: 0.5
      minAmount: 1
      maxAmount: 2
    - itemId: iron_scrap
      dropChance: 0.15
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied:
    - effectId: bleeding
      chance: 0.2
  packSize: 6
  spawnConditions: Warehouse buildings and open Grassland terrain. Always spawn in full packs. Scouts appear first on elevated positions before the main group engages. Night spawns increase pack size by 2.
  copy_from: null
  tags:
    - pack
    - fast
    - flanking
    - warehouse
    - tier_2
    - coordinated
  modelStyle: Lean quadrupedal body like a greyhound crossed with a lizard. Smooth dark skin with warehouse barcode tattoo patterns. Wide-set eyes with excellent peripheral vision. Retractable claws on all four feet.
  soundEvents: event:/sfx/fauna/pack_runner

- faunaId: stalker
  displayName: Stalker
  description: Ambush predator that haunts building interiors and dense forest. Near-invisible when stationary against dark surfaces, the stalker waits for prey to pass within striking distance before delivering a devastating first attack. If the ambush fails, it disengages rapidly and repositions for another attempt rather than standing and fighting.
  slopCommentary: "SLOP's motion sensors have difficulty tracking this entity, which SLOP finds personally offensive. It appears to have evolved active camouflage by incorporating the facility's dust and debris into its integument. SLOP recommends maintaining situational awareness at all times, which is advice SLOP gives for everything because it sounds professional."
  maxHealth: 90.0
  moveSpeed: 4.0
  attackDamage: 25.0
  attackRange: 2.5
  attackCooldown: 2.5
  sightRange: 18.0
  sightAngle: 80.0
  hearingRange: 20.0
  attackDamageType: Kinetic
  alertRange: 10.0
  strafeSpeed: 3.5
  strafeRadius: 5.0
  baseBravery: 0.3
  fleeConfidenceThreshold: 0.5
  coverSearchRadius: 15.0
  faunaType: interior
  biomeAffinity:
    - Forest
    - Ruins
    - OvergrownRuins
    - Swamp
  tier: 2
  rarity: Uncommon
  lootDrops:
    - itemId: organic_matter
      dropChance: 0.4
      minAmount: 1
      maxAmount: 3
    - itemId: chemicals
      dropChance: 0.2
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied:
    - effectId: bleeding
      chance: 0.5
  packSize: 1
  spawnConditions: Interior spawns in buildings and dense forest biomes. Always spawns near cover or dark corners. Prefers elevated positions — catwalks, shelving tops, ceiling pipes. Never spawns in open terrain.
  copy_from: null
  tags:
    - interior
    - ambush
    - stealth
    - solo
    - tier_2
    - high_alpha_damage
  modelStyle: Flattened body profile, long limbs with adhesive pads for wall-clinging. Skin shifts color to match surroundings (grey concrete, brown rust, dark green foliage). Oversized serrated claws on forelimbs. Single large eye with excellent low-light vision.
  soundEvents: event:/sfx/fauna/stalker

# --- Tier 3: building specialists ---

- faunaId: spore_crawler
  displayName: Spore crawler
  description: Fungal organisms that evolved in the chemical-saturated environments of water treatment plants and overgrown ruins. They move slowly but trail toxic spore clouds behind them, and their melee attacks inject a concentrated dose of mutagen that causes progressive toxic damage. Killing one releases a final burst of spores from the ruptured body.
  slopCommentary: "SLOP has classified this organism as a 'mobile atmospheric enhancement unit.' It produces a steady output of biological particulates that SLOP's air quality standards describe as 'beyond measurement.' The organism appears to be 40% fungus, 30% industrial chemical, and 30% bad decisions. SLOP did not make those decisions. SLOP makes excellent decisions."
  maxHealth: 120.0
  moveSpeed: 2.0
  attackDamage: 12.0
  attackRange: 2.0
  attackCooldown: 1.5
  sightRange: 10.0
  sightAngle: 360.0
  hearingRange: 6.0
  attackDamageType: Toxic
  alertRange: 12.0
  strafeSpeed: 1.0
  strafeRadius: 2.0
  baseBravery: 0.7
  fleeConfidenceThreshold: 0.15
  coverSearchRadius: 5.0
  faunaType: interior
  biomeAffinity:
    - Swamp
    - Forest
    - OvergrownRuins
  tier: 3
  rarity: Uncommon
  lootDrops:
    - itemId: chemicals
      dropChance: 0.7
      minAmount: 1
      maxAmount: 3
    - itemId: organic_matter
      dropChance: 0.8
      minAmount: 2
      maxAmount: 4
    - itemId: sulfur
      dropChance: 0.15
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied:
    - effectId: toxicity
      chance: 0.6
    - effectId: corrosion
      chance: 0.3
  packSize: 2
  spawnConditions: Chemical buildings (water treatment, electronics lab with fungal growth). Interior spawns only. Nest in ventilation systems and filtration tanks. Death explosion releases spore cloud in 3m radius.
  copy_from: null
  tags:
    - interior
    - toxic
    - fungal
    - slow
    - tier_3
    - death_explosion
    - chemical_building
  modelStyle: Bulbous mushroom-cap body on four stubby legs. Surface covered in pulsing bioluminescent pustules that release spore puffs. Tendrils trail behind leaving a faint toxic mist. Body splits open on death revealing a hollow spore cavity.
  soundEvents: event:/sfx/fauna/spore_crawler

- faunaId: biomech_hybrid
  displayName: Biomechanical hybrid
  description: The most unsettling fauna in the complex. These creatures have partially fused with abandoned industrial machinery, incorporating metal plating, wiring, and mechanical components into their biology. Found exclusively in heavy manufacturing buildings where electromagnetic fields and molten metal created the conditions for this fusion. Fast, armored, and capable of using integrated tooling as weapons.
  slopCommentary: "SLOP notes that this entity has achieved a human-machine integration ratio that the pre-collapse R&D department only theorized about. SLOP does not endorse unauthorized biomechanical augmentation, but must acknowledge the results are — from a purely engineering perspective — remarkable. The drill-arm is particularly well-calibrated."
  maxHealth: 180.0
  moveSpeed: 4.5
  attackDamage: 20.0
  attackRange: 2.5
  attackCooldown: 1.0
  sightRange: 15.0
  sightAngle: 100.0
  hearingRange: 12.0
  attackDamageType: Kinetic
  alertRange: 18.0
  strafeSpeed: 3.0
  strafeRadius: 3.5
  baseBravery: 0.75
  fleeConfidenceThreshold: 0.15
  coverSearchRadius: 8.0
  faunaType: interior
  biomeAffinity:
    - Ruins
    - OvergrownRuins
  tier: 3
  rarity: Rare
  lootDrops:
    - itemId: scrap_metal
      dropChance: 0.9
      minAmount: 2
      maxAmount: 5
    - itemId: iron_scrap
      dropChance: 0.7
      minAmount: 1
      maxAmount: 3
    - itemId: copper_ingot
      dropChance: 0.15
      minAmount: 1
      maxAmount: 1
    - itemId: iron_ingot
      dropChance: 0.2
      minAmount: 1
      maxAmount: 2
  statusEffectsApplied:
    - effectId: bleeding
      chance: 0.4
  packSize: 1
  spawnConditions: Heavy manufacturing buildings (foundry, machine shop). Interior only. Nest near active or semi-active machinery. Electromagnetic interference causes HUD static when one is nearby — serves as early warning.
  copy_from: null
  tags:
    - interior
    - armored
    - fast
    - biomechanical
    - tier_3
    - manufacturing_building
    - scrap_rich_loot
  modelStyle: Humanoid torso fused with industrial machinery. One arm replaced by a pneumatic drill or press, the other retains biological claws reinforced with metal shards. Legs are a mix of corroded hydraulic pistons and muscle. Exposed wiring pulses with bioluminescent current. Welding mask skull fused to the face.
  soundEvents: event:/sfx/fauna/biomech_hybrid

# --- Tier 4: apex predators ---

- faunaId: hive_queen
  displayName: Hive queen
  description: Apex predator found in power generation buildings where massive electromagnetic fields created the conditions for a colonial organism. The queen is the command node for a network of smaller drones that she spawns during combat. She rarely engages directly until her drone screen is destroyed, at which point she becomes extremely aggressive. Territorial — she will not leave her building.
  slopCommentary: "SLOP's ecological survey classifies this entity as a 'senior unauthorized biological occupant with delegation authority.' She has established a management hierarchy that SLOP grudgingly respects. Her quarterly output of subsidiary organisms exceeds the pre-collapse HR department's hiring rate by 400%. SLOP has considered offering her a consulting position."
  maxHealth: 400.0
  moveSpeed: 2.5
  attackDamage: 35.0
  attackRange: 3.0
  attackCooldown: 1.5
  sightRange: 25.0
  sightAngle: 360.0
  hearingRange: 30.0
  attackDamageType: Toxic
  alertRange: 40.0
  strafeSpeed: 1.5
  strafeRadius: 5.0
  baseBravery: 0.95
  fleeConfidenceThreshold: 0.05
  coverSearchRadius: 3.0
  faunaType: apex
  biomeAffinity:
    - OvergrownRuins
  tier: 4
  rarity: Epic
  lootDrops:
    - itemId: power_cell
      dropChance: 1.0
      minAmount: 1
      maxAmount: 2
    - itemId: chemicals
      dropChance: 0.8
      minAmount: 3
      maxAmount: 6
    - itemId: organic_matter
      dropChance: 1.0
      minAmount: 5
      maxAmount: 10
    - itemId: quartz
      dropChance: 0.3
      minAmount: 1
      maxAmount: 2
  statusEffectsApplied:
    - effectId: toxicity
      chance: 0.7
    - effectId: corrosion
      chance: 0.5
  packSize: 1
  spawnConditions: Power plant buildings only. One queen per building. Spawns in the central turbine hall or generator room. Accompanied by 4-6 grunt-tier drones that she replenishes during combat (one drone every 10 seconds, max 6 active).
  copy_from: null
  tags:
    - apex
    - boss_adjacent
    - territorial
    - drone_spawner
    - tier_4
    - power_building
    - endgame
  modelStyle: Massive insectoid body (3x player height) with a glowing thorax that pulses with electromagnetic energy. Crown of antenna-like protrusions that crackle with static. Armored carapace with a metallic sheen. Ovipositor on the abdomen produces drone eggs. Wings vestigial — she does not fly but uses them for threat display.
  soundEvents: event:/sfx/fauna/hive_queen

- faunaId: tunnel_worm
  displayName: Tunnel worm
  description: Rare overworld predator that burrows beneath the surface and erupts under its target. The initial emergence deals heavy area damage and the worm's thrashing body creates temporary terrain disruption. After surfacing, it fights above ground briefly before re-burrowing to reposition. Encountered only in mineral-rich terrain where the soil is loose enough for rapid tunneling.
  slopCommentary: "SLOP's seismographic sensors detect subsurface movement consistent with a biological tunneling entity of considerable mass. SLOP recommends standing very still, as the entity appears to track prey through ground vibration. Alternatively, SLOP recommends running, as standing still did not work for the last three employees who tried it."
  maxHealth: 350.0
  moveSpeed: 6.0
  attackDamage: 40.0
  attackRange: 4.0
  attackCooldown: 3.0
  sightRange: 5.0
  sightAngle: 360.0
  hearingRange: 35.0
  attackDamageType: Kinetic
  alertRange: 0.0
  strafeSpeed: 0.0
  strafeRadius: 0.0
  baseBravery: 0.8
  fleeConfidenceThreshold: 0.1
  coverSearchRadius: 0.0
  faunaType: apex
  biomeAffinity:
    - Wasteland
  tier: 4
  rarity: Rare
  lootDrops:
    - itemId: iron_ore
      dropChance: 1.0
      minAmount: 5
      maxAmount: 10
    - itemId: sulfur
      dropChance: 0.6
      minAmount: 2
      maxAmount: 5
    - itemId: coal
      dropChance: 0.5
      minAmount: 3
      maxAmount: 6
    - itemId: quartz
      dropChance: 0.25
      minAmount: 1
      maxAmount: 3
  statusEffectsApplied: []
  packSize: 1
  spawnConditions: Wasteland biome only. Rare spawn triggered by extended player presence in a hex (60+ seconds). Seismic tremor visual and audio warning 3 seconds before emergence. Surfaces, fights for 15 seconds, then re-burrows. Repeats until killed or player leaves the hex.
  copy_from: null
  tags:
    - apex
    - burrowing
    - rare
    - overworld
    - tier_4
    - aoe_emergence
    - seismic_warning
  modelStyle: Massive segmented worm body (5m length visible above ground) with concentric rings of grinding teeth at the mouth. Armored chitinous plates along the dorsal side, softer ventral surface. Dust and rock debris constantly falling from the body. Bioluminescent lateral lines pulse when attacking.
  soundEvents: event:/sfx/fauna/tunnel_worm

# --- Boss tier ---

- faunaId: tower_boss
  displayName: The warden
  description: The apex entity guarding the central tower — the final barrier between the player and the truth about SLOP. This creature has been exposed to the tower's concentrated electromagnetic output for so long that it has become something beyond biological or mechanical categorization. It is the tower's immune system made flesh and metal.
  slopCommentary: "SLOP has no records of this entity. SLOP does not know what it is. SLOP does not know how it got here. SLOP would very much like you to not engage with it and instead return to your assigned work duties. SLOP is experiencing an error in its emotional regulation module. Please disregard any trembling in SLOP's audio output."
  maxHealth: 300.0
  moveSpeed: 2.5
  attackDamage: 25.0
  attackRange: 3.5
  attackCooldown: 1.8
  sightRange: 30.0
  sightAngle: 360.0
  hearingRange: 40.0
  attackDamageType: Kinetic
  alertRange: 50.0
  strafeSpeed: 2.0
  strafeRadius: 4.0
  baseBravery: 1.0
  fleeConfidenceThreshold: 0.0
  coverSearchRadius: 0.0
  faunaType: boss
  biomeAffinity:
    - Ruins
  tier: 5
  rarity: Legendary
  lootDrops:
    - itemId: key_fragment
      dropChance: 1.0
      minAmount: 1
      maxAmount: 1
    - itemId: boss_blueprint
      dropChance: 1.0
      minAmount: 1
      maxAmount: 1
    - itemId: power_cell
      dropChance: 1.0
      minAmount: 2
      maxAmount: 3
    - itemId: reinforced_plating
      dropChance: 0.5
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied:
    - effectId: bleeding
      chance: 0.6
    - effectId: corrosion
      chance: 0.4
  packSize: 1
  spawnConditions: Tower interior only. One per tower clear attempt. Spawns at the top floor when players reach the final chamber. Multi-phase fight with environmental mechanics tied to the tower's power systems. Cannot flee. Enrages below 30% health (attack speed doubles).
  copy_from: null
  tags:
    - boss
    - tower
    - unique
    - tier_5
    - multi_phase
    - narrative_critical
    - endgame
  modelStyle: Towering humanoid frame (2.5x player height) assembled from fused server rack panels and biological tissue. One arm is a massive blade of sharpened circuit boards, the other ends in a cluster of cables that whip like tentacles. Head is a cracked monitor displaying corrupted SLOP interface fragments. Chest cavity glows with the same blue as SLOP terminal screens. Moves with a stuttering, frame-skip animation as if reality can't quite render it properly.
  soundEvents: event:/sfx/fauna/tower_boss

# --- Variant: hive drone (spawned by hive_queen, not an independent spawn) ---

- faunaId: hive_drone
  displayName: Hive drone
  description: Small, fast units spawned by the hive queen during combat. Individually fragile but produced in numbers. They exist only to screen the queen and harass players, buying time for the queen to reposition or heal.
  slopCommentary: "SLOP classifies these as 'temporary contract workers.' Their employment terms are brief and their severance package is instantaneous."
  copy_from: grunt
  maxHealth: 30.0
  moveSpeed: 4.5
  attackDamage: 5.0
  attackRange: 1.5
  attackCooldown: 0.8
  sightRange: 20.0
  baseBravery: 0.9
  fleeConfidenceThreshold: 0.0
  faunaType: pack
  biomeAffinity:
    - OvergrownRuins
  tier: 4
  rarity: Common
  lootDrops:
    - itemId: organic_matter
      dropChance: 0.3
      minAmount: 1
      maxAmount: 1
  statusEffectsApplied: []
  packSize: 6
  spawnConditions: Spawned by hive_queen during combat only. Not an independent spawn. One drone every 10 seconds, maximum 6 active simultaneously. Dies if queen is killed.
  tags:
    - pack
    - spawned
    - disposable
    - tier_4
    - queen_dependent
  modelStyle: Miniature version of the queen (knee-height to player). Translucent carapace showing internal luminescent organs. Vestigial wings that buzz but don't provide flight. Mandibles only — no ranged attack.
  soundEvents: event:/sfx/fauna/hive_drone
```
