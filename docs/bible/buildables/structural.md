# Structural

Foundations, walls, ramps, and walkways that form the physical structure of the factory base. Foundations define the build grid and snap points. Walls occupy edges between foundation cells. Ramps connect vertical levels. Each sub-type maps to a different ScriptableObject: `FoundationDefinitionSO`, `WallDefinitionSO`, `RampDefinitionSO`.

## Schema

```yaml
# === Foundation sub-schema (matches FoundationDefinitionSO) ===

foundationId: string                      # unique snake_case identifier
displayName: string                       # player-facing name
structuralType: foundation                # discriminator
size: [int, int]                          # grid footprint [width, depth]
generatesSnapPoints: bool                 # whether edges produce snap points for adjacent placement

# Design fields (bible-only)
slopCommentary: string
description: string
tier: int
tags: list
health: float
craftingRecipe: string | null
modelStyle: string

# === Wall sub-schema (matches WallDefinitionSO) ===

wallId: string                            # unique snake_case identifier
displayName: string
structuralType: wall                      # discriminator
# walls occupy a single edge, not a grid cell -- no size field

slopCommentary: string
description: string
tier: int
tags: list
health: float
craftingRecipe: string | null
modelStyle: string

# === Ramp sub-schema (matches RampDefinitionSO) ===

rampId: string                            # unique snake_case identifier
displayName: string
structuralType: ramp                      # discriminator
footprintLength: int                      # cells along the slope direction

slopCommentary: string
description: string
tier: int
tags: list
health: float
craftingRecipe: string | null
modelStyle: string
```

## Entries

```yaml
# --- Foundations ---

- foundationId: foundation_1x1
  displayName: "Foundation (1x1)"
  structuralType: foundation
  size: [1, 1]
  generatesSnapPoints: true
  slopCommentary: "The Standard Workspace Allocation Unit provides exactly one unit of approved floor space. Perfect for employees who value personal boundaries."
  description: "Single-cell foundation slab. The smallest buildable unit. Snap points on all four edges allow walls, ramps, and adjacent foundations to attach. Cheap and fast to place."
  tier: 1
  tags: [structural, foundation, starter, basic]
  health: 500.0
  craftingRecipe: craft_foundation
  modelStyle: "concrete slab with exposed rebar on edges, cracked surface, faded SLOP safety line markings"

- foundationId: foundation_2x2
  displayName: "Foundation (2x2)"
  structuralType: foundation
  size: [2, 2]
  generatesSnapPoints: true
  slopCommentary: "The Enhanced Workspace Allocation Unit offers four times the productivity potential of its smaller cousin. Room to breathe, room to work, room to contribute."
  description: "Four-cell foundation platform placed as a single unit. Faster than placing four 1x1s individually. Generates snap points on the outer perimeter. Standard choice for machine footprints."
  tier: 1
  tags: [structural, foundation, starter, efficient]
  health: 1000.0
  craftingRecipe: craft_foundation_2x2
  modelStyle: "concrete platform with construction joints at cell boundaries, industrial yellow edge paint"

- foundationId: foundation_4x4
  displayName: "Foundation (4x4)"
  structuralType: foundation
  size: [4, 4]
  generatesSnapPoints: true
  slopCommentary: "The Premium Workspace Allocation Unit is reserved for departments that have met their quarterly output targets. Everyone else gets the 1x1."
  description: "Large foundation platform covering sixteen cells. Ideal for factory floor layouts -- place machines, storage, and belts on top without worrying about individual cell placement. Higher material cost but saves setup time."
  tier: 1
  tags: [structural, foundation, large, factory_floor]
  health: 2000.0
  craftingRecipe: craft_foundation_4x4
  modelStyle: "industrial concrete deck with drainage channels, heavy rebar visible on broken edges, oil stains"

# --- Walls ---

- wallId: basic_wall
  displayName: "Basic wall"
  structuralType: wall
  slopCommentary: "The Standard Partition Element defines where your space ends and someone else's begins. Respect the wall. The wall respects you."
  description: "Simple corrugated metal wall placed on a foundation edge. Blocks line of sight and fauna pathing. Low health but cheap to produce. The first thing you build after laying foundations."
  tier: 1
  tags: [structural, wall, basic, starter, cheap]
  health: 200.0
  craftingRecipe: craft_wall
  modelStyle: "corrugated sheet metal panel, riveted to vertical angle-iron posts, dents and surface rust"

- wallId: reinforced_wall
  displayName: "Reinforced wall"
  structuralType: wall
  slopCommentary: "The Premium Partition Element has been stress-tested against category 3 unauthorized biological occupant impacts. Categories 4 and above are considered acts of nature."
  description: "Double-layered steel wall with concrete fill. Three times the health of a basic wall. Use around critical infrastructure -- generators, storage, smelters -- where a breach would cripple production."
  tier: 2
  tags: [structural, wall, reinforced, durable, mid_game]
  health: 600.0
  craftingRecipe: craft_reinforced_wall
  modelStyle: "thick steel plate bolted over concrete core, welded seams, blast-rated warning stencil"

- wallId: doorway
  displayName: "Doorway"
  structuralType: wall
  slopCommentary: "The Transitional Aperture Module allows authorized movement between workspace zones. Please proceed in an orderly fashion and avoid congregating in the frame."
  description: "Wall segment with an open archway. Players and small fauna can pass through; large fauna cannot. Provides structural continuity for wall lines while allowing access. No door -- for a closable entrance, use the reinforced gate defense."
  tier: 1
  tags: [structural, wall, doorway, passage, access]
  health: 150.0
  craftingRecipe: craft_doorway
  modelStyle: "corrugated metal wall with rectangular cutout, angle-iron frame around opening, worn threshold"

- wallId: window_wall
  displayName: "Window wall"
  structuralType: wall
  slopCommentary: "The Observation-Enabled Partition Element lets you see what's outside without having to be outside. SLOP recommends keeping the outside where it belongs."
  description: "Wall with a horizontal slit opening at eye level. Blocks fauna pathing but allows players to shoot through. Reduced health compared to a solid wall. Useful for creating firing positions behind cover."
  tier: 1
  tags: [structural, wall, window, firing_position, visibility]
  health: 150.0
  craftingRecipe: craft_window_wall
  modelStyle: "corrugated metal with narrow horizontal slot cut at chest height, reinforced lip on bottom edge"

# --- Ramps and walkways ---

- rampId: basic_ramp
  displayName: "Ramp"
  structuralType: ramp
  footprintLength: 3
  slopCommentary: "The Vertical Transition Solution connects levels without the liability concerns of ladders. Incline has been calibrated to SLOP ergonomic standard 6.2."
  description: "Sloped surface connecting level N to level N+1. Occupies 3 cells on the lower level and arrives at the top edge of the upper level. Players walk up; fauna can use ramps too, so defend upper entrances. The only way to build vertically."
  tier: 1
  tags: [structural, ramp, vertical, level_connection, starter]
  health: 400.0
  craftingRecipe: craft_ramp
  modelStyle: "steel plate ramp with anti-slip diamond tread, angle-iron side rails, welded to foundation edges"

- rampId: walkway
  displayName: "Walkway"
  structuralType: ramp
  footprintLength: 1
  slopCommentary: "The Elevated Personnel Transit Corridor keeps employees above ground-level hazards. SLOP accepts no responsibility for vertigo, wind, or what's below."
  description: "Flat elevated bridge spanning one cell, used to cross over belts, machines, or gaps without blocking them. Does not change elevation -- connects two foundations at the same level with a raised platform between them. Useful for factory floor routing."
  tier: 1
  tags: [structural, walkway, bridge, elevated, routing]
  health: 250.0
  craftingRecipe: craft_walkway
  modelStyle: "metal grate platform on I-beam supports, thin pipe handrails, bolted to adjacent foundations"
```
