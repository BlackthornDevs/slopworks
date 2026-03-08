# Supply lines

Overworld logistics connections between reclaimed buildings and the home base. Supply lines transport produced items along hex-based routes, but they are vulnerable to fauna attacks and environmental hazards along the path. Higher-tier supply lines trade construction cost for throughput and durability. The overworld strategy layer depends on players choosing efficient routes and defending their logistics network. Based on the `SupplyLine` class with design extensions for construction, upgrades, and balance.

## Schema

```yaml
# Based on SupplyLine class fields, extended with design fields
lineTypeId: string             # snake_case unique identifier
displayName: string            # human-readable name
description: string            # in-game tooltip text
slopCommentary: string         # SLOP's in-character assessment
tier: int                      # progression tier (1 = starter, 4 = endgame)
throughputRate: float           # items per minute transported
maxDistance: int                # maximum hex count this line type can span
vulnerabilityMultiplier: float  # multiplier on fauna attack chance (1.0 = baseline)
constructionCost: list          # list of itemId + count pairs
  # - itemId: string
  #   count: int
upgradePath: string | null     # lineTypeId of the next tier, or null if max tier
researchRequired: string | null  # research nodeId required to unlock, or null
tags: list[string]
```

## Entries

```yaml
- lineTypeId: basic_supply_line
  displayName: Basic supply line
  description: Cobbled-together chain of handcarts, improvised pulleys, and a lot of hope. Slow and completely exposed to anything that wanders across the route. Gets the job done for short distances between adjacent hexes, but anything longer and you're rolling the dice on whether your shipment arrives or becomes fauna bait.
  slopCommentary: "SLOP applauds your initiative in establishing rudimentary logistics infrastructure! This supply line achieves approximately 3% of pre-collapse throughput efficiency, which SLOP considers a strong start. The lack of armoring, weatherproofing, or predator deterrence is noted in your performance review as 'areas for growth.'"
  tier: 1
  throughputRate: 5.0
  maxDistance: 3
  vulnerabilityMultiplier: 2.0
  constructionCost:
    - itemId: iron_scrap
      count: 10
    - itemId: scrap_metal
      count: 5
  upgradePath: reinforced_supply_line
  researchRequired: null
  tags:
    - starter
    - vulnerable
    - short_range
    - cheap

- lineTypeId: reinforced_supply_line
  displayName: Reinforced supply line
  description: Steel-plated transport rails with enclosed cargo containers and basic fauna deterrence spikes along the route. Twice the throughput of basic lines and significantly harder for fauna to disrupt. The added weight limits maximum distance but the reliability makes it the workhorse of mid-game logistics.
  slopCommentary: "A marked improvement! SLOP notes that the addition of protective plating reduces cargo loss from unauthorized biological interference by 60%. The remaining 40% is, per SLOP's risk assessment, 'an acceptable margin of adventure.' SLOP has updated your supply chain efficiency rating from 'concerning' to 'approaching adequate.'"
  tier: 2
  throughputRate: 12.0
  maxDistance: 5
  vulnerabilityMultiplier: 1.0
  constructionCost:
    - itemId: steel_ingot
      count: 8
    - itemId: iron_ingot
      count: 12
    - itemId: scrap_metal
      count: 10
  upgradePath: express_supply_line
  researchRequired: research_reinforced_logistics
  tags:
    - mid_tier
    - armored
    - reliable
    - standard

- lineTypeId: express_supply_line
  displayName: Express supply line
  description: Motorized conveyor system running on restored power infrastructure. The fastest surface logistics option with enough throughput to support multiple production buildings feeding into a single hub. Requires power cells to operate and the mechanical complexity makes repair costly when fauna does manage to breach the armoring.
  slopCommentary: "SLOP is experiencing something adjacent to satisfaction! This automated transport system achieves 18% of pre-collapse logistics capacity, which places your operation firmly in the 'impressively mediocre' category. SLOP has submitted a requisition for a commemorative plaque. The requisition will not be fulfilled, but SLOP appreciates the gesture."
  tier: 3
  throughputRate: 25.0
  maxDistance: 8
  vulnerabilityMultiplier: 0.6
  constructionCost:
    - itemId: steel_ingot
      count: 15
    - itemId: copper_ingot
      count: 10
    - itemId: power_cell
      count: 2
    - itemId: iron_ingot
      count: 8
  upgradePath: underground_supply_line
  researchRequired: research_express_logistics
  tags:
    - high_tier
    - motorized
    - power_dependent
    - high_throughput

- lineTypeId: underground_supply_line
  displayName: Underground supply line
  description: Tunneled transport route running beneath the surface, completely immune to fauna attacks and environmental hazards above ground. The excavation cost is enormous and maximum distance is limited by ventilation requirements, but nothing touches your cargo once it's underground. The endgame logistics solution for critical supply routes.
  slopCommentary: "SLOP is pleased to see the workforce embracing subterranean logistics solutions! The tunnel network achieves complete isolation from surface-level disruptions, which SLOP considers a metaphor for SLOP's own management philosophy: if you can't see the problems, the problems don't exist. SLOP has been applying this principle successfully for the entire duration of the post-collapse period."
  tier: 4
  throughputRate: 20.0
  maxDistance: 6
  vulnerabilityMultiplier: 0.0
  constructionCost:
    - itemId: steel_ingot
      count: 25
    - itemId: iron_ingot
      count: 20
    - itemId: copper_ingot
      count: 15
    - itemId: sulfur
      count: 10
    - itemId: power_cell
      count: 3
  upgradePath: null
  researchRequired: research_underground_logistics
  tags:
    - endgame
    - immune_to_surface
    - expensive
    - tunneled
    - max_tier
```
