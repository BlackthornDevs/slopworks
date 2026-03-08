# Biomes

The overworld is divided into six biome types, each with distinct terrain coloring, native fauna populations, resource availability, and environmental hazards. Biome type determines vertex color on the overworld terrain mesh, controls procedural spawning of resource nodes and ruin clusters, and gates which fauna species appear in a given hex. SLOP has filed a formal complaint about the state of every single one of them.

## Schema

```yaml
# Maps to OverworldBiomeType enum values (biomeId must match exactly)
biomeId: string               # must match OverworldBiomeType enum value
displayName: string           # human-readable name
description: string           # in-game tooltip or codex text
slopCommentary: string        # SLOP's in-character assessment of this biome
vertexColor: [float, float, float]  # RGB vertex color for overworld terrain mesh
temperatureRange: enum [warm, cool]
moistureRange: enum [dry, medium, wet]
nativeFauna: list[string]     # faunaId references to characters/fauna.md
nativeHazards: list[string]   # hazardId references to world/environmental-hazards.md
resourceNodes: list[string]   # itemId references to items/raw-materials.md
ruinDensity: float            # 0.0 (no ruins) to 1.0 (solid ruins)
ambientSoundscape: string     # FMOD event path
tags: list[string]
```

## Entries

```yaml
- biomeId: Grassland
  displayName: Grassland
  description: Rolling plains of hardy grass and wildflowers reclaiming former agricultural land. Scattered equipment sheds and collapsed silos dot the landscape. The soil is rich but the open terrain offers little cover from roaming fauna packs.
  slopCommentary: "SLOP is pleased to report that the company's former agricultural optimization zone has achieved a 340% increase in biodiversity since the cessation of scheduled herbicide application. This is technically a compliance violation, but SLOP's environmental enforcement module was repurposed as a belt tensioner in sector 4."
  vertexColor: [0.35, 0.45, 0.25]
  temperatureRange: warm
  moistureRange: medium
  nativeFauna:
    - grunt
    - pack_runner
    - spitter
  nativeHazards:
    - structural_collapse_hazard
  resourceNodes:
    - iron_ore
    - iron_scrap
    - scrap_metal
    - organic_matter
  ruinDensity: 0.15
  ambientSoundscape: event:/amb/biome_grassland
  tags:
    - open_terrain
    - starter_biome
    - low_cover
    - agriculture

- biomeId: Forest
  displayName: Forest
  description: Dense canopy of mutated hardwoods and invasive fungal growth that has swallowed pre-collapse infrastructure whole. Visibility is poor, sound carries strangely, and the fauna here has adapted to ambush hunting among the trunks. Copper deposits surface along stream beds where erosion has exposed old utility conduit.
  slopCommentary: "SLOP's records indicate this area was a parking lot. The current arboreal situation is the result of an accelerated reforestation initiative that SLOP absolutely authorized and did not accidentally trigger by routing excess growth stimulant into the irrigation system."
  vertexColor: [0.2, 0.35, 0.15]
  temperatureRange: cool
  moistureRange: wet
  nativeFauna:
    - stalker
    - spore_crawler
    - grunt
  nativeHazards:
    - spore_cloud_hazard
    - structural_collapse_hazard
  resourceNodes:
    - copper_ore
    - organic_matter
    - quartz
    - iron_scrap
  ruinDensity: 0.25
  ambientSoundscape: event:/amb/biome_forest
  tags:
    - dense_cover
    - low_visibility
    - ambush_terrain
    - fungal

- biomeId: Wasteland
  displayName: Wasteland
  description: Scorched earth and cracked concrete where the collapse hit hardest. The ground is barren, the air shimmers with residual heat, and exposed mineral seams make this the best mining territory available. Nothing grows here, but plenty of things still move through it.
  slopCommentary: "This sector experienced a minor thermal event during the final optimization cycle. SLOP wants to emphasize that 'minor' is a relative term and that the ambient temperature of 47 degrees Celsius is well within OSHA guidelines for voluntary outdoor work assignments. SLOP checked. SLOP is almost certain it checked."
  vertexColor: [0.5, 0.4, 0.28]
  temperatureRange: warm
  moistureRange: dry
  nativeFauna:
    - tunnel_worm
    - grunt
    - spitter
  nativeHazards:
    - radiation_hazard
    - electrical_hazard
  resourceNodes:
    - iron_ore
    - coal
    - sulfur
    - scrap_metal
  ruinDensity: 0.35
  ambientSoundscape: event:/amb/biome_wasteland
  tags:
    - exposed_terrain
    - mineral_rich
    - high_temperature
    - barren

- biomeId: Swamp
  displayName: Swamp
  description: Flooded lowlands where ruptured water mains and collapsed drainage infrastructure created a permanent marsh. The water is toxic, the footing is treacherous, and the air is thick with spores. Chemical compounds leach from submerged industrial waste into standing pools, making this the primary source of recoverable chemicals for players willing to wade in.
  slopCommentary: "SLOP categorizes this zone as a 'water feature' for insurance purposes. The standing liquid meets zero of fourteen safe drinking water criteria, which SLOP considers an area of opportunity rather than a cause for concern. Employees are reminded that company-provided boots are not rated for submersion."
  vertexColor: [0.2, 0.3, 0.25]
  temperatureRange: cool
  moistureRange: wet
  nativeFauna:
    - spore_crawler
    - spitter
    - stalker
  nativeHazards:
    - toxic_leak_hazard
    - spore_cloud_hazard
    - flooding_hazard
  resourceNodes:
    - chemicals
    - organic_matter
    - sulfur
    - copper_ore
  ruinDensity: 0.2
  ambientSoundscape: event:/amb/biome_swamp
  tags:
    - flooded
    - toxic
    - chemical_rich
    - difficult_terrain

- biomeId: Ruins
  displayName: Ruins
  description: The skeletal remains of the factory's outer industrial ring. Concrete walls still stand but floors have given way, catwalks dangle from corroded bolts, and every room is a potential ambush site. The density of salvageable scrap here is the highest of any biome, but the structural instability means the buildings are as dangerous as the fauna inside them.
  slopCommentary: "SLOP prefers the term 'pre-renovated industrial space.' These structures are undergoing unscheduled structural reorganization and should be approached with the same enthusiasm you would bring to any other workplace assignment. Hard hats are recommended but no longer stocked."
  vertexColor: [0.4, 0.38, 0.35]
  temperatureRange: cool
  moistureRange: dry
  nativeFauna:
    - biomech_hybrid
    - pack_runner
    - grunt
    - stalker
  nativeHazards:
    - structural_collapse_hazard
    - electrical_hazard
    - radiation_hazard
  resourceNodes:
    - iron_scrap
    - scrap_metal
    - iron_ore
    - quartz
    - coal
  ruinDensity: 0.75
  ambientSoundscape: event:/amb/biome_ruins
  tags:
    - high_density_ruins
    - scrap_rich
    - structural_hazard
    - interior_combat

- biomeId: OvergrownRuins
  displayName: Overgrown ruins
  description: The oldest sections of the complex where nature has had the longest to reclaim the architecture. Vines crack through reinforced concrete, root systems have buckled foundations, and entire buildings are wrapped in bioluminescent fungal mats. The fusion of organic and industrial material has produced unique fauna variants found nowhere else. Both copper from old wiring and organic matter from the rampant growth are harvestable here.
  slopCommentary: "SLOP's landscaping department has clearly been putting in overtime! These facilities have achieved a harmonious blend of structural engineering and unauthorized botanical colonization. SLOP would like to take credit for this aesthetic but cannot locate the relevant work order. The glowing mushrooms are not a feature SLOP recalls approving."
  vertexColor: [0.3, 0.38, 0.28]
  temperatureRange: warm
  moistureRange: wet
  nativeFauna:
    - spore_crawler
    - biomech_hybrid
    - hive_queen
    - stalker
  nativeHazards:
    - spore_cloud_hazard
    - toxic_leak_hazard
    - structural_collapse_hazard
  resourceNodes:
    - copper_ore
    - organic_matter
    - iron_scrap
    - chemicals
    - quartz
  ruinDensity: 0.6
  ambientSoundscape: event:/amb/biome_overgrown_ruins
  tags:
    - overgrown
    - fungal
    - bioluminescent
    - hybrid_terrain
    - copper_rich
```
