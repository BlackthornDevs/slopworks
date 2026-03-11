using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that adds BuildingSnapPoint child objects to all building prefabs
/// found under Resources/Prefabs/Buildings/. Scans subfolders automatically.
/// Run via Tools > Slopworks > Add Snap Points to Prefabs.
/// </summary>
public static class SnapPointPrefabSetup
{
    private const string BuildingsRoot = "Assets/_Slopworks/Resources/Prefabs/Buildings";

    [MenuItem("Tools/Slopworks/Add Snap Points to Prefabs")]
    public static void AddSnapPointsToPrefabs()
    {
        int total = 0;

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { BuildingsRoot });
        var paths = new List<string>();
        foreach (var guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));

        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);

            var existing = root.GetComponentsInChildren<BuildingSnapPoint>();
            if (existing.Length > 0)
            {
                Debug.Log($"snap setup: skipping {prefab.name} -- already has {existing.Length} snap points");
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"snap setup: no renderer on {path}");
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            var center = bounds.center;
            var ext = bounds.extents;

            var category = DetectCategory(path);
            Debug.Log($"snap setup: {prefab.name} -- category={category}, center={center}, extents={ext}");

            int count = GenerateSnapPoints(root, center, ext, renderers[0], category);
            total += count;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"snap setup: added {count} snap points to {prefab.name}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"snap setup: done -- {total} snap points added across {paths.Count} prefabs");
    }

    private static BuildingCategory DetectCategory(string path)
    {
        if (path.Contains("/Ramps/")) return BuildingCategory.Ramp;
        if (path.Contains("/Walls/")) return BuildingCategory.Wall;
        if (path.Contains("/Machines/")) return BuildingCategory.Machine;
        if (path.Contains("/Storage/")) return BuildingCategory.Storage;
        return BuildingCategory.Foundation;
    }

    private static int GenerateSnapPoints(GameObject root, Vector3 center, Vector3 ext,
        Renderer renderer, BuildingCategory category)
    {
        bool isRamp = category == BuildingCategory.Ramp;
        bool isMachine = category == BuildingCategory.Machine;
        bool isStorage = category == BuildingCategory.Storage;

        var cardinals = new[]
        {
            (name: "North", dir: Vector3.forward, offset: new Vector3(0, 0, ext.z)),
            (name: "South", dir: Vector3.back,    offset: new Vector3(0, 0, -ext.z)),
            (name: "East",  dir: Vector3.right,   offset: new Vector3(ext.x, 0, 0)),
            (name: "West",  dir: Vector3.left,    offset: new Vector3(-ext.x, 0, 0)),
        };

        int count = 0;

        foreach (var (name, dir, offset) in cardinals)
        {
            bool isXFace = Mathf.Abs(dir.x) > 0.5f;
            var faceSize = isXFace
                ? new Vector2(ext.z * 2, ext.y * 2)
                : new Vector2(ext.x * 2, ext.y * 2);

            if (isRamp)
            {
                if (name != "South")
                {
                    AddSnapPoint(root, $"SnapPoint_{name}_Bot", center + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                    count++;
                }
            }
            else if (isMachine || isStorage)
            {
                // Machines/Storage: bottom edge only -- side-by-side alignment
                AddSnapPoint(root, $"SnapPoint_{name}_Bot", center + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                count++;
            }
            else
            {
                // Structural: full 3-tier per cardinal
                AddSnapPoint(root, $"SnapPoint_{name}_Top", center + offset + new Vector3(0, ext.y, 0), dir, faceSize);
                AddSnapPoint(root, $"SnapPoint_{name}_Mid", center + offset, dir, faceSize);
                AddSnapPoint(root, $"SnapPoint_{name}_Bot", center + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                count += 3;
            }
        }

        var topBotSize = new Vector2(ext.x * 2, ext.z * 2);

        if (isRamp)
        {
            var (highY, lowY) = GetRampEdgeHeights(renderer);
            AddSnapPoint(root, "SnapPoint_HighEdge",
                new Vector3(center.x, highY, center.z + ext.z), Vector3.forward,
                new Vector2(ext.x * 2, 0.1f));
            AddSnapPoint(root, "SnapPoint_LowEdge",
                new Vector3(center.x, lowY, center.z - ext.z), Vector3.back,
                new Vector2(ext.x * 2, 0.1f));
            AddSnapPoint(root, "SnapPoint_Center_Bot",
                center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
            count += 3;
        }
        else if (isMachine)
        {
            // Machines: Center_Bot only, no stacking
            AddSnapPoint(root, "SnapPoint_Center_Bot",
                center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
            count++;
        }
        else if (isStorage)
        {
            // Storage: Center_Top + Center_Bot for vertical stacking
            AddSnapPoint(root, "SnapPoint_Center_Top",
                center + new Vector3(0, ext.y, 0), Vector3.up, topBotSize);
            AddSnapPoint(root, "SnapPoint_Center_Bot",
                center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
            count += 2;
        }
        else
        {
            // Structural: both Top and Bottom center
            AddSnapPoint(root, "SnapPoint_Center_Top", center + new Vector3(0, ext.y, 0), Vector3.up, topBotSize);
            AddSnapPoint(root, "SnapPoint_Center_Bot", center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
            count += 2;
        }

        return count;
    }

    private static Vector3 RoundVec(Vector3 v)
    {
        return new Vector3(
            Mathf.Round(v.x * 100f) / 100f,
            Mathf.Round(v.y * 100f) / 100f,
            Mathf.Round(v.z * 100f) / 100f);
    }

    private static Vector2 RoundVec2(Vector2 v)
    {
        return new Vector2(
            Mathf.Round(v.x * 100f) / 100f,
            Mathf.Round(v.y * 100f) / 100f);
    }

    private static (float highY, float lowY) GetRampEdgeHeights(Renderer renderer)
    {
        return (renderer.bounds.max.y, renderer.bounds.min.y);
    }

    private static void AddSnapPoint(GameObject parent, string name, Vector3 worldPos, Vector3 normal, Vector2 surfaceSize)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = RoundVec(parent.transform.InverseTransformPoint(worldPos));
        child.layer = PhysicsLayers.SnapPoints;
        var snap = child.AddComponent<BuildingSnapPoint>();
        snap.SurfaceSize = RoundVec2(surfaceSize);

        var so = new SerializedObject(snap);
        var normalProp = so.FindProperty("_normalOverride");
        normalProp.vector3Value = normal;
        so.ApplyModifiedPropertiesWithoutUndo();

        var sphere = child.AddComponent<SphereCollider>();
        sphere.radius = 0.5f;
        sphere.isTrigger = true;
    }
}
