using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SnapPlacementTests
{
    private GameObject _existing;
    private GameObject _prefab;

    [TearDown]
    public void TearDown()
    {
        if (_existing != null)
            Object.DestroyImmediate(_existing);
        if (_prefab != null)
            Object.DestroyImmediate(_prefab);
    }

    private GameObject CreateCube(Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = scale;
        return go;
    }

    /// <summary>
    /// Find the snap point whose normal most closely matches the given direction.
    /// </summary>
    private BuildingSnapPoint FindSnapByNormal(GameObject go, Vector3 normalDir)
    {
        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        BuildingSnapPoint best = null;
        float bestDot = float.MinValue;
        float bestDistFromCenter = float.MaxValue;

        var renderer = go.GetComponentInChildren<Renderer>();
        var boundsCenter = renderer != null ? renderer.bounds.center : go.transform.position;

        foreach (var p in points)
        {
            float dot = Vector3.Dot(p.Normal, normalDir);
            if (dot > bestDot + 0.01f)
            {
                bestDot = dot;
                best = p;
                bestDistFromCenter = Vector3.Distance(p.transform.position, boundsCenter);
            }
            else if (dot > bestDot - 0.01f)
            {
                float dist = Vector3.Distance(p.transform.position, boundsCenter);
                if (dist < bestDistFromCenter)
                {
                    best = p;
                    bestDistFromCenter = dist;
                }
            }
        }
        return best;
    }

    // NOTE: Unity cube localBounds.center = (0,0,0) -- center-origin, not center-bottom.
    // Snap point Y = parent world Y for horizontal normals.
    // Top snap localPos.y = extents.y (0.25 for 0.5-tall cube).

    [Test]
    public void SnapPlacement_FoundationToFoundationNorth_AdjacentFlush()
    {
        // Existing 4x0.5x4 foundation at origin. Cube center at (0,0,0).
        // North snap at (0, 0, 2), normal (0,0,1).
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        _prefab = CreateCube(new Vector3(4f, 0.5f, 4f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0, 0f);

        Assert.AreEqual(4f, pos.z, 0.01f, "Z = snap Z (2) + halfDepth (2)");
        Assert.AreEqual(0f, pos.x, 0.01f, "X matches snap X");
        Assert.AreEqual(0.25f, pos.y, 0.01f, "Y = snap Y (0) + halfHeight (0.25)");
    }

    [Test]
    public void SnapPlacement_WallOnFoundationEdge_BackFaceFlush()
    {
        // North snap at (0, 0, 2), normal (0,0,1).
        // Wall (4,3,0.5): halfDepth = 0.25, halfHeight = 1.5.
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        _prefab = CreateCube(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0, 0f);

        Assert.AreEqual(2.25f, pos.z, 0.01f, "Z = snap Z (2) + halfDepth (0.25)");
        Assert.AreEqual(1.5f, pos.y, 0.01f, "Y = snap Y (0) + halfHeight (1.5)");
        Assert.AreEqual(0f, pos.x, 0.01f, "X matches snap X");
    }

    [Test]
    public void SnapPlacement_FoundationOnTop_StacksVertically()
    {
        // Top snap at (0, 0.25, 0), normal (0,1,0). Vertical snap.
        // Y = snap Y (0.25) + halfHeight (0.25) = 0.5.
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        _prefab = CreateCube(new Vector3(4f, 0.5f, 4f));

        var topSnap = FindSnapByNormal(_existing, Vector3.up);
        Assert.IsNotNull(topSnap, "Should have a top snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(topSnap, _prefab, 0, 0f);

        Assert.AreEqual(0.5f, pos.y, 0.01f, "Y = top snap Y (0.25) + halfHeight (0.25)");
        Assert.AreEqual(0f, pos.x, 0.01f, "X matches snap X");
        Assert.AreEqual(0f, pos.z, 0.01f, "Z matches snap Z");
        Assert.AreEqual(0f, rot.eulerAngles.y, 0.01f, "Rotation from param");
    }

    [Test]
    public void SnapPlacement_RotationAlignsToNormal()
    {
        // East snap at (2, 0, 0), normal (1,0,0). autoYaw = 90.
        // Wall halfDepth = 0.25. X = 2 + 0.25 = 2.25.
        // Y = snap Y (0) + halfHeight (1.5) = 1.5.
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        _prefab = CreateCube(new Vector3(4f, 3f, 0.5f));

        var eastSnap = FindSnapByNormal(_existing, Vector3.right);
        Assert.IsNotNull(eastSnap, "Should have an east snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, _prefab, 90, 0f);

        Assert.AreEqual(90f, rot.eulerAngles.y, 0.01f, "Auto-align to east normal");
        Assert.AreEqual(2.25f, pos.x, 0.01f, "X = snap X (2) + halfDepth (0.25)");
        Assert.AreEqual(1.5f, pos.y, 0.01f, "Y = snap Y (0) + halfHeight (1.5)");
    }
}
