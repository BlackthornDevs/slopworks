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
        foreach (var p in points)
        {
            float dot = Vector3.Dot(p.Normal, normalDir);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = p;
            }
        }
        return best;
    }

    [Test]
    public void SnapPlacement_FoundationToFoundationNorth_AdjacentFlush()
    {
        // Existing 4x0.5x4 foundation at origin
        // Bounds center = (0, 0.25, 0), extents = (2, 0.25, 2)
        // North snap point at (0, 0.25, 2), normal = (0,0,1)
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        // Incoming prefab: same 4x0.5x4 foundation
        _prefab = CreateCube(new Vector3(4f, 0.5f, 4f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0);

        // Snap at Z=2, incoming half-depth along Z = 2, so Z = 2 + 2 = 4
        Assert.AreEqual(4f, pos.z, 0.01f, "Z should be snap Z + incoming half-depth");
        Assert.AreEqual(0f, pos.x, 0.01f, "X should match snap X");
        // Snap Y = 0.25, plus half-height 0.25 = 0.5
        Assert.AreEqual(0.5f, pos.y, 0.01f, "Y should be snap Y + halfHeight");
    }

    [Test]
    public void SnapPlacement_WallOnFoundationEdge_BackFaceFlush()
    {
        // Existing 4x0.5x4 foundation at origin
        // North snap point at (0, 0.25, 2), normal = (0,0,1)
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        // Incoming wall: 4x3x0.5
        // extents = (2, 1.5, 0.25)
        _prefab = CreateCube(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0);

        // Normal = (0,0,1), autoYaw = atan2(0, 1) = 0 degrees
        // Auto-yaw aligns local Z with normal, halfDepth = extents.z = 0.25
        // Z = snap Z (2) + 0.25 = 2.25
        Assert.AreEqual(2.25f, pos.z, 0.01f, "Z should be snap Z + wall half-depth");
        // Y = snap Y (0.25) + halfHeight (1.5) = 1.75
        Assert.AreEqual(1.75f, pos.y, 0.01f, "Y should be snap Y + wall halfHeight");
        Assert.AreEqual(0f, pos.x, 0.01f, "X should match snap X");
    }

    [Test]
    public void SnapPlacement_FoundationOnTop_StacksVertically()
    {
        // Existing 4x0.5x4 foundation at origin
        // Top snap point at (0, 0.5, 0), normal = (0,1,0)
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        // Incoming same foundation
        _prefab = CreateCube(new Vector3(4f, 0.5f, 4f));

        var topSnap = FindSnapByNormal(_existing, Vector3.up);
        Assert.IsNotNull(topSnap, "Should have a top snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(topSnap, _prefab, 0);

        // Top snap at Y=0.5 (bounds center 0.25 + extents.y 0.25)
        // Incoming halfHeight = 0.25, so Y = 0.5 + 0.25 = 0.75
        Assert.AreEqual(0.75f, pos.y, 0.01f, "Y should be top snap Y + halfHeight");
        Assert.AreEqual(0f, pos.x, 0.01f, "X should match snap X");
        Assert.AreEqual(0f, pos.z, 0.01f, "Z should match snap Z");
        Assert.AreEqual(0f, rot.eulerAngles.y, 0.01f, "Rotation should use rotationDeg param");
    }

    [Test]
    public void SnapPlacement_RotationAlignsToNormal()
    {
        // Existing 4x0.5x4 foundation at origin
        // East snap point at (2, 0.25, 0), normal = (1,0,0)
        _existing = CreateCube(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(_existing);

        // Incoming wall: 4x3x0.5
        _prefab = CreateCube(new Vector3(4f, 3f, 0.5f));

        var eastSnap = FindSnapByNormal(_existing, Vector3.right);
        Assert.IsNotNull(eastSnap, "Should have an east snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, _prefab, 0);

        // Normal = (1,0,0), autoYaw = atan2(1, 0) = 90 degrees
        Assert.AreEqual(90f, rot.eulerAngles.y, 0.01f, "Rotation should auto-align to east-facing normal");

        // Auto-yaw rotates wall so its local Z (0.25 half) faces the normal (+X)
        // halfDepth = extents.z = 0.25, so X = snap X (2) + 0.25 = 2.25
        Assert.AreEqual(2.25f, pos.x, 0.01f, "X should be snap X + wall half-depth along normal");
        // Y = snap Y (0.25) + halfHeight (1.5) = 1.75
        Assert.AreEqual(1.75f, pos.y, 0.01f, "Y should be snap Y + wall halfHeight");
    }
}
