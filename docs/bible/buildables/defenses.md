# Defenses

Defensive structures that protect the factory from fauna waves. Turret-type defenses have targeting AI, consume ammo and power, and map to `TurretDefinitionSO`. Passive defenses (spike walls, mines, barbed wire) use a simpler schema with no targeting or ammo -- they deal damage on contact or proximity.

## Schema

```yaml
# === Turret sub-schema (matches TurretDefinitionSO) ===

turretId: string                          # unique snake_case identifier
displayName: string                       # player-facing name
defenseType: turret                       # discriminator for sub-schema
range: float                              # detection and firing range in meters
fireInterval: float                       # seconds between shots
damagePerShot: float                      # damage per projectile
damageType: enum [Kinetic, Explosive, Fire, Toxic]
ammoItemId: string                        # item ID consumed per shot
powerConsumption: float                   # watts required while active
size: [int, int]                          # grid footprint [width, depth]
powerThreshold: float                     # 0-1, minimum power ratio to fire
ammoSlotCount: int                        # internal ammo storage slots
ammoMaxStackSize: int                     # max stack per ammo slot
targetingMode: enum [Closest, LowestHealth, HighestThreat]

# Port definitions (match MachinePort struct)
ports:
  - localOffset: [int, int]
    direction: enum [north, south, east, west]
    type: enum [Input, Output]
    filter: string | null
    throughput: float

# Design fields (bible-only)
slopCommentary: string
description: string
tier: int
tags: list
health: float
repairMaterial: string
researchRequired: string | null
craftingRecipe: string | null
upgradePath: string | null
portOwnerType: enum [Machine, Storage, Belt, Turret]  # always Turret
modelStyle: string
firingSound: string
idleSound: string

# === Passive defense sub-schema (no existing SO) ===

defenseId: string                         # unique snake_case identifier
displayName: string
defenseType: passive                      # discriminator for sub-schema
damageOnContact: float                    # damage dealt to fauna touching this
damageType: enum [Kinetic, Explosive, Fire, Toxic]
slowFactor: float                         # 0-1, movement speed multiplier on contact (1 = no slow)
size: [int, int]                          # grid footprint
isConsumable: bool                        # true = destroyed after triggering (e.g. landmine)
triggerRadius: float                      # detection radius in meters (0 = contact only)
slopCommentary: string
description: string
tier: int
tags: list
health: float                             # 0 for single-use consumables
repairMaterial: string | null
researchRequired: string | null
craftingRecipe: string | null
modelStyle: string
```

## Entries

```yaml
# --- Turret-type defenses ---

- turretId: auto_turret_t1
  displayName: "Auto-turret mk.I"
  defenseType: turret
  range: 20.0
  fireInterval: 0.5
  damagePerShot: 10.0
  damageType: Kinetic
  ammoItemId: turret_ammo
  powerConsumption: 50.0
  size: [1, 1]
  powerThreshold: 0.5
  ammoSlotCount: 1
  ammoMaxStackSize: 64
  targetingMode: Closest
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: turret_ammo
      throughput: 1.0
  slopCommentary: "The SLOP Automated Perimeter Compliance Officer ensures unauthorized biological occupants receive a firm but fair kinetic reminder to vacate the premises."
  description: "Standard kinetic turret. Tracks the nearest target within 20m and fires light rounds at a high rate. Requires turret_ammo fed via belt and a minimum of 50% power. The backbone of any factory defense line."
  tier: 1
  tags: [defense, turret, kinetic, automated, starter]
  health: 400.0
  repairMaterial: iron_plate
  researchRequired: defensive_systems
  craftingRecipe: craft_turret
  upgradePath: auto_turret_t2
  portOwnerType: Turret
  modelStyle: "swiveling barrel on a tripod mount, ammo belt feeding from base, worn military olive paint"
  firingSound: "event:/combat/turret/fire"
  idleSound: "event:/combat/turret/idle"

- turretId: flamethrower_turret_t1
  displayName: "Flamethrower turret"
  defenseType: turret
  range: 10.0
  fireInterval: 0.1
  damagePerShot: 3.0
  damageType: Fire
  ammoItemId: fuel_canister
  powerConsumption: 40.0
  size: [1, 1]
  powerThreshold: 0.3
  ammoSlotCount: 1
  ammoMaxStackSize: 16
  targetingMode: Closest
  ports:
    - localOffset: [0, 0]
      direction: south
      type: Input
      filter: fuel_canister
      throughput: 0.5
  slopCommentary: "The Thermal Hospitality Dispenser provides a warm welcome to any visitors who failed to check in at reception. Short range for that personal touch."
  description: "Short-range area denial turret. Sprays fire in a cone, hitting all targets in the arc. Burns through fuel canisters fast but excels against swarm waves. Low power draw."
  tier: 2
  tags: [defense, turret, fire, area_denial, close_range]
  health: 300.0
  repairMaterial: iron_plate
  researchRequired: weapons_research
  craftingRecipe: craft_flamethrower_turret
  upgradePath: null
  portOwnerType: Turret
  modelStyle: "squat cylinder with wide nozzle, fuel line coiling down from base, heat shimmer effect"
  firingSound: "event:/combat/turret/flamethrower"
  idleSound: "event:/combat/turret/idle"

# --- Passive defenses ---

- defenseId: spike_wall
  displayName: "Spike wall"
  defenseType: passive
  damageOnContact: 15.0
  damageType: Kinetic
  slowFactor: 0.5
  size: [1, 1]
  isConsumable: false
  triggerRadius: 0.0
  slopCommentary: "The Decorative Perimeter Enhancement adds rustic charm to your workspace while gently discouraging unauthorized entry. Any resemblance to a medieval torture device is purely coincidental."
  description: "Sharpened metal spikes mounted on a wall frame. Deals kinetic damage and slows fauna that walk into it. Cheap to build, degrades over time from impacts. Place in chokepoints to funnel enemies into turret kill zones."
  tier: 1
  tags: [defense, passive, contact_damage, slow, cheap]
  health: 200.0
  repairMaterial: scrap_metal
  researchRequired: null
  craftingRecipe: craft_spike_wall
  modelStyle: "welded rebar spikes on a corrugated metal frame, rust and dried stains"

- defenseId: barbed_wire
  displayName: "Barbed wire"
  defenseType: passive
  damageOnContact: 5.0
  damageType: Kinetic
  slowFactor: 0.3
  size: [1, 1]
  isConsumable: false
  triggerRadius: 0.0
  slopCommentary: "The Personnel Flow Management System ensures all visitors proceed at a measured, thoughtful pace through designated areas. Clothing damage is not covered by SLOP liability policies."
  description: "Low coils of barbed wire spread across a cell. Deals minor damage but severely slows anything moving through it. Stacks with other slowing effects. Extremely cheap and fast to deploy."
  tier: 1
  tags: [defense, passive, contact_damage, slow, area_denial, cheap]
  health: 100.0
  repairMaterial: scrap_metal
  researchRequired: null
  craftingRecipe: craft_barbed_wire
  modelStyle: "tangled coils of rusted wire on low stakes, scraps of cloth caught in barbs"

- defenseId: reinforced_gate
  displayName: "Reinforced gate"
  defenseType: passive
  damageOnContact: 0.0
  damageType: Kinetic
  slowFactor: 1.0
  size: [1, 1]
  isConsumable: false
  triggerRadius: 0.0
  slopCommentary: "The Controlled Access Point allows authorized personnel to pass while keeping unauthorized biological occupants at bay. Please present your SLOP ID badge for entry."
  description: "Heavy metal gate that players can open and close. Blocks fauna pathing when closed. Takes significant damage before breaking. Place in walls to create entrances without compromising defense."
  tier: 1
  tags: [defense, passive, gate, door, access_control]
  health: 800.0
  repairMaterial: iron_plate
  researchRequired: null
  craftingRecipe: craft_reinforced_gate
  modelStyle: "thick steel double doors with crossbar latch, welded hinges, peephole slit"

- defenseId: landmine
  displayName: "Landmine"
  defenseType: passive
  damageOnContact: 150.0
  damageType: Explosive
  slowFactor: 1.0
  size: [1, 1]
  isConsumable: true
  triggerRadius: 1.5
  slopCommentary: "The Subsurface Greeting Package delivers a surprise welcome to the first visitor who steps on it. Single use only. SLOP recommends placing these away from high-traffic employee walkways."
  description: "Buried explosive triggered by proximity. Deals heavy explosive damage to the first fauna that enters the trigger radius, then is destroyed. Cannot be recovered after placement. High damage but one-time use makes it expensive for sustained defense."
  tier: 2
  tags: [defense, passive, explosive, single_use, proximity, high_damage]
  health: 0.0
  repairMaterial: null
  researchRequired: weapons_research
  craftingRecipe: craft_landmine
  modelStyle: "flat disc half-buried in dirt, blinking red indicator light, caution stencil"

- defenseId: spotlight
  displayName: "Spotlight"
  defenseType: passive
  damageOnContact: 0.0
  damageType: Kinetic
  slowFactor: 1.0
  size: [1, 1]
  isConsumable: false
  triggerRadius: 25.0
  slopCommentary: "The Visibility Enhancement Station ensures all employees and visitors can be clearly observed during their activities. Privacy is a privilege, not a right, under SLOP Workplace Guideline 14.7."
  description: "High-powered light that reveals fauna within its cone. Enemies illuminated by the spotlight are visible through fog and darkness, and turrets prioritize illuminated targets. No damage, but a force multiplier for turret networks. Requires power."
  tier: 2
  tags: [defense, passive, utility, detection, vision, turret_support]
  health: 150.0
  repairMaterial: iron_plate
  researchRequired: defensive_systems
  craftingRecipe: craft_spotlight
  modelStyle: "industrial floodlight on adjustable pole mount, thick power cable, cracked lens housing"
```
