using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Dresses the HomeBaseTerrain scene with PBR textures, props, terrain features, and skybox.
/// Run via Slopworks > Dress HomeBase Scenery. Idempotent — clears previous scenery on re-run.
/// Auto-dresses when the scene is opened and HomeBaseScenery is missing.
/// </summary>
[InitializeOnLoad]
public static class HomeBaseSceneryDresser
{
    static HomeBaseSceneryDresser()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (!scene.name.Contains("HomeBaseTerrain")) return;
        if (GameObject.Find("HomeBaseScenery") != null) return;
        if (Terrain.activeTerrain == null) return;

        Debug.Log("HomeBaseScenery missing — auto-dressing terrain");
        Dress();
    }

    private const int Seed = 42;
    private const float TerrainWidth = 1200f;
    private const float TerrainHeight = 220f;
    private const float FlatRadius = 50f;

    private static readonly string[] TextureSets = {
        "Assets/_Slopworks/Art/Textures/Terrain/Concrete034",
        "Assets/_Slopworks/Art/Textures/Terrain/Ground037",
        "Assets/_Slopworks/Art/Textures/Terrain/Ground054",
        "Assets/_Slopworks/Art/Textures/Terrain/Gravel022",
        "Assets/_Slopworks/Art/Textures/Terrain/Rust004",
    };

    private static readonly Vector2[] TileSizes = {
        new(20f, 20f),
        new(30f, 30f),
        new(35f, 35f),
        new(25f, 25f),
        new(15f, 15f),
    };

    private static readonly float[] NormalScales = { 1.5f, 2.0f, 1.5f, 2.0f, 1.8f };
    private static readonly float[] SmoothnessMin = { 0.2f, 0.08f, 0.08f, 0.08f, 0.15f };
    private static readonly float[] SmoothnessMax = { 0.55f, 0.35f, 0.35f, 0.4f, 0.65f };

    private static readonly List<Vector2> RiverbedPoints = new();

    // waystation world-space positions (x, z)
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
    private static readonly Vector2 HamletCenter = new(-40f, 350f);

    // merchant structure positions (world-space x, z)
    private static readonly Vector2 GasStationPos = new(120f, -30f);
    private static readonly Vector2 WoodshopPos = new(280f, 150f);
    private static readonly Vector2 GaragePos = new(-100f, 280f);

    // settlement building pad positions and sizes (world-space x, z)
    // these match the 8 buildings placed by HomeWorldPlaytestSetup
    private static readonly Vector2[] SettlementPadPositions = {
        new(0f, 0f),           // factory yard: center hub
        new(80f, 60f),         // farmstead: NE of hub
        new(-70f, 50f),        // workshop: NW of hub
        new(-120f, -80f),      // river depot: SW, near river
        new(0f, -150f),        // watchtower: south perimeter
        new(100f, -70f),       // market: SE of hub
        new(-80f, -40f),       // barracks: WSW of hub
        new(60f, 130f),        // greenhouse: north of hub
    };
    private static readonly Vector2[] SettlementPadSizes = {
        new(50f, 20f),   // factory yard (large footprint)
        new(35f, 27f),   // farmstead
        new(20f, 27f),   // workshop
        new(15f, 15f),   // river depot (placeholder)
        new(40f, 32f),   // watchtower
        new(30f, 30f),   // market
        new(22f, 22f),   // barracks
        new(15f, 15f),   // greenhouse (placeholder)
    };

    // road building positions (world-space x, z)
    private static readonly Vector2 FactoryYardPos = new(100f, 15f);

    private struct PropDef
    {
        public string Path;
        public float MinScale;
        public float MaxScale;
        public bool AddCollider;

        public PropDef(string path, float minScale, float maxScale, bool collider = true)
        {
            Path = path; MinScale = minScale; MaxScale = maxScale; AddCollider = collider;
        }
    }

    private static readonly PropDef[] RockProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-a.fbx", 2f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-b.fbx", 2f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-c.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-flat.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-flat-grass.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-sand-a.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-sand-b.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-sand-c.fbx", 1.5f, 3.5f),
    };

    private static readonly int[] SandyRockIndices = { 5, 6, 7 };
    private static readonly int[] MossyRockIndices = { 3, 4 };

    private static readonly PropDef[] TreeProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 4f, 7f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 5f, 9f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-trunk.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn.fbx", 4f, 7f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn-tall.fbx", 5f, 9f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn-trunk.fbx", 3f, 5f),
    };

    private static readonly PropDef[] UndergrowthProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass-large.fbx", 3f, 6f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass.fbx", 3f, 5f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass-large.fbx", 4f, 7f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass.fbx", 3f, 5f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-log.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-log-small.fbx", 2f, 3f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/resource-wood.fbx", 2f, 3f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/resource-stone.fbx", 1.5f, 3f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/resource-stone-large.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/resource-planks.fbx", 2f, 3f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/bucket.fbx", 2f, 3f, false),
    };

    private static readonly PropDef[] IndustrialProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/barrel.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/barrel-open.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box.fbx", 2.5f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-large.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-open.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-large-open.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence-fortified.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence-doorway.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel-screws.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel-narrow.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel-screws-half.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/signpost.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/signpost-single.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/workbench.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/workbench-anvil.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/chest.fbx", 2.5f, 3.5f),
    };

    private static readonly PropDef[] RuinProps = {
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-wall.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-window.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-doorway.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-tall.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover-stripe.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/structure-metal-wall.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/structure-metal-doorway.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/structure-metal.fbx", 3f, 3.5f),
    };

    private static readonly PropDef[] CampProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/campfire-pit.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tent-canvas-half.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tent-canvas.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tent.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/bedroll.fbx", 3f, 3.5f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/bedroll-packed.fbx", 2.5f, 3f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/bottle.fbx", 2f, 3f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/bottle-large.fbx", 2f, 3f, false),
    };

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

    [MenuItem("Slopworks/Dress HomeBase Scenery")]
    public static void Dress()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("no active terrain found — load HomeBaseTerrain scene first");
            return;
        }

        var existing = GameObject.Find("HomeBaseScenery");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var root = new GameObject("HomeBaseScenery");
        root.isStatic = true;

        var rng = new System.Random(Seed);
        var td = terrain.terrainData;
        var terrainPos = terrain.transform.position;

        RiverbedPoints.Clear();

        // resize terrain to match constants
        td.heightmapResolution = 2049;
        td.alphamapResolution = 1024;
        td.SetDetailResolution(1024, 16);
        td.size = new Vector3(TerrainWidth, TerrainHeight, TerrainWidth);
        // recenter terrain so (0,0,0) is the middle
        terrain.transform.position = new Vector3(-TerrainWidth / 2f, 0f, -TerrainWidth / 2f);
        terrainPos = terrain.transform.position;

        UpgradeTerrainTextures(td);
        AddTerrainNoise(td);           // includes regional NW tilt
        AddTerrainFeatures(td);
        CarveEscarpment(td);
        CarveOutcrops(td);
        CarveWetland(td);
        SmoothHeightmap(td, 6);
        CarveRiverValley(td);          // after smoothing so banks stay defined
        CarveWaystationPads(td);       // after river — flat pads
        CarveSettlementPads(td);       // flat pads for settlement buildings
        RepaintSplatmap(td);
        SetupSkybox();

        ScatterNature(root.transform, terrain, terrainPos, rng, td);
        PlaceSettlements(root.transform, terrain, terrainPos, rng, td);
        PlaceMerchantStructures(root.transform, terrain, terrainPos, rng, td);
        PlaceWaystations(root.transform, terrain, terrainPos, rng, td);
        PlaceRoadBuildings(root.transform, terrain, terrainPos, rng, td);
        ScatterIndustrial(root.transform, terrain, terrainPos, rng, td);
        DecorateRiverbed(root.transform, terrain, terrainPos, rng, td);
        CreateRiverWater(root.transform, terrain, terrainPos, td);
        PaintTerrainGrass(td, rng);
        SetupAmbientParticles();

        // spawn terrain explorer if not already present
        if (Object.FindAnyObjectByType<TerrainExplorer>() == null)
            SpawnTerrainExplorer.Spawn();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("homebase scenery dressed — hit Play to walk around");
    }

    // === TEXTURE SYSTEM ===

    private static void UpgradeTerrainTextures(TerrainData td)
    {
        var layers = new TerrainLayer[TextureSets.Length];

        for (int i = 0; i < TextureSets.Length; i++)
        {
            var layer = new TerrainLayer();
            string folder = TextureSets[i];
            string setName = System.IO.Path.GetFileName(folder);

            var color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{setName}_1K-PNG_Color.png");
            if (color != null) layer.diffuseTexture = color;

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{setName}_1K-PNG_NormalGL.png");
            if (normal != null)
            {
                string normalPath = AssetDatabase.GetAssetPath(normal);
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                layer.normalMapTexture = normal;
                layer.normalScale = NormalScales[i];
            }

            var maskMap = CreateMaskMap(folder, setName, i);
            if (maskMap != null)
            {
                layer.maskMapTexture = maskMap;
                layer.smoothness = 1f;
                layer.metallic = 0f;
            }
            else
            {
                layer.metallic = i == 4 ? 0.3f : 0f;
                layer.smoothness = i == 0 ? 0.4f : i == 4 ? 0.5f : i == 3 ? 0.25f : 0.2f;
            }

            layer.tileSize = TileSizes[i];
            layer.tileOffset = Vector2.zero;

            string layerPath = $"Assets/_Slopworks/Art/Terrain/HomeBase/TerrainLayer_PBR_{setName}.asset";
            AssetDatabase.CreateAsset(layer, layerPath);
            layers[i] = layer;
        }

        td.terrainLayers = layers;
        Debug.Log($"terrain textures upgraded: {layers.Length} PBR layers with mask maps");
    }

    private static Texture2D CreateMaskMap(string folder, string setName, int layerIndex)
    {
        string maskPath = $"{folder}/{setName}_1K-PNG_MaskMap.png";

        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
        if (existing != null) return existing;

        var roughness = LoadReadableTexture($"{folder}/{setName}_1K-PNG_Roughness.png");
        var ao = LoadReadableTexture($"{folder}/{setName}_1K-PNG_AmbientOcclusion.png");
        var displacement = LoadReadableTexture($"{folder}/{setName}_1K-PNG_Displacement.png");
        var metalness = LoadReadableTexture($"{folder}/{setName}_1K-PNG_Metalness.png");

        var reference = roughness ?? ao ?? displacement;
        if (reference == null) return null;

        int w = reference.width;
        int h = reference.height;
        var mask = new Texture2D(w, h, TextureFormat.RGBA32, true);

        float sMin = SmoothnessMin[layerIndex];
        float sMax = SmoothnessMax[layerIndex];

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float r = metalness != null ? metalness.GetPixel(x, y).r : 0f;
                float g = ao != null ? ao.GetPixel(x, y).r : 1f;
                float b = displacement != null ? displacement.GetPixel(x, y).r : 0.5f;
                float rawSmoothness = roughness != null ? 1f - roughness.GetPixel(x, y).r : 0.5f;
                float a = Mathf.Lerp(sMin, sMax, rawSmoothness);
                pixels[y * w + x] = new Color(r, g, b, a);
            }
        }

        mask.SetPixels(pixels);
        mask.Apply();

        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        string absolutePath = System.IO.Path.Combine(projectRoot, maskPath);
        System.IO.File.WriteAllBytes(absolutePath, mask.EncodeToPNG());
        Object.DestroyImmediate(mask);

        AssetDatabase.ImportAsset(maskPath);
        var importer = AssetImporter.GetAtPath(maskPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.textureType = TextureImporterType.Default;
            importer.SaveAndReimport();
        }

        Debug.Log($"mask map created: {setName}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
    }

    private static Texture2D LoadReadableTexture(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return tex;
    }

    // === TERRAIN NOISE (multi-octave for natural variation) ===

    private static void AddTerrainNoise(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

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
                float tiltNW = (1f - nx) * (1f - nz);
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
                    zoneMod = 0.5f;
                else if (prelimHeight > 0.70f)
                    zoneMod = 1.5f;

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

    // === TERRAIN FEATURES ===

    private static void AddTerrainFeatures(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);
        var rng = new System.Random(Seed + 1);

        // impact craters
        Vector2[] craterCenters = {
            new(0.25f, 0.25f),
            new(0.72f, 0.30f),
            new(0.30f, 0.75f),
            new(0.78f, 0.70f),
        };
        float[] craterRadii = { 0.02f, 0.015f, 0.025f, 0.012f };
        float[] craterDepths = { 8f, 5f, 10f, 6f };

        for (int c = 0; c < craterCenters.Length; c++)
        {
            float dx = craterCenters[c].x - 0.5f;
            float dz = craterCenters[c].y - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < 0.08f) continue; // keep craters away from factory center

            float craterR = craterRadii[c];
            float depth = craterDepths[c] / TerrainHeight;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float cdx = nx - craterCenters[c].x;
                    float cdz = nz - craterCenters[c].y;
                    float cdist = Mathf.Sqrt(cdx * cdx + cdz * cdz);

                    if (cdist < craterR)
                    {
                        float t = cdist / craterR;
                        float bowl = depth * (1f - t * t);
                        float rim = depth * 0.3f * Mathf.Exp(-((t - 1f) * (t - 1f)) / 0.02f);
                        heights[z, x] = heights[z, x] - bowl + rim;
                    }
                    else if (cdist < craterR * 1.5f)
                    {
                        float t = (cdist - craterR) / (craterR * 0.5f);
                        float rim = depth * 0.2f * (1f - t);
                        heights[z, x] += Mathf.Max(0f, rim);
                    }
                }
            }
        }

        // erosion gullies — small channels running downhill toward the river valley
        for (int g = 0; g < 5; g++)
        {
            float startX = 0.1f + (float)rng.NextDouble() * 0.8f;
            float startZ = 0.55f + (float)rng.NextDouble() * 0.08f; // start above the river valley
            float endZ = 0.72f; // drain toward river
            float gullyWidth = 0.006f + (float)rng.NextDouble() * 0.004f;
            float gullyDepth = (1f + (float)rng.NextDouble() * 1.5f) / TerrainHeight;
            float wobble = (float)rng.NextDouble() * 0.03f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    if (nz < startZ || nz > endZ) continue;

                    float progress = (nz - startZ) / (endZ - startZ);
                    float gullyCenter = startX + Mathf.Sin(progress * Mathf.PI * 3f) * wobble;
                    float d = Mathf.Abs(nx - gullyCenter);
                    // gully widens as it approaches the river
                    float localWidth = gullyWidth * (0.5f + progress * 0.5f);

                    if (d < localWidth)
                    {
                        float t = d / localWidth;
                        // V-shaped profile that shallows out toward the river
                        float depthHere = gullyDepth * (1f - t) * (1f - progress * 0.6f);
                        heights[z, x] -= Mathf.Max(0f, depthHere);
                    }
                }
            }
        }

        // small ridges
        for (int r = 0; r < 3; r++)
        {
            float rx = 0.15f + (float)rng.NextDouble() * 0.7f;
            float rz = 0.15f + (float)rng.NextDouble() * 0.7f;
            float angle = (float)rng.NextDouble() * Mathf.PI;
            float ridgeLen = 0.08f + (float)rng.NextDouble() * 0.06f;
            float ridgeHeight = (4f + (float)rng.NextDouble() * 6f) / TerrainHeight;
            float ridgeWidth = 0.012f;

            float rrdx = rx - 0.5f;
            float rrdz = rz - 0.5f;
            if (Mathf.Sqrt(rrdx * rrdx + rrdz * rrdz) < 0.08f) continue;

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float ddx = nx - rx;
                    float ddz = nz - rz;

                    float along = ddx * dir.x + ddz * dir.y;
                    float across = Mathf.Abs(ddx * -dir.y + ddz * dir.x);

                    if (Mathf.Abs(along) < ridgeLen && across < ridgeWidth)
                    {
                        float tAlong = Mathf.Abs(along) / ridgeLen;
                        float tAcross = across / ridgeWidth;
                        float h = ridgeHeight * (1f - tAlong) * (1f - tAcross * tAcross);
                        heights[z, x] += Mathf.Max(0f, h);
                    }
                }
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("terrain features added: craters, erosion gullies, ridges");
    }

    private static void CarveEscarpment(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        float escarpmentHeight = 30f / TerrainHeight;
        float steepWidth = 20f / TerrainWidth;
        float gentleWidth = 80f / TerrainWidth;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float lineStartX = 0.15f;
                float lineStartZ = 0.15f;
                float lineDirX = 0.30f;
                float lineDirZ = 0.25f;
                float lineLen = Mathf.Sqrt(lineDirX * lineDirX + lineDirZ * lineDirZ);
                float ldx = lineDirX / lineLen;
                float ldz = lineDirZ / lineLen;

                float px = nx - lineStartX;
                float pz = nz - lineStartZ;
                float along = px * ldx + pz * ldz;
                float across = px * (-ldz) + pz * ldx;

                float alongNorm = along / lineLen;
                if (alongNorm < -0.1f || alongNorm > 1.1f) continue;

                float endTaper = 1f;
                if (alongNorm < 0f) endTaper = 1f + alongNorm * 10f;
                else if (alongNorm > 1f) endTaper = 1f - (alongNorm - 1f) * 10f;
                endTaper = Mathf.Clamp01(endTaper);

                float warp = (Mathf.PerlinNoise(nx * 12f + 200f, nz * 12f + 200f) - 0.5f) * 0.03f;
                across += warp;

                float heightMod = 0f;
                if (across > 0f && across < steepWidth)
                {
                    float t = across / steepWidth;
                    heightMod = -escarpmentHeight * (1f - (1f - t) * (1f - t) * (1f - t));
                }
                else if (across >= steepWidth)
                {
                    heightMod = -escarpmentHeight;
                }
                else if (across < 0f && across > -gentleWidth)
                {
                    float t = -across / gentleWidth;
                    heightMod = escarpmentHeight * (1f - t) * 0.3f;
                }

                heights[z, x] += heightMod * endTaper;
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("escarpment carved: ~400m cliff face at forest/upland boundary");
    }

    private static void CarveOutcrops(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        var rng = new System.Random(Seed + 100);
        int outcropCount = 5 + rng.Next(2);

        for (int o = 0; o < outcropCount; o++)
        {
            float cx = 0.08f + (float)rng.NextDouble() * 0.27f;
            float cz = 0.08f + (float)rng.NextDouble() * 0.27f;
            float radiusNorm = (7f + (float)rng.NextDouble() * 8f) / TerrainWidth;
            float peakHeight = (8f + (float)rng.NextDouble() * 12f) / TerrainHeight;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float ddx = nx - cx;
                    float ddz = nz - cz;
                    float dist = Mathf.Sqrt(ddx * ddx + ddz * ddz);

                    if (dist > radiusNorm * 1.5f) continue;

                    if (dist < radiusNorm)
                    {
                        float t = dist / radiusNorm;
                        float profile;
                        if (t < 0.6f)
                            profile = 1f;
                        else
                        {
                            float edgeT = (t - 0.6f) / 0.4f;
                            profile = 1f - edgeT * edgeT * edgeT;
                        }
                        float roughness = Mathf.PerlinNoise(nx * 40f + o * 50f, nz * 40f + o * 50f) * 0.15f;
                        heights[z, x] += peakHeight * profile + roughness * peakHeight * 0.3f;
                    }
                    else
                    {
                        float apronT = (dist - radiusNorm) / (radiusNorm * 0.5f);
                        heights[z, x] += peakHeight * 0.1f * (1f - apronT);
                    }
                }
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log($"rocky outcrops carved: {outcropCount} formations in upland zone");
    }

    private static void CarveWetland(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        float centerX = 0.55f;
        float centerZ = 0.78f;
        float radiusNorm = 35f / TerrainWidth;
        float depth = 2.5f / TerrainHeight;
        float elongation = 1.8f;

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float ddx = (nx - centerX) / elongation;
                float ddz = nz - centerZ;
                float dist = Mathf.Sqrt(ddx * ddx + ddz * ddz);

                if (dist > radiusNorm) continue;

                float t = dist / radiusNorm;
                float profile;
                if (t < 0.7f)
                    profile = 1f;
                else
                {
                    float edgeT = (t - 0.7f) / 0.3f;
                    profile = 1f - edgeT * edgeT;
                }

                float microVar = Mathf.PerlinNoise(nx * 50f + 333f, nz * 50f + 333f) * 0.3f;
                heights[z, x] -= depth * profile * (1f + microVar * 0.2f);
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("wetland depression carved: oxbow depression in floodplain");
    }

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

            int cx = Mathf.Clamp(Mathf.RoundToInt(nx * (res - 1)), 0, res - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt(nz * (res - 1)), 0, res - 1);
            float targetHeight = heights[cz, cx];

            if (w == 2)
                targetHeight -= 3f / TerrainHeight;
            if (w == 1)
                targetHeight += 1f / TerrainHeight;

            float halfX = (padSize.x / 2f + 5f) / TerrainWidth;
            float halfZ = (padSize.y / 2f + 5f) / TerrainWidth;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float pnx = (float)x / (res - 1);
                    float pnz = (float)z / (res - 1);
                    float ddx = Mathf.Abs(pnx - nx);
                    float ddz = Mathf.Abs(pnz - nz);

                    if (ddx > halfX || ddz > halfZ) continue;

                    float padHalfX = padSize.x / 2f / TerrainWidth;
                    float padHalfZ = padSize.y / 2f / TerrainWidth;

                    if (ddx < padHalfX && ddz < padHalfZ)
                    {
                        heights[z, x] = targetHeight;
                    }
                    else
                    {
                        float blendX = ddx > padHalfX ? (ddx - padHalfX) / (halfX - padHalfX) : 0f;
                        float blendZ = ddz > padHalfZ ? (ddz - padHalfZ) / (halfZ - padHalfZ) : 0f;
                        float blend = Mathf.Max(blendX, blendZ);
                        heights[z, x] = Mathf.Lerp(targetHeight, heights[z, x], blend);
                    }
                }
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log($"waystation pads flattened: {WaystationPositions.Length} locations");
    }

    private static void CarveSettlementPads(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        for (int s = 0; s < SettlementPadPositions.Length; s++)
        {
            var pos = SettlementPadPositions[s];
            var padSize = SettlementPadSizes[s];
            float nx = (pos.x + TerrainWidth / 2f) / TerrainWidth;
            float nz = (pos.y + TerrainWidth / 2f) / TerrainWidth;

            int cx = Mathf.Clamp(Mathf.RoundToInt(nx * (res - 1)), 0, res - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt(nz * (res - 1)), 0, res - 1);
            float targetHeight = heights[cz, cx];

            // blend margin around the pad (5m feather)
            float halfX = (padSize.x / 2f + 5f) / TerrainWidth;
            float halfZ = (padSize.y / 2f + 5f) / TerrainWidth;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float pnx = (float)x / (res - 1);
                    float pnz = (float)z / (res - 1);
                    float ddx = Mathf.Abs(pnx - nx);
                    float ddz = Mathf.Abs(pnz - nz);

                    if (ddx > halfX || ddz > halfZ) continue;

                    float padHalfX = padSize.x / 2f / TerrainWidth;
                    float padHalfZ = padSize.y / 2f / TerrainWidth;

                    if (ddx < padHalfX && ddz < padHalfZ)
                    {
                        heights[z, x] = targetHeight;
                    }
                    else
                    {
                        float blendX = ddx > padHalfX ? (ddx - padHalfX) / (halfX - padHalfX) : 0f;
                        float blendZ = ddz > padHalfZ ? (ddz - padHalfZ) / (halfZ - padHalfZ) : 0f;
                        float blend = Mathf.Max(blendX, blendZ);
                        heights[z, x] = Mathf.Lerp(targetHeight, heights[z, x], blend);
                    }
                }
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log($"settlement pads flattened: {SettlementPadPositions.Length} locations");
    }

    private static void SmoothHeightmap(TerrainData td, int passes)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        for (int pass = 0; pass < passes; pass++)
        {
            float[,] smoothed = new float[res, res];

            for (int z = 1; z < res - 1; z++)
            {
                for (int x = 1; x < res - 1; x++)
                {
                    // 3x3 Gaussian-weighted average: center 4, cross 2, corners 1
                    float sum =
                        heights[z, x] * 4f +
                        heights[z - 1, x] * 2f + heights[z + 1, x] * 2f +
                        heights[z, x - 1] * 2f + heights[z, x + 1] * 2f +
                        heights[z - 1, x - 1] + heights[z + 1, x - 1] +
                        heights[z - 1, x + 1] + heights[z + 1, x + 1];
                    smoothed[z, x] = sum / 16f;
                }
            }

            // preserve terrain edges
            for (int i = 0; i < res; i++)
            {
                smoothed[0, i] = heights[0, i];
                smoothed[res - 1, i] = heights[res - 1, i];
                smoothed[i, 0] = heights[i, 0];
                smoothed[i, res - 1] = heights[i, res - 1];
            }

            heights = smoothed;
        }

        td.SetHeights(0, 0, heights);
        Debug.Log($"heightmap smoothed: {passes} passes");
    }

    // === RIVER VALLEY (geomorphologically accurate) ===

    private static float RiverCenterZ(float nx)
    {
        // single full meander with sub-harmonics for irregularity
        // wavelength ~200m (one S-curve across map), amplitude ~18m each side
        return 0.72f
            + Mathf.Sin(nx * Mathf.PI * 2f) * 0.09f
            + Mathf.Sin(nx * Mathf.PI * 5.3f) * 0.025f
            + Mathf.Sin(nx * Mathf.PI * 11f) * 0.008f;
    }

    private static float RiverCurvature(float nx)
    {
        // approximate second derivative of the meander — positive = curving south, negative = curving north
        float h = 0.005f;
        float z0 = RiverCenterZ(nx - h);
        float z1 = RiverCenterZ(nx);
        float z2 = RiverCenterZ(nx + h);
        return (z2 - 2f * z1 + z0) / (h * h);
    }

    private static void CarveRiverValley(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);

        // river dimensions scaled for 800m map with 180m height range
        // channel width: ~16m, floodplain: ~50m each side, terraces stepped
        float baseChannelHalf = 8f / TerrainWidth;    // 8m half = 16m channel
        float channelDepth = 7f / TerrainHeight;      // 7m deep channel
        float floodplainHalf = 50f / TerrainWidth;    // 50m floodplain each side
        float floodplainDrop = 3f / TerrainHeight;    // 3m below surrounding terrain
        float terraceHalf = 18f / TerrainWidth;       // 18m terrace zone
        float terraceDrop = 1.5f / TerrainHeight;     // 1.5m step

        // store riverbed points for prop placement (world space)
        for (int step = 0; step < 400; step++)
        {
            float nx = (float)step / 399f;
            float nz = RiverCenterZ(nx);
            RiverbedPoints.Add(new Vector2(
                nx * TerrainWidth - TerrainWidth / 2f,
                nz * TerrainWidth - TerrainWidth / 2f));
        }

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float riverZ = RiverCenterZ(nx);
                float signedDist = nz - riverZ; // positive = south of river, negative = north
                float d = Mathf.Abs(signedDist);

                float curvature = RiverCurvature(nx);
                // positive curvature = river curves south
                // if point is on the outside of the bend, steeper bank
                bool isOuterBank = (curvature > 0f && signedDist > 0f) || (curvature < 0f && signedDist < 0f);
                float curveMag = Mathf.Abs(curvature);

                // channel widens at bends (real rivers do this)
                float channelHalf = baseChannelHalf * (1f + Mathf.Clamp01(curveMag * 0.15f) * 0.5f);

                if (d < channelHalf)
                {
                    // inside the channel — flat bottom with slight concave profile
                    float t = d / channelHalf;

                    if (!isOuterBank && curveMag > 1f)
                    {
                        // inner bend: point bar deposit — raised area of sand/gravel
                        float barHeight = channelDepth * 0.3f * Mathf.Clamp01(curveMag * 0.08f);
                        float barProfile = t * t; // rises toward bank
                        heights[z, x] -= channelDepth * (1f - t * t * 0.3f) - barHeight * barProfile;
                    }
                    else
                    {
                        // flat-bottomed channel with slight deepening at center
                        heights[z, x] -= channelDepth * (1f - t * t * 0.15f);
                    }
                }
                else if (d < channelHalf + terraceHalf)
                {
                    // bank and lower terrace
                    float bt = (d - channelHalf) / terraceHalf;

                    if (isOuterBank)
                    {
                        // outer bank: steep cut bank (eroded by faster flow)
                        float steepProfile = 1f - bt * bt * bt; // stays steep longer
                        heights[z, x] -= channelDepth * steepProfile * 0.7f + terraceDrop * (1f - bt);
                    }
                    else
                    {
                        // inner bank: gentle slope (depositional side)
                        float gentleProfile = 1f - bt; // linear ramp
                        heights[z, x] -= channelDepth * gentleProfile * 0.4f + terraceDrop * (1f - bt);
                    }
                }
                else if (d < channelHalf + terraceHalf + floodplainHalf)
                {
                    // floodplain — nearly flat, slight slope toward channel
                    float ft = (d - channelHalf - terraceHalf) / floodplainHalf;
                    float fpDepth = floodplainDrop * (1f - ft * ft);
                    heights[z, x] -= Mathf.Max(0f, fpDepth);
                }
            }
        }

        // enforce monotonically decreasing riverbed from west to east
        // first pass: collect centerline heights, then build a monotonic ceiling
        float[] centerHeights = new float[res];
        int[] centerZIndices = new int[res];
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1);
            float riverNz = RiverCenterZ(nx);
            int cz = Mathf.Clamp(Mathf.RoundToInt(riverNz * (res - 1)), 0, res - 1);
            centerZIndices[x] = cz;
            centerHeights[x] = heights[cz, x];
        }

        // build monotonic profile: east end is lowest, walk westward and clamp
        // add a gentle gradient (8m total drop west to east)
        float totalGradient = 8f / TerrainHeight;
        float[] targetBed = new float[res];
        targetBed[res - 1] = centerHeights[res - 1];
        for (int x = res - 2; x >= 0; x--)
        {
            float minStep = totalGradient / (res - 1); // minimum drop per pixel
            targetBed[x] = Mathf.Min(centerHeights[x], targetBed[x + 1] + minStep);
        }

        // second pass: where terrain is above the target, carve it down
        // apply within a corridor wider than the channel to create a gorge through escarpments
        float gorgeHalf = baseChannelHalf * 4f; // gorge cuts 4x wider than channel
        for (int x = 0; x < res; x++)
        {
            float excess = centerHeights[x] - targetBed[x];
            if (excess <= 0f) continue; // already at or below target

            int cz = centerZIndices[x];
            int halfPixels = Mathf.CeilToInt(gorgeHalf * (res - 1));
            for (int dz = -halfPixels; dz <= halfPixels; dz++)
            {
                int zz = cz + dz;
                if (zz < 0 || zz >= res) continue;
                float d = Mathf.Abs((float)dz / (res - 1));

                // full cut within channel, fading over gorge walls
                float fade;
                if (d < baseChannelHalf)
                    fade = 1f;
                else if (d < gorgeHalf)
                    fade = 1f - (d - baseChannelHalf) / (gorgeHalf - baseChannelHalf);
                else
                    continue;

                // smooth fade: use squared falloff for natural gorge walls
                fade = fade * fade;
                heights[zz, x] -= excess * fade;
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log($"river valley carved: channel + terraces + floodplain ({RiverbedPoints.Count} path points)");
    }

    // === SPLATMAP ===

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
                    var zone = GetBiomeZone(td, nx, nz);

                    switch (zone)
                    {
                        case BiomeZone.Floodplain:
                            grass = 0.65f; dirt = 0.25f; gravel = 0.1f;
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
                            if (steepness > 15f)
                            {
                                float rockBlend = Mathf.Clamp01((steepness - 15f) / 20f);
                                gravel += rockBlend * 0.3f; grass *= (1f - rockBlend); dirt *= (1f - rockBlend * 0.5f);
                            }
                            break;
                    }

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

                // riparian zone override
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

    // === SKYBOX ===

    private static void SetupSkybox()
    {
        string hdrPath = "Assets/_Slopworks/Art/Skybox/industrial_sunset_puresky_2k.hdr";

        var importer = AssetImporter.GetAtPath(hdrPath) as TextureImporter;
        if (importer != null && importer.textureShape != TextureImporterShape.Texture2D)
        {
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.SaveAndReimport();
        }

        var hdr = AssetDatabase.LoadAssetAtPath<Texture>(hdrPath);
        if (hdr == null)
        {
            Debug.LogWarning($"skybox HDR not found at {hdrPath}, skipping");
            return;
        }

        string matPath = "Assets/_Slopworks/Materials/Environment/Skybox_IndustrialSunset.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Slopworks/Materials/Environment"))
                AssetDatabase.CreateFolder("Assets/_Slopworks/Materials", "Environment");

            mat = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = Shader.Find("Skybox/Panoramic");
        mat.SetTexture("_MainTex", hdr);
        mat.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(mat);

        RenderSettings.skybox = mat;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.45f, 0.32f, 0.22f);
        RenderSettings.fogStartDistance = 300f;
        RenderSettings.fogEndDistance = 1200f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        Debug.Log("skybox set: industrial sunset");
    }

    // === NATURE SCATTER (biome-aware, slope-aligned) ===

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
        int clusterCount = 150 + rng.Next(30);
        for (int c = 0; c < clusterCount; c++)
        {
            float cx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float cz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(cx * cx + cz * cz);
            if (dist < FlatRadius + 8f) continue;

            var zone = GetBiomeZoneFromWorldPos(td, cx, cz);
            var zoneCanopy = GetCanopyForZone(zone);
            if (zoneCanopy.Length == 0) continue;

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
                instance.transform.rotation = UprightRotation((float)rng.NextDouble() * 360f, rng, 4f);
                instance.transform.SetParent(treeParent);
                instance.isStatic = false;
                AddWindSway(instance, species.WindAmount, species.WindSpeed);

                var tag = instance.AddComponent<BiomeTag>();
                tag.Zone = zone;
                tag.SpeciesId = species.SpeciesId;
                treesPlaced++;

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
                    uinst.transform.rotation = UprightRotation((float)rng.NextDouble() * 360f, rng, 0f);
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

            if (zone == BiomeZone.RockyUpland && rng.NextDouble() > 0.2) continue;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
            var species = zoneCanopy[rng.Next(zoneCanopy.Length)];
            var instance = InstantiateProp(species.Prop, rng);
            if (instance == null) continue;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = UprightRotation((float)rng.NextDouble() * 360f, rng, 3f);
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

            if (zone == BiomeZone.Floodplain && rng.NextDouble() > 0.3) continue;

            PropDef prop;
            if (IsNearRiverbed(wx, wz, 10f))
            {
                int idx = SandyRockIndices[rng.Next(SandyRockIndices.Length)];
                prop = RockProps[idx];
            }
            else if (zone == BiomeZone.RockyUpland)
            {
                prop = RockProps[rng.Next(RockProps.Length)];
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
                tInst.transform.rotation = UprightRotation((float)rng.NextDouble() * 360f, rng, 3f);
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

    // === INDUSTRIAL + RUINS ===

    private static void ScatterIndustrial(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("IndustrialDebris").transform;
        parent.SetParent(root);

        int placed = 0;

        for (int i = 0; i < 100; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = FlatRadius + (float)rng.NextDouble() * 80f;
            float wx = Mathf.Cos(angle) * radius;
            float wz = Mathf.Sin(angle) * radius;

            float y = terrain.SampleHeight(new Vector3(wx + TerrainWidth / 2f + terrainPos.x, 0f, wz + TerrainWidth / 2f + terrainPos.z));
            y += terrainPos.y;

            var prop = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var instance = InstantiateProp(prop, rng);
            if (instance == null) continue;

            float tiltX = (float)(rng.NextDouble() - 0.5) * 15f;
            float tiltZ = (float)(rng.NextDouble() - 0.5) * 15f;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = Quaternion.Euler(tiltX, (float)rng.NextDouble() * 360f, tiltZ);
            instance.transform.SetParent(parent);
            instance.isStatic = true;

            placed++;
        }

        Debug.Log($"industrial debris placed: {placed}");
    }

    private static void PlaceSettlements(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        // disabled — focusing on one settlement at a time
        return;

        var parent = new GameObject("Settlements").transform;
        parent.SetParent(root);
        int totalPlaced = 0;

        // path constants for readability
        const string SK = "Assets/_Slopworks/Art/Kenney/survival-kit/Models/";
        const string CK = "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/";
        const string TK = "Assets/_Slopworks/Art/Kenney/tower-defense-kit/Models/";

        // --- cluster 6: farmstead A (forest, south, off a trail) ---
        {
            Vector3 clusterPos = new Vector3(-100f, 0f, -250f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);
            float facing = 30f; // face roughly NE toward implied trail

            var cluster = new GameObject("Cluster_FarmsteadA").transform;
            cluster.SetParent(parent);

            float scale = 3.5f;
            bool roofMissing = rng.NextDouble() < 0.3;

            // main cabin: structure + roof (or canvas patch if roof missing)
            var cabin = InstantiateProp(new PropDef(SK + "structure.fbx", scale, scale), rng);
            if (cabin != null)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 4f;
                cabin.transform.position = clusterPos;
                cabin.transform.rotation = Quaternion.Euler(0f, facing + jitter, 0f);
                cabin.transform.SetParent(cluster);
                cabin.isStatic = true;
                totalPlaced++;
            }

            if (roofMissing)
            {
                // canvas patch instead of roof
                var canvas = InstantiateProp(new PropDef(SK + "structure-canvas.fbx", scale, scale), rng);
                if (canvas != null)
                {
                    canvas.transform.position = clusterPos + new Vector3(0f, scale * 1.0f, 0f);
                    canvas.transform.rotation = Quaternion.Euler(0f, facing + (float)(rng.NextDouble() - 0.5) * 10f, 0f);
                    canvas.transform.SetParent(cluster);
                    canvas.isStatic = true;
                    totalPlaced++;
                }
            }
            else
            {
                var roof = InstantiateProp(new PropDef(SK + "structure-roof.fbx", scale, scale), rng);
                if (roof != null)
                {
                    roof.transform.position = clusterPos + new Vector3(0f, scale * 1.0f, 0f);
                    roof.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                    roof.transform.SetParent(cluster);
                    roof.isStatic = true;
                    totalPlaced++;
                }
            }

            // floor underneath cabin
            var floorA = InstantiateProp(new PropDef(SK + "floor-old.fbx", scale, scale), rng);
            if (floorA != null)
            {
                floorA.transform.position = clusterPos;
                floorA.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                floorA.transform.SetParent(cluster);
                floorA.isStatic = true;
                totalPlaced++;
            }

            // outbuilding: metal shed offset 12m
            float outScale = 2.5f;
            float outAngle = facing + 40f;
            Vector3 outPos = clusterPos + new Vector3(12f, 0f, 3f);
            outPos.y = SampleWorldHeight(terrain, terrainPos, outPos.x, outPos.z);

            var metalShed = InstantiateProp(new PropDef(SK + "structure-metal.fbx", outScale, outScale), rng);
            if (metalShed != null)
            {
                metalShed.transform.position = outPos;
                metalShed.transform.rotation = Quaternion.Euler(0f, outAngle, 0f);
                metalShed.transform.SetParent(cluster);
                metalShed.isStatic = true;
                totalPlaced++;
            }
            var metalRoof = InstantiateProp(new PropDef(SK + "structure-metal-roof.fbx", outScale, outScale), rng);
            if (metalRoof != null)
            {
                metalRoof.transform.position = outPos + new Vector3(0f, outScale * 1.0f, 0f);
                metalRoof.transform.rotation = Quaternion.Euler(0f, outAngle, 0f);
                metalRoof.transform.SetParent(cluster);
                metalRoof.isStatic = true;
                totalPlaced++;
            }

            // fence: 3 fence segments + 1 doorway
            string[] fencePaths = { SK + "fence.fbx", SK + "fence.fbx", SK + "fence.fbx", SK + "fence-doorway.fbx" };
            for (int f = 0; f < 4; f++)
            {
                float fAngle = ((float)f / 4f) * Mathf.PI * 2f;
                float fDist = 10f;
                float fx = clusterPos.x + Mathf.Cos(fAngle) * fDist;
                float fz = clusterPos.z + Mathf.Sin(fAngle) * fDist;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

                var fence = InstantiateProp(new PropDef(fencePaths[f], 3f, 3f), rng);
                if (fence == null) continue;
                fence.transform.position = new Vector3(fx, fy, fz);
                fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                fence.transform.SetParent(cluster);
                fence.isStatic = true;
                totalPlaced++;
            }

            // debris: barrel, box, campfire-pit
            totalPlaced += ScatterDebris(cluster, terrain, terrainPos, rng,
                clusterPos.x, clusterPos.z, 8f, 15f, 3 + rng.Next(5),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "campfire-pit.fbx", 3f, 3.5f),
                    new PropDef(SK + "box-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel.fbx", 3f, 3.5f),
                });
        }

        // --- cluster 7: farmstead B (floodplain, near river — more ruined) ---
        {
            Vector3 clusterPos = new Vector3(80f, 0f, -350f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);
            float facing = -10f;

            var cluster = new GameObject("Cluster_FarmsteadB").transform;
            cluster.SetParent(parent);

            float scale = 3.5f;

            // main cabin: no roof (collapsed), floor-hole + floor-old
            var cabinB = InstantiateProp(new PropDef(SK + "structure.fbx", scale, scale), rng);
            if (cabinB != null)
            {
                float jitter = (float)(rng.NextDouble() - 0.5) * 4f;
                cabinB.transform.position = clusterPos;
                cabinB.transform.rotation = Quaternion.Euler(0f, facing + jitter, 0f);
                cabinB.transform.SetParent(cluster);
                cabinB.isStatic = true;
                totalPlaced++;
            }

            // canvas tarp where roof was
            var tarp = InstantiateProp(new PropDef(SK + "structure-canvas.fbx", scale, scale), rng);
            if (tarp != null)
            {
                tarp.transform.position = clusterPos + new Vector3(0f, scale * 0.9f, 0f);
                tarp.transform.rotation = Quaternion.Euler(3f, facing + 8f, -2f);
                tarp.transform.SetParent(cluster);
                tarp.isStatic = true;
                totalPlaced++;
            }

            // damaged floors
            var floorHole = InstantiateProp(new PropDef(SK + "floor-hole.fbx", scale, scale), rng);
            if (floorHole != null)
            {
                floorHole.transform.position = clusterPos + new Vector3(-1f, 0f, 0f);
                floorHole.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                floorHole.transform.SetParent(cluster);
                floorHole.isStatic = true;
                totalPlaced++;
            }
            var floorOld = InstantiateProp(new PropDef(SK + "floor-old.fbx", scale, scale), rng);
            if (floorOld != null)
            {
                floorOld.transform.position = clusterPos + new Vector3(1f, 0f, 1f);
                floorOld.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                floorOld.transform.SetParent(cluster);
                floorOld.isStatic = true;
                totalPlaced++;
            }

            // outbuilding: just metal wall segments (shed walls, no roof)
            Vector3 outPosB = clusterPos + new Vector3(10f, 0f, -5f);
            outPosB.y = SampleWorldHeight(terrain, terrainPos, outPosB.x, outPosB.z);
            for (int w = 0; w < 3; w++)
            {
                var wall = InstantiateProp(new PropDef(SK + "structure-metal-wall.fbx", 2.5f, 3f), rng);
                if (wall == null) continue;
                float wOffset = (w - 1) * 2.5f;
                float jitter = (float)(rng.NextDouble() - 0.5) * 4f;
                wall.transform.position = outPosB + new Vector3(wOffset, 0f, 0f);
                wall.transform.rotation = Quaternion.Euler(0f, facing + 90f + jitter, 0f);
                wall.transform.SetParent(cluster);
                wall.isStatic = true;
                totalPlaced++;
            }

            // no fencing (overgrown feel), more debris than farmstead A
            totalPlaced += ScatterDebris(cluster, terrain, terrainPos, rng,
                clusterPos.x, clusterPos.z, 8f, 15f, 4 + rng.Next(4),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "barrel-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box-large-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel.fbx", 3f, 3.5f),
                    new PropDef(SK + "metal-panel-screws.fbx", 3f, 3.5f),
                    new PropDef(SK + "resource-stone.fbx", 2f, 3f),
                });
        }

        // --- cluster 8: river hamlet (linear along river, near subway entrance) ---
        {
            Vector3 hamletCenter = new Vector3(200f, 0f, -380f);
            float roadFacing = 0f; // buildings spread along X axis

            var cluster = new GameObject("Cluster_Hamlet").transform;
            cluster.SetParent(parent);

            // building 1: intact general store (structure + structure-roof)
            {
                Vector3 pos = hamletCenter + new Vector3(-37f, 0f, 0f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);
                float bFacing = roadFacing + 180f + (float)(rng.NextDouble() - 0.5) * 10f;
                float bScale = 3.5f;

                var walls = InstantiateProp(new PropDef(SK + "structure.fbx", bScale, bScale), rng);
                if (walls != null)
                {
                    walls.transform.position = pos;
                    walls.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    walls.transform.SetParent(cluster);
                    walls.isStatic = true;
                    totalPlaced++;
                }
                var bRoof = InstantiateProp(new PropDef(SK + "structure-roof.fbx", bScale, bScale), rng);
                if (bRoof != null)
                {
                    bRoof.transform.position = pos + new Vector3(0f, bScale * 1.0f, 0f);
                    bRoof.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    bRoof.transform.SetParent(cluster);
                    bRoof.isStatic = true;
                    totalPlaced++;
                }
            }

            // building 2: conveyor wall + window (mixed ruin), no roof
            {
                Vector3 pos = hamletCenter + new Vector3(-22f, 0f, 4f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);
                float bFacing = roadFacing + 170f + (float)(rng.NextDouble() - 0.5) * 10f;
                float bScale = 4f;

                var wall2 = InstantiateProp(new PropDef(CK + "structure-wall.fbx", bScale, bScale), rng);
                if (wall2 != null)
                {
                    wall2.transform.position = pos;
                    wall2.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    wall2.transform.SetParent(cluster);
                    wall2.isStatic = true;
                    totalPlaced++;
                }
                var win2 = InstantiateProp(new PropDef(CK + "structure-window.fbx", bScale, bScale), rng);
                if (win2 != null)
                {
                    win2.transform.position = pos + Quaternion.Euler(0f, bFacing, 0f) * new Vector3(bScale * 1.0f, 0f, 0f);
                    win2.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    win2.transform.SetParent(cluster);
                    win2.isStatic = true;
                    totalPlaced++;
                }
            }

            // building 3: collapsed cabin (structure + floor-hole)
            {
                Vector3 pos = hamletCenter + new Vector3(-7f, 0f, -3f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);
                float bFacing = roadFacing + 185f + (float)(rng.NextDouble() - 0.5) * 10f;
                float bScale = 3.5f;

                var cabin3 = InstantiateProp(new PropDef(SK + "structure.fbx", bScale, bScale), rng);
                if (cabin3 != null)
                {
                    float tilt = 4f + (float)rng.NextDouble() * 6f;
                    cabin3.transform.position = pos;
                    cabin3.transform.rotation = Quaternion.Euler(tilt, bFacing, tilt * 0.3f);
                    cabin3.transform.SetParent(cluster);
                    cabin3.isStatic = true;
                    totalPlaced++;
                }
                var floor3 = InstantiateProp(new PropDef(SK + "floor-hole.fbx", bScale, bScale), rng);
                if (floor3 != null)
                {
                    floor3.transform.position = pos;
                    floor3.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    floor3.transform.SetParent(cluster);
                    floor3.isStatic = true;
                    totalPlaced++;
                }
            }

            // building 4: market stall (conveyor doorway + wall, cover roof)
            {
                Vector3 pos = hamletCenter + new Vector3(8f, 0f, 2f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);
                float bFacing = roadFacing + 175f + (float)(rng.NextDouble() - 0.5) * 10f;
                float bScale = 3.5f;

                var door4 = InstantiateProp(new PropDef(CK + "structure-doorway.fbx", bScale, bScale), rng);
                if (door4 != null)
                {
                    door4.transform.position = pos;
                    door4.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    door4.transform.SetParent(cluster);
                    door4.isStatic = true;
                    totalPlaced++;
                }
                var wall4 = InstantiateProp(new PropDef(CK + "structure-wall.fbx", bScale, bScale), rng);
                if (wall4 != null)
                {
                    wall4.transform.position = pos + Quaternion.Euler(0f, bFacing, 0f) * new Vector3(bScale * 1.0f, 0f, 0f);
                    wall4.transform.rotation = Quaternion.Euler(0f, bFacing + 90f, 0f);
                    wall4.transform.SetParent(cluster);
                    wall4.isStatic = true;
                    totalPlaced++;
                }
                var cover4 = InstantiateProp(new PropDef(CK + "cover.fbx", bScale, bScale), rng);
                if (cover4 != null)
                {
                    cover4.transform.position = pos + new Vector3(0f, bScale * 1.0f, 0f);
                    cover4.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    cover4.transform.SetParent(cluster);
                    cover4.isStatic = true;
                    totalPlaced++;
                }
            }

            // building 5: workshop (structure-metal + structure-metal-doorway)
            {
                Vector3 pos = hamletCenter + new Vector3(23f, 0f, -2f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);
                float bFacing = roadFacing + 190f + (float)(rng.NextDouble() - 0.5) * 10f;
                float bScale = 3f;

                var metalFrame = InstantiateProp(new PropDef(SK + "structure-metal.fbx", bScale, bScale), rng);
                if (metalFrame != null)
                {
                    metalFrame.transform.position = pos;
                    metalFrame.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    metalFrame.transform.SetParent(cluster);
                    metalFrame.isStatic = true;
                    totalPlaced++;
                }
                var metalDoor = InstantiateProp(new PropDef(SK + "structure-metal-doorway.fbx", bScale, bScale), rng);
                if (metalDoor != null)
                {
                    metalDoor.transform.position = pos + Quaternion.Euler(0f, bFacing, 0f) * new Vector3(bScale * 1.0f, 0f, 0f);
                    metalDoor.transform.rotation = Quaternion.Euler(0f, bFacing, 0f);
                    metalDoor.transform.SetParent(cluster);
                    metalDoor.isStatic = true;
                    totalPlaced++;
                }
            }

            // building 6 (optional): tent-canvas camp near end
            if (rng.NextDouble() < 0.7)
            {
                Vector3 pos = hamletCenter + new Vector3(35f, 0f, 1f);
                pos.y = SampleWorldHeight(terrain, terrainPos, pos.x, pos.z);

                var tent6 = InstantiateProp(new PropDef(SK + "tent-canvas.fbx", 3f, 3.5f), rng);
                if (tent6 != null)
                {
                    tent6.transform.position = pos;
                    tent6.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    tent6.transform.SetParent(cluster);
                    tent6.isStatic = true;
                    totalPlaced++;
                }
                var campfire6 = InstantiateProp(new PropDef(SK + "campfire-pit.fbx", 3f, 3.5f), rng);
                if (campfire6 != null)
                {
                    campfire6.transform.position = pos + new Vector3(3f, 0f, 2f);
                    campfire6.transform.position = new Vector3(campfire6.transform.position.x,
                        SampleWorldHeight(terrain, terrainPos, campfire6.transform.position.x, campfire6.transform.position.z),
                        campfire6.transform.position.z);
                    campfire6.transform.SetParent(cluster);
                    campfire6.isStatic = true;
                    totalPlaced++;
                }
            }

            // debris scattered between buildings
            totalPlaced += ScatterDebris(cluster, terrain, terrainPos, rng,
                hamletCenter.x, hamletCenter.z, 5f, 40f, 6 + rng.Next(6),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "barrel-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel.fbx", 3f, 3.5f),
                    new PropDef(SK + "resource-stone.fbx", 2f, 3f),
                    new PropDef(SK + "chest.fbx", 2.5f, 3f),
                });
        }

        // --- cluster 9: watchtower — escarpment overlook (rocky upland) ---
        {
            Vector3 clusterPos = new Vector3(-350f, 0f, 300f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);

            var cluster = new GameObject("Cluster_WatchtowerEscarpment").transform;
            cluster.SetParent(parent);

            float towerScale = 3.5f;
            float pieceHeight = towerScale * 1.0f;

            // stacked tower: base, bottom, middle, top, roof
            string[] towerPieces = {
                TK + "tower-round-base.fbx",
                TK + "tower-round-bottom-a.fbx",
                TK + "tower-round-middle-a.fbx",
                TK + "tower-round-top-a.fbx",
                TK + "tower-round-roof-a.fbx",
            };
            for (int i = 0; i < towerPieces.Length; i++)
            {
                var piece = InstantiateProp(new PropDef(towerPieces[i], towerScale, towerScale), rng);
                if (piece == null) continue;
                piece.transform.position = clusterPos + new Vector3(0f, pieceHeight * i, 0f);
                piece.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                piece.transform.SetParent(cluster);
                piece.isStatic = true;
                totalPlaced++;
            }

            // 4 fence-fortified segments in a ring (radius ~8m)
            for (int f = 0; f < 4; f++)
            {
                float fAngle = ((float)f / 4f) * Mathf.PI * 2f + 0.4f;
                float fDist = 8f;
                float fx = clusterPos.x + Mathf.Cos(fAngle) * fDist;
                float fz = clusterPos.z + Mathf.Sin(fAngle) * fDist;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

                var fence = InstantiateProp(new PropDef(SK + "fence-fortified.fbx", 3.5f, 3.5f), rng);
                if (fence == null) continue;
                fence.transform.position = new Vector3(fx, fy, fz);
                fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                fence.transform.SetParent(cluster);
                fence.isStatic = true;
                totalPlaced++;
            }

            // scaffolding at base
            var scaffold = InstantiateProp(new PropDef(TK + "wood-structure.fbx", towerScale, towerScale), rng);
            if (scaffold != null)
            {
                scaffold.transform.position = clusterPos + new Vector3(3f, 0f, 2f);
                scaffold.transform.position = new Vector3(scaffold.transform.position.x,
                    SampleWorldHeight(terrain, terrainPos, scaffold.transform.position.x, scaffold.transform.position.z),
                    scaffold.transform.position.z);
                scaffold.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                scaffold.transform.SetParent(cluster);
                scaffold.isStatic = true;
                totalPlaced++;
            }
        }

        // --- cluster 10: watchtower — river crossing (forest/floodplain boundary) ---
        {
            Vector3 clusterPos = new Vector3(250f, 0f, -280f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);

            var cluster = new GameObject("Cluster_WatchtowerRiver").transform;
            cluster.SetParent(parent);

            float towerScale = 3f;
            float pieceHeight = towerScale * 1.0f;

            // shorter square tower: 4 pieces
            string[] towerPieces = {
                TK + "tower-square-bottom-a.fbx",
                TK + "tower-square-middle-a.fbx",
                TK + "tower-square-top-a.fbx",
                TK + "tower-square-roof-a.fbx",
            };
            for (int i = 0; i < towerPieces.Length; i++)
            {
                var piece = InstantiateProp(new PropDef(towerPieces[i], towerScale, towerScale), rng);
                if (piece == null) continue;
                piece.transform.position = clusterPos + new Vector3(0f, pieceHeight * i, 0f);
                piece.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                piece.transform.SetParent(cluster);
                piece.isStatic = true;
                totalPlaced++;
            }

            // wood-structure at base
            var scaffoldR = InstantiateProp(new PropDef(TK + "wood-structure.fbx", towerScale, towerScale), rng);
            if (scaffoldR != null)
            {
                scaffoldR.transform.position = clusterPos + new Vector3(-2f, 0f, 2f);
                scaffoldR.transform.position = new Vector3(scaffoldR.transform.position.x,
                    SampleWorldHeight(terrain, terrainPos, scaffoldR.transform.position.x, scaffoldR.transform.position.z),
                    scaffoldR.transform.position.z);
                scaffoldR.transform.rotation = Quaternion.Euler(0f, -30f, 0f);
                scaffoldR.transform.SetParent(cluster);
                scaffoldR.isStatic = true;
                totalPlaced++;
            }

            // 2 fence segments
            for (int f = 0; f < 2; f++)
            {
                float fAngle = f * Mathf.PI + 0.5f;
                float fDist = 5f;
                float fx = clusterPos.x + Mathf.Cos(fAngle) * fDist;
                float fz = clusterPos.z + Mathf.Sin(fAngle) * fDist;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

                var fence = InstantiateProp(new PropDef(SK + "fence.fbx", 3f, 3f), rng);
                if (fence == null) continue;
                fence.transform.position = new Vector3(fx, fy, fz);
                fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                fence.transform.SetParent(cluster);
                fence.isStatic = true;
                totalPlaced++;
            }
        }

        Debug.Log($"settlements placed: {totalPlaced} total pieces");
    }

    /// <summary>
    /// Scatter random debris props around a center point. Returns count placed.
    /// </summary>
    private static int ScatterDebris(Transform parent, Terrain terrain, Vector3 terrainPos,
        System.Random rng, float cx, float cz, float minRadius, float maxRadius, int count, PropDef[] pool)
    {
        int placed = 0;
        for (int d = 0; d < count; d++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
            float dx = cx + Mathf.Cos(angle) * dist;
            float dz = cz + Mathf.Sin(angle) * dist;
            float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

            var prop = pool[rng.Next(pool.Length)];
            var inst = InstantiateProp(prop, rng);
            if (inst == null) continue;

            float tilt = (float)(rng.NextDouble() - 0.5) * 20f;
            inst.transform.position = new Vector3(dx, dy, dz);
            inst.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, tilt * 0.3f);
            inst.transform.SetParent(parent);
            inst.isStatic = true;
            placed++;
        }
        return placed;
    }

    private static void PlaceMerchantStructures(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        // disabled — focusing on one settlement at a time
        return;

        var parent = new GameObject("Merchants").transform;
        parent.SetParent(root);
        int totalPlaced = 0;

        const string SK = "Assets/_Slopworks/Art/Kenney/survival-kit/Models/";
        const string CK = "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/";

        // --- cluster 3: mechanic's garage (near train station at waystation index 1) ---
        {
            Vector3 clusterPos = new Vector3(WaystationPositions[1].x + 20f, 0f, WaystationPositions[1].y);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);
            float facing = -90f; // door faces west toward the station

            var cluster = new GameObject("Cluster_Garage").transform;
            cluster.SetParent(parent);

            float wallScale = 4f;

            // 2 yellow-tall walls forming two sides of the garage
            var wallL = InstantiateProp(new PropDef(CK + "structure-yellow-tall.fbx", wallScale, wallScale), rng);
            if (wallL != null)
            {
                wallL.transform.position = clusterPos;
                wallL.transform.rotation = Quaternion.Euler(0f, facing + (float)(rng.NextDouble() - 0.5) * 4f, 0f);
                wallL.transform.SetParent(cluster);
                wallL.isStatic = true;
                totalPlaced++;
            }
            var wallR = InstantiateProp(new PropDef(CK + "structure-yellow-tall.fbx", wallScale, wallScale), rng);
            if (wallR != null)
            {
                wallR.transform.position = clusterPos + Quaternion.Euler(0f, facing, 0f) * new Vector3(wallScale * 1.0f, 0f, 0f);
                wallR.transform.rotation = Quaternion.Euler(0f, facing + 90f + (float)(rng.NextDouble() - 0.5) * 4f, 0f);
                wallR.transform.SetParent(cluster);
                wallR.isStatic = true;
                totalPlaced++;
            }

            // wide open door as entrance
            var door = InstantiateProp(new PropDef(CK + "door-wide-open.fbx", wallScale, wallScale), rng);
            if (door != null)
            {
                door.transform.position = clusterPos + Quaternion.Euler(0f, facing, 0f) * new Vector3(0f, 0f, -wallScale * 1.0f);
                door.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                door.transform.SetParent(cluster);
                door.isStatic = true;
                totalPlaced++;
            }

            // metal floor pad
            var floorPad = InstantiateProp(new PropDef(SK + "structure-metal-floor.fbx", wallScale, wallScale), rng);
            if (floorPad != null)
            {
                floorPad.transform.position = clusterPos;
                floorPad.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                floorPad.transform.SetParent(cluster);
                floorPad.isStatic = true;
                totalPlaced++;
            }

            // cover-stripe roof
            var roofG = InstantiateProp(new PropDef(CK + "cover-stripe.fbx", wallScale, wallScale), rng);
            if (roofG != null)
            {
                roofG.transform.position = clusterPos + new Vector3(0f, wallScale * 1.0f, 0f);
                roofG.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                roofG.transform.SetParent(cluster);
                roofG.isStatic = true;
                totalPlaced++;
            }

            // debris: barrel, metal-panel
            totalPlaced += ScatterDebris(cluster, terrain, terrainPos, rng,
                clusterPos.x, clusterPos.z, 3f, 10f, 3 + rng.Next(3),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel.fbx", 3f, 3.5f),
                    new PropDef(SK + "metal-panel-screws.fbx", 3f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                });
        }

        // --- cluster 4: gas station (on road between factory and hamlet) ---
        {
            Vector3 clusterPos = new Vector3(160f, 0f, -120f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);
            float facing = 0f; // canopy faces road (along X)

            var cluster = new GameObject("Cluster_GasStation").transform;
            cluster.SetParent(parent);

            float pillarScale = 4f;

            // 4 tall pillars in a rectangle (canopy supports)
            Vector3[] pillarOffsets = {
                new(-3f, 0f, -3f),
                new(3f, 0f, -3f),
                new(-3f, 0f, 3f),
                new(3f, 0f, 3f),
            };
            foreach (var offset in pillarOffsets)
            {
                var pillar = InstantiateProp(new PropDef(CK + "structure-tall.fbx", pillarScale, pillarScale), rng);
                if (pillar == null) continue;
                Vector3 pPos = clusterPos + offset * pillarScale * 0.5f;
                pPos.y = SampleWorldHeight(terrain, terrainPos, pPos.x, pPos.z);
                pillar.transform.position = pPos;
                pillar.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                pillar.transform.SetParent(cluster);
                pillar.isStatic = true;
                totalPlaced++;
            }

            // cover-stripe canopy on top
            var canopy = InstantiateProp(new PropDef(CK + "cover-stripe.fbx", pillarScale * 1.5f, pillarScale * 1.5f), rng);
            if (canopy != null)
            {
                canopy.transform.position = clusterPos + new Vector3(0f, pillarScale * 1.0f, 0f);
                canopy.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                canopy.transform.SetParent(cluster);
                canopy.isStatic = true;
                totalPlaced++;
            }

            // floor pad underneath canopy
            var canopyFloor = InstantiateProp(new PropDef(CK + "floor-large.fbx", pillarScale, pillarScale), rng);
            if (canopyFloor != null)
            {
                canopyFloor.transform.position = clusterPos;
                canopyFloor.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                canopyFloor.transform.SetParent(cluster);
                canopyFloor.isStatic = true;
                totalPlaced++;
            }

            // small metal shed behind the canopy
            Vector3 shedPos = clusterPos + new Vector3(-8f, 0f, 0f);
            shedPos.y = SampleWorldHeight(terrain, terrainPos, shedPos.x, shedPos.z);
            float shedFacing = facing + 90f;

            var shed = InstantiateProp(new PropDef(SK + "structure-metal.fbx", 3f, 3f), rng);
            if (shed != null)
            {
                shed.transform.position = shedPos;
                shed.transform.rotation = Quaternion.Euler(0f, shedFacing, 0f);
                shed.transform.SetParent(cluster);
                shed.isStatic = true;
                totalPlaced++;
            }
            var shedRoof = InstantiateProp(new PropDef(SK + "structure-metal-roof.fbx", 3f, 3f), rng);
            if (shedRoof != null)
            {
                shedRoof.transform.position = shedPos + new Vector3(0f, 3f, 0f);
                shedRoof.transform.rotation = Quaternion.Euler(0f, shedFacing, 0f);
                shedRoof.transform.SetParent(cluster);
                shedRoof.isStatic = true;
                totalPlaced++;
            }

            // signpost
            var sign = InstantiateProp(new PropDef(SK + "signpost.fbx", 3f, 3.5f), rng);
            if (sign != null)
            {
                Vector3 signPos = clusterPos + new Vector3(5f, 0f, -5f);
                signPos.y = SampleWorldHeight(terrain, terrainPos, signPos.x, signPos.z);
                sign.transform.position = signPos;
                sign.transform.rotation = Quaternion.Euler(0f, facing + 15f, 3f);
                sign.transform.SetParent(cluster);
                sign.isStatic = true;
                totalPlaced++;
            }

            // debris
            totalPlaced += ScatterDebris(cluster, terrain, terrainPos, rng,
                clusterPos.x, clusterPos.z, 5f, 12f, 3 + rng.Next(4),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "barrel-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel-narrow.fbx", 3f, 3.5f),
                });
        }

        // --- cluster 5: woodshop (forest edge, off road) ---
        {
            Vector3 clusterPos = new Vector3(-180f, 0f, -80f);
            clusterPos.y = SampleWorldHeight(terrain, terrainPos, clusterPos.x, clusterPos.z);
            float facing = 60f; // face roughly toward road

            var cluster = new GameObject("Cluster_Woodshop").transform;
            cluster.SetParent(parent);

            float shedScale = 3.5f;

            // open metal shed: structure-metal + structure-metal-roof
            var metalShed = InstantiateProp(new PropDef(SK + "structure-metal.fbx", shedScale, shedScale), rng);
            if (metalShed != null)
            {
                metalShed.transform.position = clusterPos;
                metalShed.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                metalShed.transform.SetParent(cluster);
                metalShed.isStatic = true;
                totalPlaced++;
            }
            var metalRoof = InstantiateProp(new PropDef(SK + "structure-metal-roof.fbx", shedScale, shedScale), rng);
            if (metalRoof != null)
            {
                metalRoof.transform.position = clusterPos + new Vector3(0f, shedScale * 1.0f, 0f);
                metalRoof.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                metalRoof.transform.SetParent(cluster);
                metalRoof.isStatic = true;
                totalPlaced++;
            }

            // workbench-anvil inside the shed
            var anvil = InstantiateProp(new PropDef(SK + "workbench-anvil.fbx", 3f, 3.5f), rng);
            if (anvil != null)
            {
                Vector3 anvilPos = clusterPos + Quaternion.Euler(0f, facing, 0f) * new Vector3(0f, 0f, 1.5f);
                anvilPos.y = SampleWorldHeight(terrain, terrainPos, anvilPos.x, anvilPos.z);
                anvil.transform.position = anvilPos;
                anvil.transform.rotation = Quaternion.Euler(0f, facing + 180f, 0f);
                anvil.transform.SetParent(cluster);
                anvil.isStatic = true;
                totalPlaced++;
            }

            // fence perimeter (3 segments)
            for (int f = 0; f < 3; f++)
            {
                float fAngle = ((float)f / 3f) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                float fDist = 8f;
                float fx = clusterPos.x + Mathf.Cos(fAngle) * fDist;
                float fz = clusterPos.z + Mathf.Sin(fAngle) * fDist;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

                var fence = InstantiateProp(new PropDef(SK + "fence.fbx", 3f, 3.5f), rng);
                if (fence == null) continue;
                fence.transform.position = new Vector3(fx, fy, fz);
                fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                fence.transform.SetParent(cluster);
                fence.isStatic = true;
                totalPlaced++;
            }

            // scattered wood resources
            PropDef[] woodDebris = {
                new(SK + "resource-wood.fbx", 2f, 3f, false),
                new(SK + "resource-planks.fbx", 2f, 3f, false),
                new(SK + "tree-log.fbx", 2f, 4f),
            };
            for (int w = 0; w < 4 + rng.Next(3); w++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 3f + (float)rng.NextDouble() * 10f;
                float wx = clusterPos.x + Mathf.Cos(angle) * dist;
                float wz = clusterPos.z + Mathf.Sin(angle) * dist;
                float wy = SampleWorldHeight(terrain, terrainPos, wx, wz);

                var prop = woodDebris[rng.Next(woodDebris.Length)];
                var inst = InstantiateProp(prop, rng);
                if (inst == null) continue;
                inst.transform.position = new Vector3(wx, wy, wz);
                inst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                inst.transform.SetParent(cluster);
                inst.isStatic = true;
                totalPlaced++;
            }
        }

        Debug.Log($"merchant structures placed: {totalPlaced} pieces");
    }

    private static void PlaceWaystations(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        // disabled — focusing on one settlement at a time
        return;

        var parent = new GameObject("Waystations").transform;
        parent.SetParent(root);
        int totalPlaced = 0;
        string[] names = { "BusStop", "TrainStation", "SubwayEntrance", "Helipad" };

        const string SK = "Assets/_Slopworks/Art/Kenney/survival-kit/Models/";
        const string CK = "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/";

        for (int w = 0; w < WaystationPositions.Length; w++)
        {
            var pos = WaystationPositions[w];
            var ws = new GameObject(names[w]).transform;
            ws.SetParent(parent);

            float wy = SampleWorldHeight(terrain, terrainPos, pos.x, pos.y);

            switch (w)
            {
                case 0: // bus stop — small shelter with bench area
                {
                    float facing = 90f; // faces east toward road
                    float shelterScale = 3.5f;

                    // metal shelter frame
                    var frame = InstantiateProp(new PropDef(SK + "structure-metal.fbx", shelterScale, shelterScale), rng);
                    if (frame != null)
                    {
                        frame.transform.position = new Vector3(pos.x, wy, pos.y);
                        frame.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        frame.transform.SetParent(ws);
                        frame.isStatic = true;
                        totalPlaced++;
                    }
                    var shelterRoof = InstantiateProp(new PropDef(SK + "structure-metal-roof.fbx", shelterScale, shelterScale), rng);
                    if (shelterRoof != null)
                    {
                        shelterRoof.transform.position = new Vector3(pos.x, wy + shelterScale * 1.0f, pos.y);
                        shelterRoof.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        shelterRoof.transform.SetParent(ws);
                        shelterRoof.isStatic = true;
                        totalPlaced++;
                    }
                    // metal floor
                    var busFloor = InstantiateProp(new PropDef(SK + "structure-metal-floor.fbx", shelterScale, shelterScale), rng);
                    if (busFloor != null)
                    {
                        busFloor.transform.position = new Vector3(pos.x, wy, pos.y);
                        busFloor.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        busFloor.transform.SetParent(ws);
                        busFloor.isStatic = true;
                        totalPlaced++;
                    }
                    // signpost
                    var busSign = InstantiateProp(new PropDef(SK + "signpost-single.fbx", 3f, 3.5f), rng);
                    if (busSign != null)
                    {
                        busSign.transform.position = new Vector3(pos.x - 5f, wy, pos.y);
                        busSign.transform.rotation = Quaternion.Euler(0f, 0f, 5f);
                        busSign.transform.SetParent(ws);
                        busSign.isStatic = true;
                        totalPlaced++;
                    }
                    break;
                }

                case 1: // train station — long platform with partial roof
                {
                    float facing = 0f; // platform runs east-west
                    float platScale = 4f;

                    // platform floor (long, 3 floor pieces)
                    for (int f = 0; f < 3; f++)
                    {
                        string floorPath = f == 1 ? SK + "floor-old.fbx" : SK + "floor.fbx";
                        var plat = InstantiateProp(new PropDef(floorPath, platScale, platScale), rng);
                        if (plat == null) continue;
                        float offsetX = (f - 1) * platScale * 1.0f;
                        plat.transform.position = new Vector3(pos.x + offsetX, wy + 0.5f, pos.y);
                        plat.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        plat.transform.SetParent(ws);
                        plat.isStatic = true;
                        totalPlaced++;
                    }

                    // partial roof shelter in center (conveyor walls + cover)
                    var stationWall1 = InstantiateProp(new PropDef(CK + "structure-wall.fbx", platScale, platScale), rng);
                    if (stationWall1 != null)
                    {
                        stationWall1.transform.position = new Vector3(pos.x, wy + 0.5f, pos.y - platScale * 0.5f);
                        stationWall1.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        stationWall1.transform.SetParent(ws);
                        stationWall1.isStatic = true;
                        totalPlaced++;
                    }
                    var stationWall2 = InstantiateProp(new PropDef(CK + "structure-window.fbx", platScale, platScale), rng);
                    if (stationWall2 != null)
                    {
                        stationWall2.transform.position = new Vector3(pos.x + platScale * 1.0f, wy + 0.5f, pos.y - platScale * 0.5f);
                        stationWall2.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        stationWall2.transform.SetParent(ws);
                        stationWall2.isStatic = true;
                        totalPlaced++;
                    }
                    // cover roof over shelter section
                    var stationRoof = InstantiateProp(new PropDef(CK + "cover-stripe.fbx", platScale, platScale), rng);
                    if (stationRoof != null)
                    {
                        stationRoof.transform.position = new Vector3(pos.x + platScale * 0.5f, wy + 0.5f + platScale * 1.0f, pos.y - platScale * 0.25f);
                        stationRoof.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        stationRoof.transform.SetParent(ws);
                        stationRoof.isStatic = true;
                        totalPlaced++;
                    }

                    // signpost at end of platform
                    var trainSign = InstantiateProp(new PropDef(SK + "signpost.fbx", 3f, 3.5f), rng);
                    if (trainSign != null)
                    {
                        trainSign.transform.position = new Vector3(pos.x - platScale * 1.5f, wy, pos.y);
                        trainSign.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                        trainSign.transform.SetParent(ws);
                        trainSign.isStatic = true;
                        totalPlaced++;
                    }
                    break;
                }

                case 2: // subway entrance — sunken doorway with walls
                {
                    float facing = 180f;
                    float entryScale = 3.5f;

                    // doorway (sunk slightly below terrain)
                    var subDoor = InstantiateProp(new PropDef(CK + "structure-doorway.fbx", entryScale, entryScale), rng);
                    if (subDoor != null)
                    {
                        subDoor.transform.position = new Vector3(pos.x, wy - 1f, pos.y);
                        subDoor.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        subDoor.transform.SetParent(ws);
                        subDoor.isStatic = true;
                        totalPlaced++;
                    }
                    // side walls
                    for (int s = 0; s < 2; s++)
                    {
                        float sideOffset = (s == 0 ? -1f : 1f) * entryScale * 0.6f;
                        var sideWall = InstantiateProp(new PropDef(CK + "structure-wall.fbx", entryScale, entryScale), rng);
                        if (sideWall == null) continue;
                        sideWall.transform.position = new Vector3(pos.x + sideOffset, wy - 0.5f, pos.y);
                        sideWall.transform.rotation = Quaternion.Euler(0f, facing + 90f, 0f);
                        sideWall.transform.SetParent(ws);
                        sideWall.isStatic = true;
                        totalPlaced++;
                    }
                    // cover over entrance
                    var subCover = InstantiateProp(new PropDef(CK + "cover.fbx", entryScale, entryScale), rng);
                    if (subCover != null)
                    {
                        subCover.transform.position = new Vector3(pos.x, wy - 0.5f + entryScale * 1.0f, pos.y);
                        subCover.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        subCover.transform.SetParent(ws);
                        subCover.isStatic = true;
                        totalPlaced++;
                    }
                    // metal floor at base of stairs
                    var subFloor = InstantiateProp(new PropDef(SK + "structure-metal-floor.fbx", entryScale, entryScale), rng);
                    if (subFloor != null)
                    {
                        subFloor.transform.position = new Vector3(pos.x, wy - 1f, pos.y + entryScale * 0.5f);
                        subFloor.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        subFloor.transform.SetParent(ws);
                        subFloor.isStatic = true;
                        totalPlaced++;
                    }
                    break;
                }

                case 3: // helipad — open pad with perimeter fencing and shack
                {
                    float facing = 0f;

                    // metal floor pad (large)
                    var heliFloor = InstantiateProp(new PropDef(CK + "floor-large.fbx", 5f, 5f), rng);
                    if (heliFloor != null)
                    {
                        heliFloor.transform.position = new Vector3(pos.x, wy, pos.y);
                        heliFloor.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                        heliFloor.transform.SetParent(ws);
                        heliFloor.isStatic = true;
                        totalPlaced++;
                    }

                    // shack to one side
                    var heliShack = InstantiateProp(new PropDef(SK + "structure-metal.fbx", 3f, 3f), rng);
                    if (heliShack != null)
                    {
                        heliShack.transform.position = new Vector3(pos.x + 8f, wy, pos.y + 8f);
                        heliShack.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                        heliShack.transform.SetParent(ws);
                        heliShack.isStatic = true;
                        totalPlaced++;
                    }
                    var heliShackRoof = InstantiateProp(new PropDef(SK + "structure-metal-roof.fbx", 3f, 3f), rng);
                    if (heliShackRoof != null)
                    {
                        heliShackRoof.transform.position = new Vector3(pos.x + 8f, wy + 3f, pos.y + 8f);
                        heliShackRoof.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                        heliShackRoof.transform.SetParent(ws);
                        heliShackRoof.isStatic = true;
                        totalPlaced++;
                    }

                    // fence-fortified perimeter
                    for (int f = 0; f < 8; f++)
                    {
                        float fAngle = ((float)f / 8f) * Mathf.PI * 2f;
                        float fDist = 10f;
                        float fx = pos.x + Mathf.Cos(fAngle) * fDist;
                        float fz = pos.y + Mathf.Sin(fAngle) * fDist;
                        float fy = SampleWorldHeight(terrain, terrainPos, fx, fz);

                        var fence = InstantiateProp(new PropDef(SK + "fence-fortified.fbx", 3f, 3.5f), rng);
                        if (fence == null) continue;
                        fence.transform.position = new Vector3(fx, fy, fz);
                        fence.transform.rotation = Quaternion.Euler(0f, fAngle * Mathf.Rad2Deg + 90f, 0f);
                        fence.transform.SetParent(ws);
                        fence.isStatic = true;
                        totalPlaced++;
                    }

                    // signpost
                    var heliSign = InstantiateProp(new PropDef(SK + "signpost.fbx", 3f, 3.5f), rng);
                    if (heliSign != null)
                    {
                        heliSign.transform.position = new Vector3(pos.x - 10f, wy, pos.y);
                        heliSign.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                        heliSign.transform.SetParent(ws);
                        heliSign.isStatic = true;
                        totalPlaced++;
                    }
                    break;
                }
            }

            // shared debris scatter around each waystation
            totalPlaced += ScatterDebris(ws, terrain, terrainPos, rng,
                pos.x, pos.y, WaystationPadSizes[w].x / 2f, WaystationPadSizes[w].x / 2f + 10f,
                4 + rng.Next(4),
                new[] {
                    new PropDef(SK + "barrel.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "barrel-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "box-open.fbx", 2.5f, 3.5f),
                    new PropDef(SK + "metal-panel.fbx", 3f, 3.5f),
                });
        }

        Debug.Log($"waystations placed: {totalPlaced} pieces across {WaystationPositions.Length} locations");
    }

    private static void PlaceRoadBuildings(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("RoadBuildings").transform;
        parent.SetParent(root);
        int totalPlaced = 0;

        const string CK = "Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/";
        const string SK = "Assets/_Slopworks/Art/Kenney/survival-kit/Models/";

        // === FACTORY YARD: industrial compound ~100m east of the factory hub ===
        // Uses complete pre-built structure meshes (structure.fbx, structure-metal.fbx)
        // at large scales so each piece reads as a real building.
        // Layout: buildings face a central yard, with entrance from the west.
        {
            float baseY = SampleWorldHeight(terrain, terrainPos, FactoryYardPos.x, FactoryYardPos.y);
            var yard = new GameObject("Cluster_FactoryYard").transform;
            yard.SetParent(parent);

            const string TK = "Assets/_Slopworks/Art/Kenney/tower-defense-kit/Models/";
            float cx = FactoryYardPos.x;
            float cz = FactoryYardPos.y;

            // ============================================================
            // BUILDING A: MAIN WAREHOUSE — largest building, center-north
            // Two complete structures side by side = wide warehouse
            // structure.fbx is a pre-built room with walls + doorway
            // ============================================================
            // Left half (scale 8 = 8m building)
            PlaceKitPiece(yard, SK + "structure.fbx", 8f,
                new Vector3(cx - 4f, baseY, cz + 5f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-roof.fbx", 8f,
                new Vector3(cx - 4f, baseY + 8f, cz + 5f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "floor.fbx", 8f,
                new Vector3(cx - 4f, baseY, cz + 5f), 180f, rng, ref totalPlaced);

            // Right half (adjacent, same orientation)
            PlaceKitPiece(yard, SK + "structure.fbx", 8f,
                new Vector3(cx + 5f, baseY, cz + 5f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-roof.fbx", 8f,
                new Vector3(cx + 5f, baseY + 8f, cz + 5f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "floor-old.fbx", 8f,
                new Vector3(cx + 5f, baseY, cz + 5f), 180f, rng, ref totalPlaced);

            // Warehouse interior props
            PlaceKitPiece(yard, SK + "workbench.fbx", 3f,
                new Vector3(cx - 2f, baseY, cz + 8f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "workbench-grind.fbx", 3f,
                new Vector3(cx + 6f, baseY, cz + 8f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel.fbx", 2.5f,
                new Vector3(cx + 3f, baseY, cz + 9f), 45f, rng, ref totalPlaced);

            // ============================================================
            // BUILDING B: METAL WORKSHOP — east side of yard
            // Complete metal structure (structure-metal.fbx = pre-built metal shed)
            // ============================================================
            PlaceKitPiece(yard, SK + "structure-metal.fbx", 7f,
                new Vector3(cx + 16f, baseY, cz + 2f), 270f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-metal-roof.fbx", 7f,
                new Vector3(cx + 16f, baseY + 7f, cz + 2f), 270f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-metal-floor.fbx", 7f,
                new Vector3(cx + 16f, baseY, cz + 2f), 270f, rng, ref totalPlaced);

            // Workshop interior: anvil workbench
            PlaceKitPiece(yard, SK + "workbench-anvil.fbx", 2.5f,
                new Vector3(cx + 14f, baseY, cz + 3f), 90f, rng, ref totalPlaced);

            // ============================================================
            // BUILDING C: FOREMAN'S OFFICE — west side, near entrance
            // Small survival-kit cabin, door faces the yard
            // ============================================================
            PlaceKitPiece(yard, SK + "structure.fbx", 6f,
                new Vector3(cx - 18f, baseY, cz + 2f), 90f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-roof.fbx", 6f,
                new Vector3(cx - 18f, baseY + 6f, cz + 2f), 90f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "floor-old.fbx", 6f,
                new Vector3(cx - 18f, baseY, cz + 2f), 90f, rng, ref totalPlaced);

            // Porch floor in front of office
            PlaceKitPiece(yard, SK + "floor.fbx", 4f,
                new Vector3(cx - 14f, baseY, cz + 2f), 0f, rng, ref totalPlaced);

            // Signpost at office
            PlaceKitPiece(yard, SK + "signpost.fbx", 4f,
                new Vector3(cx - 14f, baseY, cz + 4.5f), 90f, rng, ref totalPlaced);

            // ============================================================
            // BUILDING D: STORAGE SHED — north of warehouse, open front
            // Metal structure without a full roof (canvas patch instead)
            // ============================================================
            PlaceKitPiece(yard, SK + "structure-metal.fbx", 6f,
                new Vector3(cx - 3f, baseY, cz + 16f), 180f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-canvas.fbx", 6f,
                new Vector3(cx - 3f, baseY + 5.5f, cz + 16f), 180f, rng, ref totalPlaced);

            // Second storage shed next to it
            PlaceKitPiece(yard, SK + "structure-metal.fbx", 5f,
                new Vector3(cx + 6f, baseY, cz + 17f), 200f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "structure-metal-roof.fbx", 5f,
                new Vector3(cx + 6f, baseY + 5f, cz + 17f), 200f, rng, ref totalPlaced);

            // Crates inside storage sheds
            PlaceKitPiece(yard, SK + "box-large.fbx", 3f,
                new Vector3(cx - 2f, baseY, cz + 17f), 10f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "box-large.fbx", 3f,
                new Vector3(cx + 0.5f, baseY, cz + 17.5f), -15f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "box.fbx", 2.5f,
                new Vector3(cx - 1f, baseY + 2.2f, cz + 17f), 30f, rng, ref totalPlaced);

            // ============================================================
            // BUILDING E: RUINED CABIN — south of yard, partially collapsed
            // Shows decay: no roof, floor-hole, tilted walls
            // ============================================================
            PlaceKitPiece(yard, SK + "structure.fbx", 6f,
                new Vector3(cx + 3f, baseY, cz - 16f), 160f, rng, ref totalPlaced);
            // No roof — collapsed. Canvas tarp draped over it instead
            PlaceKitPiece(yard, SK + "structure-canvas.fbx", 6f,
                new Vector3(cx + 3f, baseY + 5f, cz - 16f), 165f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "floor-hole.fbx", 6f,
                new Vector3(cx + 3f, baseY, cz - 16f), 160f, rng, ref totalPlaced);

            // Adjacent ruined metal shed (walls only, no roof)
            PlaceKitPiece(yard, SK + "structure-metal.fbx", 5f,
                new Vector3(cx - 5f, baseY, cz - 18f), 175f, rng, ref totalPlaced);
            // Debris from collapsed roof
            PlaceKitPiece(yard, SK + "metal-panel.fbx", 4f,
                new Vector3(cx - 4f, baseY, cz - 16f), 45f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "metal-panel-screws.fbx", 3.5f,
                new Vector3(cx - 6f, baseY, cz - 15f), 120f, rng, ref totalPlaced);

            // ============================================================
            // LOADING BAY (between warehouse and south buildings)
            // Conveyors + robot arm + stacked boxes
            // ============================================================
            float loadZ = cz - 5f;

            // 4 conveyor segments running east-west
            for (int i = 0; i < 4; i++)
                PlaceKitPiece(yard, CK + "conveyor-long.fbx", 3.5f,
                    new Vector3(cx - 4f + i * 3.5f, baseY, loadZ), 90f, rng, ref totalPlaced);

            // Robot arm overseeing the line
            PlaceKitPiece(yard, CK + "robot-arm-a.fbx", 3f,
                new Vector3(cx + 10f, baseY, loadZ + 1.5f), 180f, rng, ref totalPlaced);

            // Boxes on conveyors
            PlaceKitPiece(yard, CK + "box-large.fbx", 1.8f,
                new Vector3(cx - 2f, baseY + 1.2f, loadZ), 8f, rng, ref totalPlaced);
            PlaceKitPiece(yard, CK + "box-wide.fbx", 1.8f,
                new Vector3(cx + 2f, baseY + 1.2f, loadZ), -10f, rng, ref totalPlaced);
            PlaceKitPiece(yard, CK + "box-small.fbx", 1.5f,
                new Vector3(cx + 5f, baseY + 1.2f, loadZ), 20f, rng, ref totalPlaced);

            // ============================================================
            // GUARD TOWER (SE corner)
            // 4-piece stacked tower, visible landmark
            // ============================================================
            float gtX = cx + 20f;
            float gtZ = cz - 14f;
            float gtY = SampleWorldHeight(terrain, terrainPos, gtX, gtZ);
            float gtS = 4.5f;

            string[] towerParts = {
                TK + "tower-square-bottom-a.fbx",
                TK + "tower-square-middle-a.fbx",
                TK + "tower-square-top-a.fbx",
                TK + "tower-square-roof-a.fbx",
            };
            for (int i = 0; i < towerParts.Length; i++)
                PlaceKitPiece(yard, towerParts[i], gtS,
                    new Vector3(gtX, gtY + gtS * i, gtZ), 15f, rng, ref totalPlaced);

            // Scaffolding at base
            PlaceKitPiece(yard, TK + "wood-structure.fbx", gtS,
                new Vector3(gtX + 4f, gtY, gtZ + 2f), 45f, rng, ref totalPlaced);
            PlaceKitPiece(yard, TK + "wood-structure.fbx", gtS * 0.8f,
                new Vector3(gtX - 3f, gtY, gtZ - 1f), -30f, rng, ref totalPlaced);

            // ============================================================
            // BREAK CAMP (west side, near entrance gate)
            // Survivors living in the compound
            // ============================================================
            float bcX = cx - 20f;
            float bcZ = cz - 8f;

            PlaceKitPiece(yard, SK + "tent-canvas.fbx", 4f,
                new Vector3(bcX, baseY, bcZ), 110f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "tent.fbx", 3.5f,
                new Vector3(bcX + 5f, baseY, bcZ - 2f), 70f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "bedroll.fbx", 2.5f,
                new Vector3(bcX + 1f, baseY, bcZ + 4f), 100f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "bedroll-frame.fbx", 2.5f,
                new Vector3(bcX + 4f, baseY, bcZ + 4.5f), 85f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "campfire-pit.fbx", 3f,
                new Vector3(bcX + 2.5f, baseY, bcZ + 1.5f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "campfire-stand.fbx", 3f,
                new Vector3(bcX + 3f, baseY, bcZ + 2f), 30f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "chest.fbx", 2.5f,
                new Vector3(bcX - 1f, baseY, bcZ + 3f), 120f, rng, ref totalPlaced);

            // ============================================================
            // FUEL DUMP (east side, fenced off)
            // ============================================================
            float fdX = cx + 18f;
            float fdZ = cz + 10f;

            // 6 barrels in a tight cluster
            PlaceKitPiece(yard, SK + "barrel.fbx", 3f,
                new Vector3(fdX, baseY, fdZ), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel.fbx", 3f,
                new Vector3(fdX + 2f, baseY, fdZ + 0.5f), 30f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel.fbx", 3f,
                new Vector3(fdX + 1f, baseY, fdZ + 2f), 60f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel-open.fbx", 3f,
                new Vector3(fdX + 3f, baseY, fdZ + 1.5f), 15f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel.fbx", 2.5f,
                new Vector3(fdX + 0.5f, baseY + 2f, fdZ + 0.5f), 45f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel.fbx", 2.5f,
                new Vector3(fdX + 2f, baseY + 2f, fdZ + 1f), 75f, rng, ref totalPlaced);

            // Fence around fuel dump
            PlaceKitPiece(yard, SK + "fence-fortified.fbx", 4f,
                new Vector3(fdX - 1f, baseY, fdZ - 2f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "fence-fortified.fbx", 4f,
                new Vector3(fdX + 4f, baseY, fdZ - 2f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "fence.fbx", 4f,
                new Vector3(fdX + 5.5f, baseY, fdZ + 1f), 90f, rng, ref totalPlaced);

            // ============================================================
            // CONTAINER YARD (NE, stacked crates)
            // ============================================================
            float cyX = cx + 14f;
            float cyZ = cz + 18f;

            // 3x2 grid of large crates, some stacked 2 high
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    PlaceKitPiece(yard, CK + "box-large.fbx", 3.5f,
                        new Vector3(cyX + col * 4f, baseY, cyZ + row * 3.5f),
                        row * 5f, rng, ref totalPlaced);
                }
                if (row < 2) // stack second layer on first two rows
                    PlaceKitPiece(yard, CK + "box-wide.fbx", 3.2f,
                        new Vector3(cyX + 2f, baseY + 2.5f, cyZ + row * 3.5f),
                        row * 12f + 3f, rng, ref totalPlaced);
            }

            // ============================================================
            // GROUND PROPS (scattered around the yard for life)
            // ============================================================

            // Barrels behind warehouse
            PlaceKitPiece(yard, SK + "barrel.fbx", 2.5f,
                new Vector3(cx - 6f, baseY, cz + 12f), 25f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "barrel-open.fbx", 2.5f,
                new Vector3(cx + 10f, baseY, cz + 12f), -15f, rng, ref totalPlaced);

            // Boxes near loading bay
            PlaceKitPiece(yard, SK + "box-large.fbx", 2.5f,
                new Vector3(cx - 8f, baseY, cz - 3f), 8f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "box.fbx", 2f,
                new Vector3(cx - 7.5f, baseY + 1.8f, cz - 3f), -20f, rng, ref totalPlaced);

            // Lumber between buildings
            PlaceKitPiece(yard, SK + "resource-planks.fbx", 3f,
                new Vector3(cx + 8f, baseY, cz - 9f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "resource-wood.fbx", 3f,
                new Vector3(cx + 9f, baseY, cz - 8f), 15f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "tree-log.fbx", 4f,
                new Vector3(cx - 12f, baseY, cz - 4f), 70f, rng, ref totalPlaced);

            // Tools leaning against metal workshop
            PlaceKitPiece(yard, SK + "tool-shovel.fbx", 2.5f,
                new Vector3(cx + 13f, baseY, cz + 5f), 170f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "tool-pickaxe.fbx", 2.5f,
                new Vector3(cx + 13.5f, baseY, cz + 4f), 165f, rng, ref totalPlaced);

            // Stone rubble
            PlaceKitPiece(yard, SK + "resource-stone.fbx", 2.5f,
                new Vector3(cx - 10f, baseY, cz + 14f), 0f, rng, ref totalPlaced);
            PlaceKitPiece(yard, SK + "resource-stone-large.fbx", 3f,
                new Vector3(cx + 12f, baseY, cz - 12f), 30f, rng, ref totalPlaced);

            // ============================================================
            // FENCE PERIMETER
            // Not a perfect ring — gaps and broken sections
            // ============================================================
            float fS = 4f;

            // South perimeter
            for (int i = 0; i < 7; i++)
            {
                float fx = cx - 24f + i * 7f;
                float fz2 = cz - 22f;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz2);
                string ft = (i == 0 || i == 4) ? SK + "fence-fortified.fbx" : SK + "fence.fbx";
                PlaceKitPiece(yard, ft, fS, new Vector3(fx, fy, fz2), 0f, rng, ref totalPlaced);
            }

            // North perimeter
            for (int i = 0; i < 6; i++)
            {
                float fx = cx - 20f + i * 8f;
                float fz2 = cz + 24f;
                float fy = SampleWorldHeight(terrain, terrainPos, fx, fz2);
                PlaceKitPiece(yard, SK + "fence.fbx", fS,
                    new Vector3(fx, fy, fz2), 0f, rng, ref totalPlaced);
            }

            // East perimeter
            for (int i = 0; i < 4; i++)
            {
                float fx2 = cx + 24f;
                float fz2 = cz - 16f + i * 11f;
                float fy = SampleWorldHeight(terrain, terrainPos, fx2, fz2);
                string ft = i == 2 ? SK + "fence-fortified.fbx" : SK + "fence.fbx";
                PlaceKitPiece(yard, ft, fS, new Vector3(fx2, fy, fz2), 90f, rng, ref totalPlaced);
            }

            // West perimeter with gate
            for (int i = 0; i < 3; i++)
            {
                float fx2 = cx - 25f;
                float fz2 = cz - 15f + i * 12f;
                float fy = SampleWorldHeight(terrain, terrainPos, fx2, fz2);
                if (i == 1) // gate
                    PlaceKitPiece(yard, SK + "fence-doorway.fbx", fS,
                        new Vector3(fx2, fy, fz2), 90f, rng, ref totalPlaced);
                else
                    PlaceKitPiece(yard, SK + "fence.fbx", fS,
                        new Vector3(fx2, fy, fz2), 90f, rng, ref totalPlaced);
            }

            // Entrance signpost
            float gateY = SampleWorldHeight(terrain, terrainPos, cx - 28f, cz);
            PlaceKitPiece(yard, SK + "signpost.fbx", 5f,
                new Vector3(cx - 28f, gateY, cz + 2f), 270f, rng, ref totalPlaced);

            Debug.Log($"FACTORY YARD placed at ({cx}, {baseY:F1}, {cz}): {totalPlaced} pieces — walk east from center");
        }
    }

    // === RIVERBED DECORATION ===

    private static void DecorateRiverbed(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("RiverbedDecor").transform;
        parent.SetParent(root);

        int placed = 0;

        foreach (var pt in RiverbedPoints)
        {
            if (rng.NextDouble() > 0.4) continue;

            int count = 1 + rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                float offset = ((float)rng.NextDouble() - 0.5f) * 8f;
                float along = ((float)rng.NextDouble() - 0.5f) * 4f;
                float wx = pt.x + offset;
                float wz = pt.y + along;

                float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
                float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
                if (nx < 0.02f || nx > 0.98f || nz < 0.02f || nz > 0.98f) continue;

                float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

                int idx = SandyRockIndices[rng.Next(SandyRockIndices.Length)];
                var prop = RockProps[idx];
                var instance = InstantiateProp(prop, rng);
                if (instance == null) continue;

                instance.transform.position = new Vector3(wx, y, wz);
                instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 10f);
                instance.transform.SetParent(parent);
                instance.isStatic = true;
                placed++;
            }

            // occasional undergrowth along banks
            if (rng.NextDouble() < 0.3)
            {
                float ox = ((float)rng.NextDouble() - 0.5f) * 10f;
                float oz = ((float)rng.NextDouble() - 0.5f) * 4f;
                float uwx = pt.x + ox;
                float uwz = pt.y + oz;

                float unx = (uwx + TerrainWidth / 2f) / TerrainWidth;
                float unz = (uwz + TerrainWidth / 2f) / TerrainWidth;
                if (unx < 0.02f || unx > 0.98f || unz < 0.02f || unz > 0.98f) continue;

                float uy = SampleWorldHeight(terrain, terrainPos, uwx, uwz);
                var uProp = UndergrowthProps[rng.Next(4)];
                var uInst = InstantiateProp(uProp, rng);
                if (uInst == null) continue;

                uInst.transform.position = new Vector3(uwx, uy, uwz);
                uInst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                uInst.transform.SetParent(parent);
                uInst.isStatic = false;
                AddWindSway(uInst, 2f, 1f);
                placed++;
            }
        }

        Debug.Log($"riverbed decoration placed: {placed}");
    }

    // === RIVER WATER ===

    private static void CreateRiverWater(Transform root, Terrain terrain, Vector3 terrainPos, TerrainData td)
    {
        if (RiverbedPoints.Count < 2) return;

        var waterParent = new GameObject("RiverWater").transform;
        waterParent.SetParent(root);

        // create water material (murky river water, not ocean)
        string matPath = "Assets/_Slopworks/Materials/Environment/Water_River.mat";
        var waterMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (waterMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            waterMat = new Material(shader);
            AssetDatabase.CreateAsset(waterMat, matPath);
        }

        waterMat.SetFloat("_Surface", 1f);
        waterMat.SetFloat("_Blend", 0f);
        waterMat.SetOverrideTag("RenderType", "Transparent");
        waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        waterMat.SetInt("_ZWrite", 0);
        waterMat.renderQueue = 3000;
        waterMat.SetColor("_BaseColor", new Color(0.12f, 0.22f, 0.25f, 0.7f));
        waterMat.SetFloat("_Smoothness", 0.92f);
        waterMat.SetFloat("_Metallic", 0.05f);
        waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        waterMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        EditorUtility.SetDirty(waterMat);

        // build water quads per segment, each following the meander path
        // width varies with curvature (wider at bends, like real rivers)
        int segmentCount = 50;
        int placedSegments = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = (float)i / segmentCount;
            float t1 = (float)(i + 1) / segmentCount;
            float tMid = (t0 + t1) * 0.5f;

            float z0 = RiverCenterZ(t0);
            float z1 = RiverCenterZ(t1);
            float zMid = RiverCenterZ(tMid);

            // world space positions
            float wx0 = t0 * TerrainWidth - TerrainWidth / 2f;
            float wx1 = t1 * TerrainWidth - TerrainWidth / 2f;
            float wz0 = z0 * TerrainWidth - TerrainWidth / 2f;
            float wz1 = z1 * TerrainWidth - TerrainWidth / 2f;

            float cx = (wx0 + wx1) * 0.5f;
            float cz = (wz0 + wz1) * 0.5f;

            // sample terrain at channel center for water level
            float channelY = SampleWorldHeight(terrain, terrainPos, cx, cz);
            float waterY = channelY + 1.2f; // water sits above carved bottom

            // direction of flow for quad orientation
            float dirX = wx1 - wx0;
            float dirZ = wz1 - wz0;
            float angle = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(dirX * dirX + dirZ * dirZ) + 0.5f;

            // width varies with curvature — wider at bends
            float curveMag = Mathf.Abs(RiverCurvature(tMid));
            float halfWidth = 8f + Mathf.Clamp01(curveMag * 0.08f) * 6f; // 16-28m total width

            var waterQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterQuad.name = $"WaterSegment_{i}";
            Object.DestroyImmediate(waterQuad.GetComponent<Collider>());

            waterQuad.transform.position = new Vector3(cx, waterY, cz);
            waterQuad.transform.rotation = Quaternion.Euler(90f, angle, 0f);
            waterQuad.transform.localScale = new Vector3(halfWidth * 2f, length, 1f);
            waterQuad.transform.SetParent(waterParent);

            var renderer = waterQuad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = waterMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            placedSegments++;
        }

        Debug.Log($"river water created: {placedSegments} segments following meander path");
    }

    // === TERRAIN DETAIL GRASS ===

    private static void PaintTerrainGrass(TerrainData td, System.Random rng)
    {
        // disabled — terrain detail grass uses Unity's built-in wind animation
        // which causes the green slabs to sway. Re-enable after tuning detail sizes.
        td.detailPrototypes = new DetailPrototype[0];
        return;

        var grassMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass.fbx");
        var grassLargeMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass-large.fbx");
        var patchGrassMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass.fbx");
        var patchGrassLargeMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass-large.fbx");

        if (grassMesh == null || grassLargeMesh == null)
        {
            Debug.LogWarning("grass models not found, skipping terrain detail grass");
            return;
        }

        var prototypes = new List<DetailPrototype>();

        prototypes.Add(new DetailPrototype
        {
            prototype = grassMesh,
            renderMode = DetailRenderMode.VertexLit,
            usePrototypeMesh = true,
            useInstancing = true,
            minWidth = 0.8f, maxWidth = 1.5f,
            minHeight = 0.6f, maxHeight = 1.2f,
            density = 1f,
        });

        prototypes.Add(new DetailPrototype
        {
            prototype = grassLargeMesh,
            renderMode = DetailRenderMode.VertexLit,
            usePrototypeMesh = true,
            useInstancing = true,
            minWidth = 1f, maxWidth = 2f,
            minHeight = 0.8f, maxHeight = 1.8f,
            density = 0.8f,
        });

        if (patchGrassMesh != null)
        {
            prototypes.Add(new DetailPrototype
            {
                prototype = patchGrassMesh,
                renderMode = DetailRenderMode.VertexLit,
                usePrototypeMesh = true,
                useInstancing = true,
                minWidth = 1.2f, maxWidth = 2.5f,
                minHeight = 0.5f, maxHeight = 1.0f,
                density = 0.6f,
            });
        }

        if (patchGrassLargeMesh != null)
        {
            prototypes.Add(new DetailPrototype
            {
                prototype = patchGrassLargeMesh,
                renderMode = DetailRenderMode.VertexLit,
                usePrototypeMesh = true,
                useInstancing = true,
                minWidth = 1.5f, maxWidth = 3f,
                minHeight = 0.8f, maxHeight = 1.5f,
                density = 0.5f,
            });
        }

        td.detailPrototypes = prototypes.ToArray();

        int detailRes = td.detailResolution;
        if (detailRes == 0)
        {
            td.SetDetailResolution(256, 8);
            detailRes = 256;
        }

        for (int p = 0; p < prototypes.Count; p++)
        {
            int[,] layer = new int[detailRes, detailRes];
            float noiseOffset = p * 137f;

            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    float nx = (float)x / (detailRes - 1);
                    float nz = (float)z / (detailRes - 1);
                    float dx = nx - 0.5f;
                    float dz = nz - 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    float steepness = td.GetSteepness(nx, nz);

                    if (dist < 0.06f || steepness > 30f) continue; // factory zone

                    // suppress grass in river channel, boost on floodplain
                    float riverZ = RiverCenterZ(nx);
                    float riverDist = Mathf.Abs(nz - riverZ);
                    if (riverDist < 10f / TerrainWidth) continue; // no grass in channel

                    float baseDensity = Mathf.Clamp01((dist - 0.06f) * 12f);
                    float noise = Mathf.PerlinNoise(nx * 30f + noiseOffset, nz * 30f + noiseOffset);
                    float density = baseDensity * noise;

                    // lush floodplain grass near river (but outside channel)
                    if (riverDist < 60f / TerrainWidth)
                        density *= 1.5f;

                    if (dist < 0.10f) density *= 0.3f; // transition zone near factory

                    float threshold = p == 0 ? 0.25f : p == 1 ? 0.45f : 0.5f;
                    float scale = p == 0 ? 5f : p == 1 ? 2.5f : 1.5f;

                    layer[z, x] = density > threshold ? (int)(density * scale) : 0;
                }
            }

            td.SetDetailLayer(0, 0, p, layer);
        }

        Debug.Log($"terrain detail grass painted: {prototypes.Count} prototype layers");
    }

    // === HELPERS ===

    private static BiomeZone GetBiomeZone(TerrainData td, float nx, float nz)
    {
        float height = td.GetHeight(
            Mathf.Clamp((int)(nx * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1),
            Mathf.Clamp((int)(nz * (td.heightmapResolution - 1)), 0, td.heightmapResolution - 1));
        float normalizedHeight = height / TerrainHeight;

        float riverZ = RiverCenterZ(nx);
        float riverDist = Mathf.Abs(nz - riverZ) * TerrainWidth;

        if (normalizedHeight < 0.35f || riverDist < 120f)
            return BiomeZone.Floodplain;
        if (normalizedHeight > 0.70f)
            return BiomeZone.RockyUpland;
        return BiomeZone.Forest;
    }

    private static SpeciesDef[] GetCanopyForZone(BiomeZone zone)
    {
        var list = new List<SpeciesDef>();
        foreach (var s in CanopySpecies)
            if (s.Zone == zone) list.Add(s);
        return list.ToArray();
    }

    private static BiomeZone GetBiomeZoneFromWorldPos(TerrainData td, float wx, float wz)
    {
        float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
        float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
        return GetBiomeZone(td, nx, nz);
    }

    private static float SampleWorldHeight(Terrain terrain, Vector3 terrainPos, float wx, float wz)
    {
        float y = terrain.SampleHeight(new Vector3(
            wx + TerrainWidth / 2f + terrainPos.x, 0f,
            wz + TerrainWidth / 2f + terrainPos.z));
        return y + terrainPos.y;
    }

    private static Material _survivalMat;
    private static Material _conveyorMat;
    private static Material _towerDefenseMat;
    private static Material _blasterMat;

    private static void EnsureKenneyMaterials()
    {
        if (_survivalMat == null)
            _survivalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Slopworks/Materials/Kenney/Kenney_Survival.mat");
        if (_conveyorMat == null)
            _conveyorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Slopworks/Materials/Kenney/Kenney_Conveyor.mat");
        if (_towerDefenseMat == null)
            _towerDefenseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Slopworks/Materials/Kenney/Kenney_TowerDefense.mat");
        if (_blasterMat == null)
            _blasterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Slopworks/Materials/Kenney/Kenney_Blaster.mat");
    }

    private static Material GetKenneyMaterial(string assetPath)
    {
        if (assetPath.Contains("survival-kit")) return _survivalMat;
        if (assetPath.Contains("conveyor-kit")) return _conveyorMat;
        if (assetPath.Contains("tower-defense-kit")) return _towerDefenseMat;
        if (assetPath.Contains("blaster-kit")) return _blasterMat;
        return _survivalMat;
    }

    private static GameObject InstantiateProp(PropDef prop, System.Random rng)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
        if (prefab == null) return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        float scale = prop.MinScale + (float)rng.NextDouble() * (prop.MaxScale - prop.MinScale);
        instance.transform.localScale = Vector3.one * scale;

        if (prop.AddCollider && instance.GetComponentInChildren<Collider>() == null)
            instance.AddComponent<MeshCollider>();

        // assign URP-compatible Kenney material
        EnsureKenneyMaterials();
        var mat = GetKenneyMaterial(prop.Path);
        if (mat != null)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                renderer.sharedMaterials = mats;
            }
        }

        return instance;
    }

    /// <summary>
    /// Place a single Kenney kit piece at an exact world position and Y rotation.
    /// Uses fixed scale (min=max) so all building pieces are uniform.
    /// </summary>
    private static GameObject PlaceKitPiece(Transform parent, string path, float scale,
        Vector3 worldPos, float yaw, System.Random rng, ref int count)
    {
        var inst = InstantiateProp(new PropDef(path, scale, scale), rng);
        if (inst == null) return null;
        inst.transform.position = worldPos;
        inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        inst.transform.SetParent(parent);
        inst.isStatic = true;
        count++;
        return inst;
    }

    private static Quaternion UprightRotation(float yaw, System.Random rng, float randomTiltDeg)
    {
        float tx = 0f, tz = 0f;
        if (randomTiltDeg > 0f)
        {
            tx = ((float)rng.NextDouble() - 0.5f) * 2f * randomTiltDeg;
            tz = ((float)rng.NextDouble() - 0.5f) * 2f * randomTiltDeg;
        }
        return Quaternion.Euler(tx, yaw, tz);
    }

    private static Quaternion SlopeAlignedRotation(TerrainData td, float nx, float nz, float yaw, System.Random rng, float randomTiltDeg)
    {
        Vector3 normal = td.GetInterpolatedNormal(nx, nz);
        Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, normal);
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        if (randomTiltDeg > 0f)
        {
            float tx = ((float)rng.NextDouble() - 0.5f) * 2f * randomTiltDeg;
            float tz = ((float)rng.NextDouble() - 0.5f) * 2f * randomTiltDeg;
            yawRot *= Quaternion.Euler(tx, 0f, tz);
        }

        return slopeRot * yawRot;
    }

    private static bool IsNearRiverbed(float wx, float wz, float threshold)
    {
        float thresholdSq = threshold * threshold;
        foreach (var pt in RiverbedPoints)
        {
            float dx = wx - pt.x;
            float dz = wz - pt.y;
            if (dx * dx + dz * dz < thresholdSq)
                return true;
        }
        return false;
    }

    private static bool IsNearStructure(float wx, float wz, float threshold)
    {
        float thresholdSq = threshold * threshold;

        foreach (var pos in WaystationPositions)
        {
            float ddx = wx - pos.x;
            float ddz = wz - pos.y;
            if (ddx * ddx + ddz * ddz < thresholdSq * 4f) return true;
        }

        foreach (var pos in FarmsteadPositions)
        {
            float ddx = wx - pos.x;
            float ddz = wz - pos.y;
            if (ddx * ddx + ddz * ddz < thresholdSq) return true;
        }

        float hdx = wx - HamletCenter.x;
        float hdz = wz - HamletCenter.y;
        if (hdx * hdx + hdz * hdz < 60f * 60f) return true;

        Vector2[] merchants = { GasStationPos, WoodshopPos, GaragePos };
        foreach (var pos in merchants)
        {
            float ddx = wx - pos.x;
            float ddz = wz - pos.y;
            if (ddx * ddx + ddz * ddz < thresholdSq * 2f) return true;
        }

        // factory yard — 45m clear radius for the expanded compound
        float fydx = wx - FactoryYardPos.x;
        float fydz = wz - FactoryYardPos.y;
        if (fydx * fydx + fydz * fydz < 45f * 45f) return true;

        return false;
    }

    private static void AddWindSway(GameObject go, float amount, float speed)
    {
        // disabled for now — re-enable after performance optimization pass
        return;
    }

    private static void SetupAmbientParticles()
    {
        var existingParticles = Object.FindAnyObjectByType<AmbientParticles>();
        if (existingParticles != null)
            Object.DestroyImmediate(existingParticles.gameObject);

        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            cam.gameObject.AddComponent<AmbientParticles>();
            Debug.Log("ambient particles added to camera");
        }
        else
        {
            Debug.LogWarning("no camera found for ambient particles");
        }
    }
}
