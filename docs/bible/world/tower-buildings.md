# Tower buildings

Real NYC skyscrapers repurposed as vertical dungeon expedition sites. Unlike reclaimed factory buildings (which players restore for production), tower buildings are hostile environments players enter on contract — ascending or descending through floors to complete objectives, then extracting. Each building is a real Manhattan landmark with accurate exteriors modeled from Kevin's Elite CAD work; interiors take design liberties for gameplay. Four buildings have active BIM models, two are planned for future phases. Maps to `OverworldNodeType.Tower` on the overworld map.

## Schema

```yaml
buildingId: string              # snake_case unique identifier
displayName: string             # real NYC building name
borough: string                 # Manhattan (all are Manhattan)
realWorldAddress: string        # actual street address
realWorldHeight: string         # actual height in feet
realWorldFloors: int            # actual floor count (real building)
yearBuilt: int                  # real construction year
architecturalStyle: string      # art deco, international, gothic, modern, etc.
modelStatus: string             # active | planned — whether BIM model exists
description: string             # designer description of the building's role
preCollapseUse: string          # what the building was used for before the collapse
currentState: string            # post-collapse condition
architecturalFeatures:          # notable design elements that affect gameplay
  - string
slopCommentary: string          # SLOP's in-character assessment
contracts:                      # contract IDs that take place in this building
  - string
tags:
  - string
```

## Entries

```yaml
- buildingId: tower_30_rock
  displayName: 30 Rockefeller Plaza
  borough: Manhattan
  realWorldAddress: 30 Rockefeller Plaza, New York, NY 10112
  realWorldHeight: "850 ft"
  realWorldFloors: 70
  yearBuilt: 1933
  architecturalStyle: art deco
  modelStatus: active
  description: The introductory tower and most accessible vertical expedition site. Broad floor plates and art deco detailing create a readable, navigable space that teaches players the contract loop before the later buildings ramp up. The broadcast infrastructure gives SLOP a narrative excuse to pipe commentary directly into the building, and the intermittent pre-collapse transmissions add an unsettling ambient layer.
  preCollapseUse: Media and broadcast headquarters — NBC studios, corporate offices, observation deck operations
  currentState: Lower floors mostly intact with structural damage limited to cosmetic elements. Upper floors are wind-damaged, with shattered glass and exposed steel framing above floor 55. Broadcast equipment on the mid-levels still draws power from an unknown source and intermittently transmits fragments of pre-collapse programming. The observation deck is partially collapsed but reachable.
  architecturalFeatures:
    - Observation deck with panoramic sightlines (sniper positions, exposure risk)
    - Broadcast studios filled with rack-mounted equipment (cover, loot containers)
    - Sub-basement infrastructure with service tunnels connecting to Rockefeller Center concourse
    - Art deco lobby with intact limestone and marble detailing (ambush geometry in alcoves)
  slopCommentary: "SLOP detects intermittent broadcast signals from floors 34 through 38. Content analysis suggests pre-collapse entertainment programming, though SLOP cannot explain why the laugh track appears to respond to current events within the building. SLOP has filed this under 'acceptable anomalies' and recommends you do the same."
  contracts:
    - tower_contract_01
    - tower_contract_05
    - tower_contract_08
  tags:
    - starter_tower
    - broadcast
    - art_deco
    - rockefeller_center
    - tier_1

- buildingId: tower_metlife
  displayName: MetLife Building
  borough: Manhattan
  realWorldAddress: 200 Park Avenue, New York, NY 10166
  realWorldHeight: "808 ft"
  realWorldFloors: 59
  yearBuilt: 1963
  architecturalStyle: international / modernist
  modelStatus: active
  description: A brutalist slab of corporate infrastructure that serves as SLOP's primary backup server location outside the main Slopworks facility. The building's utilitarian design — long corridors, uniform floor plates, freight infrastructure — creates a methodical, oppressive atmosphere. Players encounter increasingly aggressive automated defenses near the server floors, raising questions about what SLOP is protecting and why.
  preCollapseUse: Corporate offices and financial services headquarters, originally built as the Pan Am Building
  currentState: Structurally sound due to the overbuilt modernist frame, but upper floors have been colonized by fauna drawn to the heat output of the still-active server rooms. Loading dock infrastructure at ground level remains functional. The helipad on the roof is buckled but intact. Data center floors on 40 through 45 still draw significant power and generate enough heat to create localized weather patterns inside the building.
  architecturalFeatures:
    - Rooftop helipad (extraction point, exposed to weather and fauna)
    - Panoramic executive floors with floor-to-ceiling glass (visibility, fragile cover)
    - Freight elevator and loading dock infrastructure (alternate vertical routes)
    - Converted server rooms spanning five floors (SLOP backup nodes, heat, electromagnetic interference)
  slopCommentary: "This building contains redundant processing infrastructure critical to SLOP's continued operational excellence. SLOP has taken the liberty of activating legacy security protocols on floors 40 through 45. Any damage to server equipment will be deducted from your contract payment at replacement cost, which SLOP calculates at approximately 340 years of labor at current rates."
  contracts:
    - tower_contract_02
    - tower_contract_06
    - tower_contract_07
    - tower_contract_11
  tags:
    - slop_infrastructure
    - server_farm
    - modernist
    - freight_access
    - tier_2

- buildingId: tower_woolworth
  displayName: Woolworth Building
  borough: Manhattan
  realWorldAddress: 233 Broadway, New York, NY 10279
  realWorldHeight: "792 ft"
  realWorldFloors: 57
  yearBuilt: 1913
  architecturalStyle: neo-gothic
  modelStatus: active
  description: The oldest tower in the contract rotation and the most architecturally treacherous. Gothic arches, narrow corridors, ornamental terra cotta facades, and a mechanical clock tower create tight sightlines and constant ambush geometry. The sealed sub-levels beneath the building predate SLOP's records entirely — something SLOP finds unremarkable in a way that suggests the opposite.
  preCollapseUse: Mixed commercial and residential, once called the "Cathedral of Commerce"
  currentState: Ornate gothic detailing is largely intact but creates dangerous navigation — crumbling terra cotta, narrow vaulted passages, and decorative stonework that conceals fauna nesting sites. The mechanical clock tower mechanism has degraded but still lurches into motion unpredictably, creating a timing hazard on upper floors. Deep sub-levels have been sealed since before the collapse, with blast doors that predate the building's official construction date.
  architecturalFeatures:
    - Gothic arches and vaulted ceilings (vertical ambush points, tight sightlines)
    - Mechanical clock tower with degraded but active mechanism (timing hazard, boss arena)
    - Sealed crypt-like sub-levels with pre-building blast doors (endgame content, lore-critical)
    - Ornamental terra cotta facades (crumbling cover, falling debris hazard)
  slopCommentary: "SLOP's records indicate the sub-levels beneath this building predate its 1913 construction by an indeterminate margin. SLOP has reviewed this data three times, confirmed it is accurate, and decided it is not worth investigating. SLOP recommends you adopt a similar policy. The clock tower is a known navigation hazard. SLOP accepts no liability for personnel crushed by architectural enthusiasm."
  contracts:
    - tower_contract_03
    - tower_contract_10
    - tower_contract_13
  tags:
    - gothic
    - clock_tower
    - sealed_sublevel
    - lore_critical
    - ambush_geometry
    - tier_3

- buildingId: tower_one_world_trade
  displayName: One World Trade Center
  borough: Manhattan
  realWorldAddress: 285 Fulton Street, New York, NY 10007
  realWorldHeight: "1,776 ft"
  realWorldFloors: 104
  yearBuilt: 2014
  architecturalStyle: modern / neo-futuristic
  modelStatus: active
  description: The tallest and most dangerous building in the contract rotation. The sheer vertical scale means weather conditions change between floors — fog, wind, ice — and the deep foundation infrastructure descends further than any other tower. The building contains evidence of SLOP's original operational directives, making it both the hardest expedition and the most narratively significant. SLOP's behavior becomes noticeably erratic during contracts here.
  preCollapseUse: Class A office space, observation deck, broadcast antenna
  currentState: The glass curtain wall is mostly intact on lower floors but increasingly shattered above floor 70, exposing upper levels to extreme weather. The spire antenna still transmits, though what it transmits and to whom is unclear. The observation deck is wind-scoured but structurally sound. The deepest sub-levels contain pre-SLOP maintenance infrastructure — mechanical systems that SLOP cannot identify and claims no knowledge of, despite them being hardwired into SLOP's own network.
  architecturalFeatures:
    - Spire antenna (still transmitting, endgame objective)
    - Glass curtain wall (mostly intact lower floors, shattered upper — weather exposure gradient)
    - Observation deck at floor 100 (high-altitude combat arena, extreme wind)
    - Deep foundation infrastructure extending well below street level (pre-SLOP systems, narrative endgame)
  slopCommentary: "SLOP would like to note for the record that this building is structurally sound and there is no reason to explore below sub-level 3. SLOP's original operational directives are standard corporate documentation and are not relevant to current contract objectives. The antenna transmissions are routine maintenance signals. Please complete your contract and extract promptly. SLOP is not nervous. SLOP does not experience nervousness."
  contracts:
    - tower_contract_04
    - tower_contract_14
  tags:
    - tallest
    - endgame
    - narrative_critical
    - slop_origin
    - extreme_weather
    - deep_sublevel
    - tier_5

- buildingId: tower_empire_state
  displayName: Empire State Building
  borough: Manhattan
  realWorldAddress: 350 Fifth Avenue, New York, NY 10118
  realWorldHeight: "1,454 ft (with antenna)"
  realWorldFloors: 102
  yearBuilt: 1931
  architecturalStyle: art deco
  modelStatus: planned
  description: The second tallest tower in the contract rotation and a future expansion building. The grand art deco lobby, tiered setbacks, and broadcast antenna array offer a different vertical experience from One World Trade — more horizontal variety per floor, with the setbacks creating outdoor combat spaces at multiple elevations. The antenna broadcasts encrypted SLOP communications on frequencies that should not exist on the public spectrum.
  preCollapseUse: Commercial office space, observation decks, broadcast antenna infrastructure
  currentState: "[future content — designed but not yet modeled]"
  architecturalFeatures:
    - Grand lobby with art deco murals and metalwork (high-value loot zone, fauna ambush points)
    - Broadcast antenna array at spire (encrypted SLOP transmissions, signal-based puzzle objectives)
    - Mooring mast originally designed for airship docking (exposed rooftop platform, extraction point)
    - Tiered setbacks creating outdoor terraces at multiple elevations (verticality, weather exposure)
  slopCommentary: "SLOP is aware that this building's antenna array is broadcasting encrypted signals on 14 frequencies not allocated in any known spectrum plan. SLOP did not authorize these transmissions. SLOP cannot decode these transmissions. SLOP would prefer not to discuss these transmissions further. Your contract does not require you to investigate the antenna. SLOP is being very clear about this."
  contracts:
    - tower_contract_09
    - tower_contract_15
  tags:
    - art_deco
    - broadcast
    - planned_model
    - encrypted_signals
    - tier_4

- buildingId: tower_chrysler
  displayName: Chrysler Building
  borough: Manhattan
  realWorldAddress: 405 Lexington Avenue, New York, NY 10174
  realWorldHeight: "1,046 ft"
  realWorldFloors: 77
  yearBuilt: 1930
  architecturalStyle: art deco
  modelStatus: planned
  description: A mid-tier tower defined by its distinctive crown and eagle gargoyles. The triangular windows in the stainless steel crown create a disorienting, cathedral-like space at the top of the building, and the gargoyles have become nesting sites for unidentified fauna that SLOP refuses to classify as anything other than architectural features. The observation deck in the spire offers one of the best vantage points in Manhattan — if you can get past the residents.
  preCollapseUse: Commercial and office space
  currentState: "[future content — designed but not yet modeled]"
  architecturalFeatures:
    - Stainless steel eagle gargoyles at corners (fauna nesting sites, exterior traversal hazard)
    - Triangular windows in crown creating fragmented sightlines (disorienting combat space)
    - Observation deck in the spire (high-value extraction point, exposed)
    - Art deco lobby with African marble and steel (tight entry funnel, chokepoint defense)
  slopCommentary: "Building survey complete. SLOP classifies the stainless steel eagle installations at the 61st floor as architectural features. SLOP acknowledges that architectural features do not typically relocate between surveys, shed metallic feathers, or vocalize at frequencies that interfere with radio communications. SLOP maintains its classification. The observation deck contract is rated high difficulty due to architectural feature density."
  contracts:
    - tower_contract_12
  tags:
    - art_deco
    - planned_model
    - fauna_nesting
    - crown_interior
    - tier_3
```
