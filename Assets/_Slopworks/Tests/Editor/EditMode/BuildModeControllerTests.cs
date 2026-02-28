using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BuildModeControllerTests
{
    private FactoryGrid _grid;
    private BuildModeController _controller;
    private FoundationDefinitionSO _foundation1x1;
    private FoundationDefinitionSO _foundation2x3;

    [SetUp]
    public void SetUp()
    {
        _grid = new FactoryGrid();
        _controller = new BuildModeController();

        _foundation1x1 = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundation1x1.foundationId = "foundation_1x1";
        _foundation1x1.displayName = "Small Foundation";
        _foundation1x1.size = new Vector2Int(1, 1);

        _foundation2x3 = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundation2x3.foundationId = "foundation_2x3";
        _foundation2x3.displayName = "Large Foundation";
        _foundation2x3.size = new Vector2Int(2, 3);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_foundation1x1);
        Object.DestroyImmediate(_foundation2x3);
    }

    // -- Build mode toggling --

    [Test]
    public void EnterBuildMode_SetsIsInBuildModeTrue()
    {
        _controller.EnterBuildMode(_foundation1x1);
        Assert.IsTrue(_controller.IsInBuildMode);
    }

    [Test]
    public void ExitBuildMode_SetsIsInBuildModeFalse()
    {
        _controller.EnterBuildMode(_foundation1x1);
        _controller.ExitBuildMode();
        Assert.IsFalse(_controller.IsInBuildMode);
    }

    // -- Preview snapping --

    [Test]
    public void UpdatePreview_SnapsToGrid()
    {
        _controller.EnterBuildMode(_foundation1x1);
        _controller.UpdatePreview(new Vector3(5.7f, 0f, 8.3f), _grid);

        Assert.AreEqual(new Vector2Int(5, 8), _controller.SnappedCell);
    }

    // -- Placement success --

    [Test]
    public void TryPlace_EmptyGrid_ReturnsTrue()
    {
        _controller.EnterBuildMode(_foundation1x1);
        _controller.UpdatePreview(new Vector3(10.5f, 0f, 10.5f), _grid);

        bool result = _controller.TryPlace(_grid);
        Assert.IsTrue(result);
    }

    // -- Placement failure on overlap --

    [Test]
    public void TryPlace_OccupiedCell_ReturnsFalse()
    {
        // Place a building manually
        var existing = new BuildingData("existing", new Vector2Int(10, 10), new Vector2Int(1, 1));
        _grid.Place(new Vector2Int(10, 10), new Vector2Int(1, 1), existing);

        // Try to build on the same cell
        _controller.EnterBuildMode(_foundation1x1);
        _controller.UpdatePreview(new Vector3(10.5f, 0f, 10.5f), _grid);

        bool result = _controller.TryPlace(_grid);
        Assert.IsFalse(result);
    }

    // -- BuildingData correctness --

    [Test]
    public void TryPlace_CreatesBuildingDataWithCorrectOriginAndSize()
    {
        _controller.EnterBuildMode(_foundation2x3);
        _controller.UpdatePreview(new Vector3(5.5f, 0f, 5.5f), _grid);
        _controller.TryPlace(_grid);

        var placed = _grid.GetAt(new Vector2Int(5, 5));
        Assert.IsNotNull(placed);
        Assert.AreEqual("foundation_2x3", placed.BuildingId);
        Assert.AreEqual(new Vector2Int(5, 5), placed.Origin);
        Assert.AreEqual(new Vector2Int(2, 3), placed.Size);
        Assert.AreEqual(0, placed.Rotation);
    }

    // -- Rotation --

    [Test]
    public void RotatePreview_SwapsEffectiveSize()
    {
        _controller.EnterBuildMode(_foundation2x3);
        Assert.AreEqual(new Vector2Int(2, 3), _controller.EffectiveSize);

        _controller.RotatePreview();
        Assert.AreEqual(new Vector2Int(3, 2), _controller.EffectiveSize);
    }

    [Test]
    public void TryPlace_WithRotation_UsesRotatedSize()
    {
        _controller.EnterBuildMode(_foundation2x3);
        _controller.RotatePreview(); // 90 degrees, size becomes 3x2
        _controller.UpdatePreview(new Vector3(10.5f, 0f, 10.5f), _grid);
        _controller.TryPlace(_grid);

        var placed = _grid.GetAt(new Vector2Int(10, 10));
        Assert.IsNotNull(placed);
        Assert.AreEqual(new Vector2Int(3, 2), placed.Size);
        Assert.AreEqual(90, placed.Rotation);

        // Verify the full 3x2 footprint is occupied
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(12, 11)));
        // Cell beyond footprint should be empty
        Assert.IsNull(_grid.GetAt(new Vector2Int(13, 10)));
    }

    // -- Out of bounds --

    [Test]
    public void TryPlace_OutOfBounds_ReturnsFalse()
    {
        _controller.EnterBuildMode(_foundation2x3);
        _controller.UpdatePreview(new Vector3(199.5f, 0f, 199.5f), _grid);

        bool result = _controller.TryPlace(_grid);
        Assert.IsFalse(result);
    }

    // -- Multiple placements --

    [Test]
    public void MultiplePlacements_NonOverlapping_AllSucceed()
    {
        _controller.EnterBuildMode(_foundation1x1);

        _controller.UpdatePreview(new Vector3(0.5f, 0f, 0.5f), _grid);
        Assert.IsTrue(_controller.TryPlace(_grid));

        _controller.UpdatePreview(new Vector3(1.5f, 0f, 0.5f), _grid);
        Assert.IsTrue(_controller.TryPlace(_grid));

        _controller.UpdatePreview(new Vector3(2.5f, 0f, 0.5f), _grid);
        Assert.IsTrue(_controller.TryPlace(_grid));

        Assert.IsNotNull(_grid.GetAt(new Vector2Int(0, 0)));
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(1, 0)));
        Assert.IsNotNull(_grid.GetAt(new Vector2Int(2, 0)));
    }
}
