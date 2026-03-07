using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the HomeBaseTerrain scene with a sculpted Unity Terrain.
/// Run from Slopworks > Generate HomeBase Terrain.
/// </summary>
public static class HomeBaseTerrainGenerator
{
    private const string ScenePath = "Assets/_Slopworks/Scenes/Joe/HomeBaseTerrain.unity";
    private const string TerrainDataPath = "Assets/_Slopworks/Art/Terrain/HomeBase/HomeBaseTerrainData.asset";
    private const int HeightmapRes = 513; // must be 2^n + 1
    private const int AlphamapRes = 512;
    private const float TerrainWidth = 200f;
    private const float TerrainLength = 200f;
    private const float TerrainHeight = 60f;

    // How much of the center is kept flat (fraction of total size, measured from center)
    private const float FlatRadiusFraction = 0.25f; // 50m radius in a 200m terrain
    private const float TransitionWidth = 0.1f; // blend zone between flat and hills

    [MenuItem("Slopworks/Generate HomeBase Terrain")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var terrainData = CreateTerrainData();
        AssetDatabase.CreateAsset(terrainData, TerrainDataPath);

        var terrainObj = Terrain.CreateTerrainGameObject(terrainData);
        terrainObj.name = "Terrain";
        terrainObj.layer = PhysicsLayers.Terrain;

        // Center the terrain so (0,0,0) is in the middle
        terrainObj.transform.position = new Vector3(-TerrainWidth / 2f, 0f, -TerrainLength / 2f);

        // Terrain collider is added automatically by CreateTerrainGameObject

        SetupLighting();
        SetupFog();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"HomeBase terrain generated: {ScenePath}");
    }

    private static TerrainData CreateTerrainData()
    {
        var td = new TerrainData();
        td.heightmapResolution = HeightmapRes;
        td.alphamapResolution = AlphamapRes;
        td.size = new Vector3(TerrainWidth, TerrainHeight, TerrainLength);

        SculptHeightmap(td);
        PaintSplatmap(td);

        return td;
    }

    private static void SculptHeightmap(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = new float[res, res];
        float centerX = 0.5f;
        float centerZ = 0.5f;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1); // 0..1
                float nz = (float)z / (res - 1);

                // Distance from center (0..~0.7 for corners)
                float dx = nx - centerX;
                float dz = nz - centerZ;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                // Flat zone in center, transition to hills at edges
                float flatEnd = FlatRadiusFraction;
                float hillStart = flatEnd + TransitionWidth;

                float hillFactor;
                if (dist < flatEnd)
                    hillFactor = 0f;
                else if (dist < hillStart)
                    hillFactor = Mathf.SmoothStep(0f, 1f, (dist - flatEnd) / TransitionWidth);
                else
                    hillFactor = 1f;

                // Multi-octave Perlin noise for natural hills
                float worldX = nx * TerrainWidth;
                float worldZ = nz * TerrainLength;

                float noise = 0f;
                noise += Mathf.PerlinNoise(worldX * 0.012f + 100f, worldZ * 0.012f + 100f) * 0.5f;  // broad hills
                noise += Mathf.PerlinNoise(worldX * 0.03f + 200f, worldZ * 0.03f + 200f) * 0.25f;    // medium detail
                noise += Mathf.PerlinNoise(worldX * 0.08f + 300f, worldZ * 0.08f + 300f) * 0.1f;     // fine bumps

                // Scale to desired height range (hills up to ~15m, base at ~2m for slight ground variation)
                float baseHeight = 2f / TerrainHeight; // slight base elevation so center isn't at y=0
                float hillHeight = noise * 18f / TerrainHeight;

                // Edge ramp: push terrain higher near edges for natural boundary
                float edgeDist = Mathf.Min(nx, nz, 1f - nx, 1f - nz);
                float edgeRamp = 0f;
                if (edgeDist < 0.08f)
                    edgeRamp = Mathf.SmoothStep(8f / TerrainHeight, 0f, edgeDist / 0.08f);

                heights[z, x] = baseHeight + hillFactor * hillHeight + edgeRamp;
            }
        }

        td.SetHeights(0, 0, heights);
    }

    private static void PaintSplatmap(TerrainData td)
    {
        // Create terrain layers: 0=dirt (center), 1=grass (hills), 2=rock (steep/edges)
        var layers = new TerrainLayer[3];

        layers[0] = CreateTerrainLayer("Dirt", new Color(0.35f, 0.28f, 0.22f));
        layers[1] = CreateTerrainLayer("Grass", new Color(0.25f, 0.35f, 0.18f));
        layers[2] = CreateTerrainLayer("Rock", new Color(0.4f, 0.38f, 0.35f));

        td.terrainLayers = layers;

        int res = td.alphamapResolution;
        float[,,] alphas = new float[res, res, 3];

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float dx = nx - 0.5f;
                float dz = nz - 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                // Steepness from heightmap
                float steepness = td.GetSteepness(nx, nz);

                // Determine blend weights
                float dirt = 0f, grass = 0f, rock = 0f;

                if (steepness > 25f)
                {
                    // Steep slopes are rock
                    rock = 1f;
                }
                else if (dist < FlatRadiusFraction)
                {
                    // Center flat area is dirt/concrete
                    dirt = 1f;
                }
                else
                {
                    // Transition zone and hills are grass with some dirt
                    float t = Mathf.Clamp01((dist - FlatRadiusFraction) / TransitionWidth);
                    dirt = 1f - t;
                    grass = t;

                    // Mix in rock on moderate slopes
                    if (steepness > 12f)
                    {
                        float rockBlend = (steepness - 12f) / 13f;
                        rock = rockBlend;
                        grass *= (1f - rockBlend);
                        dirt *= (1f - rockBlend);
                    }
                }

                // Add noise to break up uniform zones
                float noiseVal = Mathf.PerlinNoise(nx * 30f + 500f, nz * 30f + 500f);
                if (dist > FlatRadiusFraction * 0.8f && noiseVal > 0.6f)
                {
                    float noiseMix = (noiseVal - 0.6f) * 2.5f; // 0..1
                    dirt += noiseMix * 0.3f;
                    grass -= noiseMix * 0.15f;
                }

                // Normalize
                float total = dirt + grass + rock;
                if (total > 0f)
                {
                    alphas[z, x, 0] = dirt / total;
                    alphas[z, x, 1] = grass / total;
                    alphas[z, x, 2] = rock / total;
                }
                else
                {
                    alphas[z, x, 0] = 1f;
                }
            }
        }

        td.SetAlphamaps(0, 0, alphas);
    }

    private static TerrainLayer CreateTerrainLayer(string name, Color tint)
    {
        var layer = new TerrainLayer();
        layer.name = name;
        layer.tileSize = new Vector2(10f, 10f);

        // Generate a simple procedural texture for the layer
        var tex = new Texture2D(64, 64, TextureFormat.RGB24, true);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.2f - 0.1f;
                float fine = Mathf.PerlinNoise(x * 0.5f + 50f, y * 0.5f + 50f) * 0.08f - 0.04f;
                Color c = new Color(
                    Mathf.Clamp01(tint.r + noise + fine),
                    Mathf.Clamp01(tint.g + noise + fine),
                    Mathf.Clamp01(tint.b + noise + fine));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        // Save texture as asset so it persists
        string texPath = $"Assets/_Slopworks/Art/Terrain/HomeBase/TerrainTex_{name}.asset";
        AssetDatabase.CreateAsset(tex, texPath);

        layer.diffuseTexture = tex;

        // Save layer as asset
        string layerPath = $"Assets/_Slopworks/Art/Terrain/HomeBase/TerrainLayer_{name}.asset";
        AssetDatabase.CreateAsset(layer, layerPath);

        return layer;
    }

    private static void SetupLighting()
    {
        // Directional light: warm, slightly orange post-apocalyptic sun
        var lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.82f, 0.6f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

        // Ambient: muted warm tones
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.4f, 0.35f, 0.28f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.25f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.12f, 0.1f);
    }

    private static void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.4f, 0.35f, 0.28f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 180f;

        // Skybox color to match fog for seamless horizon
        RenderSettings.skybox = null; // solid color fallback
        Camera.main?.gameObject?.SetActive(false); // no camera in terrain scene
    }
}
