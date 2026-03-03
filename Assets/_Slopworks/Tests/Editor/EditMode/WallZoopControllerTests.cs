using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WallZoopControllerTests
{
    private FactoryGrid _grid;
    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _structService;
    private WallZoopController _zoop;
    private FoundationDefinitionSO _foundationDef;

    [SetUp]
    public void SetUp()
    {
        _grid = new FactoryGrid();
        _snapRegistry = new SnapPointRegistry();
        _structService = new StructuralPlacementService(_grid, _snapRegistry);
        _zoop = new WallZoopController();

        _foundationDef = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundationDef.foundationId = "foundation_1x1";
        _foundationDef.size = Vector2Int.one;
        _foundationDef.generatesSnapPoints = true;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_foundationDef);
    }

    [Test]
    public void Begin_SetsIsActive_StoresOrigin()
    {
        var snap = PlaceAndGetSnap(5, 5, Vector2Int.up);

        _zoop.Begin(snap);

        Assert.IsTrue(_zoop.IsActive);
        Assert.AreEqual(snap, _zoop.Origin);
        Assert.AreEqual(1, _zoop.PlannedWalls.Count);
        Assert.AreEqual(snap, _zoop.PlannedWalls[0]);
    }

    [Test]
    public void Update_NoMovement_ReturnsOnlyOrigin()
    {
        var snap = PlaceAndGetSnap(5, 5, Vector2Int.up);
        _zoop.Begin(snap);

        _zoop.Update(snap, _snapRegistry, 0);

        Assert.AreEqual(1, _zoop.PlannedWalls.Count);
        Assert.AreEqual(snap, _zoop.PlannedWalls[0]);
    }

    [Test]
    public void Update_LocksToXAxis_WhenXChanges()
    {
        // Place foundations at (5,5), (6,5), (7,5)
        PlaceFoundation(5, 5);
        PlaceFoundation(6, 5);
        PlaceFoundation(7, 5);

        var origin = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.up);
        var target = _snapRegistry.GetAt(new Vector2Int(7, 5), 0, Vector2Int.up);

        _zoop.Begin(origin);
        _zoop.Update(target, _snapRegistry, 0);

        Assert.AreEqual(3, _zoop.PlannedWalls.Count);
        Assert.AreEqual(new Vector2Int(5, 5), _zoop.PlannedWalls[0].Cell);
        Assert.AreEqual(new Vector2Int(6, 5), _zoop.PlannedWalls[1].Cell);
        Assert.AreEqual(new Vector2Int(7, 5), _zoop.PlannedWalls[2].Cell);
    }

    [Test]
    public void Update_LocksToYAxis_WhenYChanges()
    {
        // Place foundations at (5,5), (5,6), (5,7)
        PlaceFoundation(5, 5);
        PlaceFoundation(5, 6);
        PlaceFoundation(5, 7);

        var origin = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.right);
        var target = _snapRegistry.GetAt(new Vector2Int(5, 7), 0, Vector2Int.right);

        _zoop.Begin(origin);
        _zoop.Update(target, _snapRegistry, 0);

        Assert.AreEqual(3, _zoop.PlannedWalls.Count);
        Assert.AreEqual(new Vector2Int(5, 5), _zoop.PlannedWalls[0].Cell);
        Assert.AreEqual(new Vector2Int(5, 6), _zoop.PlannedWalls[1].Cell);
        Assert.AreEqual(new Vector2Int(5, 7), _zoop.PlannedWalls[2].Cell);
    }

    [Test]
    public void Update_OccupiedSnapPointsSkipped()
    {
        PlaceFoundation(5, 5);
        PlaceFoundation(6, 5);
        PlaceFoundation(7, 5);

        // Occupy the middle snap point
        var middleSnap = _snapRegistry.GetAt(new Vector2Int(6, 5), 0, Vector2Int.up);
        middleSnap.IsOccupied = true;

        var origin = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.up);
        var target = _snapRegistry.GetAt(new Vector2Int(7, 5), 0, Vector2Int.up);

        _zoop.Begin(origin);
        _zoop.Update(target, _snapRegistry, 0);

        Assert.AreEqual(2, _zoop.PlannedWalls.Count);
        Assert.AreEqual(new Vector2Int(5, 5), _zoop.PlannedWalls[0].Cell);
        Assert.AreEqual(new Vector2Int(7, 5), _zoop.PlannedWalls[1].Cell);
    }

    [Test]
    public void End_ReturnsPlannedList_ResetsIsActive()
    {
        PlaceFoundation(5, 5);
        PlaceFoundation(6, 5);

        var origin = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.up);
        var target = _snapRegistry.GetAt(new Vector2Int(6, 5), 0, Vector2Int.up);

        _zoop.Begin(origin);
        _zoop.Update(target, _snapRegistry, 0);

        var result = _zoop.End();

        Assert.AreEqual(2, result.Count);
        Assert.IsFalse(_zoop.IsActive);
        Assert.IsNull(_zoop.Origin);
        Assert.AreEqual(0, _zoop.PlannedWalls.Count);
    }

    [Test]
    public void Cancel_ClearsPlannedList_ResetsIsActive()
    {
        var snap = PlaceAndGetSnap(5, 5, Vector2Int.up);
        _zoop.Begin(snap);

        _zoop.Cancel();

        Assert.IsFalse(_zoop.IsActive);
        Assert.IsNull(_zoop.Origin);
        Assert.AreEqual(0, _zoop.PlannedWalls.Count);
    }

    [Test]
    public void Update_IgnoresCrossAxisMovement_AfterLock()
    {
        // Place foundations along X so "up" edges stay exterior
        PlaceFoundation(5, 5);
        PlaceFoundation(6, 5);
        PlaceFoundation(7, 5);
        // Place one foundation for the diagonal target
        PlaceFoundation(7, 7);

        var origin = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.up);

        // First movement is X axis -- locks to X
        var targetX = _snapRegistry.GetAt(new Vector2Int(6, 5), 0, Vector2Int.up);
        _zoop.Begin(origin);
        _zoop.Update(targetX, _snapRegistry, 0);

        // Now try to move diagonally -- should still follow X axis
        var targetDiag = _snapRegistry.GetAt(new Vector2Int(7, 7), 0, Vector2Int.up);
        _zoop.Update(targetDiag, _snapRegistry, 0);

        // Should collect along X axis at Y=5, up to X=7
        Assert.AreEqual(3, _zoop.PlannedWalls.Count);
        Assert.AreEqual(new Vector2Int(5, 5), _zoop.PlannedWalls[0].Cell);
        Assert.AreEqual(new Vector2Int(6, 5), _zoop.PlannedWalls[1].Cell);
        Assert.AreEqual(new Vector2Int(7, 5), _zoop.PlannedWalls[2].Cell);
    }

    [Test]
    public void Update_ReverseDirection_Works()
    {
        PlaceFoundation(5, 5);
        PlaceFoundation(6, 5);
        PlaceFoundation(7, 5);

        var origin = _snapRegistry.GetAt(new Vector2Int(7, 5), 0, Vector2Int.up);
        var target = _snapRegistry.GetAt(new Vector2Int(5, 5), 0, Vector2Int.up);

        _zoop.Begin(origin);
        _zoop.Update(target, _snapRegistry, 0);

        Assert.AreEqual(3, _zoop.PlannedWalls.Count);
        Assert.AreEqual(new Vector2Int(7, 5), _zoop.PlannedWalls[0].Cell);
        Assert.AreEqual(new Vector2Int(6, 5), _zoop.PlannedWalls[1].Cell);
        Assert.AreEqual(new Vector2Int(5, 5), _zoop.PlannedWalls[2].Cell);
    }

    // -- Helpers --

    private void PlaceFoundation(int x, int y)
    {
        _structService.PlaceFoundation(_foundationDef, new Vector2Int(x, y), 0);
    }

    private SnapPoint PlaceAndGetSnap(int x, int y, Vector2Int edgeDir)
    {
        PlaceFoundation(x, y);
        return _snapRegistry.GetAt(new Vector2Int(x, y), 0, edgeDir);
    }
}
