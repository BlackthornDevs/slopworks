# Overworld terrain implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Generate a 128x128 hex-grid isometric overworld terrain with biomes, height variation, and ruin decorations.

**Architecture:** Editor-time generation via menu item. Pure C# hex math + mesh building classes (D-004 testable), thin editor script that orchestrates them into a saved scene. Vertex-colored combined meshes for biomes, Kenney model instances for ruins.

**Tech Stack:** Unity mesh API (Mesh, MeshFilter, MeshRenderer), Mathf.PerlinNoise for noise, axial hex coordinates, vertex colors for biomes, URP Lit shader with vertex color support.

**Design doc:** `docs/plans/2026-03-06-overworld-terrain-design.md`

---

### Task 1: HexGridUtility -- axial hex math

**Files:**
- Create: `Assets/_Slopworks/Scripts/World/HexGridUtility.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/HexGridUtilityTests.cs`

This is a pure C# static utility class. No MonoBehaviour, no Unity dependencies beyond `UnityEngine.Vector3` and `UnityEngine.Mathf`.

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class HexGridUtilityTests
{
    private const float Size = 1f; // 1m hex radius = 2m flat-to-flat

    // -- World position from axial coords --

    [Test]
    public void HexToWorld_Origin_ReturnsZero()
    {
        var pos = HexGridUtility.HexToWorld(0, 0, Size);
        Assert.AreEqual(0f, pos.x, 0.001f);
        Assert.AreEqual(0f, pos.z, 0.001f);
    }

    [Test]
    public void HexToWorld_Q1R0_ReturnsCorrectX()
    {
        var pos = HexGridUtility.HexToWorld(1, 0, Size);
        float expectedX = Mathf.Sqrt(3f) * Size;
        Assert.AreEqual(expectedX, pos.x, 0.001f);
        Assert.AreEqual(0f, pos.z, 0.001f);
    }

    [Test]
    public void HexToWorld_Q0R1_ReturnsCorrectXZ()
    {
        var pos = HexGridUtility.HexToWorld(0, 1, Size);
        float expectedX = Mathf.Sqrt(3f) / 2f * Size;
        float expectedZ = 1.5f * Size;
        Assert.AreEqual(expectedX, pos.x, 0.001f);
        Assert.AreEqual(expectedZ, pos.z, 0.001f);
    }

    // -- Hex corner positions (pointy-top) --

    [Test]
    public void HexCorners_Returns6Corners()
    {
        var corners = HexGridUtility.HexCorners(Vector3.zero, Size);
        Assert.AreEqual(6, corners.Length);
    }

    [Test]
    public void HexCorners_AllAtCorrectDistance()
    {
        var center = new Vector3(5f, 0f, 5f);
        var corners = HexGridUtility.HexCorners(center, Size);
        foreach (var c in corners)
        {
            float dist = Vector3.Distance(
                new Vector3(center.x, 0f, center.z),
                new Vector3(c.x, 0f, c.z));
            Assert.AreEqual(Size, dist, 0.001f);
        }
    }

    // -- Neighbor lookup --

    [Test]
    public void Neighbors_Returns6()
    {
        var neighbors = HexGridUtility.Neighbors(3, 4);
        Assert.AreEqual(6, neighbors.Length);
    }

    [Test]
    public void Neighbors_Origin_ContainsExpected()
    {
        var neighbors = HexGridUtility.Neighbors(0, 0);
        // axial neighbors of (0,0): (1,0), (0,1), (-1,1), (-1,0), (0,-1), (1,-1)
        Assert.Contains(new Vector2Int(1, 0), neighbors);
        Assert.Contains(new Vector2Int(-1, 1), neighbors);
        Assert.Contains(new Vector2Int(0, -1), neighbors);
    }

    // -- Distance --

    [Test]
    public void HexDistance_SameHex_IsZero()
    {
        Assert.AreEqual(0, HexGridUtility.HexDistance(3, 4, 3, 4));
    }

    [Test]
    public void HexDistance_Adjacent_IsOne()
    {
        Assert.AreEqual(1, HexGridUtility.HexDistance(0, 0, 1, 0));
    }

    [Test]
    public void HexDistance_TwoAway()
    {
        Assert.AreEqual(2, HexGridUtility.HexDistance(0, 0, 2, 0));
    }
}
```

**Step 2: Run tests to verify they fail**

Run via MCP Unity: `mcp__mcp-unity__run_tests` (filter: HexGridUtility)
Expected: compilation error -- `HexGridUtility` does not exist.

**Step 3: Implement HexGridUtility**

```csharp
using UnityEngine;

/// <summary>
/// Pure C# hex grid math. Pointy-top axial coordinates (q, r).
/// </summary>
public static class HexGridUtility
{
    private static readonly Vector2Int[] NeighborOffsets = {
        new(1, 0), new(0, 1), new(-1, 1),
        new(-1, 0), new(0, -1), new(1, -1)
    };

    public static Vector3 HexToWorld(int q, int r, float size)
    {
        float x = size * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float z = size * (1.5f * r);
        return new Vector3(x, 0f, z);
    }

    public static Vector3[] HexCorners(Vector3 center, float size)
    {
        var corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60f * i - 30f; // pointy-top: first corner at -30 deg
            float angleRad = Mathf.PI / 180f * angleDeg;
            corners[i] = new Vector3(
                center.x + size * Mathf.Cos(angleRad),
                center.y,
                center.z + size * Mathf.Sin(angleRad));
        }
        return corners;
    }

    public static Vector2Int[] Neighbors(int q, int r)
    {
        var result = new Vector2Int[6];
        for (int i = 0; i < 6; i++)
            result[i] = new Vector2Int(q + NeighborOffsets[i].x, r + NeighborOffsets[i].y);
        return result;
    }

    public static int HexDistance(int q1, int r1, int q2, int r2)
    {
        int dq = q2 - q1;
        int dr = r2 - r1;
        return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
    }
}
```

**Step 4: Run tests to verify they pass**

Run via MCP Unity: `mcp__mcp-unity__run_tests` (filter: HexGridUtility)
Expected: all 8 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/World/HexGridUtility.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/HexGridUtilityTests.cs
git commit -m "Add HexGridUtility with axial hex math and tests"
```

---

### Task 2: OverworldBiome -- biome enum, colors, and lookup

**Files:**
- Create: `Assets/_Slopworks/Scripts/World/OverworldBiome.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/OverworldBiomeTests.cs`

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OverworldBiomeTests
{
    [Test]
    public void Lookup_WarmDry_ReturnsWasteland()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.8f, moisture: 0.1f);
        Assert.AreEqual(OverworldBiomeType.Wasteland, biome);
    }

    [Test]
    public void Lookup_WarmWet_ReturnsSwamp()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.8f, moisture: 0.9f);
        Assert.AreEqual(OverworldBiomeType.Swamp, biome);
    }

    [Test]
    public void Lookup_CoolMedium_ReturnsForest()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.5f);
        Assert.AreEqual(OverworldBiomeType.Forest, biome);
    }

    [Test]
    public void Lookup_CoolDry_ReturnsRuins()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.1f);
        Assert.AreEqual(OverworldBiomeType.Ruins, biome);
    }

    [Test]
    public void Lookup_CoolWet_ReturnsOvergrownRuins()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.9f);
        Assert.AreEqual(OverworldBiomeType.OvergrownRuins, biome);
    }

    [Test]
    public void Lookup_MidMid_ReturnsGrassland()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.5f, moisture: 0.5f);
        Assert.AreEqual(OverworldBiomeType.Grassland, biome);
    }

    [Test]
    public void GetColor_AllBiomes_ReturnNonBlack()
    {
        foreach (OverworldBiomeType b in System.Enum.GetValues(typeof(OverworldBiomeType)))
        {
            var c = OverworldBiomeLookup.GetColor(b);
            Assert.IsTrue(c.r > 0f || c.g > 0f || c.b > 0f, $"{b} color is black");
        }
    }
}
```

**Step 2: Run tests -- expected fail (types don't exist)**

**Step 3: Implement**

```csharp
using UnityEngine;

public enum OverworldBiomeType
{
    Grassland,
    Forest,
    Wasteland,
    Swamp,
    Ruins,
    OvergrownRuins
}

/// <summary>
/// Biome lookup from temperature/moisture and biome-to-color mapping.
/// </summary>
public static class OverworldBiomeLookup
{
    public static OverworldBiomeType GetBiome(float temperature, float moisture)
    {
        bool warm = temperature > 0.45f;
        if (moisture < 0.33f)
            return warm ? OverworldBiomeType.Wasteland : OverworldBiomeType.Ruins;
        if (moisture > 0.66f)
            return warm ? OverworldBiomeType.Swamp : OverworldBiomeType.OvergrownRuins;
        return warm ? OverworldBiomeType.Grassland : OverworldBiomeType.Forest;
    }

    public static Color GetColor(OverworldBiomeType biome)
    {
        switch (biome)
        {
            case OverworldBiomeType.Grassland:      return new Color(0.35f, 0.45f, 0.25f);
            case OverworldBiomeType.Forest:          return new Color(0.20f, 0.35f, 0.15f);
            case OverworldBiomeType.Wasteland:       return new Color(0.50f, 0.40f, 0.28f);
            case OverworldBiomeType.Swamp:           return new Color(0.20f, 0.30f, 0.25f);
            case OverworldBiomeType.Ruins:           return new Color(0.40f, 0.38f, 0.35f);
            case OverworldBiomeType.OvergrownRuins:  return new Color(0.30f, 0.38f, 0.28f);
            default:                                 return Color.magenta;
        }
    }
}
```

**Step 4: Run tests -- all 7 PASS**

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/World/OverworldBiome.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/OverworldBiomeTests.cs
git commit -m "Add OverworldBiome enum, lookup table, and color mapping"
```

---

### Task 3: OverworldChunkMeshBuilder -- hex mesh generation

**Files:**
- Create: `Assets/_Slopworks/Scripts/World/OverworldChunkMeshBuilder.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/OverworldChunkMeshBuilderTests.cs`

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OverworldChunkMeshBuilderTests
{
    [Test]
    public void Build_SingleHex_Returns6Triangles()
    {
        // 1x1 chunk with one hex
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Grassland;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // a hex = 6 triangles = 18 indices
        Assert.AreEqual(18, mesh.triangles.Length);
    }

    [Test]
    public void Build_SingleHex_Has7Vertices()
    {
        // center + 6 corners
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Forest;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        Assert.AreEqual(7, mesh.vertexCount);
    }

    [Test]
    public void Build_SingleHex_VertexColorsMatchBiome()
    {
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Wasteland;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        var expected = OverworldBiomeLookup.GetColor(OverworldBiomeType.Wasteland);
        foreach (var c in mesh.colors)
            Assert.AreEqual(expected.r, c.r, 0.01f);
    }

    [Test]
    public void Build_2x2Chunk_HasCorrectHexCount()
    {
        var heights = new float[2, 2];
        var biomes = new OverworldBiomeType[2, 2];

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // 4 hexes * 7 verts each = 28 (no sharing for simplicity)
        Assert.AreEqual(28, mesh.vertexCount);
    }

    [Test]
    public void Build_WithHeight_CenterVertexAtCorrectY()
    {
        var heights = new float[1, 1];
        heights[0, 0] = 5f;
        var biomes = new OverworldBiomeType[1, 1];

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // center vertex (first) should be at y=5
        Assert.AreEqual(5f, mesh.vertices[0].y, 0.01f);
    }
}
```

**Step 2: Run tests -- expected fail**

**Step 3: Implement**

```csharp
using UnityEngine;

/// <summary>
/// Builds a combined hex mesh for one chunk of the overworld grid.
/// Each hex has a center vertex + 6 corner vertices, 6 triangles.
/// Vertex colors set from biome type.
/// </summary>
public static class OverworldChunkMeshBuilder
{
    /// <summary>
    /// Build mesh for a chunk.
    /// heights[localQ, localR] = elevation. biomes[localQ, localR] = biome type.
    /// chunkQ, chunkR = chunk offset in hex coords (multiply by chunk size to get global offset).
    /// </summary>
    public static Mesh Build(float[,] heights, OverworldBiomeType[,] biomes, float hexSize,
        int chunkQ, int chunkR)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        int hexCount = width * height;

        var vertices = new Vector3[hexCount * 7];
        var triangles = new int[hexCount * 18];
        var colors = new Color[hexCount * 7];

        int vi = 0;
        int ti = 0;

        for (int r = 0; r < height; r++)
        {
            for (int q = 0; q < width; q++)
            {
                int globalQ = chunkQ * width + q;
                int globalR = chunkR * height + r;

                var worldPos = HexGridUtility.HexToWorld(globalQ, globalR, hexSize);
                worldPos.y = heights[q, r];

                var corners = HexGridUtility.HexCorners(worldPos, hexSize);
                var biomeColor = OverworldBiomeLookup.GetColor(biomes[q, r]);

                int centerIdx = vi;
                vertices[vi] = worldPos;
                colors[vi] = biomeColor;
                vi++;

                for (int c = 0; c < 6; c++)
                {
                    vertices[vi] = corners[c];
                    colors[vi] = biomeColor;
                    vi++;
                }

                // 6 triangles: center to each edge
                for (int c = 0; c < 6; c++)
                {
                    triangles[ti++] = centerIdx;
                    triangles[ti++] = centerIdx + 1 + c;
                    triangles[ti++] = centerIdx + 1 + (c + 1) % 6;
                }
            }
        }

        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
```

**Step 4: Run tests -- all 5 PASS**

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/World/OverworldChunkMeshBuilder.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/OverworldChunkMeshBuilderTests.cs
git commit -m "Add OverworldChunkMeshBuilder for hex mesh generation"
```

---

### Task 4: OverworldTerrainGenerator -- editor script that builds the full scene

**Files:**
- Create: `Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs`

This is an editor-only script (no tests -- it orchestrates the tested components into a scene). Follow the same pattern as `HomeBaseTerrainGenerator.cs`.

**Step 1: Implement the generator**

Key constants:
- `MapSize = 128` (128x128 hexes)
- `ChunkSize = 8` (8x8 hexes per chunk)
- `HexSize = 1f` (1m radius = 2m flat-to-flat)
- `Seed = 7` (deterministic)
- `MaxElevation = 8f`

The generator:
1. Creates a new empty scene
2. Generates noise maps (elevation, temperature, moisture) for 128x128
3. Applies center-flattening (home base area)
4. Applies distance-based ruin bias
5. For each 8x8 chunk: calls `OverworldChunkMeshBuilder.Build()`, creates a GameObject with MeshFilter + MeshRenderer + MeshCollider
6. Creates a vertex-color material (URP Lit with vertex color enabled)
7. Scatters ruin decorations on ruin-biome hexes using Kenney models
8. Sets up isometric camera, lighting, fog
9. Saves the scene

**Step 2: Create a vertex-color URP material**

The generator creates a material at `Assets/_Slopworks/Materials/Environment/OverworldHex.mat` using `Universal Render Pipeline/Lit` shader with `_BaseColor` white and vertex color enabled via `_VERTEX_COLOR` keyword. If the keyword doesn't exist in URP Lit, use `Universal Render Pipeline/Simple Lit` or a custom shader that multiplies `_BaseColor` by vertex color.

**Step 3: Run via MCP Unity**

```
mcp__mcp-unity__execute_menu_item("Slopworks/Generate Overworld Terrain")
```

Verify in console:
- "overworld terrain generated: ..." log message
- No errors
- 256 chunk objects created

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs
git commit -m "Add OverworldTerrainGenerator editor script"
```

---

### Task 5: Ruin decorations and building node markers

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs`

**Step 1: Add ruin decoration placement**

After chunk mesh generation, iterate ruin-biome hexes. For each hex in a Ruins or OvergrownRuins biome with `rng.NextDouble() < 0.15`:
- Pick a random Kenney model from the same prop lists used in `HomeBaseSceneryDresser`
- Place it at the hex center world position + terrain height
- Random Y rotation, slight random tilt
- Scale 1-2x (these are smaller than HomeBase props since map is zoomed out)
- Parent under `Decorations/ChunkN`

**Step 2: Add building node markers**

Create 8-12 sample `OverworldNode` entries at fixed hex coordinates. For each node:
- Instantiate a Kenney structure model (e.g., `structure-tall.fbx` for Tower, `structure-wall.fbx` for Building)
- Position at the hex center + height
- Scale up slightly to be visible from isometric camera
- Parent under `NodeMarkers/`

**Step 3: Re-run generator and verify**

```
mcp__mcp-unity__execute_menu_item("Slopworks/Generate Overworld Terrain")
```

Verify: ruin props visible on ruin hexes, node markers visible at correct positions.

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs
git commit -m "Add ruin decorations and building node markers to overworld"
```

---

### Task 6: Apply Kenney materials and save scene

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs`

**Step 1: Apply Kenney materials to decorations**

After placing all decorations, call the same material assignment logic from `KenneyMaterialSetup` -- iterate renderers under Decorations/, match prefab source to kit, assign material.

**Step 2: Save final scene**

Ensure scene saves to `Assets/_Slopworks/Scenes/Overworld/Overworld_Terrain.unity` with all objects, materials, and lighting.

**Step 3: Final verification**

Run generator one last time. Open scene in editor. Switch to isometric view (Numpad 5 for ortho, then top-down angled). Verify:
- Hex grid visible with distinct biome colors
- Height variation creates rolling terrain
- Ruin props scattered on gray/brown hexes
- Node markers visible
- Lighting and fog match post-apocalyptic style

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/OverworldTerrainGenerator.cs \
       Assets/_Slopworks/Scenes/Overworld/
git commit -m "Finalize overworld terrain scene with materials and lighting"
```

---

## Task summary

| Task | What | Files | Tests |
|---|---|---|---|
| 1 | HexGridUtility | Scripts/World/ | 8 tests |
| 2 | OverworldBiome | Scripts/World/ | 7 tests |
| 3 | ChunkMeshBuilder | Scripts/World/ | 5 tests |
| 4 | TerrainGenerator | Scripts/Editor/ | visual |
| 5 | Decorations + nodes | Scripts/Editor/ | visual |
| 6 | Materials + save | Scripts/Editor/ | visual |

Total: 6 tasks, 20 unit tests, 1 scene output.
