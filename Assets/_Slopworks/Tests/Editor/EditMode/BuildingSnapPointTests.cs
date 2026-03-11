using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BuildingSnapPointTests
{
    private GameObject _go;

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
            Object.DestroyImmediate(_go);
    }

    private GameObject CreateCube(float sx, float sy, float sz)
    {
        _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _go.transform.localScale = new Vector3(sx, sy, sz);

        // Force renderer bounds to update
        _go.GetComponent<Renderer>().enabled = true;
        return _go;
    }

    [Test]
    public void GenerateFromBounds_CreatesCardinalAndTopSnapPoints()
    {
        var go = CreateCube(4f, 1f, 4f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        Assert.AreEqual(5, points.Length, "Should create 5 snap points: 4 cardinal + 1 top");
    }

    [Test]
    public void GenerateFromBounds_NormalsPointOutward()
    {
        var go = CreateCube(2f, 2f, 2f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        foreach (var p in points)
        {
            Assert.AreEqual(1f, p.Normal.magnitude, 0.001f,
                $"Normal on {p.gameObject.name} should be unit length");
        }
    }

    [Test]
    public void GenerateFromBounds_TopPointNormalIsUp()
    {
        var go = CreateCube(2f, 3f, 2f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        BuildingSnapPoint topPoint = null;
        float highestY = float.MinValue;

        foreach (var p in points)
        {
            if (p.transform.localPosition.y > highestY)
            {
                highestY = p.transform.localPosition.y;
                topPoint = p;
            }
        }

        Assert.IsNotNull(topPoint, "Should have a top snap point");
        Assert.AreEqual(Vector3.up, topPoint.Normal,
            "Top snap point normal should be (0,1,0)");
    }

    [Test]
    public void GenerateFromBounds_SkipsIfManualPointsExist()
    {
        var go = CreateCube(2f, 2f, 2f);

        // Add a manual snap point
        var manualChild = new GameObject("ManualSnap");
        manualChild.transform.SetParent(go.transform, false);
        manualChild.AddComponent<BuildingSnapPoint>();

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        Assert.AreEqual(1, points.Length,
            "Should not auto-generate when manual snap points exist");
    }

    [Test]
    public void GenerateFromBounds_SurfaceSizeMatchesFaceDimensions()
    {
        // 4 wide, 3 tall, 0.5 deep
        var go = CreateCube(4f, 3f, 0.5f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();

        // Find the top point (normal == up)
        BuildingSnapPoint topPoint = null;
        foreach (var p in points)
        {
            if (Vector3.Distance(p.Normal, Vector3.up) < 0.01f)
            {
                topPoint = p;
                break;
            }
        }

        Assert.IsNotNull(topPoint, "Should have a top snap point");
        // Top face of a 4x3x0.5 shape: width = x extent * 2 = 4, height = z extent * 2 = 0.5
        Assert.AreEqual(4f, topPoint.SurfaceSize.x, 0.01f, "Top face width should match x dimension");
        Assert.AreEqual(0.5f, topPoint.SurfaceSize.y, 0.01f, "Top face depth should match z dimension");
    }

    [Test]
    public void FindNearest_ReturnsClosestToPoint()
    {
        var go = CreateCube(4f, 1f, 4f);

        BuildingSnapPoint.GenerateFromBounds(go);

        // Query a point near the +Z face
        var queryPoint = new Vector3(0, 0, 10f);
        var nearest = BuildingSnapPoint.FindNearest(go, queryPoint);

        Assert.IsNotNull(nearest);
        // The north (+Z) snap point should be closest to a point far along +Z
        Assert.AreEqual(Vector3.forward, nearest.Normal,
            "Nearest to +Z query should be the north snap point");
    }

    [Test]
    public void FindNearest_ReturnsNullWhenNoSnapPoints()
    {
        _go = new GameObject("Empty");

        var result = BuildingSnapPoint.FindNearest(_go, Vector3.zero);

        Assert.IsNull(result);
    }

    [Test]
    public void Normal_UsesTransformForwardWhenNoOverride()
    {
        _go = new GameObject("Test");
        var snap = _go.AddComponent<BuildingSnapPoint>();
        _go.transform.rotation = Quaternion.LookRotation(Vector3.right);

        Assert.AreEqual(1f, Vector3.Dot(snap.Normal, Vector3.right), 0.001f,
            "Normal should match transform.forward when no override is set");
    }
}
