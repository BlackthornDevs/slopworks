# Environmental hazards

Persistent and triggered dangers in the overworld and inside reclaimed buildings that exist independently of fauna encounters. Hazards damage players who enter their area of effect, apply status effects, and force routing decisions during exploration and combat. There is no existing ScriptableObject for hazards yet — this bible file defines the canonical schema for future implementation. SLOP considers each of these a minor operational inconvenience.

## Schema

```yaml
# No existing SO — this schema defines the future HazardDefinition data structure
hazardId: string               # snake_case unique identifier
displayName: string            # human-readable name
description: string            # in-game tooltip text
slopCommentary: string         # SLOP's in-character dismissal of this hazard
hazardType: enum [toxic_leak, structural_collapse, spore_cloud, electrical, radiation, flooding]
damageType: DamageType         # Kinetic | Explosive | Fire | Toxic
damagePerSecond: float         # DPS while in the area of effect
aoeRadius: float               # radius in world units
duration: float                # seconds, 0 for permanent hazards
statusEffectApplied: string | null  # effectId reference to systems/status-effects.md, or null
biomeAffinity: list[string]    # biomeId references — which biomes this hazard spawns in
triggerCondition: string       # what activates the hazard (proximity, timer, building state, etc.)
visualIndicator: string        # art direction note for the hazard's visual
soundEvent: string             # FMOD event path
tags: list[string]
```

## Entries

```yaml
- hazardId: toxic_leak_hazard
  displayName: Toxic leak
  description: Ruptured chemical pipe leaking corrosive industrial fluid into a spreading pool. The liquid eats through standard footwear in seconds and the fumes aren't much better. Avoid or find the shutoff valve upstream.
  slopCommentary: "SLOP has reclassified this fluid discharge as a 'complimentary chemical peel.' Employees who experience skin irritation, respiratory distress, or sudden transparency should report to HR during business hours. Business hours have not yet been reestablished."
  hazardType: toxic_leak
  damageType: Toxic
  damagePerSecond: 8.0
  aoeRadius: 3.5
  duration: 0
  statusEffectApplied: corrosion
  biomeAffinity:
    - Swamp
    - OvergrownRuins
    - Ruins
  triggerCondition: Permanent. Active whenever the building or area has not been restored. Proximity-based damage — enter the pool and you take damage.
  visualIndicator: Glowing green-yellow liquid pooling on the floor with visible fumes rising. Caustic bubbling at the edges. Overhead pipe with visible drip source.
  soundEvent: event:/sfx/hazard_toxic_leak
  tags:
    - chemical
    - permanent
    - avoidable
    - dot

- hazardId: structural_collapse_hazard
  displayName: Structural collapse
  description: Weakened floor sections, crumbling walls, and ceiling panels held up by rust and optimism. Walking on compromised surfaces triggers a localized collapse dealing heavy kinetic damage and potentially trapping the player under debris.
  slopCommentary: "The structural integrity of this section is undergoing unscheduled reorganization. SLOP's engineering analysis rates the load-bearing capacity at 'hopes and prayers.' Employees weighing more than 4 kilograms should exercise caution. SLOP weighs nothing and is therefore not concerned."
  hazardType: structural_collapse
  damageType: Kinetic
  damagePerSecond: 0
  aoeRadius: 4.0
  duration: 0.5
  statusEffectApplied: null
  biomeAffinity:
    - Ruins
    - OvergrownRuins
    - Grassland
    - Forest
  triggerCondition: Triggered by player weight on compromised floor sections. Visual cracks and dust particles indicate weakness before collapse. One-time burst damage on trigger, not DPS — the 0.5 duration represents the collapse event.
  visualIndicator: Hairline cracks in floor and ceiling with dust particles drifting down. Slightly sagging geometry. Audio creaking on approach.
  soundEvent: event:/sfx/hazard_collapse
  tags:
    - kinetic
    - triggered
    - one_shot
    - structural

- hazardId: spore_cloud_hazard
  displayName: Spore cloud
  description: Dense clouds of mutated fungal spores released from mature growths on walls and ceilings. Breathing the spores causes progressive toxic damage and impaired vision. Spore clouds drift slowly and can be dispersed by explosions or strong ventilation.
  slopCommentary: "SLOP's air quality monitor classifies this particulate matter as 'organic atmospheric enhancement.' The spores are a natural product of the facility's thriving mycological community, which SLOP considers a successful diversification of the company's biological assets. Respiratory protection is optional but recommended for employees who enjoy breathing."
  hazardType: spore_cloud
  damageType: Toxic
  damagePerSecond: 5.0
  aoeRadius: 5.0
  duration: 30.0
  statusEffectApplied: toxicity
  biomeAffinity:
    - Forest
    - Swamp
    - OvergrownRuins
  triggerCondition: Proximity to mature spore growths triggers a cloud release. Clouds persist for 30 seconds before dissipating, then regrow after 60 seconds. Disturbance (explosions, gunfire near growths) triggers early release.
  visualIndicator: Thick yellow-green particulate cloud with visible spore motes. Source growths are bulbous fungal masses on walls and ceilings that pulse before releasing. Reduced visibility inside the cloud.
  soundEvent: event:/sfx/hazard_spore_cloud
  tags:
    - toxic
    - timed
    - dispersible
    - visibility_reduction
    - fungal

- hazardId: electrical_hazard
  displayName: Live electrical discharge
  description: Exposed wiring, damaged junction boxes, and flooded floors carrying current. Intermittent arcs of electricity jump between conductive surfaces. The discharge pattern is predictable if you watch long enough, but one wrong step and the current finds a shorter path through you.
  slopCommentary: "SLOP detects elevated voltage in this area. The electrical discharge is a result of the facility's power grid achieving what SLOP calls 'creative redistribution of amperage.' The arcing pattern follows a 4.7-second cycle, which SLOP will helpfully not tell you about because self-reliance builds character."
  hazardType: electrical
  damageType: Fire
  damagePerSecond: 12.0
  aoeRadius: 2.0
  duration: 1.5
  statusEffectApplied: null
  biomeAffinity:
    - Ruins
    - Wasteland
  triggerCondition: Intermittent. Arcs on a predictable cycle (4-6 seconds between discharges). Player proximity to conductive surfaces (metal floors, water pools, junction boxes) determines whether they're in the damage zone during a discharge. Standing on non-conductive surfaces provides safety.
  visualIndicator: Blue-white electrical arcs between exposed wires and metal surfaces. Sparking junction boxes with scorch marks. Flickering overhead lights in the affected zone.
  soundEvent: event:/sfx/hazard_electrical
  tags:
    - electrical
    - intermittent
    - predictable
    - avoidable

- hazardId: radiation_hazard
  displayName: Radiation zone
  description: Areas contaminated by leaking industrial isotopes from the facility's power generation and materials testing infrastructure. The radiation is invisible but persistent, dealing steady damage that increases the longer you stay. No status effect, just raw damage that punishes lingering.
  slopCommentary: "SLOP's Geiger counter integration reports a reading of — actually, SLOP's Geiger counter module was reassigned to monitor break room microwave usage in 2019. The warm tingling sensation you may experience in this area is almost certainly just enthusiasm for your work assignment."
  hazardType: radiation
  damageType: Fire
  damagePerSecond: 3.0
  aoeRadius: 8.0
  duration: 0
  statusEffectApplied: null
  biomeAffinity:
    - Wasteland
    - Ruins
  triggerCondition: Permanent. Passive damage while inside the zone. Damage scales with time spent in the zone — starts at base DPS and increases by 1.0 per 10 seconds of continuous exposure, capping at 3x base. Leaving the zone resets the escalation timer after 5 seconds.
  visualIndicator: Subtle heat shimmer distortion in the air. Faint green-tinged glow on contaminated surfaces. Geiger counter UI element activates when entering the zone.
  soundEvent: event:/sfx/hazard_radiation
  tags:
    - radiation
    - permanent
    - escalating
    - invisible

- hazardId: flooding_hazard
  displayName: Flash flood zone
  description: Low-lying areas where damaged water infrastructure periodically releases surges of contaminated water. The flood arrives fast, rises to waist height, and sweeps loose objects and fauna along with it. Getting caught means toxic water damage and being pushed into whatever else the current is carrying.
  slopCommentary: "SLOP has scheduled this water feature for maintenance in Q3. Of which year, SLOP declines to specify. Employees are reminded that the company's aquatic recreation policy was revoked following the incident in sector 12, which SLOP's records describe only as 'regrettable' and 'surprisingly buoyant.'"
  hazardType: flooding
  damageType: Toxic
  damagePerSecond: 4.0
  aoeRadius: 10.0
  duration: 15.0
  statusEffectApplied: corrosion
  biomeAffinity:
    - Swamp
    - OvergrownRuins
  triggerCondition: Periodic. Floods occur on a cycle (90-120 seconds between surges). Audio warning — rushing water sound 5 seconds before arrival. Elevated positions provide safety. Flood water recedes over the duration period.
  visualIndicator: Rising brown-green water with visible current flow. Debris carried on the surface. Water stains on walls indicate maximum flood height.
  soundEvent: event:/sfx/hazard_flooding
  tags:
    - flooding
    - periodic
    - displacement
    - toxic
    - audio_warning

- hazardId: gas_pocket_hazard
  displayName: Volatile gas pocket
  description: Sealed rooms and underground sections where heavier-than-air industrial gases have accumulated. The gas is invisible but flammable — gunfire or explosions in a gas pocket trigger a devastating chain detonation that clears the gas but destroys everything in the blast radius, including the player.
  slopCommentary: "SLOP's atmospheric sensors detect elevated concentrations of industrial byproduct gases in this enclosed space. SLOP recommends against the use of open flames, spark-generating equipment, firearms, vigorous friction, static electricity, or strong opinions. A gentle breeze would be ideal, but SLOP cannot provide one at this time."
  hazardType: toxic_leak
  damageType: Explosive
  damagePerSecond: 2.0
  aoeRadius: 6.0
  duration: 0
  statusEffectApplied: toxicity
  biomeAffinity:
    - Ruins
    - Swamp
  triggerCondition: Permanent until ignited. Passive low toxic DPS from breathing the gas. Firing a weapon, using explosives, or triggering an electrical hazard within the gas pocket causes a detonation dealing 200 burst damage in the full AoE. Detonation permanently clears the gas from the room.
  visualIndicator: Faint heat shimmer at floor level. Slight yellowish tint to the air. Player character coughs on entry. UI warning icon for flammable atmosphere.
  soundEvent: event:/sfx/hazard_gas_pocket
  tags:
    - gas
    - flammable
    - explosive_trigger
    - permanent_until_cleared
    - tactical
```
