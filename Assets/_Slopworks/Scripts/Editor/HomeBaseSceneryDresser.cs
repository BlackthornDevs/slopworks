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
    private const float TerrainWidth = 200f;
    private const float TerrainHeight = 60f;
    private const float FlatRadius = 50f; // world-space radius of flat center
    private const float PropScale = 3f; // Kenney models are ~0.25m, need 3x for world scale

    // PBR texture paths
    private static readonly string[] TextureSets = {
        "Assets/_Slopworks/Art/Textures/Terrain/Concrete034",   // 0: factory floor
        "Assets/_Slopworks/Art/Textures/Terrain/Ground037",     // 1: dirt
        "Assets/_Slopworks/Art/Textures/Terrain/Ground054",     // 2: grass/soil
        "Assets/_Slopworks/Art/Textures/Terrain/Gravel022",     // 3: gravel paths
        "Assets/_Slopworks/Art/Textures/Terrain/Rust004",       // 4: rust stains
    };

    private static readonly Vector2[] TileSizes = {
        new(8f, 8f),   // concrete — tighter tiling for detail
        new(12f, 12f),  // dirt
        new(15f, 15f),  // grass/soil — larger to avoid repetition
        new(10f, 10f),  // gravel
        new(6f, 6f),    // rust — small patches
    };

    // Prop definitions: path, min scale, max scale
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

    private static readonly PropDef[] NatureProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-a.fbx", 2f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-b.fbx", 2f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-c.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-flat.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/rock-flat-grass.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree.fbx", 3f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-tall.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-trunk.fbx", 2f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn.fbx", 3f, 5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/tree-autumn-tall.fbx", 3f, 6f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass-large.fbx", 3f, 5f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/grass.fbx", 3f, 4f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass-large.fbx", 3f, 5f, false),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/patch-grass.fbx", 3f, 4f, false),
    };

    private static readonly PropDef[] IndustrialProps = {
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/barrel.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/barrel-open.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box.fbx", 2.5f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-large.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/box-open.fbx", 2.5f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/fence-fortified.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/metal-panel-screws.fbx", 3f, 4f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/signpost.fbx", 3f, 3.5f),
        new("Assets/_Slopworks/Art/Kenney/survival-kit/Models/workbench.fbx", 3f, 3.5f),
    };

    private static readonly PropDef[] RuinProps = {
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-wall.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-window.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-doorway.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/structure-tall.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover.fbx", 1f, 1f),
        new("Assets/_Slopworks/Art/Kenney/conveyor-kit/Models/cover-stripe.fbx", 1f, 1f),
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

        // clear previous scenery
        var existing = GameObject.Find("HomeBaseScenery");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var root = new GameObject("HomeBaseScenery");
        root.isStatic = true;

        var rng = new System.Random(Seed);
        var td = terrain.terrainData;
        var terrainPos = terrain.transform.position;

        UpgradeTerrainTextures(td);
        AddTerrainFeatures(td);
        RepaintSplatmap(td);
        SetupSkybox();

        ScatterNature(root.transform, terrain, terrainPos, rng, td);
        ScatterIndustrial(root.transform, terrain, terrainPos, rng, td);
        PlaceRuinClusters(root.transform, terrain, terrainPos, rng, td);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("homebase scenery dressed — save the scene to persist");
    }

    private static void UpgradeTerrainTextures(TerrainData td)
    {
        var layers = new TerrainLayer[TextureSets.Length];

        for (int i = 0; i < TextureSets.Length; i++)
        {
            var layer = new TerrainLayer();
            string folder = TextureSets[i];
            string setName = System.IO.Path.GetFileName(folder);

            // color/diffuse
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{setName}_1K-PNG_Color.png");
            if (color != null) layer.diffuseTexture = color;

            // normal map
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{setName}_1K-PNG_NormalGL.png");
            if (normal != null)
            {
                // ensure texture import type is Normal map
                string normalPath = AssetDatabase.GetAssetPath(normal);
                var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                layer.normalMapTexture = normal;
                layer.normalScale = 1f;
            }

            // mask map (roughness in G channel for URP terrain)
            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{setName}_1K-PNG_Roughness.png");
            if (roughness != null) layer.maskMapTexture = roughness;

            layer.tileSize = TileSizes[i];
            layer.tileOffset = Vector2.zero;

            string layerPath = $"Assets/_Slopworks/Art/Terrain/HomeBase/TerrainLayer_PBR_{setName}.asset";
            AssetDatabase.CreateAsset(layer, layerPath);
            layers[i] = layer;
        }

        td.terrainLayers = layers;
        Debug.Log($"terrain textures upgraded: {layers.Length} PBR layers");
    }

    private static void AddTerrainFeatures(TerrainData td)
    {
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);
        var rng = new System.Random(Seed + 1);

        // impact craters — 4 craters in the hill zone
        Vector2[] craterCenters = {
            new(0.25f, 0.25f),
            new(0.72f, 0.30f),
            new(0.30f, 0.75f),
            new(0.78f, 0.70f),
        };
        float[] craterRadii = { 0.04f, 0.035f, 0.045f, 0.03f };
        float[] craterDepths = { 3f, 2f, 4f, 2.5f };

        for (int c = 0; c < craterCenters.Length; c++)
        {
            // skip craters that fall in the flat zone
            float dx = craterCenters[c].x - 0.5f;
            float dz = craterCenters[c].y - 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist < 0.3f) continue; // inside flat zone

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
                        // parabolic crater bowl with raised rim
                        float t = cdist / craterR;
                        float bowl = depth * (1f - t * t); // depression
                        float rim = depth * 0.3f * Mathf.Exp(-((t - 1f) * (t - 1f)) / 0.02f); // rim bump
                        heights[z, x] = heights[z, x] - bowl + rim;
                    }
                    else if (cdist < craterR * 1.5f)
                    {
                        // gentle rim falloff
                        float t = (cdist - craterR) / (craterR * 0.5f);
                        float rim = depth * 0.2f * (1f - t);
                        heights[z, x] += Mathf.Max(0f, rim);
                    }
                }
            }
        }

        // dry riverbed — sinuous path through the terrain
        float riverZ = 0.15f; // start near north edge
        float riverX = 0.4f + (float)rng.NextDouble() * 0.2f;
        float riverWidth = 0.015f;
        float riverDepth = 1.5f / TerrainHeight;

        for (int step = 0; step < 200; step++)
        {
            // meander
            riverX += (float)(rng.NextDouble() - 0.5) * 0.012f;
            riverZ += 0.004f;
            riverX = Mathf.Clamp(riverX, 0.1f, 0.9f);

            if (riverZ > 0.85f) break;

            // skip the flat center zone
            float rdx = riverX - 0.5f;
            float rdz = riverZ - 0.5f;
            if (Mathf.Sqrt(rdx * rdx + rdz * rdz) < 0.28f) continue;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float ddx = nx - riverX;
                    float ddz = nz - riverZ;
                    float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);

                    if (d < riverWidth)
                    {
                        float t = d / riverWidth;
                        float carve = riverDepth * (1f - t * t);
                        heights[z, x] -= carve;
                    }
                }
            }
        }

        // small ridges — 3 ridges in the hills
        for (int r = 0; r < 3; r++)
        {
            float rx = 0.15f + (float)rng.NextDouble() * 0.7f;
            float rz = 0.15f + (float)rng.NextDouble() * 0.7f;
            float angle = (float)rng.NextDouble() * Mathf.PI;
            float ridgeLen = 0.08f + (float)rng.NextDouble() * 0.06f;
            float ridgeHeight = (1.5f + (float)rng.NextDouble() * 2f) / TerrainHeight;
            float ridgeWidth = 0.012f;

            // skip if inside flat zone
            float rrdx = rx - 0.5f;
            float rrdz = rz - 0.5f;
            if (Mathf.Sqrt(rrdx * rrdx + rrdz * rrdz) < 0.3f) continue;

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float ddx = nx - rx;
                    float ddz = nz - rz;

                    // project onto ridge direction
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
        Debug.Log("terrain features added: craters, riverbed, ridges");
    }

    private static void RepaintSplatmap(TerrainData td)
    {
        // 5 layers: concrete, dirt, grass/soil, gravel, rust
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

                float flatEnd = 0.25f;
                float transEnd = 0.35f;

                if (dist < flatEnd)
                {
                    // center: mostly concrete with some rust patches
                    concrete = 0.8f;
                    float rustNoise = Mathf.PerlinNoise(nx * 40f + 700f, nz * 40f + 700f);
                    if (rustNoise > 0.55f)
                    {
                        rust = (rustNoise - 0.55f) * 3f;
                        concrete -= rust * 0.5f;
                    }
                    // gravel cracks
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
                    // transition: concrete fading to dirt and gravel
                    float t = (dist - flatEnd) / (transEnd - flatEnd);
                    concrete = (1f - t) * 0.5f;
                    dirt = t * 0.4f;
                    gravel = 0.3f;
                    grass = t * 0.2f;
                    // rust near industrial areas
                    float rustNoise = Mathf.PerlinNoise(nx * 25f + 900f, nz * 25f + 900f);
                    rust = rustNoise > 0.5f ? (rustNoise - 0.5f) * 1.5f : 0f;
                }
                else
                {
                    // hills: grass dominant with dirt patches
                    grass = 0.6f;
                    dirt = 0.25f;

                    // noise variation
                    float noiseVal = Mathf.PerlinNoise(nx * 20f + 500f, nz * 20f + 500f);
                    if (noiseVal > 0.55f)
                    {
                        dirt += (noiseVal - 0.55f) * 2f;
                        grass -= (noiseVal - 0.55f);
                    }

                    // gravel on paths (use a different noise frequency for path-like streaks)
                    float pathNoise = Mathf.PerlinNoise(nx * 8f + 100f, nz * 12f + 100f);
                    if (pathNoise > 0.6f && pathNoise < 0.65f)
                    {
                        gravel = 0.6f;
                        grass *= 0.3f;
                        dirt *= 0.3f;
                    }

                    // rock on steep slopes
                    if (steepness > 20f)
                    {
                        float rockBlend = Mathf.Clamp01((steepness - 20f) / 15f);
                        gravel += rockBlend * 0.5f;
                        grass *= (1f - rockBlend);
                    }
                }

                // normalize
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
                    alphas[z, x, 1] = 1f; // fallback to dirt
                }
            }
        }

        td.SetAlphamaps(0, 0, alphas);
        Debug.Log("splatmap repainted with 5 PBR layers");
    }

    private static void SetupSkybox()
    {
        // load the industrial sunset HDR as a cubemap material
        var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/_Slopworks/Art/Skybox/industrial_sunset_puresky_2k.hdr");

        if (hdr == null)
        {
            Debug.LogWarning("skybox HDR not found, skipping");
            return;
        }

        // ensure the HDR texture is imported as a cubemap for skybox use
        string hdrPath = AssetDatabase.GetAssetPath(hdr);
        var importer = AssetImporter.GetAtPath(hdrPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.SaveAndReimport();
            hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(hdrPath);
        }

        // create skybox material
        string matPath = "Assets/_Slopworks/Materials/Environment/Skybox_IndustrialSunset.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            // ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Slopworks/Materials/Environment"))
                AssetDatabase.CreateFolder("Assets/_Slopworks/Materials", "Environment");

            mat = new Material(Shader.Find("Skybox/Cubemap"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.shader = Shader.Find("Skybox/Cubemap");
        mat.SetTexture("_Tex", hdr);
        mat.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(mat);

        RenderSettings.skybox = mat;

        // update fog color to blend with the sunset
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.45f, 0.32f, 0.22f);
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 200f;

        // ambient from skybox
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        Debug.Log("skybox set: industrial sunset");
    }

    private static void ScatterNature(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("Nature").transform;
        parent.SetParent(root);

        int placed = 0;
        int attempts = 600;

        for (int i = 0; i < attempts; i++)
        {
            // random point on terrain, biased toward hills
            float wx = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float wz = (float)rng.NextDouble() * TerrainWidth - TerrainWidth / 2f;
            float dist = Mathf.Sqrt(wx * wx + wz * wz);

            // only place in hill zone (outside flat center + small buffer)
            if (dist < FlatRadius + 5f) continue;

            // sample terrain height
            float y = terrain.SampleHeight(new Vector3(wx, 0f, wz) + new Vector3(TerrainWidth / 2f + terrainPos.x, 0f, TerrainWidth / 2f + terrainPos.z));
            y += terrainPos.y;

            // skip steep slopes for trees (rocks are fine)
            float nx = (wx + TerrainWidth / 2f) / TerrainWidth;
            float nz = (wz + TerrainWidth / 2f) / TerrainWidth;
            float steepness = td.GetSteepness(nx, nz);

            PropDef prop;
            if (steepness > 30f)
            {
                // only rocks on steep slopes
                prop = NatureProps[rng.Next(5)]; // first 5 are rocks
            }
            else
            {
                prop = NatureProps[rng.Next(NatureProps.Length)];
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
            if (prefab == null) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float scale = prop.MinScale + (float)rng.NextDouble() * (prop.MaxScale - prop.MinScale);
            instance.transform.position = new Vector3(wx, y, wz);
            instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(parent);
            instance.isStatic = true;

            if (prop.AddCollider && instance.GetComponentInChildren<Collider>() == null)
            {
                instance.AddComponent<MeshCollider>();
            }

            placed++;
        }

        Debug.Log($"nature props placed: {placed}");
    }

    private static void ScatterIndustrial(Transform root, Terrain terrain, Vector3 terrainPos, System.Random rng, TerrainData td)
    {
        var parent = new GameObject("IndustrialDebris").transform;
        parent.SetParent(root);

        int placed = 0;
        int attempts = 200;

        for (int i = 0; i < attempts; i++)
        {
            // ring around flat zone edge — industrial debris
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = FlatRadius + (float)rng.NextDouble() * 25f - 5f; // -5 to +20 from edge
            float wx = Mathf.Cos(angle) * radius;
            float wz = Mathf.Sin(angle) * radius;

            float y = terrain.SampleHeight(new Vector3(wx + TerrainWidth / 2f + terrainPos.x, 0f, wz + TerrainWidth / 2f + terrainPos.z));
            y += terrainPos.y;

            var prop = IndustrialProps[rng.Next(IndustrialProps.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prop.Path);
            if (prefab == null) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            float scale = prop.MinScale + (float)rng.NextDouble() * (prop.MaxScale - prop.MinScale);

            // slight tilt for debris look
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

        // 5 ruin clusters around the map — collapsed factory buildings
        Vector2[] clusterCenters = {
            new(60f, 30f),
            new(-55f, 45f),
            new(-40f, -60f),
            new(50f, -50f),
            new(70f, 0f),
        };

        int totalPlaced = 0;

        foreach (var center in clusterCenters)
        {
            var cluster = new GameObject($"Ruin_{totalPlaced}").transform;
            cluster.SetParent(parent);

            float cy = terrain.SampleHeight(new Vector3(center.x + TerrainWidth / 2f + terrainPos.x, 0f, center.y + TerrainWidth / 2f + terrainPos.z));
            cy += terrainPos.y;

            // place 4-8 wall/structure pieces per cluster
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

                // ruins are tilted — some fallen, some leaning
                float tiltX = (float)(rng.NextDouble() - 0.5) * 30f;
                float tiltZ = (float)(rng.NextDouble() - 0.5) * 20f;
                if (rng.NextDouble() < 0.2f)
                {
                    // fully fallen wall
                    tiltX = 80f + (float)rng.NextDouble() * 10f;
                }

                instance.transform.position = new Vector3(wx, y, wz);
                instance.transform.rotation = Quaternion.Euler(tiltX, (float)rng.NextDouble() * 360f, tiltZ);
                instance.transform.localScale = Vector3.one * prop.MinScale;
                instance.transform.SetParent(cluster);
                instance.isStatic = true;

                totalPlaced++;
            }

            // add some debris (barrels, boxes) around each ruin
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
        }

        Debug.Log($"ruin cluster pieces placed: {totalPlaced}");
    }
}
