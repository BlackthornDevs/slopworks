using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural first-pass terrain painting with biome regions.
/// Large-scale noise defines biomes (grassland, wetland, forest, scrubland, rocky).
/// Terrain features (slope, height, curvature) modulate within each biome.
/// Run from Slopworks > Paint Terrain (First Pass).
/// </summary>
public static class TerrainPainter
{
    // Settlement pad positions (world-space)
    private static readonly Vector2[] SettlementPads = {
        new(0f, 0f),         // factory yard
        new(80f, 60f),       // farmstead
        new(-70f, 50f),      // workshop
        new(-120f, -80f),    // river depot
        new(0f, -150f),      // watchtower
        new(100f, -70f),     // market
        new(-80f, -40f),     // barracks
        new(60f, 130f),      // greenhouse
    };
    private static readonly float[] PadRadii = {
        30f, 20f, 15f, 12f, 25f, 20f, 15f, 12f
    };

    // Layer indices
    private static int L_SparseGrass, L_LeafyGrass, L_ForestGround, L_GrassRock;
    private static int L_Rock, L_MossyRock, L_GroundRock;
    private static int L_Dirt, L_MudRocks, L_MudLeaves;
    private static int L_Cobblestone, L_StonePath;
    private static int L_RiverRocks, L_SandRocks;
    private static int _layerCount;

    // Biome types
    private enum Biome { Grassland, Meadow, Wetland, Forest, Scrubland }

    [MenuItem("Slopworks/Paint Terrain (First Pass)")]
    public static void Paint()
    {
        TerrainLayerSetup.Setup();

        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("terrain painter: no active terrain.");
            return;
        }

        var td = terrain.terrainData;
        if (!MapLayers(td))
        {
            Debug.LogError("terrain painter: missing required layers.");
            return;
        }

        PaintAlphamap(terrain, td);
        EditorUtility.SetDirty(td);
        Debug.Log("terrain painter: first pass complete. use Paint Texture brush to refine.");
    }

    private static bool MapLayers(TerrainData td)
    {
        var layers = td.terrainLayers;
        _layerCount = layers.Length;

        L_SparseGrass = L_LeafyGrass = L_ForestGround = L_GrassRock = -1;
        L_Rock = L_MossyRock = L_GroundRock = -1;
        L_Dirt = L_MudRocks = L_MudLeaves = -1;
        L_Cobblestone = L_StonePath = -1;
        L_RiverRocks = L_SandRocks = -1;

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null || layers[i].diffuseTexture == null) continue;
            string n = layers[i].diffuseTexture.name.ToLowerInvariant();

            if (n.Contains("sparse_grass")) L_SparseGrass = i;
            else if (n.Contains("leafy_grass")) L_LeafyGrass = i;
            else if (n.Contains("forrest_ground") || n.Contains("forest_ground")) L_ForestGround = i;
            else if (n.Contains("aerial_grass_rock")) L_GrassRock = i;
            else if (n.Contains("rock_04")) L_Rock = i;
            else if (n.Contains("mossy_rock")) L_MossyRock = i;
            else if (n.Contains("aerial_ground_rock")) L_GroundRock = i;
            else if (n.Contains("dirt_diff")) L_Dirt = i;
            else if (n.Contains("brown_mud_rocks")) L_MudRocks = i;
            else if (n.Contains("brown_mud_leaves")) L_MudLeaves = i;
            else if (n.Contains("cobblestone_05")) L_Cobblestone = i;
            else if (n.Contains("grey_stone_path")) L_StonePath = i;
            else if (n.Contains("river_small_rocks")) L_RiverRocks = i;
            else if (n.Contains("coast_sand_rocks")) L_SandRocks = i;
        }

        if (L_SparseGrass < 0 || L_Dirt < 0 || L_Rock < 0)
        {
            Debug.LogError($"terrain painter: missing base layers (sparseGrass={L_SparseGrass} dirt={L_Dirt} rock={L_Rock})");
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------
    // Main painting loop
    // ---------------------------------------------------------------

    private static void PaintAlphamap(Terrain terrain, TerrainData td)
    {
        int res = td.alphamapResolution;
        int hmRes = td.heightmapResolution;
        float[,,] alphas = new float[res, res, _layerCount];
        float[,] heights = td.GetHeights(0, 0, hmRes, hmRes);
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float worldX = terrainPos.x + nx * terrainSize.x;
                float worldZ = terrainPos.z + nz * terrainSize.z;

                int hx = Mathf.Clamp(Mathf.RoundToInt(nx * (hmRes - 1)), 0, hmRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(nz * (hmRes - 1)), 0, hmRes - 1);

                float height = heights[hz, hx] * terrainSize.y;
                float steepness = td.GetSteepness(nx, nz);
                Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                float curvature = ComputeCurvature(heights, hx, hz, hmRes);

                float aspect = Mathf.Atan2(normal.z, normal.x) * Mathf.Rad2Deg;
                if (aspect < 0f) aspect += 360f;
                float northFacing = Mathf.Clamp01(Mathf.Cos((aspect - 90f) * Mathf.Deg2Rad) * 0.5f + 0.5f);

                float dx = nx - 0.5f;
                float dz = nz - 0.5f;
                float distFromCenter = Mathf.Sqrt(dx * dx + dz * dz) * 2f;

                float padProximity = GetPadProximity(worldX, worldZ);

                // BIOME noise: very low frequency for large contiguous regions
                float biomeNoise = FBM(worldX, worldZ, 0.005f, 2, 500f);
                // Secondary biome noise offset to break symmetry
                float biomeNoise2 = FBM(worldX, worldZ, 0.007f, 2, 1500f);
                // Detail noise for within-biome variation
                float detail = FBM(worldX, worldZ, 0.03f, 2, 3000f);

                // Height and wetness shift the biome selection
                // Low concave areas push toward wetland; high dry = scrubland
                float heightBias = Mathf.Clamp01(height / 25f);
                float effectiveBiome = biomeNoise - heightBias * 0.15f
                    + (curvature < -0.008f ? -0.15f : 0f);

                // Grassland is the dominant biome — most of the map should be green
                Biome biome;
                if (effectiveBiome < 0.18f)
                    biome = Biome.Wetland;       // ~10% - only truly low/wet pockets
                else if (effectiveBiome < 0.3f)
                    biome = Biome.Forest;         // ~12% - shaded/dense areas
                else if (effectiveBiome < 0.75f)
                    biome = Biome.Grassland;      // ~50% - dominant green
                else if (effectiveBiome < 0.85f)
                    biome = Biome.Meadow;         // ~15% - mixed grass variety
                else
                    biome = Biome.Scrubland;      // ~13% - drier hilltops

                float[] w = new float[_layerCount];
                PaintBiome(w, biome, detail, biomeNoise2);
                ApplyTerrainFeatures(w, height, steepness, northFacing, curvature,
                    distFromCenter, padProximity, detail);

                // Normalize
                float total = 0f;
                for (int i = 0; i < _layerCount; i++) total += w[i];
                if (total > 0.001f)
                    for (int i = 0; i < _layerCount; i++) alphas[z, x, i] = w[i] / total;
                else
                    alphas[z, x, L_LeafyGrass] = 1f;
            }
        }

        td.SetAlphamaps(0, 0, alphas);
    }

    // ---------------------------------------------------------------
    // Biome base textures — each biome is a distinct look
    // ---------------------------------------------------------------

    private static void PaintBiome(float[] w, Biome biome, float detail, float noise2)
    {
        switch (biome)
        {
            case Biome.Grassland:
                // Lush green — the dominant look of the map
                Add(w, L_LeafyGrass, 0.85f);
                Add(w, L_SparseGrass, 0.1f * detail);
                break;

            case Biome.Meadow:
                // Slightly drier mixed grasses, still clearly green
                Add(w, L_LeafyGrass, 0.5f);
                Add(w, L_SparseGrass, 0.35f);
                Add(w, L_GrassRock, 0.08f * detail);
                break;

            case Biome.Wetland:
                // Swampy lowland: dark green-brown, but still has grass
                Add(w, L_LeafyGrass, 0.3f);
                Add(w, L_ForestGround, 0.3f);
                Add(w, L_MudLeaves, 0.2f);
                Add(w, L_MudRocks, 0.08f * detail);
                Add(w, L_RiverRocks, 0.05f * noise2);
                break;

            case Biome.Forest:
                // Shaded forest: green grass under canopy with leaf litter
                Add(w, L_LeafyGrass, 0.4f);
                Add(w, L_ForestGround, 0.35f);
                Add(w, L_MossyRock, 0.08f * detail);
                Add(w, L_MudLeaves, 0.06f * noise2);
                break;

            case Biome.Scrubland:
                // Drier but still grassy — sparse grass with some dirt
                Add(w, L_SparseGrass, 0.55f);
                Add(w, L_LeafyGrass, 0.15f);
                Add(w, L_Dirt, 0.12f);
                Add(w, L_GrassRock, 0.1f * detail);
                break;
        }
    }

    // ---------------------------------------------------------------
    // Terrain features override biome base where necessary
    // ---------------------------------------------------------------

    private static void ApplyTerrainFeatures(float[] w,
        float height, float steepness, float northFacing, float curvature,
        float distFromCenter, float padProximity, float detail)
    {
        // STEEP SLOPES: rock regardless of biome
        if (steepness > 25f)
        {
            float slopeBlend = Mathf.Clamp01((steepness - 25f) / 25f);

            // Scale down existing weights to make room for rock
            float scale = 1f - slopeBlend * 0.8f;
            for (int i = 0; i < w.Length; i++) w[i] *= scale;

            Add(w, L_Rock, 0.5f * slopeBlend);
            Add(w, L_GrassRock, 0.3f * slopeBlend * (1f - Mathf.Clamp01((steepness - 40f) / 15f)));
            Add(w, L_MossyRock, 0.2f * slopeBlend * northFacing);
            Add(w, L_GroundRock, 0.15f * slopeBlend * (1f - northFacing));
        }
        else if (steepness > 12f)
        {
            // Moderate slope: blend in some grass-rock
            float modSlope = Mathf.Clamp01((steepness - 12f) / 13f);
            Add(w, L_GrassRock, 0.3f * modSlope);
        }

        // SETTLEMENT PADS: cobblestone core, dirt ring
        if (padProximity < 1.2f)
        {
            float padCore = Mathf.Clamp01(1f - padProximity / 0.6f);
            float padRing = Mathf.Clamp01(1f - Mathf.Abs(padProximity - 0.85f) / 0.35f);

            // Scale down biome in pad core
            if (padCore > 0f)
            {
                float scale = 1f - padCore * 0.9f;
                for (int i = 0; i < w.Length; i++) w[i] *= scale;
            }

            Add(w, L_Cobblestone, 0.6f * padCore);
            Add(w, L_StonePath, 0.25f * padCore * detail);
            Add(w, L_Dirt, 0.25f * padRing);
        }

        // LOWEST POINTS: river bed
        if (height < 1f && steepness < 8f)
        {
            float wetFactor = Mathf.Clamp01((1f - height) / 1f);
            float scale = 1f - wetFactor * 0.7f;
            for (int i = 0; i < w.Length; i++) w[i] *= scale;
            Add(w, L_RiverRocks, 0.4f * wetFactor);
            Add(w, L_SandRocks, 0.25f * wetFactor);
        }

        // MAP EDGES: rocky highlands
        if (distFromCenter > 0.75f)
        {
            float edgeFactor = Mathf.Clamp01((distFromCenter - 0.75f) / 0.2f);
            Add(w, L_Rock, 0.12f * edgeFactor);
            Add(w, L_GroundRock, 0.08f * edgeFactor);
        }
    }

    // ---------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------

    private static float ComputeCurvature(float[,] heights, int x, int z, int res)
    {
        int x0 = Mathf.Max(x - 1, 0);
        int x1 = Mathf.Min(x + 1, res - 1);
        int z0 = Mathf.Max(z - 1, 0);
        int z1 = Mathf.Min(z + 1, res - 1);

        float center = heights[z, x];
        float avgNeighbor = (heights[z, x0] + heights[z, x1] +
                             heights[z0, x] + heights[z1, x]) * 0.25f;
        return center - avgNeighbor;
    }

    private static float FBM(float x, float z, float frequency, int octaves, float seed)
    {
        float value = 0f;
        float amplitude = 1f;
        float totalAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += Mathf.PerlinNoise(x * frequency + seed, z * frequency + seed) * amplitude;
            totalAmplitude += amplitude;
            frequency *= 2f;
            amplitude *= 0.5f;
            seed += 100f;
        }

        return value / totalAmplitude;
    }

    private static float GetPadProximity(float worldX, float worldZ)
    {
        float nearest = float.MaxValue;
        for (int p = 0; p < SettlementPads.Length; p++)
        {
            float pdx = worldX - SettlementPads[p].x;
            float pdz = worldZ - SettlementPads[p].y;
            float dist = Mathf.Sqrt(pdx * pdx + pdz * pdz) / PadRadii[p];
            if (dist < nearest) nearest = dist;
        }
        return nearest;
    }

    private static void Add(float[] w, int index, float value)
    {
        if (index >= 0 && index < w.Length)
            w[index] += value;
    }
}
