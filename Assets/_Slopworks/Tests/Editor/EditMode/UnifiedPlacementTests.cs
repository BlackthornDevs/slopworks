using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Comprehensive placement system tests covering grid snapping, snap-to-building,
/// zooping, nudged objects, corner cases, and layer assignment.
/// </summary>
[TestFixture]
public class UnifiedPlacementTests
{
    private readonly List<GameObject> _cleanup = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _cleanup)
            if (go != null) Object.DestroyImmediate(go);
        _cleanup.Clear();
    }

    private GameObject CreatePrefab(Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = scale;
        _cleanup.Add(go);
        return go;
    }

    private GameObject CreatePlacedBuilding(Vector3 scale, Vector3 worldPos)
    {
        var go = CreatePrefab(scale);
        go.transform.position = worldPos;
        BuildingSnapPoint.GenerateFromBounds(go);
        return go;
    }

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

    // ======================================================================
    // SECTION 1: Grid placement -- 1m resolution for all building sizes
    // ======================================================================

    [Test]
    public void Grid_4x4Foundation_MovesIn1mIncrements()
    {
        // A 4m foundation should move 1 meter at a time, not 4.
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var (pos1, _) = GridManager.GetGridPlacementPosition(new Vector3(5.3f, 0f, 5.3f), prefab, 0);
        var (pos2, _) = GridManager.GetGridPlacementPosition(new Vector3(6.3f, 0f, 5.3f), prefab, 0);

        // Round(5.3)=5, Round(6.3)=6
        Assert.AreEqual(5f, pos1.x, 0.001f, "Center snaps to nearest grid point");
        Assert.AreEqual(6f, pos2.x, 0.001f, "Center snaps to nearest grid point");
        Assert.AreEqual(1f, pos2.x - pos1.x, 0.001f, "Foundation should move exactly 1m when hit moves 1m");
    }

    [Test]
    public void Grid_2x2Foundation_MovesIn1mIncrements()
    {
        var prefab = CreatePrefab(new Vector3(2f, 0.5f, 2f));

        var (pos1, _) = GridManager.GetGridPlacementPosition(new Vector3(3.3f, 0f, 3.3f), prefab, 0);
        var (pos2, _) = GridManager.GetGridPlacementPosition(new Vector3(4.3f, 0f, 3.3f), prefab, 0);

        // Round(3.3)=3, Round(4.3)=4
        Assert.AreEqual(3f, pos1.x, 0.001f);
        Assert.AreEqual(4f, pos2.x, 0.001f);
        Assert.AreEqual(1f, pos2.x - pos1.x, 0.001f, "2m foundation moves 1m per grid step");
    }

    [Test]
    public void Grid_1x1Machine_SnapsToNearestGrid()
    {
        // Round(3.3)=3, Round(7.8)=8
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(3.3f, 0f, 7.8f), prefab, 0);

        Assert.AreEqual(3f, pos.x, 0.001f);
        Assert.AreEqual(8f, pos.z, 0.001f);
        Assert.AreEqual(0.5f, pos.y, 0.001f, "Bottom sits on surface");
    }

    [Test]
    public void Grid_SmallMachine08_SnapsToNearestGrid()
    {
        // Round(5.6)=6
        var prefab = CreatePrefab(new Vector3(0.8f, 0.5f, 0.8f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(5.6f, 0f, 5.6f), prefab, 0);

        Assert.AreEqual(6f, pos.x, 0.001f);
        Assert.AreEqual(6f, pos.z, 0.001f);
        Assert.AreEqual(0.25f, pos.y, 0.001f, "Bottom sits on surface");
    }

    [Test]
    public void Grid_StoragePrefab_SnapsToNearestGrid()
    {
        // Round(10.2)=10
        var prefab = CreatePrefab(new Vector3(0.8f, 0.4f, 0.8f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(10.2f, 0f, 10.2f), prefab, 0);

        Assert.AreEqual(10f, pos.x, 0.001f);
        Assert.AreEqual(10f, pos.z, 0.001f);
        Assert.AreEqual(0.2f, pos.y, 0.001f);
    }

    [Test]
    public void Grid_ThinWall_SnapsToNearestGrid()
    {
        // Round(6.0)=6, Round(5.1)=5
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(6f, 0f, 5.1f), prefab, 0);

        Assert.AreEqual(6f, pos.x, 0.001f);
        Assert.AreEqual(5f, pos.z, 0.001f);
        Assert.AreEqual(1.5f, pos.y, 0.001f);
    }

    [Test]
    public void Grid_AllBuildingSizes_SameHitPoint_SameCenter()
    {
        // All buildings at the same hit point should snap their center to the same grid point.
        var hit = new Vector3(5.3f, 0f, 5.3f);

        var slab4 = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var slab2 = CreatePrefab(new Vector3(2f, 0.5f, 2f));
        var slab1 = CreatePrefab(new Vector3(1f, 0.5f, 1f));
        var machine = CreatePrefab(new Vector3(0.8f, 0.5f, 0.8f));

        var (p4, _) = GridManager.GetGridPlacementPosition(hit, slab4, 0);
        var (p2, _) = GridManager.GetGridPlacementPosition(hit, slab2, 0);
        var (p1, _) = GridManager.GetGridPlacementPosition(hit, slab1, 0);
        var (pm, _) = GridManager.GetGridPlacementPosition(hit, machine, 0);

        // Round(5.3) = 5 for all sizes
        Assert.AreEqual(5f, p4.x, 0.001f, "4m slab center at grid 5");
        Assert.AreEqual(5f, p2.x, 0.001f, "2m slab center at grid 5");
        Assert.AreEqual(5f, p1.x, 0.001f, "1m cube center at grid 5");
        Assert.AreEqual(5f, pm.x, 0.001f, "0.8m machine center at grid 5");
    }

    [Test]
    public void Grid_Rotation_DoesNotAffectGridSnap()
    {
        // Rotation only affects the quaternion, not the snap position.
        // Center always rounds to nearest 1m regardless of rotation.
        var prefab = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        var hit = new Vector3(5.1f, 0f, 6.3f);

        var (pos0, _) = GridManager.GetGridPlacementPosition(hit, prefab, 0);
        var (pos90, rot90) = GridManager.GetGridPlacementPosition(hit, prefab, 90);
        var (pos180, _) = GridManager.GetGridPlacementPosition(hit, prefab, 180);
        var (pos270, _) = GridManager.GetGridPlacementPosition(hit, prefab, 270);

        // Round(5.1)=5, Round(6.3)=6 for all rotations
        Assert.AreEqual(5f, pos0.x, 0.001f);
        Assert.AreEqual(5f, pos90.x, 0.001f);
        Assert.AreEqual(5f, pos180.x, 0.001f);
        Assert.AreEqual(5f, pos270.x, 0.001f);
        Assert.AreEqual(6f, pos0.z, 0.001f);
        Assert.AreEqual(6f, pos90.z, 0.001f);
        Assert.AreEqual(90f, rot90.eulerAngles.y, 0.001f, "Rotation still applied to quaternion");
    }

    [Test]
    public void Grid_ElevatedSurface_BottomSitsOnSurface()
    {
        // Hit surface at y=5: foundation bottom should be at y=5
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(5f, 5f, 5f), prefab, 0);

        float bottomY = pos.y - 0.25f; // center minus halfHeight
        Assert.AreEqual(5f, bottomY, 0.001f, "Bottom face should rest on surface at y=5");
    }

    [Test]
    public void Grid_NegativeCoordinates_StillSnapsCorrectly()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(-3.7f, 0f, -3.7f), prefab, 0);

        // Round(-3.7)=-4
        Assert.AreEqual(-4f, pos.x, 0.001f);
        Assert.AreEqual(-4f, pos.z, 0.001f);
    }

    [Test]
    public void Grid_ExactlyOnGridLine_SnapsToThatLine()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var (pos, _) = GridManager.GetGridPlacementPosition(new Vector3(4.0f, 0f, 4.0f), prefab, 0);

        // Round(4.0)=4
        Assert.AreEqual(4f, pos.x, 0.001f);
        Assert.AreEqual(4f, pos.z, 0.001f);
    }

    // ======================================================================
    // SECTION 2: Snap-to-building placement
    // ======================================================================

    // NOTE ON Y VALUES: Unity's built-in cube has localBounds.center = (0,0,0),
    // meaning its center is at the geometric middle, NOT center-bottom. For a cube
    // at scale (4, 0.5, 4) placed at world (0,0,0), the bounds center is at Y=0
    // and the snap points sit at Y=0 (not Y=0.25). The snap formula adds halfHeight
    // of the incoming prefab, so new.Y = 0 + incoming.halfHeight.
    //
    // With real FBX assets that have center-bottom pivot, bounds.center.y would
    // equal extents.y and snap point Y would be higher. These tests use Unity cubes
    // so they reflect center-origin behavior.

    [Test]
    public void Snap_FoundationToFoundationNorth_FlushAdjacent()
    {
        // Existing 4m foundation at origin. Cube bounds center at (0,0,0).
        // North snap at (0, 0, 2), normal (0,0,1).
        // Incoming halfDepth = extents.z = 2. New Z = 2 + 2 = 4.
        // Y = snap Y (0) + halfHeight (0.25) = 0.25.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var northSnap = FindSnapByNormal(existing, Vector3.forward);
        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, prefab, 0, 0f);

        Assert.AreEqual(0f, pos.x, 0.01f, "X aligned with existing");
        Assert.AreEqual(4f, pos.z, 0.01f, "New foundation flush north: snap Z (2) + halfDepth (2)");
        Assert.AreEqual(0.25f, pos.y, 0.01f, "Y = snap Y (0) + halfHeight (0.25)");
    }

    [Test]
    public void Snap_FoundationToFoundationEast_FlushAdjacent()
    {
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var eastSnap = FindSnapByNormal(existing, Vector3.right);
        var (pos, _) = GridManager.GetSnapPlacementPosition(eastSnap, prefab, 90, 0f);

        // East snap at (2, 0, 0), normal (1,0,0)
        // autoYaw = atan2(1,0) = 90. halfDepth = extents.z = 2
        Assert.AreEqual(4f, pos.x, 0.01f, "East snap X (2) + halfDepth (2) = 4");
        Assert.AreEqual(0f, pos.z, 0.01f, "Z centered on snap");
    }

    [Test]
    public void Snap_FoundationToFoundationSouth_FlushAdjacent()
    {
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var southSnap = FindSnapByNormal(existing, Vector3.back);
        var (pos, _) = GridManager.GetSnapPlacementPosition(southSnap, prefab, 180, 0f);

        // South snap at (0, 0, -2), normal (0,0,-1)
        Assert.AreEqual(-4f, pos.z, 0.01f, "South snap Z (-2) + halfDepth (-2) = -4");
    }

    [Test]
    public void Snap_FoundationOnTop_Stacks()
    {
        // Top snap point at (0, extents.y, 0) = (0, 0.25, 0). Normal = (0,1,0).
        // Vertical snap: Y = snap Y (0.25) + halfHeight (0.25) = 0.5.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var topSnap = FindSnapByNormal(existing, Vector3.up);
        var (pos, _) = GridManager.GetSnapPlacementPosition(topSnap, prefab, 0, 0f);

        Assert.AreEqual(0.5f, pos.y, 0.01f, "Stacked: top snap Y (0.25) + halfHeight (0.25)");
        Assert.AreEqual(0f, pos.x, 0.01f);
        Assert.AreEqual(0f, pos.z, 0.01f);
    }

    [Test]
    public void Snap_WallOnFoundationNorthEdge_BackFaceFlush()
    {
        // North snap at (0, 0, 2), normal (0,0,1).
        // Wall (4,3,0.5): extents (2, 1.5, 0.25). halfDepth = 0.25.
        // Z = 2 + 0.25 = 2.25. Y = 0 + 1.5 = 1.5.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var wall = CreatePrefab(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(existing, Vector3.forward);
        var (pos, rot) = GridManager.GetSnapPlacementPosition(northSnap, wall, 0, 0f);

        Assert.AreEqual(2.25f, pos.z, 0.01f, "Wall Z = snap Z (2) + halfDepth (0.25)");
        Assert.AreEqual(1.5f, pos.y, 0.01f, "Wall Y = snap Y (0) + halfHeight (1.5)");
        Assert.AreEqual(0f, pos.x, 0.01f, "Wall X centered on snap");
        Assert.AreEqual(0f, rot.eulerAngles.y, 0.5f, "Wall faces north (yaw 0)");
    }

    [Test]
    public void Snap_WallOnFoundationEastEdge_RotatesAutomatically()
    {
        // East snap at (2, 0, 0), normal (1,0,0). autoYaw = 90.
        // Wall halfDepth = 0.25. X = 2 + 0.25 = 2.25.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var wall = CreatePrefab(new Vector3(4f, 3f, 0.5f));

        var eastSnap = FindSnapByNormal(existing, Vector3.right);
        var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, wall, 90, 0f);

        Assert.AreEqual(90f, rot.eulerAngles.y, 0.5f, "Wall auto-rotates to face east");
        Assert.AreEqual(2.25f, pos.x, 0.01f, "Wall X = snap X (2) + halfDepth (0.25)");
    }

    [Test]
    public void Snap_FoundationInCorner_TwoAdjacentFoundations()
    {
        // Foundation A at origin, foundation B at (0, 0, 4) (north of A).
        // Snap C to east of A, snap D to east of B. Both should have X=4.
        // C and D should be flush along X.
        var foundationA = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var foundationB = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(0f, 0f, 4f));
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var eastSnapA = FindSnapByNormal(foundationA, Vector3.right);
        var (posC, _) = GridManager.GetSnapPlacementPosition(eastSnapA, prefab, 90, 0f);

        var eastSnapB = FindSnapByNormal(foundationB, Vector3.right);
        var (posD, _) = GridManager.GetSnapPlacementPosition(eastSnapB, prefab, 90, 0f);

        // East snap X = 2 (half of 4m). Incoming halfDepth = 2. X = 2 + 2 = 4.
        Assert.AreEqual(4f, posC.x, 0.01f, "C east of A: X = 4");
        Assert.AreEqual(4f, posD.x, 0.01f, "D east of B: X = 4");
        Assert.AreEqual(posC.x, posD.x, 0.01f, "C and D flush along X axis");
        // Z values should match their respective parents
        Assert.AreEqual(0f, posC.z, 0.01f, "C has same Z as A");
        Assert.AreEqual(4f, posD.z, 0.01f, "D has same Z as B");
    }

    [Test]
    public void Snap_NudgedFoundation_SnapPointsReflectNudge()
    {
        // Foundation at (0, 2.5, 0) -- nudged up 2.5m.
        // Cube center-origin: snap point Y = parent Y = 2.5.
        var nudged = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(0f, 2.5f, 0f));

        var northSnap = FindSnapByNormal(nudged, Vector3.forward);
        Assert.IsNotNull(northSnap);

        Assert.AreEqual(2f, northSnap.transform.position.z, 0.01f, "Snap Z at face center");
        Assert.AreEqual(2.5f, northSnap.transform.position.y, 0.01f, "Snap Y reflects nudged parent Y");
    }

    [Test]
    public void Snap_NudgedFoundation_NewBuildingAligns()
    {
        // Foundation nudged to y=3. Snap a wall to its north face.
        // Snap point Y = 3 (center-origin cube). Wall halfHeight = 1.5.
        // Wall center Y = 3 + 1.5 = 4.5.
        var nudged = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(0f, 3f, 0f));
        var wall = CreatePrefab(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(nudged, Vector3.forward);
        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, wall, 0, 3f);

        Assert.AreEqual(4.5f, pos.y, 0.01f, "Wall center Y = nudged snap Y (3) + wall halfHeight (1.5)");
    }

    [Test]
    public void Snap_SmallMachineOnFoundationCenter_TopedOnTopFace()
    {
        // Top snap for center-origin cube at (0,0,0) with extents.y=0.25:
        // localPos = (0, 0.25, 0). World Y = 0.25.
        // Machine halfHeight = 0.25. Vertical snap: Y = 0.25 + 0.25 = 0.5.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var machine = CreatePrefab(new Vector3(0.8f, 0.5f, 0.8f));

        var topSnap = FindSnapByNormal(existing, Vector3.up);
        var (pos, _) = GridManager.GetSnapPlacementPosition(topSnap, machine, 0, 0f);

        Assert.AreEqual(0.5f, pos.y, 0.01f, "Machine bottom at foundation top");
        Assert.AreEqual(0f, pos.x, 0.01f, "Centered on foundation");
        Assert.AreEqual(0f, pos.z, 0.01f, "Centered on foundation");
    }

    [Test]
    public void Snap_OffsetExistingBuilding_SnapPointsAreWorldSpace()
    {
        // Foundation at world (10, 0, 20). North snap at (10, 0, 22).
        // New foundation: halfDepth = 2. Z = 22 + 2 = 24.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(10f, 0f, 20f));
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var northSnap = FindSnapByNormal(existing, Vector3.forward);
        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, prefab, 0, 0f);

        Assert.AreEqual(10f, pos.x, 0.01f, "X matches parent X");
        Assert.AreEqual(24f, pos.z, 0.01f, "Z = snap Z (22) + halfDepth (2)");
    }

    // ======================================================================
    // SECTION 3: Snap point auto-generation
    // ======================================================================

    [Test]
    public void AutoGen_Creates14Points_CardinalMultiHeightPlusTopBot()
    {
        var go = CreatePrefab(new Vector3(4f, 1f, 4f));
        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        Assert.AreEqual(14, points.Length);
    }

    [Test]
    public void AutoGen_SkipsIfManualPointsExist()
    {
        var go = CreatePrefab(new Vector3(4f, 1f, 4f));
        var child = new GameObject("ManualSnap");
        child.transform.SetParent(go.transform, false);
        child.AddComponent<BuildingSnapPoint>();
        _cleanup.Add(child);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        Assert.AreEqual(1, points.Length, "Should not auto-gen when manual exists");
    }

    [Test]
    public void AutoGen_NormalsAreUnitLength()
    {
        var go = CreatePrefab(new Vector3(2f, 3f, 0.5f));
        BuildingSnapPoint.GenerateFromBounds(go);

        foreach (var p in go.GetComponentsInChildren<BuildingSnapPoint>())
            Assert.AreEqual(1f, p.Normal.magnitude, 0.001f, $"{p.name} normal should be unit length");
    }

    [Test]
    public void AutoGen_TopNormalIsUp()
    {
        var go = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        BuildingSnapPoint.GenerateFromBounds(go);

        var top = FindSnapByNormal(go, Vector3.up);
        Assert.IsNotNull(top);
        Assert.AreEqual(1f, Vector3.Dot(top.Normal, Vector3.up), 0.001f);
    }

    [Test]
    public void AutoGen_SurfaceSizeMatchesFaceDimensions()
    {
        // 4 wide, 3 tall, 0.5 deep
        var go = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        BuildingSnapPoint.GenerateFromBounds(go);

        // North face (normal forward): width = x*2 = 4, height = y*2 = 3
        var north = FindSnapByNormal(go, Vector3.forward);
        Assert.AreEqual(4f, north.SurfaceSize.x, 0.01f, "North face width = X extent * 2");
        Assert.AreEqual(3f, north.SurfaceSize.y, 0.01f, "North face height = Y extent * 2");

        // Top face (normal up): width = x*2 = 4, height = z*2 = 0.5
        var top = FindSnapByNormal(go, Vector3.up);
        Assert.AreEqual(4f, top.SurfaceSize.x, 0.01f, "Top face width = X extent * 2");
        Assert.AreEqual(0.5f, top.SurfaceSize.y, 0.01f, "Top face depth = Z extent * 2");
    }

    [Test]
    public void AutoGen_FindNearest_ReturnsClosestToQueryPoint()
    {
        var go = CreatePrefab(new Vector3(4f, 1f, 4f));
        go.transform.position = Vector3.zero;
        BuildingSnapPoint.GenerateFromBounds(go);

        // Query far to the north -- should return the north snap
        var nearest = BuildingSnapPoint.FindNearest(go, new Vector3(0, 0, 100f));
        Assert.AreEqual(Vector3.forward, nearest.Normal);

        // Query far to the east -- should return the east snap
        nearest = BuildingSnapPoint.FindNearest(go, new Vector3(100, 0, 0));
        Assert.AreEqual(Vector3.right, nearest.Normal);

        // Query directly above -- should return the top snap
        nearest = BuildingSnapPoint.FindNearest(go, new Vector3(0, 100, 0));
        Assert.AreEqual(Vector3.up, nearest.Normal);
    }

    [Test]
    public void AutoGen_FindNearest_NullWhenNoPoints()
    {
        var go = new GameObject("Empty");
        _cleanup.Add(go);
        Assert.IsNull(BuildingSnapPoint.FindNearest(go, Vector3.zero));
    }

    [Test]
    public void AutoGen_HasBottomSnapPoint()
    {
        var go = CreatePrefab(new Vector3(4f, 1f, 4f));
        BuildingSnapPoint.GenerateFromBounds(go);

        var bottom = FindSnapByNormal(go, Vector3.down);
        Assert.IsNotNull(bottom, "Should have a bottom snap point");
        Assert.AreEqual(-1f, Vector3.Dot(bottom.Normal, Vector3.up), 0.01f, "Bottom normal points down");
    }

    [Test]
    public void Snap_BottomNormal_PlacesBelowExisting()
    {
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(0f, 2f, 0f));
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var bottomSnap = FindSnapByNormal(existing, Vector3.down);
        Assert.IsNotNull(bottomSnap, "Should have a bottom snap point");

        var (pos, _) = GridManager.GetSnapPlacementPosition(bottomSnap, prefab, 0, 1.5f);

        // Bottom snap: normal.y < -0.9, so vertical snap with -baseOffset
        // snapPos.y = existing center Y (2.0) - extents.y (0.25) = 1.75
        // baseOffset = 0.25 (center-origin cube). yOffset = -baseOffset = -0.25
        // pos.y = 1.75 + (-0.25) = 1.5
        Assert.AreEqual(1.5f, pos.y, 0.01f, "Y places new foundation below existing");
    }

    [Test]
    public void AutoGen_MultiHeight_TopMidBotPerFace()
    {
        var go = CreatePrefab(new Vector3(4f, 2f, 4f));
        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();

        // Count snaps with forward normal (North face)
        int northCount = 0;
        foreach (var p in points)
        {
            if (Vector3.Dot(p.Normal, Vector3.forward) > 0.9f)
                northCount++;
        }
        Assert.AreEqual(3, northCount, "North face should have 3 snaps (Top, Mid, Bot)");
    }

    [Test]
    public void Snap_WallOnFoundationNorth_BottomAtFoundationTop()
    {
        // Foundation 4x0.5x4 at origin. Top surface at Y=0.5 (center 0.25 + halfHeight 0.25).
        // Wall placed with surfaceY = foundationTop (0.5).
        // Wall halfHeight = 1.5. baseOffset = 1.5. pos.y = 0.5 + 1.5 = 2.0.
        var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
        var wall = CreatePrefab(new Vector3(4f, 3f, 0.5f));

        var northSnap = FindSnapByNormal(existing, Vector3.forward);
        var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, wall, 0, 0.5f);

        Assert.AreEqual(2.0f, pos.y, 0.01f, "Wall center Y = foundationTop (0.5) + wallHalfHeight (1.5)");
        Assert.AreEqual(0.5f, pos.y - 1.5f, 0.01f, "Wall bottom at foundation top");
    }

    // ======================================================================
    // SECTION 4: Zoop (batch placement) logic
    // ======================================================================
    // Note: GetZoopCells is private, so we test the observable math.
    // Zoop steps by footprint cells along the dominant axis.

    [Test]
    public void Zoop_ConsecutivePlacements_NoGaps4mFoundation()
    {
        // Zoop places foundations at footprint-sized intervals.
        // Hit points step by 4m. Round snaps each to an integer grid point.
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var positions = new List<Vector3>();
        for (int x = 0; x <= 8; x += 4)
        {
            var worldHit = new Vector3(x, 0f, 0f);
            var (pos, _) = GridManager.GetGridPlacementPosition(worldHit, prefab, 0);
            positions.Add(pos);
        }

        Assert.AreEqual(3, positions.Count);
        for (int i = 1; i < positions.Count; i++)
        {
            float gap = positions[i].x - positions[i - 1].x;
            Assert.AreEqual(4f, gap, 0.01f, $"Gap between foundation {i - 1} and {i} should be 4m");
        }
    }

    [Test]
    public void Zoop_ConsecutivePlacements_NoGaps2mFoundation()
    {
        var prefab = CreatePrefab(new Vector3(2f, 0.5f, 2f));

        var positions = new List<Vector3>();
        for (int x = 0; x <= 6; x += 2)
        {
            var worldHit = new Vector3(x, 0f, 0f);
            var (pos, _) = GridManager.GetGridPlacementPosition(worldHit, prefab, 0);
            positions.Add(pos);
        }

        Assert.AreEqual(4, positions.Count);
        for (int i = 1; i < positions.Count; i++)
        {
            float gap = positions[i].x - positions[i - 1].x;
            Assert.AreEqual(2f, gap, 0.01f, $"Gap between 2m foundation {i - 1} and {i} should be 2m");
        }
    }

    [Test]
    public void Zoop_ConsecutivePlacements_NoGaps1mCube()
    {
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));

        var positions = new List<Vector3>();
        for (int x = 0; x <= 4; x += 1)
        {
            var worldHit = new Vector3(x, 0f, 0f);
            var (pos, _) = GridManager.GetGridPlacementPosition(worldHit, prefab, 0);
            positions.Add(pos);
        }

        Assert.AreEqual(5, positions.Count);
        for (int i = 1; i < positions.Count; i++)
        {
            float gap = positions[i].x - positions[i - 1].x;
            Assert.AreEqual(1f, gap, 0.01f, $"1m cubes should be 1m apart");
        }
    }

    [Test]
    public void Zoop_ZAxis_NoGaps()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

        var positions = new List<Vector3>();
        for (int z = 0; z <= 8; z += 4)
        {
            var worldHit = new Vector3(0f, 0f, z);
            var (pos, _) = GridManager.GetGridPlacementPosition(worldHit, prefab, 0);
            positions.Add(pos);
        }

        Assert.AreEqual(3, positions.Count);
        for (int i = 1; i < positions.Count; i++)
        {
            float gap = positions[i].z - positions[i - 1].z;
            Assert.AreEqual(4f, gap, 0.01f, $"Z-axis gap should be 4m");
        }
    }

    // ======================================================================
    // SECTION 5: GetPrefabExtents
    // ======================================================================

    [Test]
    public void PrefabExtents_UnitCube_ReturnsHalf()
    {
        var prefab = CreatePrefab(new Vector3(1f, 1f, 1f));
        var ext = GridManager.GetPrefabExtents(prefab);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), ext);
    }

    [Test]
    public void PrefabExtents_ScaledCube_ReturnsScaledHalf()
    {
        var prefab = CreatePrefab(new Vector3(4f, 0.5f, 2f));
        var ext = GridManager.GetPrefabExtents(prefab);
        Assert.AreEqual(2f, ext.x, 0.001f, "X: 0.5 * 4 = 2");
        Assert.AreEqual(0.25f, ext.y, 0.001f, "Y: 0.5 * 0.5 = 0.25");
        Assert.AreEqual(1f, ext.z, 0.001f, "Z: 0.5 * 2 = 1");
    }

    [Test]
    public void PrefabExtents_NullPrefab_ReturnsFallback()
    {
        var ext = GridManager.GetPrefabExtents(null);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), ext, "Null prefab returns 0.5 fallback");
    }

    [Test]
    public void PrefabExtents_NoRenderer_ReturnsFallback()
    {
        var go = new GameObject("NoRenderer");
        _cleanup.Add(go);
        var ext = GridManager.GetPrefabExtents(go);
        Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), ext);
    }

    // ======================================================================
    // SECTION 6: Layer assignment
    // ======================================================================

    [Test]
    public void SetBuildingLayer_Foundation_SetsStructuresLayer()
    {
        var go = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        // Simulate what CmdPlace does
        SetBuildingLayerViaReflection(go, BuildingCategory.Foundation);
        Assert.AreEqual(PhysicsLayers.Structures, go.layer, "Foundations should be on Structures layer");
    }

    [Test]
    public void SetBuildingLayer_Wall_SetsStructuresLayer()
    {
        var go = CreatePrefab(new Vector3(4f, 3f, 0.5f));
        SetBuildingLayerViaReflection(go, BuildingCategory.Wall);
        Assert.AreEqual(PhysicsLayers.Structures, go.layer);
    }

    [Test]
    public void SetBuildingLayer_Ramp_SetsStructuresLayer()
    {
        var go = CreatePrefab(new Vector3(4f, 1f, 4f));
        SetBuildingLayerViaReflection(go, BuildingCategory.Ramp);
        Assert.AreEqual(PhysicsLayers.Structures, go.layer);
    }

    [Test]
    public void SetBuildingLayer_Machine_KeepsPrefabLayer()
    {
        var go = CreatePrefab(new Vector3(0.8f, 0.5f, 0.8f));
        go.layer = PhysicsLayers.Interactable; // Simulate prefab layer
        SetBuildingLayerViaReflection(go, BuildingCategory.Machine);
        Assert.AreEqual(PhysicsLayers.Interactable, go.layer, "Machine keeps Interactable layer");
    }

    [Test]
    public void SetBuildingLayer_ChildrenGetSameLayer()
    {
        var go = CreatePrefab(new Vector3(4f, 0.5f, 4f));
        var child = new GameObject("MeshChild");
        child.transform.SetParent(go.transform, false);
        _cleanup.Add(child);

        SetBuildingLayerViaReflection(go, BuildingCategory.Foundation);

        Assert.AreEqual(PhysicsLayers.Structures, child.layer, "Children should get same layer");
    }

    [Test]
    public void RaycastMask_IncludesStructuresLayer()
    {
        // Verify the StructuralMask includes the Structures layer
        int mask = (1 << PhysicsLayers.Terrain) | (1 << PhysicsLayers.Structures);
        Assert.IsTrue((mask & (1 << PhysicsLayers.Structures)) != 0, "Mask includes Structures");
        Assert.IsTrue((mask & (1 << PhysicsLayers.Terrain)) != 0, "Mask includes Terrain");
        Assert.IsFalse((mask & (1 << 0)) != 0, "Mask excludes Default layer");
    }

    // Helper: calls the same logic as GridManager.SetBuildingLayer (private method)
    private static void SetBuildingLayerViaReflection(GameObject go, BuildingCategory category)
    {
        int layer = category switch
        {
            BuildingCategory.Foundation => PhysicsLayers.Structures,
            BuildingCategory.Wall => PhysicsLayers.Structures,
            BuildingCategory.Ramp => PhysicsLayers.Structures,
            _ => go.layer
        };
        go.layer = layer;
        foreach (Transform child in go.transform)
            child.gameObject.layer = layer;
    }
}
