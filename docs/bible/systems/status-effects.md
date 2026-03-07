# Status effects

Temporary conditions applied to players, fauna, or buildings. Each effect modifies stats over a duration, ticking at regular intervals. Effects come from fauna attacks, environmental hazards, consumables, or equipment. No existing SO -- these definitions will drive a future `StatusEffectSO`.

## Schema

```yaml
effectId: string                          # unique snake_case identifier
displayName: string                       # player-facing name
description: string                       # design description
slopCommentary: string                    # in-character SLOP quote
effectType: enum [damage_over_time, slow, damage_resistance, speed_boost, heal_over_time, stun]
magnitude: float                          # per-tick value (damage, heal amount, speed multiplier, etc.)
duration: float                           # total effect duration in seconds
tickInterval: float                       # seconds between each application of the effect
stackable: bool                           # can multiple instances of this effect exist simultaneously
maxStacks: int                            # maximum stack count (1 if not stackable)
source: enum [fauna_attack, environmental, consumable, equipment]
visualIndicator: string                   # VFX or UI description for the player
tags: list                                # lowercase string tags
```

## Entries

```yaml
- effectId: corrosion
  displayName: "Corrosion"
  description: "Acid damage over time inflicted by chemical fauna (spitters, acid crawlers). Eats through armor and deals consistent damage regardless of damage resistance. The most dangerous fauna DoT because it bypasses defensive buffs."
  slopCommentary: "You appear to be dissolving. SLOP's Material Integrity Division classifies this as 'accelerated depreciation.' Please file a maintenance request before your structural integrity reaches zero."
  effectType: damage_over_time
  magnitude: 4.0
  duration: 10.0
  tickInterval: 1.0
  stackable: true
  maxStacks: 3
  source: fauna_attack
  visualIndicator: "green bubbling particle effect on character model, acid drip trails on ground"
  tags: [debuff, damage, chemical, fauna, acid, armor_piercing]

- effectId: toxicity
  displayName: "Toxicity"
  description: "Poison damage from environmental spore clouds and toxic fauna attacks. Slower tick rate than corrosion but longer duration. Stacks from repeated exposure in contaminated areas, so lingering in spore zones compounds the damage."
  slopCommentary: "Minor atmospheric adjustment detected in your vicinity. SLOP recommends holding your breath for the next thirty seconds. If you cannot hold your breath for thirty seconds, SLOP recommends reconsidering your career in hazardous environments."
  effectType: damage_over_time
  magnitude: 3.0
  duration: 15.0
  tickInterval: 2.5
  stackable: true
  maxStacks: 5
  source: environmental
  visualIndicator: "purple fog overlay on screen edges, coughing sound effect, toxic icon on HUD"
  tags: [debuff, damage, poison, environmental, spore, cumulative]

- effectId: bleeding
  displayName: "Bleeding"
  description: "Physical damage over time from melee fauna attacks (claws, bites). Fast tick rate, short duration. Each hit from a melee fauna refreshes and stacks the effect, making sustained close combat deadly even against weak enemies."
  slopCommentary: "SLOP's Occupational Health Monitor detects a fluid leak in your biological containment system. Please apply the complimentary adhesive bandage located in your SLOP welcome kit. If you did not receive a welcome kit, please disregard."
  effectType: damage_over_time
  magnitude: 2.0
  duration: 6.0
  tickInterval: 0.5
  stackable: true
  maxStacks: 5
  source: fauna_attack
  visualIndicator: "red vignette pulse on screen, blood drop particles from character, heartbeat sound"
  tags: [debuff, damage, physical, melee, fauna, fast_tick]

- effectId: stimmed
  displayName: "Stimmed"
  description: "Movement speed boost from consuming a stim pack. Increases move speed by 40% for the duration. Does not stack -- using another stim pack refreshes the timer. Used for kiting fauna, repositioning during wave events, or sprint-looting tower floors."
  slopCommentary: "The SLOP Metabolic Enhancement Formula temporarily increases your operational velocity. Any feelings of invincibility are a side effect and should not be acted upon. SLOP disclaims all liability for actions taken while stimmed."
  effectType: speed_boost
  magnitude: 0.4
  duration: 20.0
  tickInterval: 0.0
  stackable: false
  maxStacks: 1
  source: consumable
  visualIndicator: "blue motion blur on screen edges, speed lines particle effect, footstep sound pitch increase"
  tags: [buff, speed, consumable, stim, movement, tactical]

- effectId: armored
  displayName: "Armored"
  description: "Damage resistance from consuming an armor plating consumable or equipping heavy gear. Reduces all incoming damage by the magnitude percentage. Does not prevent status effects from being applied, only reduces the initial hit damage."
  slopCommentary: "Your Personal Damage Mitigation Field is active. SLOP reminds you that this is not a license to stand in front of the turrets. They cannot tell the difference between you and an unauthorized biological occupant. Neither can we, frankly."
  effectType: damage_resistance
  magnitude: 0.3
  duration: 30.0
  tickInterval: 0.0
  stackable: false
  maxStacks: 1
  source: consumable
  visualIndicator: "metallic sheen overlay on character model, shield icon on HUD with remaining duration"
  tags: [buff, defense, resistance, consumable, damage_reduction]

- effectId: stunned
  displayName: "Stunned"
  description: "Complete immobilization from electrical damage sources. The target cannot move, attack, or use items for the duration. Short but devastating -- being stunned during a wave event while surrounded is often fatal. Applies to both players and fauna."
  slopCommentary: "An unscheduled electrical discharge has temporarily interrupted your motor functions. SLOP suggests using this involuntary pause to reflect on workplace safety practices. You have two seconds."
  effectType: stun
  magnitude: 0.0
  duration: 2.0
  tickInterval: 0.0
  stackable: false
  maxStacks: 1
  source: environmental
  visualIndicator: "electric arc particles on character, screen shake, static overlay on HUD, buzzing sound"
  tags: [debuff, crowd_control, stun, electrical, immobilize]

- effectId: burning
  displayName: "Burning"
  description: "Fire damage over time from flamethrower turrets, incendiary sources, or fire-type fauna. Moderate damage with a medium tick rate. Fire spreads -- burning fauna that touch other fauna can ignite them, creating chain reactions in swarm waves."
  slopCommentary: "Your ambient temperature has exceeded recommended levels. SLOP's Climate Comfort Division reminds you to stop, drop, and roll. If rolling is not possible due to workplace layout constraints, standing and screaming is also acceptable."
  effectType: damage_over_time
  magnitude: 5.0
  duration: 8.0
  tickInterval: 1.0
  stackable: false
  maxStacks: 1
  source: fauna_attack
  visualIndicator: "flame particles on character model, orange screen edge glow, crackling fire sound"
  tags: [debuff, damage, fire, spread, area_effect]

- effectId: regeneration
  displayName: "Regeneration"
  description: "Heal over time from consuming a med kit. Restores health gradually rather than instantly, rewarding players who use med kits proactively before taking fatal damage. Using another med kit while regenerating refreshes and extends the effect."
  slopCommentary: "The SLOP Biological Repair Sequence is underway. Your tissues are regenerating at a rate deemed acceptable by our medical algorithms. Please remain alive for the duration of treatment."
  effectType: heal_over_time
  magnitude: 8.0
  duration: 12.0
  tickInterval: 1.5
  stackable: false
  maxStacks: 1
  source: consumable
  visualIndicator: "green pulse overlay on screen edges, rising green particles from character, soft chime per tick"
  tags: [buff, healing, consumable, med_kit, survival, gradual]
```
