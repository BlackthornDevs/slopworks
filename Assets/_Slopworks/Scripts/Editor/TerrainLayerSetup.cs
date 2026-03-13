using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates TerrainLayer assets for all PolyHaven texture sets and adds them to the active terrain.
/// Run from Slopworks > Setup Terrain Layers.
/// </summary>
public static class TerrainLayerSetup
{
    private const string TextureRoot = "Assets/_Slopworks/Art/Textures/Terrain";
    private const string LayerOutputPath = "Assets/_Slopworks/Art/Terrain/HomeBase";

    // PolyHaven texture sets: folder name, display name, tiling size
    private static readonly (string folder, string name, float tileSize)[] PolyHavenSets =
    {
        ("SparseGrass",       "Sparse Grass",        8f),
        ("LeafyGrass",        "Leafy Grass",         8f),
        ("ForestGround01",    "Forest Ground",       8f),
        ("AerialGrassRock",   "Grass Rock",         10f),
        ("Rock04",            "Rock",               10f),
        ("MossyRock",         "Mossy Rock",         10f),
        ("AerialGroundRock",  "Ground Rock",        10f),
        ("Dirt",              "Dirt",                8f),
        ("BrownMudRocks01",   "Mud Rocks",           8f),
        ("BrownMudLeaves01",  "Mud Leaves",          8f),
        ("Cobblestone05",     "Cobblestone",         6f),
        ("GreyStonePath",     "Stone Path",          6f),
        ("RiverSmallRocks",   "River Rocks",         8f),
        ("CoastSandRocks02",  "Sand Rocks",         10f),
    };

    [MenuItem("Slopworks/Setup Terrain Layers")]
    public static void Setup()
    {
        int created = 0;
        int skipped = 0;

        foreach (var (folder, name, tileSize) in PolyHavenSets)
        {
            string layerPath = $"{LayerOutputPath}/TerrainLayer_PH_{folder}.asset";

            // skip if already exists
            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) != null)
            {
                skipped++;
                continue;
            }

            // find diffuse texture
            string diffPath = FindTexture(folder, "_diff_");
            if (diffPath == null)
            {
                Debug.LogWarning($"terrain layer setup: no diffuse texture found in {folder}");
                continue;
            }

            // find normal map
            string norPath = FindTexture(folder, "_nor_gl_");

            var layer = new TerrainLayer();
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
            layer.tileSize = new Vector2(tileSize, tileSize);

            if (norPath != null)
            {
                // ensure normal map import settings are correct
                var importer = AssetImporter.GetAtPath(norPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }

                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(norPath);
                layer.normalScale = 1f;
            }

            AssetDatabase.CreateAsset(layer, layerPath);
            created++;
            Debug.Log($"terrain layer setup: created {name} ({folder})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // add all layers to active terrain
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            AddLayersToTerrain(terrain);
            Debug.Log($"terrain layer setup: done. created {created}, skipped {skipped} (already existed). layers added to terrain.");
        }
        else
        {
            Debug.Log($"terrain layer setup: done. created {created}, skipped {skipped}. no active terrain found -- open your terrain scene and run again to add layers.");
        }
    }

    private static void AddLayersToTerrain(Terrain terrain)
    {
        var existingLayers = terrain.terrainData.terrainLayers;
        var allLayers = new System.Collections.Generic.List<TerrainLayer>();

        // strip null entries -- MicroSplat crashes on null terrain layers
        int removed = 0;
        foreach (var layer in existingLayers)
        {
            if (layer != null)
                allLayers.Add(layer);
            else
                removed++;
        }
        if (removed > 0)
            Debug.Log($"terrain layer setup: removed {removed} null terrain layer entries");

        // find all TerrainLayer assets in output folder
        var guids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { LayerOutputPath });
        int added = 0;

        foreach (var guid in guids)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
            if (layer == null) continue;

            // skip if already on terrain
            bool exists = false;
            foreach (var existing in allLayers)
            {
                if (existing == layer) { exists = true; break; }
            }
            if (exists) continue;

            allLayers.Add(layer);
            added++;
        }

        if (added > 0)
        {
            terrain.terrainData.terrainLayers = allLayers.ToArray();
            EditorUtility.SetDirty(terrain.terrainData);
            Debug.Log($"terrain layer setup: added {added} new layers to terrain (total: {allLayers.Count})");
        }
    }

    private static string FindTexture(string folder, string pattern)
    {
        string folderPath = $"{TextureRoot}/{folder}";
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(pattern))
                return path;
        }
        return null;
    }
}
