# Wave events

Fauna attack waves that assault the factory at escalating intensity. Each wave definition specifies enemy composition, spawn timing, and threat tier. Maps to `WaveDefinition` in code for the core timing fields (enemyCount, spawnDelay, timeBetweenWaves, faunaIds). Design fields here extend the definition with directional spawning, boss flags, and flavor.

## Schema

```yaml
# Core fields (match WaveDefinition)
enemyCount: int                           # total enemies spawned in this wave
spawnDelay: float                         # seconds between individual enemy spawns
timeBetweenWaves: float                   # seconds of peace after this wave before the next
faunaIds: list                            # string IDs of fauna types in the spawn pool

# Design fields (bible-only)
waveId: string                            # unique snake_case identifier
displayName: string                       # player-facing name (shown on HUD warning)
description: string                       # design description
slopCommentary: string                    # in-character SLOP quote (broadcast on wave start)
threatTier: int                           # difficulty tier (1 = early, 2 = mid, 3 = late)
spawnDirections: list                     # enum values: north, south, east, west, multi
bossWave: bool                            # true if wave contains a boss-class fauna
lootBonus: float                          # multiplier on loot drop chance (1.0 = normal)
tags: list                                # lowercase string tags
```

## Entries

```yaml
- waveId: scout_probe
  displayName: "Scout probe"
  enemyCount: 5
  spawnDelay: 2.0
  timeBetweenWaves: 90.0
  faunaIds:
    - grunt
  threatTier: 1
  spawnDirections:
    - south
  bossWave: false
  lootBonus: 1.0
  slopCommentary: "Attention employees: a small group of unauthorized biological occupants has been detected approaching from the south. SLOP classifies this as a routine perimeter test. Please ensure your defenses are active and your insurance paperwork is current."
  description: "The introductory wave. Five basic grunts approach from a single direction at a leisurely pace. Designed to teach players that waves happen and defenses matter, without punishing unpreparedness too hard. Long cooldown before the next wave gives time to build."
  tags: [wave, tier_1, tutorial, single_direction, grunts, early_game]

- waveId: raiding_party
  displayName: "Raiding party"
  enemyCount: 12
  spawnDelay: 1.5
  timeBetweenWaves: 75.0
  faunaIds:
    - grunt
    - pack_runner
  threatTier: 1
  spawnDirections:
    - south
    - east
  bossWave: false
  lootBonus: 1.0
  slopCommentary: "A mixed group of unauthorized biological occupants has been detected on two approach vectors. SLOP recommends distributing your perimeter compliance resources accordingly. Remember: a turret facing the wrong direction is just expensive furniture."
  description: "First real test. Mixed grunts and fast runners from two directions. Players who only defended one side will take hits. Teaches the lesson that multi-directional defense matters before the stakes get higher."
  tags: [wave, tier_1, mixed, two_directions, early_game, teaching_moment]

- waveId: coordinated_assault
  displayName: "Coordinated assault"
  enemyCount: 20
  spawnDelay: 1.0
  timeBetweenWaves: 60.0
  faunaIds:
    - grunt
    - pack_runner
    - spitter
  threatTier: 2
  spawnDirections:
    - north
    - south
    - east
  bossWave: false
  lootBonus: 1.2
  slopCommentary: "Multiple unauthorized biological occupant groups are converging on your facility from three directions simultaneously. SLOP's Behavioral Analysis Division notes this level of coordination is 'concerning.' Your turrets have been notified."
  description: "Three-direction attack with ranged spitters mixed in. Spitters hang back and fire acid, forcing players to either push out and engage or accept sustained damage on walls. Turret placement and kill zone design are tested here."
  tags: [wave, tier_2, ranged, three_directions, spitters, mid_game, tactical]

- waveId: swarm
  displayName: "Swarm"
  enemyCount: 35
  spawnDelay: 0.4
  timeBetweenWaves: 60.0
  faunaIds:
    - pack_runner
    - pack_runner
  threatTier: 2
  spawnDirections:
    - multi
  bossWave: false
  lootBonus: 1.3
  slopCommentary: "An unusually large population of unauthorized biological occupants is approaching from all directions at high velocity. SLOP's statistical models did not predict this. SLOP's statistical models have been fired."
  description: "Overwhelming numbers of fast, low-health enemies from every direction. Individual enemies are weak but the sheer volume overwhelms single turrets. Tests whether the player has built distributed defense coverage and has enough ammo throughput. The ammo-economy check wave."
  tags: [wave, tier_2, swarm, fast, all_directions, ammo_test, mid_game]

- waveId: siege
  displayName: "Siege"
  enemyCount: 18
  spawnDelay: 2.5
  timeBetweenWaves: 45.0
  faunaIds:
    - biomech_hybrid
    - spitter
    - spore_crawler
  threatTier: 3
  spawnDirections:
    - south
    - west
  bossWave: false
  lootBonus: 1.5
  slopCommentary: "Heavy-class unauthorized biological occupants have been detected. These units exceed the structural tolerance ratings of standard perimeter elements. SLOP recommends reinforced walls, concentrated turret fire, and a positive attitude."
  description: "Slow but devastating. Heavy grunts absorb enormous amounts of turret fire while spitters and acid crawlers deal sustained damage from range. Walls crumble if not reinforced. Tests late-game defenses: steel walls, high-DPS turrets, and ammo supply lines that can sustain prolonged engagement."
  tags: [wave, tier_3, heavy, siege, tank, late_game, wall_breaker]

- waveId: apex_emergence
  displayName: "Apex emergence"
  enemyCount: 25
  spawnDelay: 1.5
  timeBetweenWaves: 120.0
  faunaIds:
    - grunt
    - biomech_hybrid
    - spitter
    - hive_queen
  threatTier: 3
  spawnDirections:
    - multi
  bossWave: true
  lootBonus: 2.0
  slopCommentary: "ATTENTION: an apex-class unauthorized biological occupant has been detected. This entity exceeds all previously observed threat parameters. SLOP's official recommendation is... one moment... SLOP's official recommendation is to survive. End of message."
  description: "Boss wave. The apex predator is a massive high-health, high-damage fauna that smashes through walls and ignores barbed wire. Supported by a mixed escort of grunts, heavies, and spitters from all sides. Defeating the apex drops rare loot and grants a long peace period. The culmination of the wave defense system."
  tags: [wave, tier_3, boss, apex, all_directions, rare_loot, late_game, climax]
```
