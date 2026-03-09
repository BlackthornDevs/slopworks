using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that adds BuildingSnapPoint child objects to all building prefabs.
/// Run via Tools > Slopworks > Add Snap Points to Prefabs.
/// </summary>
public static class SnapPointPrefabSetup
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Foundations/SLAB_1m.prefab",
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Foundations/SLAB_2m.prefab",
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Foundations/SLAB_4m.prefab",
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Walls/WALL_0.5m.prefab",
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Ramps/RAMP 4x1.prefab",
        "Assets/_Slopworks/Resources/Prefabs/Buildings/Ramps/RAMP 4x2.prefab",
    };

    [MenuItem("Tools/Slopworks/Add Snap Points to Prefabs")]
    public static void AddSnapPointsToPrefabs()
    {
        int total = 0;

        foreach (var path in PrefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"snap setup: prefab not found at {path}");
                continue;
            }

            // Open prefab for editing
            var root = PrefabUtility.LoadPrefabContents(path);

            // Skip prefabs that already have snap points (preserve manual edits)
            var existing = root.GetComponentsInChildren<BuildingSnapPoint>();
            if (existing.Length > 0)
            {
                Debug.Log($"snap setup: skipping {prefab.name} -- already has {existing.Length} snap points");
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            // Get renderer bounds
            var renderer = root.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"snap setup: no renderer on {path}");
                PrefabUtility.UnloadPrefabContents(root);
                continue;
            }

            // For prefabs at scale (1,1,1) with baked FBX dimensions,
            // bounds.extents already represents world-space half-sizes.
            var bounds = renderer.bounds;
            var center = bounds.center;
            var ext = bounds.extents;

            Debug.Log($"snap setup: {prefab.name} -- center={center}, extents={ext}");

            bool isRamp = path.Contains("/Ramps/");

            var cardinals = new[]
            {
                (name: "North", dir: Vector3.forward, offset: new Vector3(0, 0, ext.z)),
                (name: "South", dir: Vector3.back,    offset: new Vector3(0, 0, -ext.z)),
                (name: "East",  dir: Vector3.right,   offset: new Vector3(ext.x, 0, 0)),
                (name: "West",  dir: Vector3.left,    offset: new Vector3(-ext.x, 0, 0)),
            };

            foreach (var (name, dir, offset) in cardinals)
            {
                bool isXFace = Mathf.Abs(dir.x) > 0.5f;
                var faceSize = isXFace
                    ? new Vector2(ext.z * 2, ext.y * 2)
                    : new Vector2(ext.x * 2, ext.y * 2);

                if (isRamp)
                {
                    // Ramps: bottom edge only per cardinal (no mid/top -- slope blocks them)
                    // Skip South face -- HighEdge/LowEdge handle the slope ends
                    if (name != "South")
                        AddSnapPoint(root, $"SnapPoint_{name}_Bot", center + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
                else
                {
                    AddSnapPoint(root, $"SnapPoint_{name}_Top", center + offset + new Vector3(0, ext.y, 0), dir, faceSize);
                    AddSnapPoint(root, $"SnapPoint_{name}_Mid", center + offset, dir, faceSize);
                    AddSnapPoint(root, $"SnapPoint_{name}_Bot", center + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
            }

            // Top/Bottom face centers (non-ramp only -- ramps add Bot_Center in their own block)
            var topBotSize = new Vector2(ext.x * 2, ext.z * 2);
            if (!isRamp)
            {
                AddSnapPoint(root, "SnapPoint_Top_Center", center + new Vector3(0, ext.y, 0), Vector3.up, topBotSize);
                AddSnapPoint(root, "SnapPoint_Bot_Center", center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
            }

            if (isRamp)
            {
                // Slope snaps: HighEdge and LowEdge at actual mesh heights
                // Bot_Center on bottom face
                var (highY, lowY) = GetRampEdgeHeights(renderer);
                AddSnapPoint(root, "SnapPoint_HighEdge",
                    new Vector3(center.x, highY, center.z + ext.z), Vector3.forward,
                    new Vector2(ext.x * 2, 0.1f));
                AddSnapPoint(root, "SnapPoint_LowEdge",
                    new Vector3(center.x, lowY, center.z - ext.z), Vector3.back,
                    new Vector2(ext.x * 2, 0.1f));
                AddSnapPoint(root, "SnapPoint_Bot_Center",
                    center + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
                total += 6; // 3 cardinal bot + high + low + bot_center
                Debug.Log($"snap setup: added 6 snap points to {prefab.name} (highY={highY:F2}, lowY={lowY:F2})");
            }
            else
            {
                total += 14;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            int count = isRamp ? 7 : 14;
            Debug.Log($"snap setup: added {count} snap points to {prefab.name}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"snap setup: done -- {total} snap points added across {PrefabPaths.Length} prefabs");
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
        // Convert world position to local, round to clean values
        child.transform.localPosition = RoundVec(parent.transform.InverseTransformPoint(worldPos));
        child.layer = PhysicsLayers.SnapPoints;
        var snap = child.AddComponent<BuildingSnapPoint>();
        snap.SurfaceSize = RoundVec2(surfaceSize);

        // Set normal via serialized field
        var so = new SerializedObject(snap);
        var normalProp = so.FindProperty("_normalOverride");
        normalProp.vector3Value = normal;
        so.ApplyModifiedPropertiesWithoutUndo();

        var sphere = child.AddComponent<SphereCollider>();
        sphere.radius = 0.5f;
        sphere.isTrigger = true;
    }
}
