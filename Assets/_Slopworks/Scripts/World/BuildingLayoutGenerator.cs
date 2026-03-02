using UnityEngine;

/// <summary>
/// Return type for BuildingLayoutGenerator. Contains references to key transforms
/// within the generated building layout.
/// </summary>
public struct BuildingLayout
{
    public GameObject Root;
    public Transform EntranceSpawn;
    public Transform ExitSpawn;
    public Transform[] EnemySpawnPoints;
    public Transform[] MEPPositions;
}

/// <summary>
/// Static utility that creates a multi-room warehouse interior from primitives at runtime.
/// Layout is ~30x20 units with 4 rooms: entry hall, main hall, mechanical room, storage area.
/// All geometry on PhysicsLayers.BIM_Static for NavMesh baking, fauna LOS, and weapon hits.
/// </summary>
public static class BuildingLayoutGenerator
{
    private const float WallThickness = 0.2f;
    private const float WallHeight = 3f;
    private const float FloorThickness = 0.1f;
    private const float DoorWidth = 2f;

    public static BuildingLayout GenerateWarehouse(Vector3 origin)
    {
        var root = new GameObject("Warehouse");
        root.transform.position = origin;

        // Dimensions: 30 wide (x), 20 deep (z)
        float width = 30f;
        float depth = 20f;

        // Floor
        CreateFloor(root.transform, origin, width, depth);

        // Ceiling
        CreateCeiling(root.transform, origin, width, depth);

        // Outer walls with doorway gaps
        CreateOuterWalls(root.transform, origin, width, depth);

        // Interior walls dividing into 4 rooms
        // Room layout:
        //   Entry hall (south, 30x6) -> Main hall (center, 30x8) -> Mechanical room (NW, 15x6) + Storage (NE, 15x6)
        CreateInteriorWalls(root.transform, origin, width, depth);

        // Spawn points
        var entranceSpawn = CreateMarker(root.transform, "EntranceSpawn",
            origin + new Vector3(width * 0.5f, 0f, 2f));

        var exitSpawn = CreateMarker(root.transform, "ExitSpawn",
            origin + new Vector3(width * 0.5f, 0f, -2f));

        // Enemy spawn points in main hall and mechanical room
        var enemySpawnPoints = new Transform[]
        {
            CreateMarker(root.transform, "EnemySpawn_0", origin + new Vector3(5f, 0f, 10f)),
            CreateMarker(root.transform, "EnemySpawn_1", origin + new Vector3(25f, 0f, 10f)),
            CreateMarker(root.transform, "EnemySpawn_2", origin + new Vector3(8f, 0f, 17f)),
            CreateMarker(root.transform, "EnemySpawn_3", origin + new Vector3(22f, 0f, 17f)),
        };

        // MEP positions in mechanical room (northwest quadrant)
        var mepPositions = new Transform[]
        {
            CreateMarker(root.transform, "MEP_Electrical", origin + new Vector3(3f, 1f, 17f)),
            CreateMarker(root.transform, "MEP_Plumbing", origin + new Vector3(7f, 1f, 17f)),
            CreateMarker(root.transform, "MEP_Mechanical", origin + new Vector3(3f, 1f, 19f)),
            CreateMarker(root.transform, "MEP_HVAC", origin + new Vector3(7f, 1f, 19f)),
        };

        return new BuildingLayout
        {
            Root = root,
            EntranceSpawn = entranceSpawn,
            ExitSpawn = exitSpawn,
            EnemySpawnPoints = enemySpawnPoints,
            MEPPositions = mepPositions,
        };
    }

    private static void CreateFloor(Transform parent, Vector3 origin, float width, float depth)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.isStatic = true;
        floor.layer = PhysicsLayers.BIM_Static;
        floor.transform.SetParent(parent, true);
        floor.transform.position = origin + new Vector3(width * 0.5f, -FloorThickness * 0.5f, depth * 0.5f);
        floor.transform.localScale = new Vector3(width, FloorThickness, depth);
        SetColor(floor, new Color(0.35f, 0.35f, 0.35f));
    }

    private static void CreateCeiling(Transform parent, Vector3 origin, float width, float depth)
    {
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.isStatic = true;
        ceiling.layer = PhysicsLayers.BIM_Static;
        ceiling.transform.SetParent(parent, true);
        ceiling.transform.position = origin + new Vector3(width * 0.5f, WallHeight + FloorThickness * 0.5f, depth * 0.5f);
        ceiling.transform.localScale = new Vector3(width, FloorThickness, depth);
        SetColor(ceiling, new Color(0.4f, 0.4f, 0.4f));
    }

    private static void CreateOuterWalls(Transform parent, Vector3 origin, float width, float depth)
    {
        float halfHeight = WallHeight * 0.5f;

        // South wall (entry) -- gap in center for entrance doorway
        float doorHalf = DoorWidth * 0.5f;
        float centerX = width * 0.5f;
        // Left section
        CreateWall(parent, "Wall_South_L", origin + new Vector3((centerX - doorHalf) * 0.5f, halfHeight, 0f),
            new Vector3(centerX - doorHalf, WallHeight, WallThickness));
        // Right section
        CreateWall(parent, "Wall_South_R", origin + new Vector3(centerX + doorHalf + (width - centerX - doorHalf) * 0.5f, halfHeight, 0f),
            new Vector3(width - centerX - doorHalf, WallHeight, WallThickness));

        // North wall (solid)
        CreateWall(parent, "Wall_North", origin + new Vector3(width * 0.5f, halfHeight, depth),
            new Vector3(width, WallHeight, WallThickness));

        // West wall (solid)
        CreateWall(parent, "Wall_West", origin + new Vector3(0f, halfHeight, depth * 0.5f),
            new Vector3(WallThickness, WallHeight, depth));

        // East wall (solid)
        CreateWall(parent, "Wall_East", origin + new Vector3(width, halfHeight, depth * 0.5f),
            new Vector3(WallThickness, WallHeight, depth));
    }

    private static void CreateInteriorWalls(Transform parent, Vector3 origin, float width, float depth)
    {
        float halfHeight = WallHeight * 0.5f;
        float doorHalf = DoorWidth * 0.5f;

        // Divider 1: entry hall (z=6) separating south from main hall -- gap at center
        float div1Z = 6f;
        float centerX = width * 0.5f;
        CreateWall(parent, "Wall_Div1_L", origin + new Vector3((centerX - doorHalf) * 0.5f, halfHeight, div1Z),
            new Vector3(centerX - doorHalf, WallHeight, WallThickness));
        CreateWall(parent, "Wall_Div1_R", origin + new Vector3(centerX + doorHalf + (width - centerX - doorHalf) * 0.5f, halfHeight, div1Z),
            new Vector3(width - centerX - doorHalf, WallHeight, WallThickness));

        // Divider 2: main hall to back rooms (z=14) -- gap at center
        float div2Z = 14f;
        CreateWall(parent, "Wall_Div2_L", origin + new Vector3((centerX - doorHalf) * 0.5f, halfHeight, div2Z),
            new Vector3(centerX - doorHalf, WallHeight, WallThickness));
        CreateWall(parent, "Wall_Div2_R", origin + new Vector3(centerX + doorHalf + (width - centerX - doorHalf) * 0.5f, halfHeight, div2Z),
            new Vector3(width - centerX - doorHalf, WallHeight, WallThickness));

        // Divider 3: splits back area into mechanical room (west) and storage (east) at x=15
        // Gap at z=17 center
        float div3X = width * 0.5f;
        float backMidZ = (14f + depth) * 0.5f;
        float backHeight = depth - 14f;
        CreateWall(parent, "Wall_Div3_Lower", origin + new Vector3(div3X, halfHeight, 14f + (backMidZ - 14f - doorHalf) * 0.5f + doorHalf * 0.5f),
            new Vector3(WallThickness, WallHeight, backMidZ - 14f - doorHalf));
        CreateWall(parent, "Wall_Div3_Upper", origin + new Vector3(div3X, halfHeight, backMidZ + doorHalf + (depth - backMidZ - doorHalf) * 0.5f),
            new Vector3(WallThickness, WallHeight, depth - backMidZ - doorHalf));
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.isStatic = true;
        wall.layer = PhysicsLayers.BIM_Static;
        wall.transform.SetParent(parent, true);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        SetColor(wall, new Color(0.5f, 0.5f, 0.5f));
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, true);
        marker.transform.position = position;
        return marker.transform;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var mat = new Material(renderer.sharedMaterial);
        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
