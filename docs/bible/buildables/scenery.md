# Scenery

Decorative and environmental objects placed in the world for atmosphere, storytelling, and visual variety. Scenery has no production function and no SO in the current codebase -- these definitions will drive a future `SceneryDefinitionSO`. Some scenery is interactive (SLOP terminals), most is purely visual. Scenery can be placed on foundations, mounted on walls, or positioned freeform on terrain.

## Schema

```yaml
sceneryId: string                         # unique snake_case identifier
displayName: string                       # player-facing name
description: string                       # design description
slopCommentary: string                    # in-character SLOP quote
sceneryType: enum [prop, signage, decoration, vegetation, debris]
placementMode: enum [foundation_snap, freeform, wall_mount]
size: [int, int]                          # footprint [width, depth], ignored for wall_mount
isDestructible: bool                      # can players or fauna destroy this
health: float                             # hit points, 0 if not destructible
biomeAffinity: list                       # biome names where this spawns naturally
tags: list                                # lowercase string tags
modelStyle: string                        # visual description for art direction
interactable: bool                        # whether the player can interact (default false)
interactionDescription: string | null     # what happens on interaction
```

## Entries

```yaml
- sceneryId: safety_sign
  displayName: "Safety sign"
  description: "SLOP-branded workplace safety poster mounted on walls or posts. Displays cheerfully outdated safety slogans that ignore the current state of the world. Collectible flavor text -- reading different signs unlocks SLOP dialogue lines."
  slopCommentary: "Attention all employees: this sign has been placed for your protection. Please read it carefully and then return to your assigned duties. SLOP cares about your wellbeing, which is why we made this sign instead of fixing the problem."
  sceneryType: signage
  placementMode: wall_mount
  size: [1, 1]
  isDestructible: true
  health: 25.0
  biomeAffinity: [Ruins, OvergrownRuins]
  tags: [scenery, signage, slop_lore, interactable, collectible]
  modelStyle: "faded laminated poster in a cracked plastic frame, SLOP logo header, cheerful clip-art worker giving thumbs up alongside grim safety statistics"
  interactable: true
  interactionDescription: "read the sign to reveal a SLOP safety tip"

- sceneryId: barrel_stack
  displayName: "Barrel stack"
  description: "Cluster of industrial barrels stacked two high. Purely decorative -- cannot be looted or used for storage. Some leak unidentified fluid. Found near former industrial sites."
  slopCommentary: "These Material Retention Cylinders contain proprietary compounds that are definitely not hazardous. The leaking is a scheduled pressure release. Please do not lick the barrels."
  sceneryType: prop
  placementMode: freeform
  size: [1, 1]
  isDestructible: true
  health: 75.0
  biomeAffinity: [Ruins, Wasteland]
  tags: [scenery, prop, industrial, destructible]
  modelStyle: "three rusted 55-gallon drums, two upright with one on top, faded hazmat labels, green liquid pooling at base"
  interactable: false
  interactionDescription: null

- sceneryId: broken_conveyor
  displayName: "Broken conveyor"
  description: "Remnant of a pre-collapse factory belt system. Non-functional and cannot be repaired or salvaged. Serves as environmental storytelling -- the factories used to work, once. Blocks walking if placed on a foundation cell."
  slopCommentary: "This Legacy Material Transport System has been decommissioned pending a maintenance review that was scheduled for seventeen years ago. The work order is still in the queue."
  sceneryType: debris
  placementMode: freeform
  size: [2, 1]
  isDestructible: true
  health: 150.0
  biomeAffinity: [Ruins, OvergrownRuins]
  tags: [scenery, debris, industrial, lore, blocking]
  modelStyle: "bent conveyor belt frame with torn rubber belt, seized rollers, weeds growing through supports"
  interactable: false
  interactionDescription: null

- sceneryId: rusty_fence
  displayName: "Rusty fence"
  description: "Chain-link fence section in various states of collapse. Placed freeform on terrain to define boundaries of ruined areas. Partially transparent -- players can see through but not walk through intact sections."
  slopCommentary: "The Perimeter Delineation Mesh remains structurally sound according to the last inspection, which was conducted by someone who no longer works here. Or anywhere."
  sceneryType: prop
  placementMode: freeform
  size: [2, 1]
  isDestructible: true
  health: 50.0
  biomeAffinity: [Ruins, Wasteland, OvergrownRuins]
  tags: [scenery, prop, barrier, partially_transparent, terrain]
  modelStyle: "chain-link fence on bent metal posts, sections sagging or torn, barbed wire top strand mostly missing"
  interactable: false
  interactionDescription: null

- sceneryId: slop_terminal_prop
  displayName: "SLOP terminal"
  description: "A still-functioning SLOP access terminal. Interact to hear SLOP commentary about the local area, current threat levels, or factory productivity metrics that no longer mean anything. Primary delivery mechanism for SLOP's in-game dialogue."
  slopCommentary: "Welcome back, valued employee. This SLOP Interactive Wellness Station is fully operational and ready to assist with any questions, concerns, or existential crises you may be experiencing. Please note that existential crisis support has been discontinued."
  sceneryType: decoration
  placementMode: foundation_snap
  size: [1, 1]
  isDestructible: false
  health: 0.0
  biomeAffinity: [Ruins, OvergrownRuins]
  tags: [scenery, decoration, slop_lore, interactable, dialogue, key_prop]
  modelStyle: "standing kiosk with cracked CRT monitor, glowing green text, SLOP logo on housing, one speaker producing tinny audio, cables trailing to nowhere"
  interactable: true
  interactionDescription: "access SLOP dialogue system for area-specific commentary"

- sceneryId: overgrown_crate
  displayName: "Overgrown crate"
  description: "Shipping container partially swallowed by vegetation. Vines and moss cover the exterior. Found in overgrown areas as environmental set dressing. Cannot be opened -- whatever was inside is long gone or fused to the walls."
  slopCommentary: "This Expedited Delivery Container was scheduled for retrieval in Q3. Nature has filed a counterclaim. SLOP legal is reviewing the matter."
  sceneryType: vegetation
  placementMode: freeform
  size: [2, 1]
  isDestructible: true
  health: 200.0
  biomeAffinity: [Forest, OvergrownRuins, Swamp]
  tags: [scenery, vegetation, cover, nature_reclamation, atmospheric]
  modelStyle: "corrugated steel shipping container, one end buried in dirt, thick vines and moss covering 60% of surface, small tree growing from roof seam"
  interactable: false
  interactionDescription: null
```
