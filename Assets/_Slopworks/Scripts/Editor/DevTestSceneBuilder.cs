using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

public static class DevTestSceneBuilder
{
    [MenuItem("Slopworks/Build Dev Test Arena")]
    public static void BuildArena()
    {
        var parent = new GameObject("Environment");
        parent.isStatic = true;

        BuildPerimeterWalls(parent.transform);
        BuildCoverCrates(parent.transform);
        BuildCenterStructure(parent.transform);
        BuildScatteredBarrels(parent.transform);
        BuildRamps(parent.transform);
        BuildSpawnMarkers(parent.transform);

        // assign all children to BIM_Static layer (blocks LOS, stops bullets)
        SetLayerRecursive(parent, PhysicsLayers.BIM_Static);

        // re-bake NavMesh with obstacles
        NavMeshBuilder.BuildNavMesh();

        EditorUtility.SetDirty(parent);
        Debug.Log("dev test arena built — " + parent.transform.childCount + " objects");
    }

    private static void BuildPerimeterWalls(Transform parent)
    {
        // low walls around the arena edges (ground is 100x100 at scale 10)
        float halfSize = 45f;
        float wallHeight = 3f;
        float wallThickness = 1f;

        // north wall
        CreateBox("Wall_North", parent,
            new Vector3(0, wallHeight / 2f, halfSize),
            new Vector3(halfSize * 2f, wallHeight, wallThickness));

        // south wall
        CreateBox("Wall_South", parent,
            new Vector3(0, wallHeight / 2f, -halfSize),
            new Vector3(halfSize * 2f, wallHeight, wallThickness));

        // east wall
        CreateBox("Wall_East", parent,
            new Vector3(halfSize, wallHeight / 2f, 0),
            new Vector3(wallThickness, wallHeight, halfSize * 2f));

        // west wall
        CreateBox("Wall_West", parent,
            new Vector3(-halfSize, wallHeight / 2f, 0),
            new Vector3(wallThickness, wallHeight, halfSize * 2f));
    }

    private static void BuildCoverCrates(Transform parent)
    {
        // scattered cover positions — asymmetric for interesting combat
        Vector3[] positions = new[]
        {
            new Vector3(8f, 0.75f, 4f),
            new Vector3(-6f, 0.75f, 10f),
            new Vector3(12f, 0.75f, -8f),
            new Vector3(-10f, 0.75f, -5f),
            new Vector3(3f, 0.75f, -12f),
            new Vector3(-15f, 0.75f, 15f),
            new Vector3(20f, 0.75f, 12f),
            new Vector3(-20f, 0.75f, -15f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float yaw = Random.Range(0f, 45f);
            var crate = CreateBox("Crate_" + i, parent, positions[i], new Vector3(2f, 1.5f, 2f));
            crate.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private static void BuildCenterStructure(Transform parent)
    {
        // a small building in the center — two walls and a roof
        float cx = 0f, cz = 15f;

        CreateBox("Building_WallA", parent,
            new Vector3(cx - 3f, 1.5f, cz),
            new Vector3(0.5f, 3f, 6f));

        CreateBox("Building_WallB", parent,
            new Vector3(cx + 3f, 1.5f, cz),
            new Vector3(0.5f, 3f, 6f));

        CreateBox("Building_Roof", parent,
            new Vector3(cx, 3.1f, cz),
            new Vector3(7f, 0.3f, 7f));

        // back wall with gap for door
        CreateBox("Building_BackWall", parent,
            new Vector3(cx, 1.5f, cz + 3f),
            new Vector3(6f, 3f, 0.5f));
    }

    private static void BuildScatteredBarrels(Transform parent)
    {
        // cylindrical obstacles using capsules (standing upright)
        Vector3[] positions = new[]
        {
            new Vector3(5f, 0.5f, -3f),
            new Vector3(-4f, 0.5f, -7f),
            new Vector3(15f, 0.5f, 5f),
            new Vector3(-12f, 0.5f, 8f),
            new Vector3(7f, 0.5f, 18f),
            new Vector3(-8f, 0.5f, 20f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel_" + i;
            barrel.transform.parent = parent;
            barrel.transform.position = positions[i];
            barrel.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            barrel.isStatic = true;
        }
    }

    private static void BuildRamps(Transform parent)
    {
        // angled ramps for elevation variety
        var ramp1 = CreateBox("Ramp_A", parent,
            new Vector3(-18f, 0.75f, 0f),
            new Vector3(4f, 0.3f, 6f));
        ramp1.transform.rotation = Quaternion.Euler(0f, 0f, -15f);

        var ramp2 = CreateBox("Ramp_B", parent,
            new Vector3(18f, 0.75f, -10f),
            new Vector3(4f, 0.3f, 6f));
        ramp2.transform.rotation = Quaternion.Euler(0f, 90f, -15f);
    }

    private static void BuildSpawnMarkers(Transform parent)
    {
        // small flat discs marking enemy spawn positions (visual only)
        Vector3[] spawnPositions = new[]
        {
            new Vector3(30f, 0.05f, 30f),
            new Vector3(-30f, 0.05f, 30f),
            new Vector3(30f, 0.05f, -30f),
            new Vector3(-30f, 0.05f, -30f),
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "SpawnMarker_" + i;
            marker.transform.parent = parent;
            marker.transform.position = spawnPositions[i];
            marker.transform.localScale = new Vector3(2f, 0.02f, 2f);
            marker.isStatic = true;

            // give spawn markers a red tint
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.8f, 0.2f, 0.2f, 1f);
                renderer.sharedMaterial = mat;
            }
        }
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.parent = parent;
        box.transform.position = position;
        box.transform.localScale = size;
        box.isStatic = true;
        return box;
    }

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
