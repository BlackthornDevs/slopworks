using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Dresses the HomeBaseTerrain scene with PBR textures, props, terrain features, and skybox.
/// Run via Slopworks > Dress HomeBase Scenery. Idempotent — clears previous scenery on re-run.
/// </summary>
public static class HomeBaseSceneryDresser
{
    private const int Seed = 42;
    private const float TerrainWidth = 800f;
    private const float TerrainHeight = 180f;
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
        td.heightmapResolution = 1025;
        td.alphamapResolution = 512;
        td.SetDetailResolution(512, 16);
        td.size = new Vector3(TerrainWidth, TerrainHeight, TerrainWidth);
        // recenter terrain so (0,0,0) is the middle
        terrain.transform.position = new Vector3(-TerrainWidth / 2f, 0f, -TerrainWidth / 2f);
        terrainPos = terrain.transform.position;

        UpgradeTerrainTextures(td);
        AddTerrainNoise(td);
        AddTerrainFeatures(td);
        SmoothHeightmap(td, 6);
        CarveRiverValley(td);  // after smoothing so banks stay defined
        RepaintSplatmap(td);
        SetupSkybox();

        ScatterNature(root.transform, terrain, terrainPos, rng, td);
        ScatterIndustrial(root.transform, terrain, terrainPos, rng, td);
        PlaceRuinClusters(root.transform, terrain, terrainPos, rng, td);
        PlaceAbandonedCamps(root.transform, terrain, terrainPos, rng, td);
        DecorateRiverbed(root.transform, terrain, terrainPos, rng, td);
        CreateRiverWater(root.transform, terrain, terrainPos, td);
        PaintTerrainGrass(td, rng);
        SetupAmbientParticles();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("homebase scenery dressed — save the scene to persist");
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

        // multi-octave Perlin noise — gives fractal micro-terrain like real landscapes
        // octave 1: broad rolling hills (wavelength ~400m)
        // octave 2: medium undulation (wavelength ~130m)
        // octave 3: small bumps (wavelength ~50m)
        // octave 4: micro detail (wavelength ~20m)
        float[] frequencies = { 0.0025f, 0.0075f, 0.02f, 0.05f };
        float[] amplitudes = { 18f, 7f, 3f, 1.2f };  // meters — bigger for 180m height range
        float[] offsets = { 0f, 137f, 293f, 431f };   // decorrelate octaves

        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                // world-space coordinates for noise sampling
                float wx = nx * TerrainWidth;
                float wz = nz * TerrainWidth;

                // distance from center — reduce noise near the flat factory area
                float dx = nx - 0.5f;
                float dz = nz - 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float outerMask = Mathf.Clamp01((dist - 0.06f) / 0.06f); // ramps 0→1 from factory edge outward

                float totalNoise = 0f;
                for (int o = 0; o < frequencies.Length; o++)
                {
                    float n = Mathf.PerlinNoise(
                        wx * frequencies[o] + offsets[o],
                        wz * frequencies[o] + offsets[o] + 50f);
                    // center around 0 instead of 0.5
                    totalNoise += (n - 0.5f) * amplitudes[o];
                }

                // broad valley depression in southern third where the river will go
                float riverValleyCenter = 0.72f;
                float valleyDist = Mathf.Abs(nz - riverValleyCenter);
                float valleyWidth = 0.08f; // ~64m each side = 128m total valley at 800m scale
                float valleyDepth = 14f;   // meters — deeper for 180m height range
                float valleyFactor = 0f;
                if (valleyDist < valleyWidth)
                {
                    float t = valleyDist / valleyWidth;
                    // concave parabolic profile — natural valley shape from erosion
                    valleyFactor = valleyDepth * (1f - t * t);
                }

                float heightDelta = (totalNoise * outerMask - valleyFactor) / TerrainHeight;
                heights[z, x] += heightDelta;
            }
        }

        td.SetHeights(0, 0, heights);
        Debug.Log("terrain noise added: 4 octaves + valley depression");
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

                float concrete = 0f, dirt = 0f, grass = 0f, gravel = 0f, rust = 0f;

                float flatEnd = 0.06f;   // ~48m radius = factory zone
                float transEnd = 0.10f;  // ~80m = transition to wilderness

                if (dist < flatEnd)
                {
                    concrete = 0.8f;
                    float rustNoise = Mathf.PerlinNoise(nx * 40f + 700f, nz * 40f + 700f);
                    if (rustNoise > 0.55f)
                    {
                        rust = (rustNoise - 0.55f) * 3f;
                        concrete -= rust * 0.5f;
                    }
                    float gravelNoise = Mathf.PerlinNoise(nx * 60f + 800f, nz * 60f + 800f);
                    if (gravelNoise > 0.65f)
                    {
                        gravel = (gravelNoise - 0.65f) * 3f;
                        concrete -= gravel * 0.3f;
                    }
                    dirt = Mathf.Max(0f, 1f - concrete - rust - gravel);
                }
                else if (dist < transEnd)
                {
                    float t = (dist - flatEnd) / (transEnd - flatEnd);
                    concrete = (1f - t) * 0.5f;
                    dirt = t * 0.4f;
                    gravel = 0.3f;
                    grass = t * 0.2f;
                    float rustNoise = Mathf.PerlinNoise(nx * 25f + 900f, nz * 25f + 900f);
                    rust = rustNoise > 0.5f ? (rustNoise - 0.5f) * 1.5f : 0f;
                }
                else
                {
                    grass = 0.6f;
                    dirt = 0.25f;

                    float noiseVal = Mathf.PerlinNoise(nx * 20f + 500f, nz * 20f + 500f);
                    if (noiseVal > 0.55f)
                    {
                        dirt += (noiseVal - 0.55f) * 2f;
                        grass -= (noiseVal - 0.55f);
                    }

                    float pathNoise = Mathf.PerlinNoise(nx * 8f + 100f, nz * 12f + 100f);
                    if (pathNoise > 0.6f && pathNoise < 0.65f)
                    {
                        gravel = 0.6f;
                        grass *= 0.3f;
                        dirt *= 0.3f;
                    }

                    if (steepness > 20f)
                    {
                        float rockBlend = Mathf.Clamp01((steepness - 20f) / 15f);
                        gravel += rockBlend * 0.5f;
                        grass *= (1f - rockBlend);
                    }
                }

                // riparian zone — moisture gradient based on distance to river
                float riverCenterZ = RiverCenterZ(nx);
                float riverDist = Mathf.Abs(nz - riverCenterZ);
                float channelHalf = 8f / TerrainWidth;
                float floodplainEdge = 80f / TerrainWidth; // total influence radius in meters

                if (riverDist < floodplainEdge)
                {
                    float riverBlend = 1f - riverDist / floodplainEdge;

                    if (riverDist < channelHalf)
                    {
                        // inside channel: mostly gravel/sand riverbed
                        gravel = 0.8f;
                        dirt = 0.2f;
                        grass = 0f;
                        concrete = 0f;
                        rust = 0f;
                    }
                    else if (riverDist < channelHalf + 12f / TerrainWidth)
                    {
                        // immediate banks: wet dirt and gravel
                        float bankT = (riverDist - channelHalf) / (12f / TerrainWidth);
                        gravel = 0.6f * (1f - bankT) + 0.3f * bankT;
                        dirt = 0.3f * (1f - bankT) + 0.5f * bankT;
                        grass = 0.1f * bankT;
                        concrete *= (1f - riverBlend);
                        rust *= (1f - riverBlend);
                    }
                    else
                    {
                        // floodplain: lush grass with dirt patches, decreasing moisture
                        float fpBlend = riverBlend * 0.7f;
                        grass += fpBlend * 0.4f;
                        dirt += fpBlend * 0.2f;
                        gravel += fpBlend * 0.1f;
                        concrete *= (1f - fpBlend);
                    }
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
        Debug.Log("splatmap repainted with 5 PBR layers");
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
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 800f;

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

        int treesPlaced = 0;
        int rocksPlaced = 0;
        int undergrowthPlaced = 0;

        // phase 1: tree clusters — scaled for 800m map
        int clusterCount = 70 + rng.Next(20);
        for (int c = 0; c < clusterCount; c++)
        {
            float cx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float cz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(cx * cx + cz * cz);
            if (dist < FlatRadius + 8f) continue;

            float clusterRadius = 8f + (float)rng.NextDouble() * 12f;
            int treesInCluster = 4 + rng.Next(7);

            for (int t = 0; t < treesInCluster; t++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = (float)rng.NextDouble() * clusterRadius;
                float wx = cx + Mathf.Cos(angle) * r;
                float wz = cz + Mathf.Sin(angle) * r;

                float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
                float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
                if (nx < 0.02f || nx > 0.98f || nz < 0.02f || nz > 0.98f) continue;

                float steepness = td.GetSteepness(nx, nz);
                if (steepness > 25f) continue;

                float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

                var prop = TreeProps[rng.Next(TreeProps.Length)];
                var instance = InstantiateProp(prop, rng);
                if (instance == null) continue;

                float yaw = (float)rng.NextDouble() * 360f;
                instance.transform.position = new Vector3(wx, y, wz);
                instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, yaw, rng, 4f);
                instance.transform.SetParent(treeParent);
                instance.isStatic = false;
                AddWindSway(instance, 1.2f + (float)rng.NextDouble() * 0.6f, 0.7f + (float)rng.NextDouble() * 0.3f);
                treesPlaced++;

                // 2-4 undergrowth around each tree
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

                    // grass-type undergrowth near trees
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

        // phase 2: scattered solo trees
        for (int i = 0; i < 500; i++)
        {
            float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist < FlatRadius + 5f) continue;

            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
            float steepness = td.GetSteepness(nx, nz);
            if (steepness > 25f) continue;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
            var prop = TreeProps[rng.Next(TreeProps.Length)];
            var instance = InstantiateProp(prop, rng);
            if (instance == null) continue;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 3f);
            instance.transform.SetParent(treeParent);
            instance.isStatic = false;
            AddWindSway(instance, 1.2f + (float)rng.NextDouble() * 0.6f, 0.7f + (float)rng.NextDouble() * 0.3f);
            treesPlaced++;
        }

        // phase 3: rocks — biome-aware selection
        for (int i = 0; i < 800; i++)
        {
            float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist < FlatRadius) continue;

            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

            // pick rock type based on location
            PropDef prop;
            if (IsNearRiverbed(wx, wz, 10f))
            {
                int idx = SandyRockIndices[rng.Next(SandyRockIndices.Length)];
                prop = RockProps[idx];
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

        // phase 4: undergrowth patches in open areas
        for (int i = 0; i < 1200; i++)
        {
            float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist < FlatRadius + 3f) continue;

            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
            float steepness = td.GetSteepness(nx, nz);
            if (steepness > 35f) continue;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);
            int propIdx = rng.Next(UndergrowthProps.Length);
            var prop = UndergrowthProps[propIdx];
            var instance = InstantiateProp(prop, rng);
            if (instance == null) continue;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 0f);
            instance.transform.SetParent(undergrowthParent);

            // only vegetation sways — logs, stones, planks, buckets are rigid
            bool isVegetation = propIdx < 4;
            instance.isStatic = !isVegetation;
            if (isVegetation)
                AddWindSway(instance, 1.8f + (float)rng.NextDouble() * 1.2f, 0.9f + (float)rng.NextDouble() * 0.4f);
            undergrowthPlaced++;
        }

        // phase 5: micro-detail clusters (rock + grass combos for natural groupings)
        for (int i = 0; i < 250; i++)
        {
            float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);
            if (dist < FlatRadius + 5f) continue;

            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
            if (td.GetSteepness(nx, nz) > 30f) continue;

            float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

            // small rock
            var rockProp = RockProps[rng.Next(3)]; // rock-a/b/c only
            var rock = InstantiateProp(new PropDef(rockProp.Path, 1.5f, 3f, true), rng);
            if (rock != null)
            {
                rock.transform.position = new Vector3(wx, y, wz);
                rock.transform.rotation = SlopeAlignedRotation(td, nx, nz, (float)rng.NextDouble() * 360f, rng, 8f);
                rock.transform.SetParent(rockParent);
                rock.isStatic = true;
                rocksPlaced++;
            }

            // 2-3 grass tufts around it
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

        // phase 6: riparian buffer — dense vegetation corridor along the river
        // real riparian ecology: dense trees 10-25m from water, undergrowth 3-10m
        for (int step = 0; step < RiverbedPoints.Count; step += 3)
        {
            var pt = RiverbedPoints[step];

            // dense undergrowth band: 3-10m from channel center (both sides)
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
                var uProp = UndergrowthProps[rng.Next(4)]; // grass variants only
                var uInst = InstantiateProp(uProp, rng);
                if (uInst == null) continue;

                uInst.transform.position = new Vector3(uwx, uy, uwz);
                uInst.transform.rotation = SlopeAlignedRotation(td, unx, unz, (float)rng.NextDouble() * 360f, rng, 0f);
                uInst.transform.SetParent(undergrowthParent);
                uInst.isStatic = false;
                AddWindSway(uInst, 2.5f + (float)rng.NextDouble() * 1f, 1.2f);
                undergrowthPlaced++;
            }

            // riparian tree band: 10-25m from channel center
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

                float dist = Mathf.Sqrt(twx * twx + twz * twz);
                if (dist < FlatRadius + 5f) continue;

                float ty = SampleWorldHeight(terrain, terrainPos, twx, twz);
                // prefer autumn trees near water (stand-in for willows/cottonwoods)
                var tProp = TreeProps[3 + rng.Next(3)]; // autumn variants
                var tInst = InstantiateProp(tProp, rng);
                if (tInst == null) continue;

                tInst.transform.position = new Vector3(twx, ty, twz);
                tInst.transform.rotation = SlopeAlignedRotation(td, tnx, tnz, (float)rng.NextDouble() * 360f, rng, 3f);
                tInst.transform.SetParent(treeParent);
                tInst.isStatic = false;
                AddWindSway(tInst, 1.5f + (float)rng.NextDouble() * 0.5f, 0.8f);
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

        for (int i = 0; i < 350; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = FlatRadius + (float)rng.NextDouble() * 60f - 10f;
            float wx = Mathf.Cos(angle) * radius;
            float wz = Mathf.Sin(angle) * radius;

            float y = terrain.SampleHeight(new Vector3(wx + TerrainWidth / 2f + terrainPos.x, 0f, wz + TerrainWidth / 2f + terrainPos.z));
            y += terrainPos.y;

            var prop = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
            if (prefab == null) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float scale = prop.MinScale + (float)rng.NextDouble() * (prop.MaxScale - prop.MinScale);

            float tiltX = (float)(rng.NextDouble() - 0.5) * 15f;
            float tiltZ = (float)(rng.NextDouble() - 0.5) * 15f;

            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = Quaternion.Euler(tiltX, (float)rng.NextDouble() * 360f, tiltZ);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(parent);
            instance.isStatic = true;

            placed++;
        }

        Debug.Log($"industrial debris placed: {placed}");
    }

    private static void PlaceRuinClusters(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("RuinClusters").transform;
        parent.SetParent(root);

        Vector2[] clusterCenters = {
            new(150f, 80f),
            new(-140f, 120f),
            new(-100f, -180f),
            new(180f, -150f),
            new(250f, 20f),
            new(-200f, -80f),
            new(80f, 220f),
            new(-280f, 150f),
            new(300f, -200f),
            new(-60f, 280f),
        };

        int totalPlaced = 0;

        foreach (var center in clusterCenters)
        {
            var cluster = new GameObject($"Ruin_{totalPlaced}").transform;
            cluster.SetParent(parent);

            int pieceCount = 4 + rng.Next(5);
            for (int p = 0; p < pieceCount; p++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * 12f;
                float oz = (float)(rng.NextDouble() - 0.5) * 12f;
                float wx = center.x + ox;
                float wz = center.y + oz;

                float y = terrain.SampleHeight(new Vector3(wx + TerrainWidth / 2f + terrainPos.x, 0f, wz + TerrainWidth / 2f + terrainPos.z));
                y += terrainPos.y;

                var prop = RuinProps[rng.Next(RuinProps.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
                if (prefab == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

                float tiltX = (float)(rng.NextDouble() - 0.5) * 30f;
                float tiltZ = (float)(rng.NextDouble() - 0.5) * 20f;
                if (rng.NextDouble() < 0.2f)
                    tiltX = 80f + (float)rng.NextDouble() * 10f;

                instance.transform.position = new Vector3(wx, y, wz);
                instance.transform.rotation = Quaternion.Euler(tiltX, (float)rng.NextDouble() * 360f, tiltZ);
                instance.transform.localScale = Vector3.one * prop.MinScale;
                instance.transform.SetParent(cluster);
                instance.isStatic = true;

                totalPlaced++;
            }

            for (int d = 0; d < 3 + rng.Next(4); d++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * 16f;
                float oz = (float)(rng.NextDouble() - 0.5) * 16f;
                float wx = center.x + ox;
                float wz = center.y + oz;

                float y = terrain.SampleHeight(new Vector3(wx + TerrainWidth / 2f + terrainPos.x, 0f, wz + TerrainWidth / 2f + terrainPos.z));
                y += terrainPos.y;

                var prop = IndustrialProps[rng.Next(IndustrialProps.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
                if (prefab == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                float scale = prop.MinScale + (float)rng.NextDouble() * (prop.MaxScale - prop.MinScale);
                float tilt = (float)(rng.NextDouble() - 0.5) * 25f;

                instance.transform.position = new Vector3(wx, y, wz);
                instance.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, 0f);
                instance.transform.localScale = Vector3.one * scale;
                instance.transform.SetParent(cluster);
                instance.isStatic = true;

                totalPlaced++;
            }

            // undergrowth reclaiming ruins
            for (int u = 0; u < 4 + rng.Next(5); u++)
            {
                float ox = (float)(rng.NextDouble() - 0.5) * 14f;
                float oz = (float)(rng.NextDouble() - 0.5) * 14f;
                float wx = center.x + ox;
                float wz = center.y + oz;
                float y = SampleWorldHeight(terrain, terrainPos, wx, wz);

                var uprop = UndergrowthProps[rng.Next(4)];
                var uinst = InstantiateProp(uprop, rng);
                if (uinst == null) continue;

                uinst.transform.position = new Vector3(wx, y, wz);
                uinst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                uinst.transform.SetParent(cluster);
                uinst.isStatic = false;
                AddWindSway(uinst, 2f, 1f);
                totalPlaced++;
            }
        }

        Debug.Log($"ruin cluster pieces placed: {totalPlaced}");
    }

    // === ABANDONED CAMPS ===

    private static void PlaceAbandonedCamps(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("AbandonedCamps").transform;
        parent.SetParent(root);

        Vector2[] campCenters = {
            new(120f, 60f),
            new(-130f, 100f),
            new(70f, -200f),
            new(-180f, -90f),
            new(100f, 250f),
            new(-250f, 40f),
            new(300f, 100f),
            new(-80f, -300f),
        };

        int totalPlaced = 0;

        foreach (var center in campCenters)
        {
            float cnx = (center.x + TerrainWidth / 2f) / TerrainWidth;
            float cnz = (center.y + TerrainWidth / 2f) / TerrainWidth;
            if (td.GetSteepness(cnx, cnz) > 15f) continue;

            var camp = new GameObject($"Camp_{totalPlaced}").transform;
            camp.SetParent(parent);

            float cy = SampleWorldHeight(terrain, terrainPos, center.x, center.y);

            // campfire at center
            var fire = InstantiateProp(CampProps[0], rng);
            if (fire != null)
            {
                fire.transform.position = new Vector3(center.x, cy, center.y);
                fire.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                fire.transform.SetParent(camp);
                fire.isStatic = true;
                totalPlaced++;
            }

            // 1-2 tents facing the fire
            int tentCount = 1 + rng.Next(2);
            for (int t = 0; t < tentCount; t++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 3f + (float)rng.NextDouble() * 4f;
                float tx = center.x + Mathf.Cos(angle) * dist;
                float tz = center.y + Mathf.Sin(angle) * dist;
                float ty = SampleWorldHeight(terrain, terrainPos, tx, tz);

                float facingAngle = Mathf.Atan2(center.y - tz, center.x - tx) * Mathf.Rad2Deg;
                var tentProp = CampProps[1 + rng.Next(3)];
                var tent = InstantiateProp(tentProp, rng);
                if (tent == null) continue;

                tent.transform.position = new Vector3(tx, ty, tz);
                tent.transform.rotation = Quaternion.Euler(0f, facingAngle + ((float)rng.NextDouble() - 0.5f) * 30f, 0f);
                tent.transform.SetParent(camp);
                tent.isStatic = true;
                totalPlaced++;
            }

            // 1-3 bedrolls
            for (int b = 0; b < 1 + rng.Next(3); b++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 2f + (float)rng.NextDouble() * 5f;
                float bx = center.x + Mathf.Cos(angle) * dist;
                float bz = center.y + Mathf.Sin(angle) * dist;
                float by = SampleWorldHeight(terrain, terrainPos, bx, bz);

                var bedProp = CampProps[4 + rng.Next(2)];
                var bedroll = InstantiateProp(bedProp, rng);
                if (bedroll == null) continue;

                bedroll.transform.position = new Vector3(bx, by, bz);
                bedroll.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                bedroll.transform.SetParent(camp);
                bedroll.isStatic = true;
                totalPlaced++;
            }

            // 2-5 scattered bottles/debris
            for (int d = 0; d < 2 + rng.Next(4); d++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 1f + (float)rng.NextDouble() * 6f;
                float dx = center.x + Mathf.Cos(angle) * dist;
                float dz = center.y + Mathf.Sin(angle) * dist;
                float dy = SampleWorldHeight(terrain, terrainPos, dx, dz);

                var debrisProp = CampProps[6 + rng.Next(2)];
                var debris = InstantiateProp(debrisProp, rng);
                if (debris == null) continue;

                float tilt = (float)(rng.NextDouble() - 0.5) * 40f;
                debris.transform.position = new Vector3(dx, dy, dz);
                debris.transform.rotation = Quaternion.Euler(tilt, (float)rng.NextDouble() * 360f, tilt * 0.3f);
                debris.transform.SetParent(camp);
                debris.isStatic = true;
                totalPlaced++;
            }

            // undergrowth growing through the abandoned camp
            for (int u = 0; u < 3 + rng.Next(4); u++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = (float)rng.NextDouble() * 8f;
                float ux = center.x + Mathf.Cos(angle) * dist;
                float uz = center.y + Mathf.Sin(angle) * dist;
                float uy = SampleWorldHeight(terrain, terrainPos, ux, uz);

                var uProp = UndergrowthProps[rng.Next(4)];
                var uinst = InstantiateProp(uProp, rng);
                if (uinst == null) continue;

                uinst.transform.position = new Vector3(ux, uy, uz);
                uinst.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                uinst.transform.SetParent(camp);
                uinst.isStatic = false;
                AddWindSway(uinst, 2f + (float)rng.NextDouble() * 1f, 1f + (float)rng.NextDouble() * 0.3f);
                totalPlaced++;
            }
        }

        Debug.Log($"abandoned camp pieces placed: {totalPlaced}");
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

    private static float SampleWorldHeight(Terrain terrain, Vector3 terrainPos, float wx, float wz)
    {
        float y = terrain.SampleHeight(new Vector3(
            wx + TerrainWidth / 2f + terrainPos.x, 0f,
            wz + TerrainWidth / 2f + terrainPos.z));
        return y + terrainPos.y;
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

        return instance;
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

    private static void AddWindSway(GameObject go, float amount, float speed)
    {
        var sway = go.AddComponent<WindSway>();
        var so = new UnityEditor.SerializedObject(sway);
        so.FindProperty("_swayAmount").floatValue = amount;
        so.FindProperty("_swaySpeed").floatValue = speed;
        so.ApplyModifiedPropertiesWithoutUndo();
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
