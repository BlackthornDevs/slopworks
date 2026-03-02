using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for BuildingLayoutGenerator. Verifies the generated warehouse layout
/// has correct hierarchy, physics layers, static flags, colliders, and all
/// expected child transforms (spawn points, MEP positions).
/// These are integration tests -- they exercise the actual Unity object creation.
/// </summary>
[TestFixture]
public class BuildingLayoutGeneratorTests
{
    private BuildingLayout _layout;

    [SetUp]
    public void SetUp()
    {
        _layout = BuildingLayoutGenerator.GenerateWarehouse(Vector3.zero);
    }

    [TearDown]
    public void TearDown()
    {
        if (_layout.Root != null)
            Object.DestroyImmediate(_layout.Root);
    }

    // -- Root object --

    [Test]
    public void GenerateWarehouse_CreatesRootObject()
    {
        Assert.IsNotNull(_layout.Root);
        Assert.AreEqual("Warehouse", _layout.Root.name);
    }

    [Test]
    public void GenerateWarehouse_RootAtOrigin()
    {
        Assert.AreEqual(Vector3.zero, _layout.Root.transform.position);
    }

    [Test]
    public void GenerateWarehouse_WithOffset_RootAtOffset()
    {
        var offset = new Vector3(200f, 0f, 200f);
        Object.DestroyImmediate(_layout.Root);
        _layout = BuildingLayoutGenerator.GenerateWarehouse(offset);

        Assert.AreEqual(offset, _layout.Root.transform.position);
    }

    // -- Required transforms --

    [Test]
    public void GenerateWarehouse_HasEntranceSpawn()
    {
        Assert.IsNotNull(_layout.EntranceSpawn);
        Assert.AreEqual("EntranceSpawn", _layout.EntranceSpawn.name);
    }

    [Test]
    public void GenerateWarehouse_HasExitSpawn()
    {
        Assert.IsNotNull(_layout.ExitSpawn);
        Assert.AreEqual("ExitSpawn", _layout.ExitSpawn.name);
    }

    [Test]
    public void GenerateWarehouse_EntranceInsideBuilding()
    {
        // Entrance should be inside the 30x20 footprint (z > 0)
        Assert.Greater(_layout.EntranceSpawn.position.z, 0f);
        Assert.Less(_layout.EntranceSpawn.position.z, 20f);
    }

    [Test]
    public void GenerateWarehouse_ExitOutsideBuilding()
    {
        // Exit spawn is just outside the south wall (z <= 0)
        Assert.LessOrEqual(_layout.ExitSpawn.position.z, 0f);
    }

    // -- Enemy spawn points --

    [Test]
    public void GenerateWarehouse_Has4EnemySpawnPoints()
    {
        Assert.IsNotNull(_layout.EnemySpawnPoints);
        Assert.AreEqual(4, _layout.EnemySpawnPoints.Length);
    }

    [Test]
    public void GenerateWarehouse_EnemySpawnPointsAreNonNull()
    {
        for (int i = 0; i < _layout.EnemySpawnPoints.Length; i++)
            Assert.IsNotNull(_layout.EnemySpawnPoints[i], $"EnemySpawnPoint[{i}] is null");
    }

    [Test]
    public void GenerateWarehouse_EnemySpawnPointsInsideBuilding()
    {
        for (int i = 0; i < _layout.EnemySpawnPoints.Length; i++)
        {
            var pos = _layout.EnemySpawnPoints[i].position;
            Assert.Greater(pos.x, 0f, $"EnemySpawn[{i}] x out of bounds");
            Assert.Less(pos.x, 30f, $"EnemySpawn[{i}] x out of bounds");
            Assert.Greater(pos.z, 0f, $"EnemySpawn[{i}] z out of bounds");
            Assert.Less(pos.z, 20f, $"EnemySpawn[{i}] z out of bounds");
        }
    }

    [Test]
    public void GenerateWarehouse_EnemySpawnPointsAreChildrenOfRoot()
    {
        for (int i = 0; i < _layout.EnemySpawnPoints.Length; i++)
            Assert.AreSame(_layout.Root.transform, _layout.EnemySpawnPoints[i].parent,
                $"EnemySpawn[{i}] not parented to root");
    }

    // -- MEP positions --

    [Test]
    public void GenerateWarehouse_Has4MEPPositions()
    {
        Assert.IsNotNull(_layout.MEPPositions);
        Assert.AreEqual(4, _layout.MEPPositions.Length);
    }

    [Test]
    public void GenerateWarehouse_MEPPositionsAreNonNull()
    {
        for (int i = 0; i < _layout.MEPPositions.Length; i++)
            Assert.IsNotNull(_layout.MEPPositions[i], $"MEPPosition[{i}] is null");
    }

    [Test]
    public void GenerateWarehouse_MEPPositionsInsideBuilding()
    {
        for (int i = 0; i < _layout.MEPPositions.Length; i++)
        {
            var pos = _layout.MEPPositions[i].position;
            Assert.Greater(pos.x, 0f, $"MEP[{i}] x out of bounds");
            Assert.Less(pos.x, 30f, $"MEP[{i}] x out of bounds");
            Assert.Greater(pos.z, 0f, $"MEP[{i}] z out of bounds");
            Assert.Less(pos.z, 20f, $"MEP[{i}] z out of bounds");
        }
    }

    [Test]
    public void GenerateWarehouse_MEPPositionsInMechanicalRoom()
    {
        // Mechanical room is the NW quadrant: x < 15, z > 14
        for (int i = 0; i < _layout.MEPPositions.Length; i++)
        {
            var pos = _layout.MEPPositions[i].position;
            Assert.Less(pos.x, 15f, $"MEP[{i}] should be in west half (mechanical room)");
            Assert.Greater(pos.z, 14f, $"MEP[{i}] should be in north section (mechanical room)");
        }
    }

    // -- Floor and ceiling --

    [Test]
    public void GenerateWarehouse_HasFloor()
    {
        var floor = _layout.Root.transform.Find("Floor");
        Assert.IsNotNull(floor, "Floor child not found");
    }

    [Test]
    public void GenerateWarehouse_FloorIsStatic()
    {
        var floor = _layout.Root.transform.Find("Floor");
        Assert.IsTrue(floor.gameObject.isStatic, "Floor should be static for NavMesh");
    }

    [Test]
    public void GenerateWarehouse_FloorOnBIMStaticLayer()
    {
        var floor = _layout.Root.transform.Find("Floor");
        Assert.AreEqual(PhysicsLayers.BIM_Static, floor.gameObject.layer,
            "Floor should be on BIM_Static layer");
    }

    [Test]
    public void GenerateWarehouse_FloorHasCollider()
    {
        var floor = _layout.Root.transform.Find("Floor");
        var collider = floor.GetComponent<Collider>();
        Assert.IsNotNull(collider, "Floor needs a collider for NavMesh baking and physics");
    }

    [Test]
    public void GenerateWarehouse_HasCeiling()
    {
        var ceiling = _layout.Root.transform.Find("Ceiling");
        Assert.IsNotNull(ceiling, "Ceiling child not found");
        Assert.IsTrue(ceiling.gameObject.isStatic, "Ceiling should be static");
        Assert.AreEqual(PhysicsLayers.BIM_Static, ceiling.gameObject.layer);
    }

    // -- Walls --

    [Test]
    public void GenerateWarehouse_HasOuterWalls()
    {
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_North"), "North wall missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_West"), "West wall missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_East"), "East wall missing");
        // South wall is split into L/R for doorway
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_South_L"), "South left wall missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_South_R"), "South right wall missing");
    }

    [Test]
    public void GenerateWarehouse_HasInteriorDividers()
    {
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div1_L"), "Divider 1 left missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div1_R"), "Divider 1 right missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div2_L"), "Divider 2 left missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div2_R"), "Divider 2 right missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div3_Lower"), "Divider 3 lower missing");
        Assert.IsNotNull(_layout.Root.transform.Find("Wall_Div3_Upper"), "Divider 3 upper missing");
    }

    [Test]
    public void GenerateWarehouse_AllWallsAreStatic()
    {
        var children = _layout.Root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (!child.name.StartsWith("Wall_")) continue;
            Assert.IsTrue(child.gameObject.isStatic,
                $"{child.name} should be static for NavMesh baking");
        }
    }

    [Test]
    public void GenerateWarehouse_AllWallsOnBIMStaticLayer()
    {
        var children = _layout.Root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (!child.name.StartsWith("Wall_")) continue;
            Assert.AreEqual(PhysicsLayers.BIM_Static, child.gameObject.layer,
                $"{child.name} should be on BIM_Static layer");
        }
    }

    [Test]
    public void GenerateWarehouse_AllWallsHaveColliders()
    {
        var children = _layout.Root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (!child.name.StartsWith("Wall_")) continue;
            Assert.IsNotNull(child.GetComponent<Collider>(),
                $"{child.name} needs a collider for LOS blocking and weapon hits");
        }
    }

    [Test]
    public void GenerateWarehouse_AllWallsHaveRenderers()
    {
        var children = _layout.Root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (!child.name.StartsWith("Wall_")) continue;
            Assert.IsNotNull(child.GetComponent<Renderer>(),
                $"{child.name} should be visible");
        }
    }

    [Test]
    public void GenerateWarehouse_WallsHaveNonZeroScale()
    {
        var children = _layout.Root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (!child.name.StartsWith("Wall_")) continue;
            var scale = child.localScale;
            Assert.Greater(scale.x, 0f, $"{child.name} has zero x scale");
            Assert.Greater(scale.y, 0f, $"{child.name} has zero y scale");
            Assert.Greater(scale.z, 0f, $"{child.name} has zero z scale");
        }
    }

    // -- Offset correctness --

    [Test]
    public void GenerateWarehouse_WithOffset_SpawnPointsOffsetCorrectly()
    {
        var offset = new Vector3(200f, 0f, 200f);
        Object.DestroyImmediate(_layout.Root);
        _layout = BuildingLayoutGenerator.GenerateWarehouse(offset);

        Assert.Greater(_layout.EntranceSpawn.position.x, 200f);
        Assert.Greater(_layout.EntranceSpawn.position.z, 200f);

        for (int i = 0; i < _layout.MEPPositions.Length; i++)
        {
            Assert.Greater(_layout.MEPPositions[i].position.x, 200f,
                $"MEP[{i}] x should be offset");
            Assert.Greater(_layout.MEPPositions[i].position.z, 200f,
                $"MEP[{i}] z should be offset");
        }
    }

    // -- Total child count sanity --

    [Test]
    public void GenerateWarehouse_HasExpectedChildCount()
    {
        // Floor, ceiling, 5 outer walls, 6 interior walls, entrance, exit, 4 enemy spawns, 4 MEP markers = 22 children
        int childCount = _layout.Root.transform.childCount;
        Assert.GreaterOrEqual(childCount, 20,
            "Warehouse should have at least 20 children (walls, floor, ceiling, markers)");
    }
}
