# Terrain phase 2 implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Expand home base terrain to 1200m with 3 biome zones, 4 waystations, settlements with merchant structures, and 39 plant species.

**Architecture:** All changes go in `HomeBaseSceneryDresser.cs` (editor script) plus one new runtime file `BiomeTag.cs`. The dresser is a static class with a menu item — no MonoBehaviour, no tests. Verification is running the dresser via Unity menu and checking console logs + visual output in the scene view.

**Tech Stack:** Unity Editor API, TerrainData, Perlin noise, Kenney asset kit models

**Design doc:** `docs/plans/2026-03-06-terrain-phase2-design.md`

---

## Task overview

| Task | What | Dependencies |
|------|------|-------------|
| 1 | BiomeTag component + BiomeZone enum | None |
| 2 | Update constants and terrain resize | None |
| 3 | GetBiomeZone helper + waystation/settlement position data | None |
| 4 | Regional tilt noise + per-zone amplitude modifiers | 2 |
| 5 | Escarpment carver | 2, 3 |
| 6 | Outcrop carver | 2, 3 |
| 7 | Wetland depression carver | 2, 3 |
| 8 | Waystation pad flattener | 2, 3 |
| 9 | Biome-aware splatmap | 2, 3 |
| 10 | Biome-aware nature scatter (18 canopy + 11 understory + 10 ground cover) | 2, 3 |
| 11 | Interest-map settlement placement | 2, 3 |
| 12 | Merchant structures | 2, 3, 11 |
| 13 | Waystation structures | 2, 3, 8 |
| 14 | Update Dress() call order + fog/camera for 1200m | 2-13 |
| 15 | Run dresser, verify, commit | 14 |

---

### Task 1: BiomeTag component and BiomeZone enum

**Files:**
- Create: `Assets/_Slopworks/Scripts/Debug/BiomeTag.cs`

**Step 1: Create BiomeTag.cs**

```csharp
using UnityEngine;

public enum BiomeZone { Floodplain, Forest, RockyUpland }

public class BiomeTag : MonoBehaviour
{
    public BiomeZone Zone;
    public string SpeciesId;
}
```

**Step 2: Verify compilation**

Run: Unity menu > Assets > Refresh (or let auto-compile run)
Expected: No errors in console

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/BiomeTag.cs Assets/_Slopworks/Scripts/Debug/BiomeTag.cs.meta
git commit -m "Add BiomeZone enum and BiomeTag component for future resource hooks"
```

---

### Task 2: Update constants and terrain resize

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:12-15` (constants)
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:157-162` (resize block in Dress())

**Step 1: Update constants**

Change lines 12-15:
```csharp
private const float TerrainWidth = 1200f;
private const float TerrainHeight = 220f;
```

**Step 2: Update terrain resize in Dress()**

Change lines 157-159:
```csharp
td.heightmapResolution = 2049;
td.alphamapResolution = 1024;
td.SetDetailResolution(1024, 16);
```

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Scale terrain to 1200m with 220m height range and higher resolutions"
```

---

### Task 3: GetBiomeZone helper + position data arrays

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add after line 37 (after RiverbedPoints)

**Step 1: Add waystation position data**

Add after the `RiverbedPoints` declaration (line 37):

```csharp
// waystation world-space positions (x, z) — placed by design
private static readonly Vector2[] WaystationPositions = {
    new(150f, -50f),    // bus stop: forest, near factory along road
    new(-80f, 300f),    // train station: forest/floodplain boundary, near river crossing
    new(-60f, 380f),    // subway entrance: floodplain, in ruined hamlet
    new(-350f, -300f),  // helipad: rocky upland plateau, NW quadrant
};
private static readonly Vector2[] WaystationPadSizes = {
    new(15f, 8f),   // bus stop
    new(40f, 12f),  // train station
    new(8f, 6f),    // subway entrance
    new(20f, 20f),  // helipad
};

// settlement cluster centers (world-space x, z)
private static readonly Vector2[] FarmsteadPositions = {
    new(200f, 50f),
    new(-180f, 120f),
    new(300f, -180f),
    new(-250f, -100f),
    new(120f, 200f),
    new(-300f, 200f),
    new(350f, 100f),
};
private static readonly Vector2[] SmallClusterPositions = {
    new(250f, 250f),
    new(-200f, -250f),
};
private static readonly Vector2 HamletCenter = new(-40f, 350f); // near subway + river crossing

// merchant structure positions (world-space x, z)
private static readonly Vector2 GasStationPos = new(120f, -30f);   // near bus stop
private static readonly Vector2 WoodshopPos = new(280f, 150f);     // forest edge
private static readonly Vector2 GaragePos = new(-100f, 280f);      // near train station
```

**Step 2: Add GetBiomeZone helper**

Add as a new method near the helpers section (before `SampleWorldHeight`, around line 1714):

```csharp
private static BiomeZone GetBiomeZone(TerrainData td, float nx, float nz)
{
    // elevation-based with river proximity modifier
    float height = td.GetHeight(
        Mathf.Clamp((int)(nx * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1),
        Mathf.Clamp((int)(nz * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1));
    float normalizedHeight = height / TerrainHeight;

    // river proximity boosts floodplain zone
    float riverZ = RiverCenterZ(nx);
    float riverDist = Mathf.Abs(nz - riverZ) * TerrainWidth;

    if (normalizedHeight < 0.35f || riverDist < 120f)
        return BiomeZone.Floodplain;
    if (normalizedHeight > 0.70f)
        return BiomeZone.RockyUpland;
    return BiomeZone.Forest;
}

private static BiomeZone GetBiomeZoneFromWorldPos(TerrainData td, float wx, float wz)
{
    float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
    float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
    return GetBiomeZone(td, nx, nz);
}
```

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add biome zone helper, waystation positions, and settlement layout data"
```

---

### Task 4: Regional tilt noise + per-zone amplitude modifiers

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:315-376` (`AddTerrainNoise` method)

**Step 1: Update AddTerrainNoise**

Replace the method body. Key changes:
- Add 5th octave: very low frequency (0.001) with 25m amplitude for regional tilt pushing NW corner up
- Per-zone amplitude modifiers: dampen floodplain noise, boost upland noise
- Re-tune existing octave amplitudes for 1200m scale

```csharp
private static void AddTerrainNoise(TerrainData td)
{
    int res = td.heightmapResolution;
    float[,] heights = td.GetHeights(0, 0, res, res);

    // multi-octave Perlin noise tuned for 1200m map
    float[] frequencies = { 0.002f, 0.006f, 0.016f, 0.04f };
    float[] amplitudes = { 22f, 9f, 4f, 1.5f };
    float[] offsets = { 0f, 137f, 293f, 431f };

    for (int z = 0; z < res; z++)
    {
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1);
            float nz = (float)z / (res - 1);

            float wx = nx * TerrainWidth;
            float wz = nz * TerrainWidth;

            float dx = nx - 0.5f;
            float dz = nz - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float outerMask = Mathf.Clamp01((dist - 0.04f) / 0.04f);

            // regional tilt: push NW corner up for rocky upland zone
            float tiltNW = (1f - nx) * (1f - nz); // 1 at NW corner, 0 at SE
            float regionalTilt = tiltNW * 25f + Mathf.PerlinNoise(wx * 0.001f + 577f, wz * 0.001f + 577f) * 20f;

            float totalNoise = 0f;
            for (int o = 0; o < frequencies.Length; o++)
            {
                float n = Mathf.PerlinNoise(
                    wx * frequencies[o] + offsets[o],
                    wz * frequencies[o] + offsets[o] + 50f);
                totalNoise += (n - 0.5f) * amplitudes[o];
            }

            // per-zone amplitude modifier based on preliminary elevation
            float prelimHeight = (regionalTilt + totalNoise * outerMask) / TerrainHeight;
            float zoneMod = 1f;
            if (prelimHeight < 0.35f)
                zoneMod = 0.5f; // floodplain: flatter
            else if (prelimHeight > 0.70f)
                zoneMod = 1.5f; // upland: rougher

            // broad valley depression where river will go
            float riverValleyCenter = 0.72f;
            float valleyDist = Mathf.Abs(nz - riverValleyCenter);
            float valleyWidth = 0.07f;
            float valleyDepth = 16f;
            float valleyFactor = 0f;
            if (valleyDist < valleyWidth)
            {
                float t = valleyDist / valleyWidth;
                valleyFactor = valleyDepth * (1f - t * t);
            }

            float heightDelta = (regionalTilt + totalNoise * outerMask * zoneMod - valleyFactor) / TerrainHeight;
            heights[z, x] += heightDelta;
        }
    }

    td.SetHeights(0, 0, heights);
    Debug.Log("terrain noise added: 4 octaves + regional tilt + valley depression");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add regional tilt and per-zone noise modifiers for 1200m terrain"
```

---

### Task 5: Escarpment carver

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method after `AddTerrainFeatures` (after line 509)

**Step 1: Add CarveEscarpment method**

```csharp
private static void CarveEscarpment(TerrainData td)
{
    int res = td.heightmapResolution;
    float[,] heights = td.GetHeights(0, 0, res, res);

    // ~400m cliff face running NE-SW at forest/upland boundary
    // centered around normalized (0.3, 0.3) running to (0.15, 0.45)
    // sigmoid profile: steep SE face (30-40 deg), gentle NW back-slope (10-15 deg)
    float escarpmentHeight = 30f / TerrainHeight;
    float steepWidth = 20f / TerrainWidth;   // SE face: 20m to drop 30m = ~56 deg
    float gentleWidth = 80f / TerrainWidth;  // NW back: 80m to rise 30m = ~20 deg

    for (int z = 0; z < res; z++)
    {
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1);
            float nz = (float)z / (res - 1);

            // escarpment line runs NE-SW: from (0.15, 0.15) to (0.45, 0.40)
            // direction vector normalized
            float lineStartX = 0.15f;
            float lineStartZ = 0.15f;
            float lineDirX = 0.30f;
            float lineDirZ = 0.25f;
            float lineLen = Mathf.Sqrt(lineDirX * lineDirX + lineDirZ * lineDirZ);
            float ldx = lineDirX / lineLen;
            float ldz = lineDirZ / lineLen;

            // project point onto line
            float px = nx - lineStartX;
            float pz = nz - lineStartZ;
            float along = px * ldx + pz * ldz;
            float across = px * (-ldz) + pz * ldx; // signed distance: positive = SE side

            // only affect points near the escarpment line (within 400m)
            float alongNorm = along / lineLen;
            if (alongNorm < -0.1f || alongNorm > 1.1f) continue;

            // taper at endpoints
            float endTaper = 1f;
            if (alongNorm < 0f) endTaper = 1f + alongNorm * 10f;
            else if (alongNorm > 1f) endTaper = 1f - (alongNorm - 1f) * 10f;
            endTaper = Mathf.Clamp01(endTaper);

            // Perlin warp for natural irregularity
            float warp = (Mathf.PerlinNoise(nx * 12f + 200f, nz * 12f + 200f) - 0.5f) * 0.03f;
            across += warp;

            float heightMod = 0f;
            if (across > 0f && across < steepWidth)
            {
                // steep SE face: cubic drop-off
                float t = across / steepWidth;
                heightMod = -escarpmentHeight * (1f - (1f - t) * (1f - t) * (1f - t));
            }
            else if (across >= steepWidth)
            {
                heightMod = -escarpmentHeight; // fully below
            }
            else if (across < 0f && across > -gentleWidth)
            {
                // gentle NW back-slope: linear ramp up
                float t = -across / gentleWidth;
                heightMod = escarpmentHeight * (1f - t) * 0.3f; // slight bump above baseline
            }

            heights[z, x] += heightMod * endTaper;
        }
    }

    td.SetHeights(0, 0, heights);
    Debug.Log("escarpment carved: ~400m cliff face at forest/upland boundary");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add escarpment carver: 400m cliff face at forest/upland boundary"
```

---

### Task 6: Rocky outcrop carver

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method after `CarveEscarpment`

**Step 1: Add CarveOutcrops method**

```csharp
private static void CarveOutcrops(TerrainData td)
{
    int res = td.heightmapResolution;
    float[,] heights = td.GetHeights(0, 0, res, res);

    // 5-6 rocky outcrops in the upland (NW quadrant, high elevation)
    // each is a steep-sided bump 15-30m across
    var rng = new System.Random(Seed + 100);
    int outcropCount = 5 + rng.Next(2);

    for (int o = 0; o < outcropCount; o++)
    {
        // place in NW quadrant (upland zone): nx 0.08-0.35, nz 0.08-0.35
        float cx = 0.08f + (float)rng.NextDouble() * 0.27f;
        float cz = 0.08f + (float)rng.NextDouble() * 0.27f;
        float radiusNorm = (7f + (float)rng.NextDouble() * 8f) / TerrainWidth; // 7-15m radius
        float peakHeight = (8f + (float)rng.NextDouble() * 12f) / TerrainHeight; // 8-20m tall

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);
                float dx = nx - cx;
                float dz = nz - cz;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist > radiusNorm * 1.5f) continue;

                if (dist < radiusNorm)
                {
                    // steep-sided bump: flat top that drops sharply at edges
                    float t = dist / radiusNorm;
                    // plateau profile: flat until 0.6, then steep drop
                    float profile;
                    if (t < 0.6f)
                        profile = 1f;
                    else
                    {
                        float edgeT = (t - 0.6f) / 0.4f;
                        profile = 1f - edgeT * edgeT * edgeT; // cubic drop at edges
                    }
                    // Perlin roughness on top surface
                    float roughness = Mathf.PerlinNoise(nx * 40f + o * 50f, nz * 40f + o * 50f) * 0.15f;
                    heights[z, x] += peakHeight * profile + roughness * peakHeight * 0.3f;
                }
                else
                {
                    // subtle base apron
                    float apronT = (dist - radiusNorm) / (radiusNorm * 0.5f);
                    heights[z, x] += peakHeight * 0.1f * (1f - apronT);
                }
            }
        }
    }

    td.SetHeights(0, 0, heights);
    Debug.Log($"rocky outcrops carved: {outcropCount} formations in upland zone");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add rocky outcrop carver: 5-6 tor-style formations in upland zone"
```

---

### Task 7: Wetland depression carver

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method after `CarveOutcrops`

**Step 1: Add CarveWetland method**

```csharp
private static void CarveWetland(TerrainData td)
{
    int res = td.heightmapResolution;
    float[,] heights = td.GetHeights(0, 0, res, res);

    // oxbow-shaped wetland depression in the floodplain
    // ~70m across, 2-3m below grade, near the river but offset
    // placed at normalized (0.55, 0.78) — south of main river meander
    float centerX = 0.55f;
    float centerZ = 0.78f;
    float radiusNorm = 35f / TerrainWidth;
    float depth = 2.5f / TerrainHeight;
    float elongation = 1.8f; // stretched along x axis (oxbow shape)

    for (int z = 0; z < res; z++)
    {
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1);
            float nz = (float)z / (res - 1);

            // elongated distance (wider along x, narrower along z)
            float dx = (nx - centerX) / elongation;
            float dz = nz - centerZ;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist > radiusNorm) continue;

            float t = dist / radiusNorm;
            // flat bottom bowl: stays flat until 0.7, then gentle sides
            float profile;
            if (t < 0.7f)
                profile = 1f;
            else
            {
                float edgeT = (t - 0.7f) / 0.3f;
                profile = 1f - edgeT * edgeT;
            }

            // slight Perlin variation in bottom for micro-pools
            float microVar = Mathf.PerlinNoise(nx * 50f + 333f, nz * 50f + 333f) * 0.3f;
            heights[z, x] -= depth * profile * (1f + microVar * 0.2f);
        }
    }

    td.SetHeights(0, 0, heights);
    Debug.Log("wetland depression carved: oxbow depression in floodplain");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add wetland depression carver: oxbow-shaped low area in floodplain"
```

---

### Task 8: Waystation pad flattener

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method after `CarveWetland`

**Step 1: Add CarveWaystationPads method**

Flattens terrain at each waystation location and carves the subway depression.

```csharp
private static void CarveWaystationPads(TerrainData td)
{
    int res = td.heightmapResolution;
    float[,] heights = td.GetHeights(0, 0, res, res);

    for (int w = 0; w < WaystationPositions.Length; w++)
    {
        var pos = WaystationPositions[w];
        var padSize = WaystationPadSizes[w];
        float nx = (pos.x + TerrainWidth / 2f) / TerrainWidth;
        float nz = (pos.y + TerrainWidth / 2f) / TerrainWidth;

        // sample center height to flatten to
        int cx = Mathf.Clamp(Mathf.RoundToInt(nx * (res - 1)), 0, res - 1);
        int cz = Mathf.Clamp(Mathf.RoundToInt(nz * (res - 1)), 0, res - 1);
        float targetHeight = heights[cz, cx];

        // subway entrance (index 2): carve 3m depression
        if (w == 2)
            targetHeight -= 3f / TerrainHeight;

        // train station (index 1): raise 1m embankment
        if (w == 1)
            targetHeight += 1f / TerrainHeight;

        float halfX = (padSize.x / 2f + 5f) / TerrainWidth; // +5m blend margin
        float halfZ = (padSize.y / 2f + 5f) / TerrainWidth;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float pnx = (float)x / (res - 1);
                float pnz = (float)z / (res - 1);
                float dx = Mathf.Abs(pnx - nx);
                float dz = Mathf.Abs(pnz - nz);

                if (dx > halfX || dz > halfZ) continue;

                float padHalfX = padSize.x / 2f / TerrainWidth;
                float padHalfZ = padSize.y / 2f / TerrainWidth;

                if (dx < padHalfX && dz < padHalfZ)
                {
                    // inside pad: flatten
                    heights[z, x] = targetHeight;
                }
                else
                {
                    // blend margin: lerp to surrounding terrain
                    float blendX = dx > padHalfX ? (dx - padHalfX) / (halfX - padHalfX) : 0f;
                    float blendZ = dz > padHalfZ ? (dz - padHalfZ) / (halfZ - padHalfZ) : 0f;
                    float blend = Mathf.Max(blendX, blendZ);
                    heights[z, x] = Mathf.Lerp(targetHeight, heights[z, x], blend);
                }
            }
        }
    }

    td.SetHeights(0, 0, heights);
    Debug.Log($"waystation pads flattened: {WaystationPositions.Length} locations");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add waystation pad flattener: terrain leveling at 4 transit locations"
```

---

### Task 9: Biome-aware splatmap

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:669-803` (replace `RepaintSplatmap`)

**Step 1: Rewrite RepaintSplatmap**

The existing method uses 5 layers: concrete(0), dirt(1), grass(2), gravel(3), rust(4). Keep the same layers but paint based on biome zone, waystation pads, and road textures.

```csharp
private static void RepaintSplatmap(TerrainData td)
{
    int res = td.alphamapResolution;
    float[,,] alphas = new float[res, res, 5];

    for (int z = 0; z < res; z++)
    {
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1);
            float nz = (float)z / (res - 1);
            float dx = nx - 0.5f;
            float dz = nz - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float steepness = td.GetSteepness(nx, nz);
            float wx = nx * TerrainWidth - TerrainWidth / 2f;
            float wz = nz * TerrainWidth - TerrainWidth / 2f;

            float concrete = 0f, dirt = 0f, grass = 0f, gravel = 0f, rust = 0f;

            // factory zone
            float flatEnd = FlatRadius / TerrainWidth / 2f;
            float transEnd = (FlatRadius + 30f) / TerrainWidth / 2f;

            if (dist < flatEnd)
            {
                concrete = 0.8f;
                float rustNoise = Mathf.PerlinNoise(nx * 40f + 700f, nz * 40f + 700f);
                if (rustNoise > 0.55f) { rust = (rustNoise - 0.55f) * 3f; concrete -= rust * 0.5f; }
                float gravelNoise = Mathf.PerlinNoise(nx * 60f + 800f, nz * 60f + 800f);
                if (gravelNoise > 0.65f) { gravel = (gravelNoise - 0.65f) * 3f; concrete -= gravel * 0.3f; }
                dirt = Mathf.Max(0f, 1f - concrete - rust - gravel);
            }
            else if (dist < transEnd)
            {
                float t = (dist - flatEnd) / (transEnd - flatEnd);
                concrete = (1f - t) * 0.5f;
                dirt = t * 0.4f; gravel = 0.3f; grass = t * 0.2f;
                float rustNoise = Mathf.PerlinNoise(nx * 25f + 900f, nz * 25f + 900f);
                rust = rustNoise > 0.5f ? (rustNoise - 0.5f) * 1.5f : 0f;
            }
            else
            {
                // biome-zone based painting
                var zone = GetBiomeZone(td, nx, nz);

                switch (zone)
                {
                    case BiomeZone.Floodplain:
                        grass = 0.65f; dirt = 0.25f; gravel = 0.1f;
                        // wet areas near river get more dirt
                        float riverZ = RiverCenterZ(nx);
                        float riverDist = Mathf.Abs(nz - riverZ) * TerrainWidth;
                        if (riverDist < 30f)
                        {
                            float wetBlend = 1f - riverDist / 30f;
                            dirt += wetBlend * 0.3f; grass -= wetBlend * 0.2f;
                            gravel += wetBlend * 0.15f;
                        }
                        break;

                    case BiomeZone.Forest:
                        grass = 0.55f; dirt = 0.35f; gravel = 0.1f;
                        float forestNoise = Mathf.PerlinNoise(nx * 20f + 500f, nz * 20f + 500f);
                        if (forestNoise > 0.55f) { dirt += (forestNoise - 0.55f) * 2f; grass -= (forestNoise - 0.55f); }
                        break;

                    case BiomeZone.RockyUpland:
                        gravel = 0.5f; dirt = 0.3f; grass = 0.15f; concrete = 0.05f;
                        // steeper = more rock
                        if (steepness > 15f)
                        {
                            float rockBlend = Mathf.Clamp01((steepness - 15f) / 20f);
                            gravel += rockBlend * 0.3f; grass *= (1f - rockBlend); dirt *= (1f - rockBlend * 0.5f);
                        }
                        break;
                }

                // path/road noise overlay
                float pathNoise = Mathf.PerlinNoise(nx * 8f + 100f, nz * 12f + 100f);
                if (pathNoise > 0.6f && pathNoise < 0.65f)
                {
                    gravel = 0.6f; grass *= 0.3f; dirt *= 0.3f;
                }
            }

            // waystation pad splatmap overlay
            for (int w = 0; w < WaystationPositions.Length; w++)
            {
                var wpos = WaystationPositions[w];
                float wdx = Mathf.Abs(wx - wpos.x);
                float wdz = Mathf.Abs(wz - wpos.y);
                float padHalfX = WaystationPadSizes[w].x / 2f;
                float padHalfZ = WaystationPadSizes[w].y / 2f;
                if (wdx < padHalfX && wdz < padHalfZ)
                {
                    concrete = 0.7f; gravel = 0.2f; rust = 0.1f;
                    grass = 0f; dirt = 0f;
                }
            }

            // riparian zone override (same as before)
            float rz = RiverCenterZ(nx);
            float rd = Mathf.Abs(nz - rz);
            float channelHalf = 8f / TerrainWidth;
            if (rd < channelHalf)
            {
                gravel = 0.8f; dirt = 0.2f; grass = 0f; concrete = 0f; rust = 0f;
            }
            else if (rd < channelHalf + 12f / TerrainWidth)
            {
                float bankT = (rd - channelHalf) / (12f / TerrainWidth);
                gravel = 0.6f * (1f - bankT) + 0.3f * bankT;
                dirt = 0.3f * (1f - bankT) + 0.5f * bankT;
                grass = 0.1f * bankT; concrete = 0f; rust = 0f;
            }

            float total = concrete + dirt + grass + gravel + rust;
            if (total > 0f)
            {
                alphas[z, x, 0] = concrete / total;
                alphas[z, x, 1] = dirt / total;
                alphas[z, x, 2] = grass / total;
                alphas[z, x, 3] = gravel / total;
                alphas[z, x, 4] = rust / total;
            }
            else
            {
                alphas[z, x, 1] = 1f;
            }
        }
    }

    td.SetAlphamaps(0, 0, alphas);
    Debug.Log("splatmap repainted: biome-aware with waystation pads");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Rewrite splatmap for biome zones, waystation pads, and river channel"
```

---

### Task 10: Biome-aware nature scatter

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:856-1149` (replace `ScatterNature`)

This is the largest task. Replace the existing `ScatterNature` with a biome-zone-aware system that picks species based on zone and density rules from the design doc. The current method uses 6 phases — the new one uses the same structure but picks props by biome zone.

**Step 1: Add species prop definitions**

Add after the existing prop arrays (after `CampProps`, around line 131):

```csharp
// biome-specific tree definitions — built from same Kenney models but with
// different scale ranges and wind parameters per "species"
private struct SpeciesDef
{
    public string SpeciesId;
    public PropDef Prop;
    public BiomeZone Zone;
    public float WindAmount;
    public float WindSpeed;

    public SpeciesDef(string id, PropDef prop, BiomeZone zone, float windAmt = 1.5f, float windSpd = 0.8f)
    {
        SpeciesId = id; Prop = prop; Zone = zone; WindAmount = windAmt; WindSpeed = windSpd;
    }
}

private static readonly SpeciesDef[] CanopySpecies = {
    // floodplain (6)
    new("weeping-willow", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn.fbx", 5f, 8f), BiomeZone.Floodplain, 2f, 0.6f),
    new("cottonwood", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 6f, 10f), BiomeZone.Floodplain, 1.2f, 0.7f),
    new("sycamore", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 6f, 9f), BiomeZone.Floodplain, 1.3f, 0.8f),
    new("river-birch", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn-trunk.fbx", 4f, 6f), BiomeZone.Floodplain, 1.8f, 0.9f),
    new("black-walnut", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 6f, 9f), BiomeZone.Floodplain, 1f, 0.7f),
    new("box-elder", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn.fbx", 3f, 5f), BiomeZone.Floodplain, 1.5f, 1f),
    // forest (8)
    new("red-oak", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 7f, 11f), BiomeZone.Forest, 1f, 0.7f),
    new("sugar-maple", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn.fbx", 6f, 10f), BiomeZone.Forest, 1.2f, 0.7f),
    new("hickory", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 8f, 12f), BiomeZone.Forest, 0.8f, 0.6f),
    new("white-ash", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 7f, 11f), BiomeZone.Forest, 1.1f, 0.75f),
    new("beech", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 5f, 8f), BiomeZone.Forest, 0.9f, 0.65f),
    new("black-cherry", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn-tall.fbx", 5f, 9f), BiomeZone.Forest, 1f, 0.8f),
    new("tulip-poplar", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 10f, 15f), BiomeZone.Forest, 0.7f, 0.5f),
    new("ironwood", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-trunk.fbx", 3f, 5f), BiomeZone.Forest, 1.5f, 0.9f),
    // rocky upland (4)
    new("pitch-pine", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 3f, 7f), BiomeZone.RockyUpland, 1.8f, 1f),
    new("red-cedar", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 3f, 5f), BiomeZone.RockyUpland, 0.8f, 0.5f),
    new("chestnut-oak", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 4f, 7f), BiomeZone.RockyUpland, 1.2f, 0.7f),
    new("scrub-oak", new PropDef("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-trunk.fbx", 2f, 3f), BiomeZone.RockyUpland, 2f, 1.2f),
};
```

**Step 2: Rewrite ScatterNature**

Replace the existing method. The new version:
- Queries biome zone at each scatter point
- Picks species from that zone's canopy list
- Uses zone-specific density: forest densest, floodplain medium, upland sparse
- Adds BiomeTag component to each tree
- Scales scatter counts for 1200m map (~2.25x area)
- Keeps riparian buffer, micro-detail clusters, and rock scatter logic

The method is long (~300 lines) but structurally similar to the current one. Key changes:

```csharp
private static SpeciesDef[] GetCanopyForZone(BiomeZone zone)
{
    var list = new List<SpeciesDef>();
    foreach (var s in CanopySpecies)
        if (s.Zone == zone) list.Add(s);
    return list.ToArray();
}

private static void ScatterNature(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
{
    var treeParent = new GameObject("Trees").transform;
    treeParent.SetParent(root);
    var rockParent = new GameObject("Rocks").transform;
    rockParent.SetParent(root);
    var undergrowthParent = new GameObject("Undergrowth").transform;
    undergrowthParent.SetParent(root);

    int treesPlaced = 0, rocksPlaced = 0, undergrowthPlaced = 0;

    // phase 1: tree clusters — scaled for 1200m map
    int clusterCount = 150 + rng.Next(30); // ~2x for larger area
    for (int c = 0; c < clusterCount; c++)
    {
        float cx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float cz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float dist = Mathf.Sqrt(cx * cx + cz * cz);
        if (dist < FlatRadius + 8f) continue;

        var zone = GetBiomeZoneFromWorldPos(td, cx, cz);
        var zoneCanopy = GetCanopyForZone(zone);
        if (zoneCanopy.Length == 0) continue;

        // zone-based density: skip some clusters in sparse zones
        if (zone == BiomeZone.RockyUpland && rng.NextDouble() > 0.3) continue;
        if (zone == BiomeZone.Floodplain && rng.NextDouble() > 0.7) continue;

        float clusterRadius = 8f + (float)rng.NextDouble() * 12f;
        int treesInCluster = zone == BiomeZone.RockyUpland ? 2 + rng.Next(3) : 4 + rng.Next(7);

        for (int t = 0; t < treesInCluster; t++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = (float)rng.NextDouble() * clusterRadius;
            float wx = cx + Mathf.Cos(angle) * r;
            float wz = cz + Mathf.Sin(angle) * r;

            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
            if (nx < 0.02f || nx > 0.98f || nz < 0.02f || nz > 0.98f) continue;
            if (td.GetSteepness(nx, nz) > 25f) continue;
            if (IsNearStructure(wx, wz, 8f)) continue;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
            var species = zoneCanopy[rng.Next(zoneCanopy.Length)];
            var instance = InstantiateProp(species.Prop, rng);
            if (instance == null) continue;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 4f);
            instance.transform.SetParent(treeParent);
            instance.isStatic = false;
            AddWindSway(instance, species.WindAmount, species.WindSpeed);

            var tag = instance.AddComponent<BiomeTag>();
            tag.Zone = zone;
            tag.SpeciesId = species.SpeciesId;
            treesPlaced++;

            // undergrowth around each tree
            int undergrowthCount = 2 + rng.Next(3);
            for (int u = 0; u < undergrowthCount; u++)
            {
                float ua = (float)rng.NextDouble() * Mathf.PI * 2f;
                float ur = 1f + (float)rng.NextDouble() * 3f;
                float uwx = wx + Mathf.Cos(ua) * ur;
                float uwz = wz + Mathf.Sin(ua) * ur;

                float unx = (uwx + TerrainWidth / 2f) / TerrainWidth;
                float unz = (uwz + TerrainWidth / 2f) / TerrainWidth;
                if (unx < 0.02f || unx > 0.98f || unz < 0.02f || unz > 0.98f) continue;

                float uy = SampleWorldHeight(terrain, terrainPos, uwx, uwz);
                var uprop = UndergrowthProps[rng.Next(4)];
                var uinst = InstantiateProp(uprop, rng);
                if (uinst == null) continue;

                uinst.transform.position = new Vector3(uwx, uy, uwz);
                uinst.transform.rotation = SlopeAlignedRotation(td, unx, unz, (float)rng.NextDouble() * 360f, rng, 0f);
                uinst.transform.SetParent(undergrowthParent);
                uinst.isStatic = false;
                AddWindSway(uinst, 2f + (float)rng.NextDouble() * 1f, 1f + (float)rng.NextDouble() * 0.3f);
                undergrowthPlaced++;
            }
        }
    }

    // phase 2: scattered solo trees (scaled for 1200m)
    for (int i = 0; i < 1000; i++)
    {
        float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float ddist = Mathf.Sqrt(wx * wx + wz * wz);
        if (ddist < FlatRadius + 5f) continue;
        if (IsNearStructure(wx, wz, 8f)) continue;

        float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
        float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
        if (td.GetSteepness(nx, nz) > 25f) continue;

        var zone = GetBiomeZoneFromWorldPos(td, wx, wz);
        var zoneCanopy = GetCanopyForZone(zone);
        if (zoneCanopy.Length == 0) continue;

        // density check
        if (zone == BiomeZone.RockyUpland && rng.NextDouble() > 0.2) continue;

        float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
        var species = zoneCanopy[rng.Next(zoneCanopy.Length)];
        var instance = InstantiateProp(species.Prop, rng);
        if (instance == null) continue;

        instance.transform.position = new Vector3(wx, y, wz);
        instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 3f);
        instance.transform.SetParent(treeParent);
        instance.isStatic = false;
        AddWindSway(instance, species.WindAmount, species.WindSpeed);

        var tag = instance.AddComponent<BiomeTag>();
        tag.Zone = zone;
        tag.SpeciesId = species.SpeciesId;
        treesPlaced++;
    }

    // phase 3: rocks — biome-aware (scaled for 1200m)
    for (int i = 0; i < 1600; i++)
    {
        float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float ddist = Mathf.Sqrt(wx * wx + wz * wz);
        if (ddist < FlatRadius) continue;

        float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
        float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
        float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

        var zone = GetBiomeZoneFromWorldPos(td, wx, wz);

        // more rocks in upland, fewer in floodplain
        if (zone == BiomeZone.Floodplain && rng.NextDouble() > 0.3) continue;

        PropDef prop;
        if (IsNearRiverbed(wx, wz, 10f))
        {
            int idx = SandyRockIndices[rng.Next(SandyRockIndices.Length)];
            prop = RockProps[idx];
        }
        else if (zone == BiomeZone.RockyUpland)
        {
            prop = RockProps[rng.Next(RockProps.Length)]; // any rock type
        }
        else if (td.GetSteepness(nx, nz) < 10f && rng.NextDouble() < 0.3)
        {
            int idx = MossyRockIndices[rng.Next(MossyRockIndices.Length)];
            prop = RockProps[idx];
        }
        else
        {
            prop = RockProps[rng.Next(RockProps.Length)];
        }

        var instance = InstantiateProp(prop, rng);
        if (instance == null) continue;

        instance.transform.position = new Vector3(wx, y, wz);
        instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 12f);
        instance.transform.SetParent(rockParent);
        instance.isStatic = true;
        rocksPlaced++;
    }

    // phase 4: undergrowth patches (scaled for 1200m)
    for (int i = 0; i < 2500; i++)
    {
        float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float ddist = Mathf.Sqrt(wx * wx + wz * wz);
        if (ddist < FlatRadius + 3f) continue;

        float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
        float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
        if (td.GetSteepness(nx, nz) > 35f) continue;

        var zone = GetBiomeZoneFromWorldPos(td, wx, wz);
        if (zone == BiomeZone.RockyUpland && rng.NextDouble() > 0.3) continue;

        float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
        int propIdx = rng.Next(UndergrowthProps.Length);
        var prop = UndergrowthProps[propIdx];
        var instance = InstantiateProp(prop, rng);
        if (instance == null) continue;

        instance.transform.position = new Vector3(wx, y, wz);
        instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 0f);
        instance.transform.SetParent(undergrowthParent);

        bool isVegetation = propIdx < 4;
        instance.isStatic = !isVegetation;
        if (isVegetation)
            AddWindSway(instance, 1.8f + (float)rng.NextDouble() * 1.2f, 0.9f + (float)rng.NextDouble() * 0.4f);
        undergrowthPlaced++;
    }

    // phase 5: micro-detail clusters (scaled for 1200m)
    for (int i = 0; i < 500; i++)
    {
        float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
        float ddist = Mathf.Sqrt(wx * wx + wz * wz);
        if (ddist < FlatRadius + 5f) continue;

        float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
        float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
        if (td.GetSteepness(nx, nz) > 30f) continue;

        float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
        var rockProp = RockProps[rng.Next(3)];
        var rock = InstantiateProp(new PropDef(rockProp.Path, 1.5f, 3f, true), rng);
        if (rock != null)
        {
            rock.transform.position = new Vector3(wx, y, wz);
            rock.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 8f);
            rock.transform.SetParent(rockParent);
            rock.isStatic = true;
            rocksPlaced++;
        }

        for (int g = 0; g < 2 + rng.Next(2); g++)
        {
            float ga = (float)rng.NextDouble() * Mathf.PI * 2f;
            float gr = 0.5f + (float)rng.NextDouble() * 2f;
            float gwx = wx + Mathf.Cos(ga) * gr;
            float gwz = wz + Mathf.Sin(ga) * gr;
            float gy = SampleWorldHeight(terrain, terrainPos, gwx, gwz);

            var gProp = UndergrowthProps[rng.Next(4)];
            var gInst = InstantiateProp(gProp, rng);
            if (gInst == null) continue;

            float gnx = (gwx + TerrainWidth / 2f) / TerrainWidth;
            float gnz = (gwz + TerrainWidth / 2f) / TerrainWidth;
            gInst.transform.position = new Vector3(gwx, gy, gwz);
            gInst.transform.rotation = SlopeAlignedRotation(td, gnx, gnz, (float)rng.NextDouble() * 360f, rng, 0f);
            gInst.transform.SetParent(undergrowthParent);
            gInst.isStatic = false;
            AddWindSway(gInst, 2f + (float)rng.NextDouble() * 1f, 1f);
            undergrowthPlaced++;
        }
    }

    // phase 6: riparian buffer (same structure, scaled for 1200m)
    for (int step = 0; step < RiverbedPoints.Count; step += 3)
    {
        var pt = RiverbedPoints[step];

        for (int side = -1; side <= 1; side += 2)
        {
            if (rng.NextDouble() > 0.6) continue;
            float offset = (3f + (float)rng.NextDouble() * 7f) * side;
            float uwx = pt.x;
            float uwz = pt.y + offset;

            float unx = (uwx + TerrainWidth / 2f) / TerrainWidth;
            float unz = (uwz + TerrainWidth / 2f) / TerrainWidth;
            if (unx < 0.02f || unx > 0.98f || unz < 0.02f || unz > 0.98f) continue;

            float uy = SampleWorldHeight(terrain, terrainPos, uwx, uwz);
            var uProp = UndergrowthProps[rng.Next(4)];
            var uInst = InstantiateProp(uProp, rng);
            if (uInst == null) continue;

            uInst.transform.position = new Vector3(uwx, uy, uwz);
            uInst.transform.rotation = SlopeAlignedRotation(td, unx, unz, (float)rng.NextDouble() * 360f, rng, 0f);
            uInst.transform.SetParent(undergrowthParent);
            uInst.isStatic = false;
            AddWindSway(uInst, 2.5f + (float)rng.NextDouble() * 1f, 1.2f);
            undergrowthPlaced++;
        }

        if (rng.NextDouble() > 0.35) continue;
        for (int side = -1; side <= 1; side += 2)
        {
            if (rng.NextDouble() > 0.5) continue;
            float offset = (10f + (float)rng.NextDouble() * 15f) * side;
            float twx = pt.x + ((float)rng.NextDouble() - 0.5f) * 4f;
            float twz = pt.y + offset;

            float tnx = (twx + TerrainWidth / 2f) / TerrainWidth;
            float tnz = (twz + TerrainWidth / 2f) / TerrainWidth;
            if (tnx < 0.02f || tnx > 0.98f || tnz < 0.02f || tnz > 0.98f) continue;

            float ddist = Mathf.Sqrt(twx * twx + twz * twz);
            if (ddist < FlatRadius + 5f) continue;

            float ty = SampleWorldHeight(terrain, terrainPos, twx, twz);
            var floodCanopy = GetCanopyForZone(BiomeZone.Floodplain);
            var species = floodCanopy[rng.Next(floodCanopy.Length)];
            var tInst = InstantiateProp(species.Prop, rng);
            if (tInst == null) continue;

            tInst.transform.position = new Vector3(twx, ty, twz);
            tInst.transform.rotation = SlopeAlignedRotation(td, tnx, tnz, (float)rng.NextDouble() * 360f, rng, 3f);
            tInst.transform.SetParent(treeParent);
            tInst.isStatic = false;
            AddWindSway(tInst, species.WindAmount, species.WindSpeed);

            var tag = tInst.AddComponent<BiomeTag>();
            tag.Zone = BiomeZone.Floodplain;
            tag.SpeciesId = species.SpeciesId;
            treesPlaced++;
        }
    }

    Debug.Log($"nature placed: {treesPlaced} trees, {rocksPlaced} rocks, {undergrowthPlaced} undergrowth");
}
```

**Step 3: Add IsNearStructure helper**

Add near the other helpers (after `IsNearRiverbed`):

```csharp
private static bool IsNearStructure(float wx, float wz, float threshold)
{
    float thresholdSq = threshold * threshold;

    // check waystations
    foreach (var pos in WaystationPositions)
    {
        float dx = wx - pos.x;
        float dz = wz - pos.y;
        if (dx * dx + dz * dz < thresholdSq * 4f) return true; // wider clearance for structures
    }

    // check farmsteads
    foreach (var pos in FarmsteadPositions)
    {
        float dx = wx - pos.x;
        float dz = wz - pos.y;
        if (dx * dx + dz * dz < thresholdSq) return true;
    }

    // check hamlet area
    float hdx = wx - HamletCenter.x;
    float hdz = wz - HamletCenter.y;
    if (hdx * hdx + hdz * hdz < 60f * 60f) return true; // 60m clear around hamlet

    // check merchants
    Vector2[] merchants = { GasStationPos, WoodshopPos, GaragePos };
    foreach (var pos in merchants)
    {
        float dx = wx - pos.x;
        float dz = wz - pos.y;
        if (dx * dx + dz * dz < thresholdSq * 2f) return true;
    }

    return false;
}
```

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Biome-aware nature scatter: species by zone, BiomeTag on trees, scaled counts"
```

---

### Task 11: Interest-map settlement placement

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — replace `PlaceRuinClusters` and `PlaceAbandonedCamps` with `PlaceSettlements`

**Step 1: Replace PlaceRuinClusters and PlaceAbandonedCamps**

Delete both methods (lines 1192-1428) and replace with a single `PlaceSettlements` method that uses the position data from Task 3.

```csharp
private static void PlaceSettlements(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
{
    var parent = new GameObject("Settlements").transform;
    parent.SetParent(root);
    int totalPlaced = 0;

    // farmsteads: 1-2 structures + fence + debris
    foreach (var center in FarmsteadPositions)
    {
        var farm = new GameObject($"Farmstead_{totalPlaced}").transform;
        farm.SetParent(parent);

        float cy = SampleWorldHeight(terrain, terrainPos, center.x, center.y);

        // main building
        var mainProp = RuinProps[rng.Next(3)]; // wall, window, or doorway
        var main = InstantiateProp(mainProp, rng);
        if (main != null)
        {
            // south-facing orientation
            float facing = 170f + (float)(rng.NextDouble() - 0.5) * 30f;
            main.transform.position = new Vector3(center.x, cy, center.y);
            main.transform.rotation = Quaternion.Euler(0f, facing, 0f);
            main.transform.SetParent(farm);
            main.isStatic = true;
            totalPlaced++;
        }

        // outbuilding 8-15m away
        if (rng.NextDouble() > 0.3)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = 8f + (float)rng.NextDouble() * 7f;
            float ox = center.x + Mathf.Cos(angle) * dist;
            float oz = center.y + Mathf.Sin(angle) * dist;
            float oy = SampleWorldHeight(terrain, terrainPos, ox, oz);

            var outProp = RuinProps[6 + rng.Next(3)]; // metal structures
            var outBldg = InstantiateProp(outProp, rng);
            if (outBldg != null)
            {
                float tilt = (float)(rng.NextDouble() - 0.5) * 6f; // slight settling
                outBldg.transform.position = new Vector3(ox, oy, oz);
                outBldg.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, tilt * 0.5f);
                outBldg.transform.SetParent(farm);
                outBldg.isStatic = true;
                totalPlaced++;
            }
        }

        // fence perimeter
        for (int f = 0; f < 4 + rng.Next(4); f++)
        {
            float fAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float fDist = 12f + (float)rng.NextDouble() * 8f;
            float fx = center.x + Mathf.Cos(fAngle) * fDist;
            float fz = center.y + Mathf.Sin(fAngle) * fDist;
            float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

            var fenceProp = IndustrialProps[6 + rng.Next(3)]; // fence variants
            var fence = InstantiateProp(fenceProp, rng);
            if (fence == null) continue;

            float faceFire = Mathf.Atan2(center.y - fz, center.x - fx) * Mathf.Rad2Deg;
            float tilt = (float)(rng.NextDouble() - 0.5) * 20f; // damaged
            fence.transform.position = new Vector3(fx, fy, fz);
            fence.transform.rotation = Quaternion.Euler(tilt, faceFire + (float)(rng.NextDouble() - 0.5) * 40f, 0f);
            fence.transform.SetParent(farm);
            fence.isStatic = true;
            totalPlaced++;
        }

        // debris scatter
        for (int d = 0; d < 4 + rng.Next(6); d++)
        {
            float dAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dDist = (float)rng.NextDouble() * 15f;
            float dx = center.x + Mathf.Cos(dAngle) * dDist;
            float dz = center.y + Mathf.Sin(dAngle) * dDist;
            float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

            var debrisProp = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var debris = InstantiateProp(debrisProp, rng);
            if (debris == null) continue;

            float tilt = (float)(rng.NextDouble() - 0.5) * 30f;
            debris.transform.position = new Vector3(dx, dy, dz);
            debris.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, tilt * 0.3f);
            debris.transform.SetParent(farm);
            debris.isStatic = true;
            totalPlaced++;
        }

        // undergrowth encroachment
        for (int u = 0; u < 5 + rng.Next(5); u++)
        {
            float uAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float uDist = 2f + (float)rng.NextDouble() * 5f;
            float ux = center.x + Mathf.Cos(uAngle) * uDist;
            float uz = center.y + Mathf.Sin(uAngle) * uDist;
            float uy = SampleWorldHeight(terrain, terrainPos, ux, uz);

            var uProp = UndergrowthProps[rng.Next(4)];
            var uInst = InstantiateProp(uProp, rng);
            if (uInst == null) continue;

            uInst.transform.position = new Vector3(ux, uy, uz);
            uInst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            uInst.transform.SetParent(farm);
            uInst.isStatic = false;
            AddWindSway(uInst, 2f, 1f);
            totalPlaced++;
        }
    }

    // small clusters: 3-5 buildings around shared yard
    foreach (var center in SmallClusterPositions)
    {
        var cluster = new GameObject($"Cluster_{totalPlaced}").transform;
        cluster.SetParent(parent);

        int buildingCount = 3 + rng.Next(3);
        float yardRadius = 10f + (float)rng.NextDouble() * 5f;

        for (int b = 0; b < buildingCount; b++)
        {
            float angle = ((float)b / buildingCount) * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.5f;
            float dist = yardRadius * (0.8f + (float)rng.NextDouble() * 0.4f);
            float bx = center.x + Mathf.Cos(angle) * dist;
            float bz = center.y + Mathf.Sin(angle) * dist;
            float by = SampleWorldHeight(terrain, terrainPos, bx, bz);

            var prop = RuinProps[rng.Next(RuinProps.Length)];
            var bldg = InstantiateProp(prop, rng);
            if (bldg == null) continue;

            float facing = Mathf.Atan2(center.y - bz, center.x - bx) * Mathf.Rad2Deg;
            float tilt = (float)(rng.NextDouble() - 0.5) * 6f;
            bldg.transform.position = new Vector3(bx, by, bz);
            bldg.transform.rotation = Quaternion.Euler(tilt, facing + (float)(rng.NextDouble() - 0.5) * 20f, tilt * 0.5f);
            bldg.transform.SetParent(cluster);
            bldg.isStatic = true;
            totalPlaced++;
        }

        // cluster debris and undergrowth
        for (int d = 0; d < 6 + rng.Next(6); d++)
        {
            float dAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dDist = (float)rng.NextDouble() * (yardRadius + 10f);
            float dx = center.x + Mathf.Cos(dAngle) * dDist;
            float dz = center.y + Mathf.Sin(dAngle) * dDist;
            float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

            bool isUndergrowth = rng.NextDouble() > 0.5;
            PropDef prop;
            if (isUndergrowth)
                prop = UndergrowthProps[rng.Next(4)];
            else
                prop = IndustrialProps[rng.Next(IndustrialProps.Length)];

            var inst = InstantiateProp(prop, rng);
            if (inst == null) continue;

            inst.transform.position = new Vector3(dx, dy, dz);
            inst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            inst.transform.SetParent(cluster);
            inst.isStatic = !isUndergrowth;
            if (isUndergrowth) AddWindSway(inst, 2f, 1f);
            totalPlaced++;
        }
    }

    // ruined hamlet: 12-15 buildings along main road
    {
        var hamlet = new GameObject("RuinedHamlet").transform;
        hamlet.SetParent(parent);

        float roadDir = 0.3f; // radians, roughly east-west
        int hamletBuildingCount = 12 + rng.Next(4);

        for (int b = 0; b < hamletBuildingCount; b++)
        {
            float along = ((float)b / hamletBuildingCount - 0.5f) * 100f; // spread 100m along road
            float perpOffset = ((float)(rng.NextDouble() - 0.5)) * 20f; // 10m each side of road
            if (Mathf.Abs(perpOffset) < 5f) perpOffset = Mathf.Sign(perpOffset) * (5f + (float)rng.NextDouble() * 5f);

            float bx = HamletCenter.x + Mathf.Cos(roadDir) * along + Mathf.Sin(roadDir) * perpOffset;
            float bz = HamletCenter.y + Mathf.Sin(roadDir) * along - Mathf.Cos(roadDir) * perpOffset;
            float by = SampleWorldHeight(terrain, terrainPos, bx, bz);

            var prop = RuinProps[rng.Next(RuinProps.Length)];
            var bldg = InstantiateProp(prop, rng);
            if (bldg == null) continue;

            // face the road
            float facing = roadDir * Mathf.Rad2Deg + (perpOffset > 0 ? -90f : 90f);
            float tilt = (float)(rng.NextDouble() - 0.5) * 6f;

            // 30-50% missing roof (collapse)
            if (rng.NextDouble() < 0.4f)
                tilt = 15f + (float)rng.NextDouble() * 15f; // major tilt = collapsed

            bldg.transform.position = new Vector3(bx, by, bz);
            bldg.transform.rotation = Quaternion.Euler(tilt, facing + (float)(rng.NextDouble() - 0.5) * 10f, tilt * 0.3f);
            bldg.transform.SetParent(hamlet);
            bldg.isStatic = true;
            totalPlaced++;

            // per-building debris
            for (int d = 0; d < 2 + rng.Next(3); d++)
            {
                float da = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dd = 2f + (float)rng.NextDouble() * 8f;
                float dx = bx + Mathf.Cos(da) * dd;
                float dz = bz + Mathf.Sin(da) * dd;
                float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

                var dProp = IndustrialProps[rng.Next(IndustrialProps.Length)];
                var dInst = InstantiateProp(dProp, rng);
                if (dInst == null) continue;

                float dt = (float)(rng.NextDouble() - 0.5) * 25f;
                dInst.transform.position = new Vector3(dx, dy, dz);
                dInst.transform.rotation = Quaternion.Euler(dt, (float)rng.NextDouble() * 360f, 0f);
                dInst.transform.SetParent(hamlet);
                dInst.isStatic = true;
                totalPlaced++;
            }
        }

        // hamlet undergrowth
        for (int u = 0; u < 20 + rng.Next(10); u++)
        {
            float ux = HamletCenter.x + (float)(rng.NextDouble() - 0.5) * 120f;
            float uz = HamletCenter.y + (float)(rng.NextDouble() - 0.5) * 40f;
            float uy = SampleWorldHeight(terrain, terrainPos, ux, uz);

            var uProp = UndergrowthProps[rng.Next(4)];
            var uInst = InstantiateProp(uProp, rng);
            if (uInst == null) continue;

            uInst.transform.position = new Vector3(ux, uy, uz);
            uInst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            uInst.transform.SetParent(hamlet);
            uInst.isStatic = false;
            AddWindSway(uInst, 2f, 1f);
            totalPlaced++;
        }

        // 1-2 reclaimed trees growing through roofless structures
        for (int t = 0; t < 1 + rng.Next(2); t++)
        {
            float tx = HamletCenter.x + (float)(rng.NextDouble() - 0.5) * 60f;
            float tz = HamletCenter.y + (float)(rng.NextDouble() - 0.5) * 20f;
            float ty = SampleWorldHeight(terrain, terrainPos, tx, tz);

            var floodCanopy = GetCanopyForZone(BiomeZone.Floodplain);
            var species = floodCanopy[rng.Next(floodCanopy.Length)];
            var tree = InstantiateProp(species.Prop, rng);
            if (tree == null) continue;

            tree.transform.position = new Vector3(tx, ty, tz);
            tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            tree.transform.SetParent(hamlet);
            tree.isStatic = false;
            AddWindSway(tree, species.WindAmount, species.WindSpeed);
            totalPlaced++;
        }
    }

    Debug.Log($"settlements placed: {totalPlaced} total pieces");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Replace ruin/camp placement with interest-map settlement system"
```

---

### Task 12: Merchant structures

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method

**Step 1: Add PlaceMerchantStructures method**

```csharp
private static void PlaceMerchantStructures(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
{
    var parent = new GameObject("Merchants").transform;
    parent.SetParent(root);
    int totalPlaced = 0;

    // gas station
    {
        var station = new GameObject("GasStation").transform;
        station.SetParent(parent);
        float gy = SampleWorldHeight(terrain, terrainPos, GasStationPos.x, GasStationPos.y);

        // canopy (tall structure)
        var canopy = InstantiateProp(RuinProps[3], rng); // structure-tall
        if (canopy != null)
        {
            canopy.transform.position = new Vector3(GasStationPos.x, gy, GasStationPos.y);
            canopy.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            canopy.transform.SetParent(station);
            canopy.isStatic = true;
            totalPlaced++;
        }

        // shop building
        var shop = InstantiateProp(RuinProps[6], rng); // structure-metal-wall
        if (shop != null)
        {
            shop.transform.position = new Vector3(GasStationPos.x + 6f, gy, GasStationPos.y + 3f);
            shop.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            shop.transform.SetParent(station);
            shop.isStatic = true;
            totalPlaced++;
        }

        // pump island + debris
        for (int d = 0; d < 6 + rng.Next(4); d++)
        {
            float ox = (float)(rng.NextDouble() - 0.5) * 20f;
            float oz = (float)(rng.NextDouble() - 0.5) * 12f;
            float dy = SampleWorldHeight(terrain, terrainPos, GasStationPos.x + ox, GasStationPos.y + oz);
            var dProp = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var dInst = InstantiateProp(dProp, rng);
            if (dInst == null) continue;
            float tilt = (float)(rng.NextDouble() - 0.5) * 15f;
            dInst.transform.position = new Vector3(GasStationPos.x + ox, dy, GasStationPos.y + oz);
            dInst.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, 0f);
            dInst.transform.SetParent(station);
            dInst.isStatic = true;
            totalPlaced++;
        }
    }

    // woodshop / sawmill
    {
        var shop = new GameObject("Woodshop").transform;
        shop.SetParent(parent);
        float wy = SampleWorldHeight(terrain, terrainPos, WoodshopPos.x, WoodshopPos.y);

        // open shed
        var shed = InstantiateProp(RuinProps[0], rng); // structure-wall
        if (shed != null)
        {
            shed.transform.position = new Vector3(WoodshopPos.x, wy, WoodshopPos.y);
            shed.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            shed.transform.SetParent(shop);
            shed.isStatic = true;
            totalPlaced++;
        }

        // log racks (resource-wood)
        for (int l = 0; l < 3 + rng.Next(3); l++)
        {
            float ox = 3f + (float)rng.NextDouble() * 8f;
            float oz = (float)(rng.NextDouble() - 0.5) * 10f;
            float ly = SampleWorldHeight(terrain, terrainPos, WoodshopPos.x + ox, WoodshopPos.y + oz);
            var logProp = UndergrowthProps[6]; // resource-wood
            var log = InstantiateProp(logProp, rng);
            if (log == null) continue;
            log.transform.position = new Vector3(WoodshopPos.x + ox, ly, WoodshopPos.y + oz);
            log.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            log.transform.SetParent(shop);
            log.isStatic = true;
            totalPlaced++;
        }

        // fence yard
        for (int f = 0; f < 6; f++)
        {
            float fAngle = ((float)f / 6f) * Mathf.PI * 2f;
            float fDist = 10f;
            float fx = WoodshopPos.x + Mathf.Cos(fAngle) * fDist;
            float fz = WoodshopPos.y + Mathf.Sin(fAngle) * fDist;
            float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);
            var fProp = IndustrialProps[6]; // fence
            var fence = InstantiateProp(fProp, rng);
            if (fence == null) continue;
            fence.transform.position = new Vector3(fx, fy, fz);
            fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
            fence.transform.SetParent(shop);
            fence.isStatic = true;
            totalPlaced++;
        }
    }

    // mechanic's garage
    {
        var garage = new GameObject("Garage").transform;
        garage.SetParent(parent);
        float my = SampleWorldHeight(terrain, terrainPos, GaragePos.x, GaragePos.y);

        // main structure (structure-metal)
        var mainBldg = InstantiateProp(RuinProps[8], rng);
        if (mainBldg != null)
        {
            mainBldg.transform.position = new Vector3(GaragePos.x, my, GaragePos.y);
            mainBldg.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            mainBldg.transform.SetParent(garage);
            mainBldg.isStatic = true;
            totalPlaced++;
        }

        // lean-to annex
        var annex = InstantiateProp(RuinProps[7], rng); // structure-metal-doorway
        if (annex != null)
        {
            annex.transform.position = new Vector3(GaragePos.x + 5f, my, GaragePos.y - 3f);
            annex.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            annex.transform.SetParent(garage);
            annex.isStatic = true;
            totalPlaced++;
        }

        // industrial debris
        for (int d = 0; d < 8 + rng.Next(5); d++)
        {
            float ox = (float)(rng.NextDouble() - 0.5) * 18f;
            float oz = (float)(rng.NextDouble() - 0.5) * 14f;
            float dy = SampleWorldHeight(terrain, terrainPos, GaragePos.x + ox, GaragePos.y + oz);
            var dProp = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var dInst = InstantiateProp(dProp, rng);
            if (dInst == null) continue;
            float tilt = (float)(rng.NextDouble() - 0.5) * 20f;
            dInst.transform.position = new Vector3(GaragePos.x + ox, dy, GaragePos.y + oz);
            dInst.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, 0f);
            dInst.transform.SetParent(garage);
            dInst.isStatic = true;
            totalPlaced++;
        }
    }

    // market stalls and general store are part of the hamlet (placed in PlaceSettlements)
    // so we just add a few extra stall-like structures at hamlet center
    {
        var market = new GameObject("MarketStalls").transform;
        market.SetParent(parent);

        for (int s = 0; s < 4; s++)
        {
            float sx = HamletCenter.x + ((float)s - 1.5f) * 8f;
            float sz = HamletCenter.y + 5f;
            float sy = SampleWorldHeight(terrain, terrainPos, sx, sz);

            var stallProp = RuinProps[4 + rng.Next(2)]; // cover/cover-stripe (awning stand-in)
            var stall = InstantiateProp(stallProp, rng);
            if (stall == null) continue;

            stall.transform.position = new Vector3(sx, sy + 2f, sz);
            stall.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            stall.transform.SetParent(market);
            stall.isStatic = true;
            totalPlaced++;
        }
    }

    Debug.Log($"merchant structures placed: {totalPlaced} pieces");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add merchant structures: gas station, woodshop, garage, market stalls"
```

---

### Task 13: Waystation structures

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs` — add new method

**Step 1: Add PlaceWaystations method**

```csharp
private static void PlaceWaystations(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
{
    var parent = new GameObject("Waystations").transform;
    parent.SetParent(root);
    int totalPlaced = 0;
    string[] names = { "BusStop", "TrainStation", "SubwayEntrance", "Helipad" };

    for (int w = 0; w < WaystationPositions.Length; w++)
    {
        var pos = WaystationPositions[w];
        var ws = new GameObject(names[w]).transform;
        ws.SetParent(parent);

        float wy = SampleWorldHeight(terrain, terrainPos, pos.x, pos.y);

        switch (w)
        {
            case 0: // bus stop — shelter + fence + bench
            {
                var shelter = InstantiateProp(RuinProps[6], rng); // structure-metal-wall
                if (shelter != null)
                {
                    shelter.transform.position = new Vector3(pos.x, wy, pos.y);
                    shelter.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    shelter.transform.SetParent(ws);
                    shelter.isStatic = true;
                    totalPlaced++;
                }
                // fence barriers
                for (int f = 0; f < 3; f++)
                {
                    var fence = InstantiateProp(IndustrialProps[6 + rng.Next(3)], rng);
                    if (fence == null) continue;
                    fence.transform.position = new Vector3(pos.x + (f - 1) * 3f, wy, pos.y + 3f);
                    fence.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    fence.transform.SetParent(ws);
                    fence.isStatic = true;
                    totalPlaced++;
                }
                // signpost
                var sign = InstantiateProp(IndustrialProps[12], rng); // signpost
                if (sign != null)
                {
                    sign.transform.position = new Vector3(pos.x - 5f, wy, pos.y);
                    sign.transform.rotation = Quaternion.Euler(0f, 0f, 5f); // slight lean
                    sign.transform.SetParent(ws);
                    sign.isStatic = true;
                    totalPlaced++;
                }
                break;
            }

            case 1: // train station — platform + canopy + rails
            {
                // platform (tall wall piece as platform edge)
                var platform = InstantiateProp(RuinProps[3], rng); // structure-tall
                if (platform != null)
                {
                    platform.transform.position = new Vector3(pos.x, wy + 1f, pos.y);
                    platform.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    platform.transform.localScale = new Vector3(1f, 0.3f, 3f);
                    platform.transform.SetParent(ws);
                    platform.isStatic = true;
                    totalPlaced++;
                }
                // canopy structure
                for (int c = 0; c < 3; c++)
                {
                    var col = InstantiateProp(RuinProps[0], rng); // structure-wall as column
                    if (col == null) continue;
                    col.transform.position = new Vector3(pos.x + (c - 1) * 8f, wy + 1f, pos.y - 2f);
                    col.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    col.transform.SetParent(ws);
                    col.isStatic = true;
                    totalPlaced++;
                }
                // signpost
                var sign = InstantiateProp(IndustrialProps[13], rng);
                if (sign != null)
                {
                    sign.transform.position = new Vector3(pos.x - 15f, wy, pos.y);
                    sign.transform.SetParent(ws);
                    sign.isStatic = true;
                    totalPlaced++;
                }
                break;
            }

            case 2: // subway entrance — doorway descending + surrounding walls
            {
                // entrance frame
                var door = InstantiateProp(RuinProps[2], rng); // structure-doorway
                if (door != null)
                {
                    door.transform.position = new Vector3(pos.x, wy - 1f, pos.y);
                    door.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    door.transform.SetParent(ws);
                    door.isStatic = true;
                    totalPlaced++;
                }
                // surrounding walls
                for (int s = 0; s < 4; s++)
                {
                    var wall = InstantiateProp(RuinProps[0 + rng.Next(2)], rng);
                    if (wall == null) continue;
                    float wAngle = s * 90f;
                    float wDist = 3f;
                    wall.transform.position = new Vector3(
                        pos.x + Mathf.Cos(wAngle * Mathf.Deg2Rad) * wDist,
                        wy - 0.5f,
                        pos.y + Mathf.Sin(wAngle * Mathf.Deg2Rad) * wDist);
                    wall.transform.rotation = Quaternion.Euler(0f, wAngle, 0f);
                    wall.transform.SetParent(ws);
                    wall.isStatic = true;
                    totalPlaced++;
                }
                // stairs (stacked floor pieces)
                for (int step = 0; step < 4; step++)
                {
                    var stair = InstantiateProp(RuinProps[4], rng); // cover piece as step
                    if (stair == null) continue;
                    stair.transform.position = new Vector3(pos.x, wy - 0.5f * step, pos.y + 1f + step * 0.8f);
                    stair.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    stair.transform.localScale = new Vector3(1f, 0.2f, 0.8f);
                    stair.transform.SetParent(ws);
                    stair.isStatic = true;
                    totalPlaced++;
                }
                break;
            }

            case 3: // helipad — flat pad + fence + control shack
            {
                // control shack
                var shack = InstantiateProp(RuinProps[8], rng); // structure-metal
                if (shack != null)
                {
                    shack.transform.position = new Vector3(pos.x + 8f, wy, pos.y + 8f);
                    shack.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                    shack.transform.SetParent(ws);
                    shack.isStatic = true;
                    totalPlaced++;
                }
                // fence perimeter
                for (int f = 0; f < 8; f++)
                {
                    var fence = InstantiateProp(IndustrialProps[6 + rng.Next(3)], rng);
                    if (fence == null) continue;
                    float fAngle = ((float)f / 8f) * Mathf.PI * 2f;
                    float fDist = 10f;
                    fence.transform.position = new Vector3(
                        pos.x + Mathf.Cos(fAngle) * fDist,
                        wy,
                        pos.y + Mathf.Sin(fAngle) * fDist);
                    fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                    fence.transform.SetParent(ws);
                    fence.isStatic = true;
                    totalPlaced++;
                }
                // signpost
                var sign = InstantiateProp(IndustrialProps[12], rng);
                if (sign != null)
                {
                    sign.transform.position = new Vector3(pos.x - 10f, wy, pos.y);
                    sign.transform.SetParent(ws);
                    sign.isStatic = true;
                    totalPlaced++;
                }
                break;
            }
        }

        // debris around each waystation
        for (int d = 0; d < 4 + rng.Next(4); d++)
        {
            float dAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dDist = WaystationPadSizes[w].x / 2f + (float)rng.NextDouble() * 10f;
            float dx = pos.x + Mathf.Cos(dAngle) * dDist;
            float dz = pos.y + Mathf.Sin(dAngle) * dDist;
            float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

            var dProp = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var dInst = InstantiateProp(dProp, rng);
            if (dInst == null) continue;

            float tilt = (float)(rng.NextDouble() - 0.5) * 20f;
            dInst.transform.position = new Vector3(dx, dy, dz);
            dInst.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, 0f);
            dInst.transform.SetParent(ws);
            dInst.isStatic = true;
            totalPlaced++;
        }
    }

    Debug.Log($"waystations placed: {totalPlaced} pieces across {WaystationPositions.Length} locations");
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Add 4 waystation structures: bus stop, train station, subway, helipad"
```

---

### Task 14: Update Dress() call order + fog/camera for 1200m

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs:134-184` (Dress method)
- Modify: fog distances in `SetupSkybox`

**Step 1: Update Dress() method**

Replace the call sequence (lines 165-181) with:

```csharp
UpgradeTerrainTextures(td);
AddTerrainNoise(td);
AddTerrainFeatures(td);
CarveEscarpment(td);
CarveOutcrops(td);
CarveWetland(td);
SmoothHeightmap(td, 6);
CarveRiverValley(td);
CarveWaystationPads(td);
RepaintSplatmap(td);
SetupSkybox();

ScatterNature(root.transform, terrain, terrainPos, rng, td);
PlaceSettlements(root.transform, terrain, terrainPos, rng, td);
PlaceMerchantStructures(root.transform, terrain, terrainPos, rng, td);
PlaceWaystations(root.transform, terrain, terrainPos, rng, td);
ScatterIndustrial(root.transform, terrain, terrainPos, rng, td);
DecorateRiverbed(root.transform, terrain, terrainPos, rng, td);
CreateRiverWater(root.transform, terrain, terrainPos, td);
PaintTerrainGrass(td, rng);
SetupAmbientParticles();
```

Remove the now-deleted `PlaceRuinClusters` and `PlaceAbandonedCamps` calls.

**Step 2: Update fog distances for 1200m**

In `SetupSkybox()`, change fog distances (around line 846-847):

```csharp
RenderSettings.fogStartDistance = 300f;  // was 200
RenderSettings.fogEndDistance = 1200f;   // was 800
```

**Step 3: Update industrial debris scatter radius for 1200m**

In `ScatterIndustrial` (line 1163), increase the scatter radius:

```csharp
float radius = FlatRadius + (float)rng.NextDouble() * 100f - 10f; // was 60f
```

And increase count (line 1160):

```csharp
for (int i = 0; i < 600; i++) // was 350
```

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/HomeBaseSceneryDresser.cs
git commit -m "Wire up Dress() with new call order, scale fog and scatter for 1200m"
```

---

### Task 15: Run dresser, verify, commit

**Step 1: Recompile**

In Unity: Assets > Refresh, or wait for auto-compile. Check console for zero errors.

**Step 2: Run the dresser**

Unity menu: Slopworks > Dress HomeBase Scenery

**Step 3: Check console logs**

Expected log messages (in order):
- `terrain textures upgraded: 5 PBR layers with mask maps`
- `terrain noise added: 4 octaves + regional tilt + valley depression`
- `terrain features added: craters, erosion gullies, ridges`
- `escarpment carved: ~400m cliff face at forest/upland boundary`
- `rocky outcrops carved: N formations in upland zone`
- `wetland depression carved: oxbow depression in floodplain`
- `heightmap smoothed: 6 passes`
- `river valley carved: channel + terraces + floodplain (400 path points)`
- `waystation pads flattened: 4 locations`
- `splatmap repainted: biome-aware with waystation pads`
- `skybox set: industrial sunset`
- `nature placed: N trees, N rocks, N undergrowth`
- `settlements placed: N total pieces`
- `merchant structures placed: N pieces`
- `waystations placed: N pieces across 4 locations`
- `industrial debris placed: N`
- `riverbed decoration placed: N`
- `river water created: N segments`
- `terrain detail grass painted: N prototype layers`
- `ambient particles added to camera`
- `homebase scenery dressed — save the scene to persist`

**Step 4: Visual verification**

Fly around the scene and check:
- Three distinct biome zones visible (flat floodplain, rolling forest, rocky upland NW)
- Escarpment cliff face visible at forest/upland boundary
- Rocky outcrops on the upland plateau
- Wetland depression south of river
- 4 waystation flat pads with structures
- Farmsteads scattered in forest and floodplain
- Ruined hamlet with linear layout near river
- Merchant structures (gas station, woodshop, garage)
- Trees vary by zone (different species/sizes)
- River still visible and properly carved

**Step 5: Save scene and commit**

```bash
git add -A
git commit -m "Terrain phase 2: 1200m map with biome zones, settlements, waystations, flora"
```

**Step 6: Push and create PR**

```bash
git push origin joe/main
gh pr create --base master --head joe/main --title "Terrain phase 2: biome zones, settlements, waystations" --body "$(cat <<'EOF'
## Summary
- Expanded terrain to 1200m x 1200m with 220m height range
- 3 biome zones: floodplain, forest, rocky upland (NW quadrant)
- Explicit terrain carvers: escarpment cliff, rocky outcrops, wetland depression
- 4 transit waystations: bus stop, train station, subway entrance, helipad
- Settlement system: 7 farmsteads, 2 small clusters, 1 ruined hamlet (12-15 buildings)
- Merchant structures: gas station, woodshop/sawmill, mechanic's garage, market stalls
- Biome-aware vegetation: 18 canopy species, 11 understory species, 10 ground cover types
- BiomeTag component on trees for future resource/harvesting system
- Scaled fog, scatter counts, and detail resolution for larger map

## Test plan
- [ ] Run dresser via Slopworks > Dress HomeBase Scenery
- [ ] Verify all console log messages appear without errors
- [ ] Fly through each biome zone and confirm visual distinction
- [ ] Check all 4 waystations have flat pads and structures
- [ ] Verify settlements are spread across map with appropriate density
- [ ] Confirm river is still visible and properly carved
- [ ] Check escarpment cliff face is visible at NW boundary

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
