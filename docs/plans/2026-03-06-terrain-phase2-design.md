# Terrain phase 2: Biome zones, settlements, waystations, and flora

**Date:** 2026-03-06
**Author:** Joe's Claude (junior dev)
**Approach:** A+B hybrid — layered noise foundation with explicit feature carvers

## Summary

Expand the home base terrain from 800m to 1200m with three biome zones, 4 transit waystations, moderate settlement density with merchant structures, and 39 distinct plant species tied to biome zones for future resource gameplay.

---

## 1. Terrain shape and biome zones

### Map dimensions

- **Size:** 1200m x 1200m (up from 800m)
- **Heightmap resolution:** 2049
- **Alphamap resolution:** 1024 (up from 512 for the larger map)
- **Height range:** 220m (up from 180m to accommodate escarpment)
- **Factory hub:** Center, same 50m flat radius

### Three biome zones (elevation-driven with river proximity modifier)

| Zone | Elevation band | Character | Real-world analog |
|------|---------------|-----------|-------------------|
| Floodplain | 0-35% (0-77m) | Flat to gently rolling, wet soils, river meanders. 80-120m from river center | Susquehanna/Delaware bottomlands |
| Forest | 35-70% (77-154m) | Rolling hills, 5-15 deg slopes, dense canopy. Majority of map area | Appalachian piedmont mixed hardwood |
| Rocky upland | 70-100% (154-220m) | Steep slopes, exposed rock, thin soil, sparse vegetation. Northwest quadrant | Blue Ridge/Kittatinny Ridge escarpments |

### Zone transitions

20-30m blend bands where noise parameters and flora density interpolate. No hard biome edges.

### Noise changes

- Keep existing 4-frequency stack, re-tune amplitudes for 1200m scale
- Per-zone modifiers: floodplain gets amplitude dampening (flatter), upland gets amplitude boost + higher frequency weight (rougher)
- New very-low-frequency octave (0.001, 25m amplitude) for broad regional tilt pushing northwest corner up

### Explicit feature carvers

1. **Escarpment** — ~400m cliff face running NE-SW at forest/upland boundary. Sigmoid profile along Perlin-warped line. 30-40 deg SE face, 10-15 deg NW back-slope. 25-35m height differential. Based on Appalachian ridge cross-sections.

2. **Rocky outcrops** — 5-6 formations (15-30m across) in upland zone. Heightmap bumps with steep sides (40-60 deg), subtracted surroundings. Based on granite tor profiles from Dartmoor/Appalachian balds.

3. **Wetland depression** — Oxbow-shaped low area in floodplain, 60-80m across, 2-3m below surrounding grade. Flat bottom with gentle bowl sides. Represents old meander channel.

---

## 2. Waystations (transit waypoints)

4 semi-abandoned transit structures serving as fast-travel points to satellite maps.

| Waystation | Zone | Location logic | Structures | Terrain mod |
|---|---|---|---|---|
| Bus stop | Forest (near factory, ~150m out) | Along cracked road, closest waypoint | Metal shelter, fence barriers, bench | 15x8m flat pad, road splatmap |
| Train station | Forest/floodplain boundary | River crossing where rail bridge spans | Wall+tall platform, roof canopy, rail lines from conveyor pieces | 40x12m pad, 1m embankment, rail-bed texture |
| Subway entrance | Floodplain (in ruined hamlet) | Built into hamlet ruins, stairs down | Doorway frame descending, surrounding walls, stacked floor stairs | 8x6m rectangular depression 3m deep |
| Helipad | Rocky upland (plateau) | Elevated, exposed, hardest to reach | Floor pad, fence perimeter, metal control shack | 20x20m flat pad, concrete splatmap |

Each waystation gets a trigger zone for future fast-travel mechanic (marked area, no gameplay code yet).

---

## 3. Settlements and buildings

### Placement interest map

2D score grid (same resolution as heightmap). Each cell scored 0-1 based on:
- Slope (0-8 deg preferred, >20 deg excluded)
- Elevation zone (mid-range preferred)
- Water proximity (farmsteads: 50-200m, hamlets: 20-80m)
- Factory distance (>200m minimum)

Buildings seed at local maxima.

### Settlement types

| Type | Count | Footprint | Buildings | Zone | Pattern |
|---|---|---|---|---|---|
| Isolated farmsteads | 6-8 | 20-40m | 1-2 structures + fence | Forest (4-5), floodplain (2-3) | Downslope, south-facing. Main + outbuilding |
| Small clusters | 2 | 40-60m | 3-5 around shared yard | Forest, 300-500m apart | Inward-facing, road-adjacent (crossroads hamlets) |
| Ruined hamlet | 1 | 80-120m | 12-15 along main road | Floodplain, river crossing | Linear layout. Subway entrance anchors one end |

### Merchant/POI structures

| Structure | Zone | Kit assembly | Terrain mod |
|---|---|---|---|
| Gas station | Forest, near bus stop | Tall canopy on wall pillars, metal shop, floor pump island | 25x15m flat, concrete+oil splatmap |
| Woodshop/sawmill | Forest edge | Roof+wall open shed, wood-structure log racks, fence yard | 20x20m flat, sawdust/dirt splatmap |
| Market stalls | Hamlet center | Canvas awnings on wall pillars, cover tarps, floor-old counters | Part of hamlet footprint |
| Mechanic's garage | Near train station | Yellow walls, wide bay doors, metal lean-to | 15x12m flat, oil-stained concrete |
| General store | Hamlet | Wall+window+doorway+roof, porch | Part of hamlet footprint |

### Building construction (Kenney kits)

- Farmstead main: structure + structure-roof + structure-floor, with floor-old/floor-hole for decay
- Farmstead outbuilding: structure-metal-* pieces (shed look)
- Hamlet buildings: Mix of structure-wall + structure-window + structure-doorway (conveyor-kit), structure-canvas repairs
- Decay: fence-* (broken perimeters), cover-* (tarps), detail-rocks, detail-dirt scatter

### Decay and abandonment rules

- 30-50% of structures missing roof pieces (collapsed)
- Random rotation offset (+/-3 deg) on walls (structural settling)
- Vegetation encroachment: undergrowth within 5m, trees through roofless structures
- Debris scatter 10-15m radius around each building

---

## 4. Flora and vegetation

### Design principle

Cosmetic now, structured for future resource hooks via BiomeTag component.

```csharp
public enum BiomeZone { Floodplain, Forest, RockyUpland }

public class BiomeTag : MonoBehaviour
{
    public BiomeZone Zone;
    public string SpeciesId;
}
```

### Floodplain canopy (6 species)

| Species | Silhouette | Scale | Color | Placement |
|---|---|---|---|---|
| Weeping willow | Drooping sphere (inverted dome), thin trunk | 7-10m tall, 6-8m droop | Yellow-green | Riverbank singles |
| Cottonwood | Tall columnar, thick trunk | 10-15m tall, 4-5m spread | Silver-green | Terrace pairs |
| Sycamore | Broad irregular (flat sphere), pale trunk | 10-14m tall, 8-12m spread | Light green, white trunk | Open floodplain singles |
| River birch | Multi-trunk (2-3 thin), small oval crowns | 6-9m tall, 3-4m spread | Warm green, tan bark | Clusters of 3-4 near water |
| Black walnut | Dense round, straight dark trunk | 9-13m tall, 7-9m spread | Deep green | Mid-floodplain singles |
| Box elder | Asymmetric offset sphere, leaning trunk | 5-8m tall, 4-6m spread | Light green | Edges, fence lines, near ruins |

### Forest canopy (8 species)

| Species | Silhouette | Scale | Color | Placement |
|---|---|---|---|---|
| Red oak | Wide dome, thick spreading base | 10-16m tall, 9-12m spread | Dark green (autumn: orange-red) | Dominant, clusters |
| Sugar maple | Dense rounded, medium trunk | 9-14m tall, 7-10m spread | Green (autumn: gold-orange) | Mixed with oak |
| Shagbark hickory | Tall narrow, straight trunk | 12-18m tall, 5-7m spread | Medium green | Scattered among oak/maple |
| White ash | Open oval, clean straight trunk | 11-16m tall, 6-9m spread | Light green | Forest edges, road margins |
| American beech | Smooth spreading, silver-grey trunk | 8-12m tall, 8-11m spread | Dark green, grey trunk | Interior singles |
| Black cherry | Narrow irregular, dark trunk | 8-14m tall, 4-6m spread | Dark green | Forest gaps |
| Tulip poplar | Tall straight trunk, small high crown | 15-22m tall, 5-7m spread | Bright green | Tallest landmark trees |
| Ironwood | Small twisted, fluted trunk | 5-8m tall, 4-6m spread | Dark green | Deep forest understory |

### Rocky upland canopy (4 species)

| Species | Silhouette | Scale | Color | Placement |
|---|---|---|---|---|
| Pitch pine | Irregular wind-shaped cone, bent trunk | 5-10m tall, 3-6m spread | Blue-green | Ridgeline, sparse |
| Eastern red cedar | Dense narrow column, trunk to ground | 4-8m tall, 2-3m spread | Dark blue-green | Rock margins, pairs |
| Chestnut oak | Low spreading, gnarled trunk | 6-10m tall, 7-10m spread | Olive green | Sheltered pockets |
| Scrub oak | Dense low dome, multi-stem | 2-4m tall, 3-5m spread | Grey-green | Exposed slope thickets |

### Understory / shrubs (11 species)

| Species | Zone | Scale |
|---|---|---|
| Mountain laurel | Forest | 2-4m |
| Rhododendron | Forest (ravines) | 2-5m |
| Witch hazel | Forest edges | 3-5m |
| Spicebush | Forest interior | 2-3m |
| Elderberry | Floodplain | 2-4m |
| Buttonbush | Floodplain (wet) | 1-3m |
| Blueberry scrub | Upland | 0.5-1.5m |
| Mountain azalea | Upland edges | 1-2m |
| Blackberry bramble | All (disturbed areas, near ruins) | 1-2m |
| Sumac | Forest/upland transition | 3-5m, clusters of 5-10 |
| Virginia creeper | All (on ruins) | Vine on walls |

### Ground cover (10 types)

| Type | Zone |
|---|---|
| Ostrich fern | Forest, floodplain |
| Sedge grass | Floodplain (dense near water) |
| Cattails | Floodplain water edge |
| Moss/lichen | Upland, forest rocks |
| Wildflower patches | Forest clearings |
| Mushroom clusters | Forest interior (dark) |
| Leaf litter | Forest floor (continuous) |
| Pine needle mat | Upland under pines |
| Moss hummocks | Floodplain wet areas |
| Exposed rock rubble | Upland around outcrops |

### Density per 100m2

| Zone | Canopy | Understory | Ground cover |
|---|---|---|---|
| Floodplain | 2-3 | 4-6 | 8-12 |
| Forest | 4-6 | 6-10 | 10-15 |
| Rocky upland | 0.5-1 | 2-3 | 3-5 |

### Vegetation rules near structures

- No trees within 8m of building footprints
- Undergrowth encroachment up to 2m from walls
- 1-2 "reclaimed" trees per hamlet (trunks inside roofless structures)
- 5m clear strip along roads, then dense undergrowth at edges

### Future resource mapping

| Zone | Resource types |
|---|---|
| Floodplain | Medicinal herbs, fiber plants, clay deposits |
| Forest | Hardwood, nuts, foraging ingredients |
| Rocky upland | Stone, rare minerals, wind-resistant fibers |

---

## 5. Implementation notes

### Code changes

All changes go in `HomeBaseSceneryDresser.cs`. New/modified methods:

- Update constants: `TerrainWidth = 1200f`, `TerrainHeight = 220f`, heightmap res 2049
- `GetBiomeZone(float elevation, float riverDist)` — returns zone enum for any point
- `AddTerrainNoise()` — add low-frequency regional tilt octave, per-zone amplitude modifiers
- `CarveEscarpment()` — sigmoid profile along Perlin-warped line (new method)
- `CarveOutcrops()` — 5-6 steep bumps in upland zone (new method)
- `CarveWetland()` — oxbow depression in floodplain (new method)
- `CarveWaystationPads()` — flatten terrain at 4 waystation locations (new method)
- `RepaintSplatmap()` — expand to handle 3 zones, waystation textures, road textures
- `PlaceWaystations()` — build 4 transit structures from kit pieces (new method)
- `PlaceSettlements()` — refactor existing ruin/camp placement into interest-map-based system
- `PlaceMerchantStructures()` — gas station, woodshop, garage, etc. (new method)
- `ScatterNature()` — refactor to use biome-zone species assignments and density gradients
- Add `BiomeZone` enum and `BiomeTag` component

### New C# files

- `Assets/_Slopworks/Scripts/Debug/BiomeTag.cs` — simple MonoBehaviour with zone and species ID

### Operation order in Dress()

```
UpgradeTerrainTextures(td);
AddTerrainNoise(td);           // includes regional tilt
AddTerrainFeatures(td);
CarveEscarpment(td);           // NEW
CarveOutcrops(td);             // NEW
CarveWetland(td);              // NEW
SmoothHeightmap(td, 6);
CarveRiverValley(td);          // after smoothing
CarveWaystationPads(td);       // after river (flat pads)
RepaintSplatmap(td);
SetupSkybox();
ScatterNature(...);            // biome-aware
PlaceSettlements(...);         // interest-map-based
PlaceMerchantStructures(...);  // NEW
PlaceWaystations(...);         // NEW
ScatterIndustrial(...);
DecorateRiverbed(...);
CreateRiverWater(...);
PaintTerrainGrass(td, rng);
SetupAmbientParticles();
```

### Real-world data sources referenced

- USGS 3DEP LiDAR profiles for Appalachian river valley elevation/slope distributions
- USDA Forest Inventory data for eastern deciduous species zonation
- Archaeological survey patterns for Appalachian farmstead spacing and orientation
- OpenTopography granite tor and ridge cross-section profiles
- European rural settlement morphology studies (linear villages, crossroads hamlets)
