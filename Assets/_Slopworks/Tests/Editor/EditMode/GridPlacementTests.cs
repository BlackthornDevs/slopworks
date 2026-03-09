using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GridPlacementTests
{
    private GameObject _prefab;

    [TearDown]
    public void TearDown()
    {
        if (_prefab != null)
            Object.DestroyImmediate(_prefab);
    }

    private GameObject CreatePrefab(Vector3 scale)
    {
        _prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _prefab.transform.localScale = scale;
        return _prefab;
    }

    [Test]
    public void GridPlacement_4x4Foundation_SnapsToNearest1mGrid()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.7f, 0f, 5.7f), prefab, 0);

        // Round(5.7) = 6
        Assert.AreEqual(6f, pos.x, 0.001f, "X should round to nearest 1m grid");
        Assert.AreEqual(0.25f, pos.y, 0.001f, "Y should be halfHeight above surface");
        Assert.AreEqual(6f, pos.z, 0.001f, "Z should round to nearest 1m grid");
    }

    [Test]
    public void GridPlacement_1x1Machine_SnapsToNearest1mGrid()
    {
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(3.3f, 0f, 7.8f), prefab, 0);

        // Round(3.3) = 3, Round(7.8) = 8
        Assert.AreEqual(3f, pos.x, 0.001f, "X should round to nearest 1m grid");
        Assert.AreEqual(0.5f, pos.y, 0.001f, "Y should be halfHeight above surface");
        Assert.AreEqual(8f, pos.z, 0.001f, "Z should round to nearest 1m grid");
    }

    [Test]
    public void GridPlacement_ThinWall_SnapsToNearest1mGrid()
    {
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(6f, 0f, 5.1f), prefab, 0);

        // Round(6.0) = 6, Round(5.1) = 5
        Assert.AreEqual(6f, pos.x, 0.001f, "X should round to nearest 1m grid");
        Assert.AreEqual(1.5f, pos.y, 0.001f, "Y should be halfHeight");
        Assert.AreEqual(5f, pos.z, 0.001f, "Z should round to nearest 1m grid");
    }

    [Test]
    public void GridPlacement_RotatedWall_SameSnapBehavior()
    {
        // Rotation no longer affects grid snap -- center always rounds to nearest 1m
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.1f, 0f, 6f), prefab, 90);

        // Round(5.1) = 5, Round(6.0) = 6
        Assert.AreEqual(5f, pos.x, 0.001f, "X should round to nearest 1m grid");
        Assert.AreEqual(1.5f, pos.y, 0.001f, "Y should be halfHeight");
        Assert.AreEqual(6f, pos.z, 0.001f, "Z should round to nearest 1m grid");
    }

    [Test]
    public void GridPlacement_WithSurfaceY_AddsToHeight()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.7f, 3f, 5.7f), prefab, 0);

        // Round(5.7) = 6
        Assert.AreEqual(6f, pos.x, 0.001f, "X should round to nearest 1m grid");
        Assert.AreEqual(3.25f, pos.y, 0.001f, "Y should be surfaceY + halfHeight");
    }

    [Test]
    public void GridPlacement_ReturnsCorrectRotation()
    {
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));
        var (_, rot) = GridManager.GetGridPlacementPosition(
            new Vector3(0f, 0f, 0f), prefab, 90);

        Assert.AreEqual(90f, rot.eulerAngles.y, 0.001f, "Rotation Y should be 90");
    }

    [Test]
    public void GridPlacement_NegativeCoords_RoundsCorrectly()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(-3.3f, 0f, -7.7f), prefab, 0);

        // Round(-3.3) = -3, Round(-7.7) = -8
        Assert.AreEqual(-3f, pos.x, 0.001f, "Negative X should round to nearest 1m grid");
        Assert.AreEqual(-8f, pos.z, 0.001f, "Negative Z should round to nearest 1m grid");
    }
}
