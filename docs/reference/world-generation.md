# World generation and exploration reference

Slopworks has three distinct world spaces, each generated differently.

---

## Three world spaces

| Space | Type | Generation | Camera |
|---|---|---|---|
| Home Base | Persistent, player-built | Flat terrain, procedural resource nodes at edges | Isometric + first-person |
| Reclaimed Buildings | BIM-sourced levels | Imported from Revit/Navisworks data | First-person |
| Overworld / Network Map | Territory map | Procedural region + threat map | Isometric only |

These are **separate scenes**, not a streaming open world. No seamless transitions required. Load/unload on scene change.

---

## Overworld map generation

The overworld shows territory, buildings, supply lines, threat levels, and the unexplored frontier. It's tile-based (isometric).

### Noise stack

Use **FastNoiseLite** (MIT, C# port available). Much better than Unity's `Mathf.PerlinNoise` — supports domain warping, avoids directional artifacts.

**Repo:** `https://github.com/Auburn/FastNoiseLite`

Layer order:
1. **Elevation** — Simplex noise, 4–6 fBm octaves
2. **Temperature + humidity** — two separate low-frequency noise maps → biome lookup table
3. **Domain warping** — distort sample coordinates before biome lookup to break rectangular biome boundaries
4. **Building placement** — separate noise pass per building type, biome-weighted thresholds

### Chunk system (overworld)

Chunk size: 16×16 tiles. Server generates on demand from seed.

```
Pristine chunk: generated from seed on demand, not persisted
Dirty chunk:    any tile modified (building placed, territory claimed) → serialize to disk
```

Delta format: store only positions that differ from the seed baseline. Untouched territory costs nothing to store.

### Building placement on overworld map

Follow the Factorio autoplace model:
1. After terrain gen, run a separate pass for each building type
2. For each building: `if noise(x, y, buildingSeed + typeOffset) > threshold` → candidate location
3. Biome-weight the threshold: hospitals more likely in urban biomes, warehouses in industrial zones
4. Minimum distance guard: no claimable buildings within N tiles of start point
5. Scale threat level with distance from origin: nearby = easy, frontier = hard

---

## Home Base terrain

Flat buildable terrain with procedural resource nodes at the edges (ore patches, fuel deposits). Players lay foundation tiles before building — foundations snap to a fixed grid.

Terrain generation is simple: flat heightmap with decorative noise for ground texture variation. Resource nodes use the same Factorio-style noise placement as the overworld.

### Resource node design (recommended: Satisfactory hybrid)

Fixed node locations (always there), but extraction rate scales with node purity (high/medium/low). Nodes never deplete — you upgrade your extractor. Scarcity comes from location and logistics, not from the node running out.

This is more robust for multiplayer: players compete for high-purity nodes, not race to exhaust finite patches before their partner.

---

## Reclaimed Buildings (BIM pipeline)

This is Slopworks' core differentiator. Real building models from Kevin's BIM data become explorable first-person levels.

### Import pipeline (to be designed)

```
Revit/Navisworks model
  → IFC or FBX export
  → Unity import (FBX importer or paid IFC plugin)
  → Automated mesh cleanup (combine materials, LOD generation)
  → MEP system identification (pipes = restorable, ducts = restorable)
  → Collision mesh generation
  → Navmesh bake for fauna AI
  → Fauna spawn point placement (near mechanical rooms, dark areas)
  → Hazard placement (toxic zones, structural damage)
```

Each building is a self-contained scene with its own:
- Fauna population (type scales with building difficulty)
- MEP systems to restore (which systems exist depends on building type)
- Loot distribution
- Atmospheric narrative (AI-generated dossier from building metadata)

### Ruined variants

Point cloud data can generate damaged/overgrown versions:
- Remove sections of the mesh to simulate collapse
- Add vegetation via scatter placement
- Add water/flooding in low points
- Dirty/corrode material overrides

---

## Multiplayer world sync

### World generation authority

Server generates all world content. Clients never generate terrain independently.

```
Player enters chunk range
  → Client sends chunk request to server
  → Server generates or loads chunk from disk
  → Server sends chunk data to client
  → Client renders from received data
```

Clients do not generate from the seed locally — even with the same seed, floating-point and platform differences cause drift over time.

### Delta sync for world changes

After initial chunk load, server sends only modification events:

```
TileClaimed(chunkId, x, y, playerId)
BuildingPlaced(chunkId, x, y, buildingType, rotation)
BuildingDestroyed(chunkId, x, y)
ResourceNodeDepleted(nodeId, remaining)
ThreatLevelChanged(newLevel)
```

Clients apply deltas and re-render affected tiles. No full chunk re-sends after initial load.

### Interest management

Clients subscribe to chunks within view distance. Home Base view distance is larger (players need to see their whole factory). Buildings have natural bounds (you're inside them). Overworld manages large distances differently — use LOD tiles beyond close range.

---

## Day/night cycle

Server maintains `worldTime` float. Broadcast to all clients on a slow tick (every 1–2 seconds). Clients interpolate locally between ticks using `NetworkTime.ServerTime`.

```csharp
// Client-side lighting driver
void Update() {
    float serverTime = NetworkManager.TimeManager.TicksToTime(NetworkManager.TimeManager.Tick);
    float timeOfDay = serverTime % DAY_LENGTH_SECONDS / DAY_LENGTH_SECONDS;
    sun.transform.rotation = Quaternion.Euler(timeOfDay * 360f - 90f, 30f, 0f);
    // drive ambient + fog from timeOfDay curve
}
```

---

## Performance

### Tile rendering

Use GPU instancing for repeated tile objects: `Graphics.DrawMeshInstancedProcedural`. Group all tiles of the same type into one draw call. Floor tiles use static batching (they never move).

### LOD

All 3D world objects (buildings on overworld, resource nodes, trees) use Unity LOD Groups:
- LOD0: full detail, close range
- LOD1: reduced geometry, mid range
- LOD2: billboard/impostor, far range
- Culled: beyond render distance

### Chunk mesh generation

Use Unity Job System + Burst Compiler for background chunk mesh generation. Never block the main thread for terrain work. Push completed meshes to main thread only for upload.

---

## Tools

| Tool | Purpose |
|---|---|
| FastNoiseLite | Noise generation (replace `Mathf.PerlinNoise`) |
| Unity Addressables | Async chunk/building asset loading |
| Unity Job System + Burst | Background chunk generation |
| Wave Function Collapse (Tessera) | Structured content: enemy camps, ruins, interiors |
| MapMagic 2 | Optional: node-based terrain for Home Base (check Unity 6 compatibility before using) |
