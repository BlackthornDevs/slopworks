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
    public void GridPlacement_4x4Foundation_SnapsToFoundationGrid()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.7f, 0f, 5.7f), prefab, 0);

        Assert.AreEqual(6f, pos.x, 0.001f, "X should snap to 4m grid center");
        Assert.AreEqual(0.25f, pos.y, 0.001f, "Y should be halfHeight above surface");
        Assert.AreEqual(6f, pos.z, 0.001f, "Z should snap to 4m grid center");
    }

    [Test]
    public void GridPlacement_1x1Machine_SnapsToCellCenter()
    {
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(3.3f, 0f, 7.8f), prefab, 0);

        Assert.AreEqual(3.5f, pos.x, 0.001f, "X should snap to 1m cell center");
        Assert.AreEqual(0.5f, pos.y, 0.001f, "Y should be halfHeight above surface");
        Assert.AreEqual(7.5f, pos.z, 0.001f, "Z should snap to nearest 1m cell center");
    }

    [Test]
    public void GridPlacement_ThinWall_BackFaceFlushToGridLine()
    {
        // 4x3x0.5 wall, rotation 0: X=4m (wide), Z=0.5m (thin)
        // X: Floor(6/4)*4 = 4, center = 4+2 = 6
        // Z: thin, Round(5.1) = 5, center = 5 + 0.25 = 5.25
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(6f, 0f, 5.1f), prefab, 0);

        Assert.AreEqual(6f, pos.x, 0.001f, "X should snap to 4m grid center");
        Assert.AreEqual(1.5f, pos.y, 0.001f, "Y should be halfHeight");
        Assert.AreEqual(5.25f, pos.z, 0.001f, "Z back face flush at grid line 5, center offset by 0.25");
    }

    [Test]
    public void GridPlacement_RotatedWall_SwapsAxes()
    {
        // 4x3x0.5 wall, rotation 90: X and Z extents swap
        // footprintX = 0.5 (thin), footprintZ = 4 (wide)
        // X: thin, Round(5.1) = 5, center = 5 + 0.25 = 5.25
        // Z: Floor(6/4)*4 = 4, center = 4+2 = 6
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.1f, 0f, 6f), prefab, 90);

        Assert.AreEqual(5.25f, pos.x, 0.001f, "X back face flush at grid line 5, center offset by 0.25");
        Assert.AreEqual(1.5f, pos.y, 0.001f, "Y should be halfHeight");
        Assert.AreEqual(6f, pos.z, 0.001f, "Z should snap to 4m grid center");
    }

    [Test]
    public void GridPlacement_WithSurfaceY_AddsToHeight()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(
            new Vector3(5.7f, 3f, 5.7f), prefab, 0);

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
}
