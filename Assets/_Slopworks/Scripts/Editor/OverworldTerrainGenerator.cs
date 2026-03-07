using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates the overworld terrain scene: 128x128 hex grid with biomes, height, and ruin decorations.
/// Run from Slopworks > Generate Overworld Terrain.
/// </summary>
public static class OverworldTerrainGenerator
{
    private const string ScenePath = "Assets/_Slopworks/Scenes/Overworld/Overworld_Terrain.unity";
    private const string MaterialPath = "Assets/_Slopworks/Materials/Environment/OverworldHex.mat";

    private const int MapSize = 128;
    private const int ChunkSize = 8;
    private const float HexSize = 1f; // 1m radius = 2m flat-to-flat
    private const float MaxElevation = 8f;
    private const float FlatCenterRadius = 0.15f; // fraction of map radius kept flat
    private const int Seed = 7;

    // Noise offsets for different layers
    private const float ElevationOffsetX = 100f;
    private const float ElevationOffsetZ = 100f;
    private const float TemperatureOffset = 300f;
    private const float MoistureOffset = 500f;

    // Ruin decoration prop paths
    private static readonly string[] RuinProps = {
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-wall.fbx",
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-window.fbx",
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-doorway.fbx",
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-tall.fbx",
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover.fbx",
        "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover-stripe.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/barrel.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/box.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-large.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel.fbx",
    };

    // Nature decoration props
    private static readonly string[] NatureProps = {
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-a.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-b.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-c.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass-large.fbx",
        "Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass-large.fbx",
    };

    // Kenney materials (created by KenneyMaterialSetup)
    private static readonly string[] KenneyMaterialPaths = {
        "Assets/_Slopworks/Materials/Kenney/Kenney_Survival.mat",
        "Assets/_Slopworks/Materials/Kenney/Kenney_Conveyor.mat",
    };

    [MenuItem("Slopworks/Generate Overworld Terrain")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rng = new System.Random(Seed);

        // Generate noise maps
        float[,] elevation = new float[MapSize, MapSize];
        float[,] temperature = new float[MapSize, MapSize];
        float[,] moisture = new float[MapSize, MapSize];
        OverworldBiomeType[,] biomes = new OverworldBiomeType[MapSize, MapSize];

        float mapCenter = MapSize / 2f;
        float maxDist = MapSize * 0.5f;

        for (int r = 0; r < MapSize; r++)
        {
            for (int q = 0; q < MapSize; q++)
            {
                float nq = (float)q / MapSize;
                float nr = (float)r / MapSize;
                float dist = Mathf.Sqrt((q - mapCenter) * (q - mapCenter) + (r - mapCenter) * (r - mapCenter)) / maxDist;

                // Elevation: 3-octave Perlin, flat near center, rising toward edges
                float elev = 0f;
                elev += Mathf.PerlinNoise(nq * 4f + ElevationOffsetX, nr * 4f + ElevationOffsetZ) * 0.5f;
                elev += Mathf.PerlinNoise(nq * 8f + ElevationOffsetX + 50f, nr * 8f + ElevationOffsetZ + 50f) * 0.3f;
                elev += Mathf.PerlinNoise(nq * 16f + ElevationOffsetX + 100f, nr * 16f + ElevationOffsetZ + 100f) * 0.15f;

                // Center flattening
                float flatFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - FlatCenterRadius) / 0.15f));
                elevation[q, r] = elev * MaxElevation * flatFactor;

                // Temperature and moisture: low frequency, single octave
                temperature[q, r] = Mathf.PerlinNoise(nq * 2f + TemperatureOffset, nr * 2f + TemperatureOffset);
                moisture[q, r] = Mathf.PerlinNoise(nq * 2.5f + MoistureOffset, nr * 2.5f + MoistureOffset);

                // Distance-based ruin bias: further from center = cooler + drier = more ruins
                float ruinBias = Mathf.Clamp01(dist * 0.3f);
                temperature[q, r] = Mathf.Clamp01(temperature[q, r] - ruinBias * 0.15f);
                moisture[q, r] = Mathf.Clamp01(moisture[q, r] - ruinBias * 0.1f);

                biomes[q, r] = OverworldBiomeLookup.GetBiome(temperature[q, r], moisture[q, r]);
            }
        }

        // Create hex material
        var hexMat = CreateHexMaterial();

        // Build chunks
        var chunksRoot = new GameObject("Chunks");
        int chunksPerAxis = MapSize / ChunkSize;

        for (int cr = 0; cr < chunksPerAxis; cr++)
        {
            for (int cq = 0; cq < chunksPerAxis; cq++)
            {
                // Extract chunk data
                var chunkHeights = new float[ChunkSize, ChunkSize];
                var chunkBiomes = new OverworldBiomeType[ChunkSize, ChunkSize];

                for (int lr = 0; lr < ChunkSize; lr++)
                {
                    for (int lq = 0; lq < ChunkSize; lq++)
                    {
                        int gq = cq * ChunkSize + lq;
                        int gr = cr * ChunkSize + lr;
                        chunkHeights[lq, lr] = elevation[gq, gr];
                        chunkBiomes[lq, lr] = biomes[gq, gr];
                    }
                }

                var mesh = OverworldChunkMeshBuilder.Build(chunkHeights, chunkBiomes, HexSize, cq, cr);

                var chunkObj = new GameObject($"Chunk_{cq}_{cr}");
                chunkObj.transform.SetParent(chunksRoot.transform);

                var mf = chunkObj.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = chunkObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = hexMat;

                var mc = chunkObj.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;

                chunkObj.isStatic = true;
            }
        }

        // Decorations
        var decoRoot = new GameObject("Decorations");
        ScatterDecorations(decoRoot.transform, elevation, biomes, rng);

        // Node markers
        var nodeRoot = new GameObject("NodeMarkers");
        PlaceNodeMarkers(nodeRoot.transform, elevation);

        // Camera, lighting, fog
        SetupIsometricCamera();
        SetupLighting();
        SetupFog();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"overworld terrain generated: {ScenePath} ({chunksPerAxis * chunksPerAxis} chunks, {MapSize * MapSize} hexes)");
    }

    private static Material CreateHexMaterial()
    {
        // Check if material already exists
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        var shader = Shader.Find("Slopworks/VertexColorLit");
        if (shader == null)
        {
            Debug.LogWarning("Slopworks/VertexColorLit shader not found, falling back to Simple Lit");
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        }
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.name = "OverworldHex";
        mat.SetFloat("_Smoothness", 0.3f);
        mat.enableInstancing = true;

        // Ensure folders exist
        if (!AssetDatabase.IsValidFolder("Assets/_Slopworks/Materials/Environment"))
            AssetDatabase.CreateFolder("Assets/_Slopworks/Materials", "Environment");

        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    private static void ScatterDecorations(Transform root, float[,] elevation,
        OverworldBiomeType[,] biomes, System.Random rng)
    {
        var survivalMat = AssetDatabase.LoadAssetAtPath<Material>(KenneyMaterialPaths[0]);
        var conveyorMat = AssetDatabase.LoadAssetAtPath<Material>(KenneyMaterialPaths[1]);

        int placed = 0;

        for (int r = 0; r < MapSize; r++)
        {
            for (int q = 0; q < MapSize; q++)
            {
                var biome = biomes[q, r];
                bool isRuin = biome == OverworldBiomeType.Ruins || biome == OverworldBiomeType.OvergrownRuins;
                bool isForest = biome == OverworldBiomeType.Forest || biome == OverworldBiomeType.OvergrownRuins;
                bool isGrass = biome == OverworldBiomeType.Grassland || biome == OverworldBiomeType.Swamp;

                float chance = isRuin ? 0.12f : isForest ? 0.08f : isGrass ? 0.04f : 0.02f;
                if (rng.NextDouble() > chance) continue;

                string[] propList = isRuin ? RuinProps : NatureProps;
                string propPath = propList[rng.Next(propList.Length)];

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(propPath);
                if (prefab == null) continue;

                var worldPos = HexGridUtility.HexToWorld(q, r, HexSize);
                worldPos.y = elevation[q, r];

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root);
                instance.transform.position = worldPos;
                instance.transform.rotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 8f - 4f),
                    (float)(rng.NextDouble() * 360f),
                    (float)(rng.NextDouble() * 8f - 4f));

                // Scale: smaller than HomeBase props since this is a zoomed-out map view
                float scale = 0.5f + (float)rng.NextDouble() * 0.5f;
                instance.transform.localScale = Vector3.one * scale;
                instance.isStatic = true;

                // Assign material based on prop source
                var mat = propPath.Contains("conveyor-kit") ? conveyorMat : survivalMat;
                if (mat != null)
                {
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterial = mat;
                }

                placed++;
            }
        }

        Debug.Log($"overworld decorations: {placed} props placed");
    }

    private static void PlaceNodeMarkers(Transform root, float[,] elevation)
    {
        // Sample building node positions — fixed coordinates for repeatable generation
        var nodePositions = new (int q, int r, string label, Color color)[] {
            (64, 64, "HomeBase", new Color(1f, 0.8f, 0.2f)),
            (40, 50, "Smelter", new Color(0.8f, 0.3f, 0.1f)),
            (85, 45, "Warehouse", new Color(0.4f, 0.5f, 0.7f)),
            (50, 90, "ChemPlant", new Color(0.3f, 0.7f, 0.3f)),
            (95, 80, "PowerStation", new Color(0.9f, 0.9f, 0.2f)),
            (30, 30, "Outpost_NW", new Color(0.6f, 0.6f, 0.6f)),
            (100, 30, "Outpost_NE", new Color(0.6f, 0.6f, 0.6f)),
            (30, 100, "Outpost_SW", new Color(0.6f, 0.6f, 0.6f)),
            (100, 100, "Outpost_SE", new Color(0.6f, 0.6f, 0.6f)),
            (64, 40, "Tower_N", new Color(0.5f, 0.2f, 0.2f)),
            (64, 88, "Tower_S", new Color(0.5f, 0.2f, 0.2f)),
        };

        foreach (var (q, r, label, color) in nodePositions)
        {
            var worldPos = HexGridUtility.HexToWorld(q, r, HexSize);
            worldPos.y = elevation[q, r] + 0.5f; // slightly above terrain

            var marker = GameObject.CreatePrimitive(
                label.StartsWith("HomeBase") ? PrimitiveType.Sphere :
                label.StartsWith("Tower") ? PrimitiveType.Cylinder :
                PrimitiveType.Cube);

            marker.name = $"Node_{label}";
            marker.transform.SetParent(root);
            marker.transform.position = worldPos;

            float scale = label.StartsWith("HomeBase") ? 2f : 1f;
            marker.transform.localScale = new Vector3(scale, label.StartsWith("Tower") ? 3f : scale, scale);

            // Color the marker
            var mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", color);
            marker.GetComponent<Renderer>().sharedMaterial = mat;

            marker.isStatic = true;
        }
    }

    private static void SetupIsometricCamera()
    {
        var camObj = new GameObject("IsometricCamera");
        var cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.25f, 0.22f, 0.18f);

        // Center of the map in world space
        var mapCenter = HexGridUtility.HexToWorld(MapSize / 2, MapSize / 2, HexSize);

        // Classic isometric angle: 30 degrees from horizontal, rotated 45 degrees
        camObj.transform.position = mapCenter + new Vector3(0f, 80f, -80f);
        camObj.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
    }

    private static void SetupLighting()
    {
        var lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.82f, 0.6f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.4f, 0.35f, 0.28f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.25f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.12f, 0.1f);
    }

    private static void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.35f, 0.30f, 0.25f);
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 250f;
        RenderSettings.skybox = null;
    }
}
