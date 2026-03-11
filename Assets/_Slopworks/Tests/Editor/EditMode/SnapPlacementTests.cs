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

    private GameObject CreateTestBuildingWithSnaps(Vector3 scale, BuildingCategory category = BuildingCategory.Foundation)
    {
        var go = CreateCube(scale);
        BuildingSnapPoint.GenerateFromBounds(go, category);
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

    private BuildingSnapPoint FindSnapByName(GameObject go, string nameFragment)
    {
        foreach (var p in go.GetComponentsInChildren<BuildingSnapPoint>())
        {
            if (p.gameObject.name.Contains(nameFragment))
                return p;
        }
        return null;
    }

    // Snap-to-snap: target snap meets ghost's opposite snap.
    // ghostPos = targetSnapWorldPos - Rot * ghostSnapLocalPos.
    // No extents/offsets -- positions are baked into snap point children.

    [Test]
    public void SnapPlacement_FoundationToFoundationNorth_AdjacentFlush()
    {
        // Existing 4x0.5x4 at origin. North_Mid at (0, 0, 2), normal (0,0,1).
        // Ghost 4x0.5x4. Ghost's South_Mid at local (0, 0, -2).
        // ghostPos = (0,0,2) - (0,0,-2) = (0, 0, 4). Surfaces flush at Z=2.
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0, 0f);

        Assert.AreEqual(4f, pos.z, 0.01f, "Z: surfaces flush at Z=2");
        Assert.AreEqual(0f, pos.x, 0.01f, "X: centers aligned");
        Assert.AreEqual(0f, pos.y, 0.01f, "Y: Mid-to-Mid centers aligned");
    }

    [Test]
    public void SnapPlacement_WallOnFoundationEdge_BackFaceFlush()
    {
        // Foundation North_Mid at (0, 0, 2), normal (0,0,1).
        // Wall ghost South_Mid at local (0, 0, -0.25).
        // ghostPos = (0, 0, 2) - (0, 0, -0.25) = (0, 0, 2.25). Flush at Z=2.
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(_existing, Vector3.forward);
        Assert.IsNotNull(northSnap, "Should have a north snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, _prefab, 0, 0f);

        Assert.AreEqual(2.25f, pos.z, 0.01f, "Z: wall back flush with foundation north");
        Assert.AreEqual(0f, pos.y, 0.01f, "Y: Mid-to-Mid centers aligned");
        Assert.AreEqual(0f, pos.x, 0.01f, "X: centers aligned");
    }

    [Test]
    public void SnapPlacement_FoundationOnTop_StacksVertically()
    {
        // Center_Top at (0, 0.25, 0), normal (0,1,0).
        // Ghost's Center_Bot at local (0, -0.25, 0).
        // ghostPos = (0, 0.25, 0) - (0, -0.25, 0) = (0, 0.5, 0).
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));

        var topSnap = FindSnapByNormal(_existing, Vector3.up);
        Assert.IsNotNull(topSnap, "Should have a top snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(topSnap, _prefab, 0, 0f);

        Assert.AreEqual(0.5f, pos.y, 0.01f, "Y: ghost bottom at existing top");
        Assert.AreEqual(0f, pos.x, 0.01f, "X: centers aligned");
        Assert.AreEqual(0f, pos.z, 0.01f, "Z: centers aligned");
        Assert.AreEqual(0f, rot.eulerAngles.y, 0.01f, "Rotation from param");
    }

    [Test]
    public void SnapPlacement_RotationAlignsToNormal()
    {
        // East_Mid at (2, 0, 0), normal (1,0,0). Rotation 90.
        // Ghost at 90 deg: desiredLocal = Inv(90Y) * (-1,0,0) = (0,0,-1) = South.
        // Ghost's South_Mid at local (0, 0, -0.25). Rotated by 90Y: (-0.25, 0, 0).
        // ghostPos = (2, 0, 0) - (-0.25, 0, 0) = (2.25, 0, 0).
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));

        var eastSnap = FindSnapByNormal(_existing, Vector3.right);
        Assert.IsNotNull(eastSnap, "Should have an east snap point");

        var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, _prefab, 90, 0f);

        Assert.AreEqual(90f, rot.eulerAngles.y, 0.01f, "Rotation = 90");
        Assert.AreEqual(2.25f, pos.x, 0.01f, "X: wall flush with foundation east");
        Assert.AreEqual(0f, pos.y, 0.01f, "Y: Mid-to-Mid centers aligned");
    }

    [Test]
    public void SnapPlacement_BottomSnap_HangsBelowExistingBuilding()
    {
        // Center_Bot at (0, -0.25, 0), normal (0,-1,0).
        // Ghost's Center_Top at local (0, 0.25, 0).
        // ghostPos = (0, -0.25, 0) - (0, 0.25, 0) = (0, -0.5, 0).
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));

        var botSnap = FindSnapByNormal(_existing, Vector3.down);
        Assert.IsNotNull(botSnap, "Should have a bottom snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(botSnap, _prefab, 0, 0f);

        Assert.AreEqual(-0.5f, pos.y, 0.01f, "Y: ghost top at existing bottom");
        Assert.AreEqual(0f, pos.x, 0.01f, "X: centers aligned");
        Assert.AreEqual(0f, pos.z, 0.01f, "Z: centers aligned");
    }

    [Test]
    public void SnapPlacement_HorizontalBot_ExtendsDownward()
    {
        // Wall North_Bot at (0, -1.5, 0.25), normal (0,0,1).
        // Ghost wall South_Top at local (0, 1.5, -0.25). Opposite normal, opposite tier.
        // ghostPos = (0, -1.5, 0.25) - (0, 1.5, -0.25) = (0, -3.0, 0.5).
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));

        var northBot = FindSnapByName(_existing, "North_Bot");
        Assert.IsNotNull(northBot, "Should have a North_Bot snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northBot, _prefab, 0, 0f);

        Assert.AreEqual(-3.0f, pos.y, 0.01f, "Y: ghost extends downward from target bottom");
    }

    [Test]
    public void SnapPlacement_HorizontalTop_ExtendsUpward()
    {
        // Wall North_Top at (0, 1.5, 0.25), normal (0,0,1).
        // Ghost wall South_Bot at local (0, -1.5, -0.25). Opposite normal, opposite tier.
        // ghostPos = (0, 1.5, 0.25) - (0, -1.5, -0.25) = (0, 3.0, 0.5).
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 3f, 0.5f));

        var northTop = FindSnapByName(_existing, "North_Top");
        Assert.IsNotNull(northTop, "Should have a North_Top snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(northTop, _prefab, 0, 0f);

        Assert.AreEqual(3.0f, pos.y, 0.01f, "Y: ghost extends upward from target top");
    }

    [Test]
    public void SnapPlacement_BottomSnap_SurfaceYDiffersFromExisting()
    {
        // Center_Bot placement must produce different surfaceY than existing building
        // so HasBuildingAt doesn't block it.
        _existing = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));
        _prefab = CreateTestBuildingWithSnaps(new Vector3(4f, 0.5f, 4f));

        var botSnap = FindSnapByNormal(_existing, Vector3.down);
        Assert.IsNotNull(botSnap, "Should have a bottom snap point");

        var (ghostPos, _) = GridManager.GetSnapPlacementPosition(botSnap, _prefab, 0, 0f);

        float placeSurfaceY = ghostPos.y - GridManager.GetPrefabBaseOffset(_prefab);

        Assert.Less(placeSurfaceY, -0.5f, "Bottom snap surfaceY must be below existing building");
    }
}
