# Overworld terrain design

**Date:** 2026-03-06
**Author:** Joe (brainstormed with Claude)
**Status:** Approved

---

## Summary

Isometric hex-grid terrain for the overworld strategy map. 128x128 pointy-top hexes grouped into 8x8 chunks. Each chunk is a single combined mesh with vertex-colored biomes and noise-driven height variation. Ruin decorations use Kenney models. Building nodes from `OverworldMap` are placed as 3D markers on specific hexes.

---

## Grid system

128x128 **pointy-top hex grid**. Each hex is ~2m across (flat-to-flat), making the full map ~256m wide. Hexes grouped into **8x8 chunks** (16 chunks per axis = 256 chunks total). Each chunk builds a single combined mesh with shared vertices at hex boundaries for smooth height transitions.

Hex coordinates use **axial (q, r)** notation internally. Conversion to world-space XZ:
- `x = size * (sqrt(3) * q + sqrt(3)/2 * r)`
- `z = size * (3/2 * r)`
- `y` = sampled from noise heightmap

---

## Noise generation

Three noise layers using `Mathf.PerlinNoise` with offset seeds:

1. **Elevation** -- low frequency, 3 octaves. Range 0-8m. Flat near center (home base), rising toward edges.
2. **Temperature** -- very low frequency, single octave. Maps to warm/cold axis.
3. **Moisture** -- very low frequency, single octave, different seed. Maps to wet/dry axis.

Temperature + moisture produce a biome via lookup table.

---

## Biome lookup table

| | Dry | Medium | Wet |
|---|---|---|---|
| **Warm** | Wasteland | Grassland | Swamp |
| **Cool** | Ruins | Forest | Overgrown Ruins |

Home base is always at center. Ruins biome probability increases with distance from center.

---

## Biome vertex colors

Each biome maps to a vertex color on the hex mesh:

| Biome | Color (RGB) |
|---|---|
| Grassland | `(0.35, 0.45, 0.25)` |
| Forest | `(0.2, 0.35, 0.15)` |
| Wasteland | `(0.5, 0.4, 0.28)` |
| Swamp | `(0.2, 0.3, 0.25)` |
| Ruins | `(0.4, 0.38, 0.35)` |
| Overgrown Ruins | `(0.3, 0.38, 0.28)` |

The hex mesh shader reads vertex color as base albedo. Hex edges blend between neighboring biome colors for smooth transitions.

---

## Height variation

Each hex center gets height from elevation noise. Hex vertices average the heights of adjacent hex centers for smooth slopes. This gives a rolling terrain feel without sharp cliffs. Steep height differences between hexes get a cliff-face side mesh (darker color).

---

## Ruin decorations

Hexes in ruin biomes have a chance to spawn small Kenney model clusters:
- Conveyor-kit wall fragments, covers, structures
- Survival-kit barrels, metal panels, fences
- Scaled and tilted for a collapsed/overgrown look
- Placed as child GameObjects of the chunk, static-batched

---

## Building nodes

Building nodes from `OverworldMap` are placed on specific hexes. Each node gets a small 3D marker (Kenney structure model or colored primitive) at the hex surface height. Marker style varies by `OverworldNodeType`:
- **HomeBase**: bright marker at map center
- **Building**: building-type icon (varies by biome)
- **Tower**: tall vertical marker

---

## Chunk loading

All 256 chunks generate at startup. 128x128 hexes is ~16K hexes, ~100K vertices total -- small enough to generate in one pass. No streaming needed. Generation runs in an editor script and saves as a scene.

---

## Scene structure

Scene: `Assets/_Slopworks/Scenes/Overworld/Overworld_Terrain.unity`

```
OverworldTerrain (root)
  Chunks/          -- 256 chunk GameObjects (MeshFilter + MeshRenderer + MeshCollider)
  Decorations/     -- ruin props grouped by chunk
  NodeMarkers/     -- building node indicators
IsometricCamera    -- orthographic, positioned above center
Directional Light  -- warm post-apocalyptic sun
```

Lighting and fog match HomeBase scene (warm orange sun, linear fog).

---

## Implementation files

| File | Location | Purpose |
|---|---|---|
| `OverworldTerrainGenerator.cs` | `Scripts/Editor/` | Editor menu script that generates the full scene |
| `HexGridUtility.cs` | `Scripts/World/` | Axial hex math, world-space conversion, neighbor lookup |
| `OverworldBiome.cs` | `Scripts/World/` | Biome enum and color lookup |
| `OverworldChunkMeshBuilder.cs` | `Scripts/World/` | Builds combined hex mesh for one 8x8 chunk |

All generation is editor-time. No runtime generation needed for the terrain itself (the `OverworldMap` node registry is runtime, but the visual terrain is baked).

---

## Open questions

- Exact hex size may need tuning after visual inspection (2m might be too small or too large for the isometric camera distance)
- Fog of war / unexplored territory masking is a gameplay feature, not part of this terrain task
- Supply line visuals (lines connecting building nodes) will be a separate task
