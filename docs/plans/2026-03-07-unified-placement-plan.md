# Unified Placement System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace per-building-type placement logic with a unified two-mode system (grid + snap-to-building) where snap points are defined on prefabs.

**Architecture:** Single raycast determines mode (terrain=grid, building=snap). BuildingSnapPoint MonoBehaviour on prefab children defines attachment surfaces. Auto-generated from bounds when no manual snap points exist. All building types use identical position formula in grid mode.

**Tech Stack:** Unity 2022+ / C# / FishNet networking

---

### Task 1: Create BuildingSnapPoint component

**Files:**
- Create: `Assets/_Slopworks/Scripts/Building/BuildingSnapPoint.cs`
- Create: `Assets/_Slopworks/Tests/EditMode/BuildingSnapPointTests.cs`

**Context:** This MonoBehaviour goes on child GameObjects of building prefabs. Each instance represents one attachment surface. Position comes from the child's transform. Normal defaults to `transform.forward`. When a building has no manual snap points, they're auto-generated from renderer bounds.

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class BuildingSnapPointTests
{
    [Test]
    public void GenerateFromBounds_CreatesCardinalAndTopSnapPoints()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // 4x1x4 foundation-like shape
        go.transform.localScale = new Vector3(4f, 1f, 4f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        // 4 cardinal + 1 top = 5
        Assert.AreEqual(5, points.Length);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenerateFromBounds_NormalsPointOutward()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(4f, 1f, 4f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        // All normals should be unit length axis-aligned
        foreach (var p in points)
        {
            float mag = p.Normal.magnitude;
            Assert.AreEqual(1f, mag, 0.01f, $"Snap point normal not unit length: {p.Normal}");
        }

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenerateFromBounds_TopPointNormalIsUp()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(4f, 1f, 4f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        bool hasTop = false;
        foreach (var p in points)
        {
            if (Vector3.Dot(p.Normal, Vector3.up) > 0.9f)
            {
                hasTop = true;
                // Top point should be at the top of the bounds
                Assert.AreEqual(0.5f, p.transform.localPosition.y, 0.01f);
            }
        }
        Assert.IsTrue(hasTop, "No top-facing snap point found");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenerateFromBounds_SkipsIfManualPointsExist()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var child = new GameObject("ManualSnap");
        child.transform.SetParent(go.transform);
        child.AddComponent<BuildingSnapPoint>();

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        // Should still be just the 1 manual point
        Assert.AreEqual(1, points.Length);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenerateFromBounds_SurfaceSizeMatchesFaceDimensions()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // 4 wide, 3 tall, 0.5 deep (wall-like)
        go.transform.localScale = new Vector3(4f, 3f, 0.5f);

        BuildingSnapPoint.GenerateFromBounds(go);

        var points = go.GetComponentsInChildren<BuildingSnapPoint>();
        // Find the top snap point (normal up)
        BuildingSnapPoint topPoint = null;
        foreach (var p in points)
        {
            if (Vector3.Dot(p.Normal, Vector3.up) > 0.9f)
                topPoint = p;
        }

        Assert.IsNotNull(topPoint);
        // Top face of a 4x0.5 shape: SurfaceSize should be (4, 0.5)
        Assert.AreEqual(4f, topPoint.SurfaceSize.x, 0.01f);
        Assert.AreEqual(0.5f, topPoint.SurfaceSize.y, 0.01f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FindNearest_ReturnsClosestToPoint()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(4f, 1f, 4f);
        go.transform.position = Vector3.zero;

        BuildingSnapPoint.GenerateFromBounds(go);

        // Point near the north face (+Z)
        var queryPoint = new Vector3(0f, 0f, 2.5f);
        var nearest = BuildingSnapPoint.FindNearest(go, queryPoint);

        Assert.IsNotNull(nearest);
        // Should be the north face snap point, normal = (0,0,1)
        Assert.AreEqual(1f, Vector3.Dot(nearest.Normal, Vector3.forward), 0.01f);

        Object.DestroyImmediate(go);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `Unity EditMode tests -- BuildingSnapPointTests`
Expected: FAIL -- `BuildingSnapPoint` class doesn't exist yet.

**Step 3: Implement BuildingSnapPoint**

```csharp
using UnityEngine;

/// <summary>
/// Defines an attachment surface on a building prefab.
/// Place on child GameObjects positioned at each snap location.
/// Normal defaults to transform.forward -- orient the child to face outward.
/// </summary>
public class BuildingSnapPoint : MonoBehaviour
{
    [Tooltip("Outward direction of this snap surface. Defaults to transform.forward if zero.")]
    [SerializeField] private Vector3 _normalOverride;

    [Tooltip("Width and height of the attachment area.")]
    public Vector2 SurfaceSize = Vector2.one;

    /// <summary>
    /// The outward-facing normal of this snap point.
    /// Uses _normalOverride if set, otherwise transform.forward.
    /// </summary>
    public Vector3 Normal => _normalOverride.sqrMagnitude > 0.001f
        ? _normalOverride.normalized
        : transform.forward;

    /// <summary>
    /// Auto-generate snap points from renderer bounds if none exist on the object.
    /// Creates 5 points: north, south, east, west, top.
    /// Skips generation if any BuildingSnapPoint already exists on children.
    /// </summary>
    public static void GenerateFromBounds(GameObject go)
    {
        if (go.GetComponentInChildren<BuildingSnapPoint>() != null)
            return;

        var renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        var bounds = renderer.bounds;
        var center = bounds.center;
        var extents = bounds.extents;

        // Local-space center offset (bounds.center is world-space)
        var localCenter = go.transform.InverseTransformPoint(center);

        // Cardinal faces + top
        var faces = new[]
        {
            (dir: Vector3.forward,  offset: new Vector3(0, 0, extents.z),  size: new Vector2(extents.x * 2, extents.y * 2)),
            (dir: Vector3.back,     offset: new Vector3(0, 0, -extents.z), size: new Vector2(extents.x * 2, extents.y * 2)),
            (dir: Vector3.right,    offset: new Vector3(extents.x, 0, 0),  size: new Vector2(extents.z * 2, extents.y * 2)),
            (dir: Vector3.left,     offset: new Vector3(-extents.x, 0, 0), size: new Vector2(extents.z * 2, extents.y * 2)),
            (dir: Vector3.up,       offset: new Vector3(0, extents.y, 0),  size: new Vector2(extents.x * 2, extents.z * 2)),
        };

        foreach (var (dir, offset, size) in faces)
        {
            var child = new GameObject($"SnapPoint_{dir}");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = localCenter + offset;
            var snap = child.AddComponent<BuildingSnapPoint>();
            snap._normalOverride = dir;
            snap.SurfaceSize = size;
        }
    }

    /// <summary>
    /// Find the snap point on the given building closest to a world-space point.
    /// Returns null if no snap points exist.
    /// </summary>
    public static BuildingSnapPoint FindNearest(GameObject building, Vector3 worldPoint)
    {
        var points = building.GetComponentsInChildren<BuildingSnapPoint>();
        if (points.Length == 0) return null;

        BuildingSnapPoint nearest = null;
        float bestDist = float.MaxValue;

        foreach (var p in points)
        {
            float dist = Vector3.Distance(p.transform.position, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = p;
            }
        }

        return nearest;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `Unity EditMode tests -- BuildingSnapPointTests`
Expected: All 6 PASS

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Building/BuildingSnapPoint.cs Assets/_Slopworks/Tests/EditMode/BuildingSnapPointTests.cs
git commit -m "Add BuildingSnapPoint component with auto-generation from bounds"
```

---

### Task 2: Add unified grid snap formula to GridManager

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:104-196`
- Create: `Assets/_Slopworks/Tests/EditMode/GridPlacementTests.cs`

**Context:** Replace `GetPlacementPos`, `GetFoundationPlacementPos`, `GetDirectionalPlacement` with one method: `GetGridPlacementPosition`. It takes a world-space hit point, prefab, and rotation, then returns position + rotation. The position formula: snap center-bottom to the nearest grid-aligned position for the prefab's footprint. All building types use this same formula.

The prefab's footprint is derived from its renderer bounds (X and Z extents). Grid alignment quantizes to the footprint size: a 4m-wide object snaps to 4m intervals, a 1m object snaps to 1m intervals.

For thin objects (depth < 1m), the back face snaps flush to the nearest grid line (not centered).

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class GridPlacementTests
{
    private GameObject CreateTestPrefab(Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = scale;
        return go;
    }

    [Test]
    public void GridPlacement_4x4Foundation_SnapsToFoundationGrid()
    {
        var prefab = CreateTestPrefab(new Vector3(4f, 0.5f, 4f));

        // Hit point at world (5.7, 0, 5.7) -- should snap to nearest 4m boundary
        var result = GridManager.GetGridPlacementPosition(
            new Vector3(5.7f, 0f, 5.7f), prefab, 0);

        // Nearest 4m grid: (4, 0, 4) origin, center at (6, 0.25, 6)
        Assert.AreEqual(6f, result.position.x, 0.01f);
        Assert.AreEqual(0.25f, result.position.y, 0.01f);
        Assert.AreEqual(6f, result.position.z, 0.01f);

        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void GridPlacement_1x1Machine_SnapsToCellCenter()
    {
        var prefab = CreateTestPrefab(new Vector3(1f, 1f, 1f));

        // Hit point at world (3.3, 0, 7.8)
        var result = GridManager.GetGridPlacementPosition(
            new Vector3(3.3f, 0f, 7.8f), prefab, 0);

        // Nearest 1m cell center: (3.5, 0.5, 7.5) -- note 7.8 rounds to 7, center = 7.5
        Assert.AreEqual(3.5f, result.position.x, 0.01f);
        Assert.AreEqual(0.5f, result.position.y, 0.01f);
        Assert.AreEqual(7.5f, result.position.z, 0.01f);

        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void GridPlacement_ThinWall_BackFaceFlushToGridLine()
    {
        // Wall: 4 wide, 3 tall, 0.5 deep
        var prefab = CreateTestPrefab(new Vector3(4f, 3f, 0.5f));

        // Hit point at (6, 0, 5.1) with rotation 0 (facing +Z)
        // Back face (-Z side) should snap flush to Z=5 grid line
        var result = GridManager.GetGridPlacementPosition(
            new Vector3(6f, 0f, 5.1f), prefab, 0);

        // X: 4m grid, nearest to 6 = origin 4, center 6
        // Z: back face at Z=5, center at Z=5.25 (5 + 0.5/2)
        // Y: 0 + 1.5 halfHeight
        Assert.AreEqual(6f, result.position.x, 0.01f);
        Assert.AreEqual(1.5f, result.position.y, 0.01f);
        Assert.AreEqual(5.25f, result.position.z, 0.01f);

        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void GridPlacement_RotatedWall_SwapsAxes()
    {
        // Wall: 4 wide, 3 tall, 0.5 deep -- rotated 90 degrees
        var prefab = CreateTestPrefab(new Vector3(4f, 3f, 0.5f));

        // At 90 degrees, the 4m width goes along Z, 0.5m depth along X
        var result = GridManager.GetGridPlacementPosition(
            new Vector3(5.1f, 0f, 6f), prefab, 90);

        // Z: 4m grid, nearest to 6 = origin 4, center 6
        // X: back face at X=5, center at X=5.25
        // Y: 0 + 1.5
        Assert.AreEqual(5.25f, result.position.x, 0.01f);
        Assert.AreEqual(1.5f, result.position.y, 0.01f);
        Assert.AreEqual(6f, result.position.z, 0.01f);

        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void GridPlacement_WithSurfaceY_AddsToHeight()
    {
        var prefab = CreateTestPrefab(new Vector3(4f, 0.5f, 4f));

        var result = GridManager.GetGridPlacementPosition(
            new Vector3(6f, 3f, 6f), prefab, 0);

        // Y = surfaceY (3) + halfHeight (0.25) = 3.25
        Assert.AreEqual(3.25f, result.position.y, 0.01f);

        Object.DestroyImmediate(prefab);
    }

    [Test]
    public void GridPlacement_ReturnsCorrectRotation()
    {
        var prefab = CreateTestPrefab(new Vector3(4f, 1f, 4f));

        var result = GridManager.GetGridPlacementPosition(
            new Vector3(6f, 0f, 6f), prefab, 90);

        // Rotation should be 90 degrees around Y
        var euler = result.rotation.eulerAngles;
        Assert.AreEqual(90f, euler.y, 0.1f);

        Object.DestroyImmediate(prefab);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `Unity EditMode tests -- GridPlacementTests`
Expected: FAIL -- `GridManager.GetGridPlacementPosition` doesn't exist.

**Step 3: Implement GetGridPlacementPosition**

Add this static method to `GridManager.cs`, replacing the old placement methods. Keep the old methods temporarily (they're still called by CmdPlace RPCs) -- we'll update the RPCs in Task 5.

```csharp
/// <summary>
/// Unified grid placement: snaps center-bottom of prefab to nearest grid-aligned
/// position based on its footprint. Thin objects snap back face flush to grid line.
/// </summary>
public static (Vector3 position, Quaternion rotation) GetGridPlacementPosition(
    Vector3 hitPoint, GameObject prefab, int rotationDeg)
{
    var renderer = prefab.GetComponentInChildren<Renderer>();
    var extents = renderer != null ? renderer.bounds.extents : new Vector3(0.5f, 0.5f, 0.5f);

    float halfHeight = extents.y;

    // Get footprint in world axes after rotation
    bool rotated = rotationDeg == 90 || rotationDeg == 270;
    float footprintX = rotated ? extents.z * 2f : extents.x * 2f;
    float footprintZ = rotated ? extents.x * 2f : extents.z * 2f;
    float depthX = rotated ? extents.x * 2f : extents.z * 2f; // depth along the thin axis
    float depthZ = rotated ? extents.z * 2f : extents.x * 2f;

    // Determine which axis is "thin" (< 1m) for back-face alignment
    float rawX = rotated ? extents.z * 2f : extents.x * 2f;
    float rawZ = rotated ? extents.x * 2f : extents.z * 2f;

    float snapX, snapZ;

    // X axis: if thin, back face flush to grid line; if thick, center on grid
    if (rawX < 1f)
    {
        float gridLine = Mathf.Round(hitPoint.x);
        snapX = gridLine + rawX * 0.5f;
    }
    else
    {
        float gridStep = rawX;
        float origin = Mathf.Round(hitPoint.x / gridStep) * gridStep - gridStep * 0.5f;
        snapX = origin + gridStep * 0.5f;
    }

    // Z axis: same logic
    if (rawZ < 1f)
    {
        float gridLine = Mathf.Round(hitPoint.z);
        snapZ = gridLine + rawZ * 0.5f;
    }
    else
    {
        float gridStep = rawZ;
        float origin = Mathf.Round(hitPoint.z / gridStep) * gridStep - gridStep * 0.5f;
        snapZ = origin + gridStep * 0.5f;
    }

    var position = new Vector3(snapX, hitPoint.y + halfHeight, snapZ);
    var rotation = Quaternion.Euler(0f, rotationDeg, 0f);

    return (position, rotation);
}
```

**Step 4: Run tests to verify they pass**

Run: `Unity EditMode tests -- GridPlacementTests`
Expected: All 6 PASS

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs Assets/_Slopworks/Tests/EditMode/GridPlacementTests.cs
git commit -m "Add unified grid placement formula for all building types"
```

---

### Task 3: Add snap-to-building position calculation

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`
- Create: `Assets/_Slopworks/Tests/EditMode/SnapPlacementTests.cs`

**Context:** When the build tool's raycast hits an existing building, find the nearest `BuildingSnapPoint` and calculate where the new building should go: flush against that snap surface. Formula: `snapPoint.position + snapPoint.normal * incomingHalfDepthAlongNormal`.

**Step 1: Write failing tests**

```csharp
using NUnit.Framework;
using UnityEngine;

public class SnapPlacementTests
{
    private GameObject CreateBuildingWithSnapPoints(Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = scale;
        go.transform.position = Vector3.zero;
        BuildingSnapPoint.GenerateFromBounds(go);
        return go;
    }

    [Test]
    public void SnapPlacement_FoundationToFoundationNorth_AdjacentFlush()
    {
        // Existing 4x0.5x4 foundation at origin
        var existing = CreateBuildingWithSnapPoints(new Vector3(4f, 0.5f, 4f));
        // New 4x0.5x4 foundation
        var newPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newPrefab.transform.localScale = new Vector3(4f, 0.5f, 4f);

        // Hit point on north face of existing
        var hitPoint = new Vector3(0f, 0f, 2.1f);
        var nearestSnap = BuildingSnapPoint.FindNearest(existing, hitPoint);

        var result = GridManager.GetSnapPlacementPosition(nearestSnap, newPrefab, 0);

        // New foundation should be at Z = 2 (snap point) + 2 (half depth of new) = 4
        Assert.AreEqual(0f, result.position.x, 0.01f);
        Assert.AreEqual(0.25f, result.position.y, 0.01f); // same height
        Assert.AreEqual(4f, result.position.z, 0.01f);

        Object.DestroyImmediate(existing);
        Object.DestroyImmediate(newPrefab);
    }

    [Test]
    public void SnapPlacement_WallOnFoundationEdge_BackFaceFlush()
    {
        // 4x0.5x4 foundation
        var existing = CreateBuildingWithSnapPoints(new Vector3(4f, 0.5f, 4f));
        // 4x3x0.5 wall
        var wallPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallPrefab.transform.localScale = new Vector3(4f, 3f, 0.5f);

        var hitPoint = new Vector3(0f, 0f, 2.1f);
        var nearestSnap = BuildingSnapPoint.FindNearest(existing, hitPoint);

        var result = GridManager.GetSnapPlacementPosition(nearestSnap, wallPrefab, 0);

        // Wall center Z = snap Z (2) + half wall depth (0.25) = 2.25
        // Wall center Y = snap Y (0.25) + half wall height (1.5) = 1.75
        Assert.AreEqual(2.25f, result.position.z, 0.01f);
        Assert.AreEqual(1.75f, result.position.y, 0.01f);

        Object.DestroyImmediate(existing);
        Object.DestroyImmediate(wallPrefab);
    }

    [Test]
    public void SnapPlacement_FoundationOnTop_StacksVertically()
    {
        var existing = CreateBuildingWithSnapPoints(new Vector3(4f, 0.5f, 4f));
        var newPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newPrefab.transform.localScale = new Vector3(4f, 0.5f, 4f);

        // Hit point on top face
        var hitPoint = new Vector3(0f, 0.6f, 0f);
        var nearestSnap = BuildingSnapPoint.FindNearest(existing, hitPoint);

        var result = GridManager.GetSnapPlacementPosition(nearestSnap, newPrefab, 0);

        // Snap point at Y=0.25 (top of foundation), normal up
        // New foundation Y = snap Y (0.25) + half height (0.25) = 0.5
        Assert.AreEqual(0.5f, result.position.y, 0.01f);
        // XZ should match the snap point (centered on existing)
        Assert.AreEqual(0f, result.position.x, 0.01f);
        Assert.AreEqual(0f, result.position.z, 0.01f);

        Object.DestroyImmediate(existing);
        Object.DestroyImmediate(newPrefab);
    }

    [Test]
    public void SnapPlacement_RotationAligns ToNormal()
    {
        var existing = CreateBuildingWithSnapPoints(new Vector3(4f, 0.5f, 4f));
        var wallPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallPrefab.transform.localScale = new Vector3(4f, 3f, 0.5f);

        // Hit east face
        var hitPoint = new Vector3(2.1f, 0f, 0f);
        var nearestSnap = BuildingSnapPoint.FindNearest(existing, hitPoint);

        var result = GridManager.GetSnapPlacementPosition(nearestSnap, wallPrefab, 0);

        // East face normal = right, wall should rotate 90 to face east
        var euler = result.rotation.eulerAngles;
        Assert.AreEqual(90f, euler.y, 0.1f);

        Object.DestroyImmediate(existing);
        Object.DestroyImmediate(wallPrefab);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `Unity EditMode tests -- SnapPlacementTests`
Expected: FAIL -- `GridManager.GetSnapPlacementPosition` doesn't exist.

**Step 3: Implement GetSnapPlacementPosition**

Add to `GridManager.cs`:

```csharp
/// <summary>
/// Snap placement: position a new building flush against an existing building's snap point.
/// The new building's opposing face aligns to the snap surface.
/// For horizontal normals: offset along normal by half the incoming depth.
/// For vertical normals (top): place center-bottom on snap point.
/// </summary>
public static (Vector3 position, Quaternion rotation) GetSnapPlacementPosition(
    BuildingSnapPoint snapPoint, GameObject prefab, int rotationDeg)
{
    var renderer = prefab.GetComponentInChildren<Renderer>();
    var extents = renderer != null ? renderer.bounds.extents : new Vector3(0.5f, 0.5f, 0.5f);

    var normal = snapPoint.Normal.normalized;
    var snapPos = snapPoint.transform.position;

    // Determine rotation from snap normal for horizontal snaps
    float autoYaw = 0f;
    bool isVertical = Mathf.Abs(normal.y) > 0.9f;

    if (!isVertical)
    {
        // Auto-rotate to face along the snap normal
        autoYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
    }

    float effectiveYaw = isVertical ? rotationDeg : autoYaw;
    bool rotated = Mathf.Abs(effectiveYaw % 180f - 90f) < 1f;

    if (isVertical)
    {
        // Top snap: place center-bottom directly on snap point
        float halfHeight = extents.y;
        var position = snapPos + new Vector3(0f, halfHeight, 0f);
        return (position, Quaternion.Euler(0f, effectiveYaw, 0f));
    }
    else
    {
        // Horizontal snap: offset along normal by half-depth of incoming object
        // Depth is the extent along the normal direction
        float halfDepth = rotated ? extents.x : extents.z;
        float halfHeight = extents.y;

        // Position: snap point + normal * halfDepth, Y adjusted to center
        var offset = normal * halfDepth;
        // Y: snap point Y + half height (center-bottom placement)
        var position = new Vector3(
            snapPos.x + offset.x,
            snapPos.y + halfHeight,
            snapPos.z + offset.z);

        return (position, Quaternion.Euler(0f, effectiveYaw, 0f));
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `Unity EditMode tests -- SnapPlacementTests`
Expected: All 4 PASS

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs Assets/_Slopworks/Tests/EditMode/SnapPlacementTests.cs
git commit -m "Add snap-to-building placement calculation"
```

---

### Task 4: Unified raycast in NetworkBuildController

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Context:** Replace `RaycastGrid()`, `RaycastWallPlacement()`, and `GetFacingEdgeDirection()` with one method: `RaycastPlacement()`. Returns hit info + whether it hit terrain (grid mode) or a building (snap mode). If snap mode, also returns the nearest `BuildingSnapPoint`.

**Step 1: Add the placement mode enum and unified raycast**

At the top of `NetworkBuildController`, add:

```csharp
private enum PlacementMode { None, Grid, Snap }
```

Replace `RaycastGrid`, `RaycastWallPlacement`, and `GetFacingEdgeDirection` with:

```csharp
private PlacementMode _placementMode;
private BuildingSnapPoint _activeSnapPoint;

private bool RaycastPlacement(out RaycastHit hit)
{
    var ray = new Ray(_camera.transform.position, _camera.transform.forward);
    if (!Physics.Raycast(ray, out hit, _placementRange, StructuralMask))
    {
        _placementMode = PlacementMode.None;
        _activeSnapPoint = null;
        return false;
    }

    // Check if we hit an existing building
    var placement = hit.collider.GetComponentInParent<PlacementInfo>();
    if (placement != null)
    {
        var nearest = BuildingSnapPoint.FindNearest(placement.gameObject, hit.point);
        if (nearest != null)
        {
            _placementMode = PlacementMode.Snap;
            _activeSnapPoint = nearest;
            _surfaceY = placement.SurfaceY + placement.ObjectHeight;
            return true;
        }
    }

    // Terrain hit -- grid mode
    _placementMode = PlacementMode.Grid;
    _activeSnapPoint = null;
    _surfaceY = hit.point.y;
    return true;
}
```

**Step 2: Delete old raycast methods**

Remove these methods entirely:
- `RaycastGrid` (lines 437-450)
- `RaycastWallPlacement` (lines 456-481)
- `GetFacingEdgeDirection` (lines 485-493)

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Replace three raycast methods with unified RaycastPlacement"
```

---

### Task 5: Unified HandleBuildInput replacing per-tool handlers

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Context:** Replace `HandleFoundationInput`, `HandleWallInput`, `HandleRampInput`, `HandleMachineInput`, `HandleStorageInput` with one `HandleBuildInput`. Belt keeps its own handler (out of scope per design). The ghost position comes from either `GetGridPlacementPosition` or `GetSnapPlacementPosition` depending on `_placementMode`.

This is the largest single task. The new handler:
1. Calls `RaycastPlacement`
2. Gets position from grid or snap formula
3. Updates ghost
4. On click, sends the appropriate CmdPlace RPC

**Step 1: Replace the tool switch in Update**

Change the tool switch (lines 176-196) to:

```csharp
switch (_currentTool)
{
    case BuildTool.Belt:
        HandleBeltInput(mouse);
        break;
    default:
        HandleBuildInput(mouse);
        break;
}
```

**Step 2: Implement HandleBuildInput**

```csharp
private void HandleBuildInput(Mouse mouse)
{
    if (!RaycastPlacement(out var hit))
    {
        HideGhost();
        return;
    }

    var prefab = GetSelectedPrefab();
    if (prefab == null) return;

    Vector3 ghostPos;
    Quaternion ghostRot;

    if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
    {
        var result = GridManager.GetSnapPlacementPosition(
            _activeSnapPoint, prefab, _placeRotation);
        ghostPos = result.position + new Vector3(0f, _nudgeOffset, 0f);
        ghostRot = result.rotation;
    }
    else
    {
        var effectiveHit = new Vector3(hit.point.x, EffectiveY, hit.point.z);
        var result = GridManager.GetGridPlacementPosition(
            effectiveHit, prefab, _placeRotation);
        ghostPos = result.position;
        ghostRot = result.rotation;
    }

    // Update ghost
    EnsurePrefabGhost(prefab);
    _ghost.transform.position = ghostPos;
    _ghost.transform.rotation = ghostRot;
    _ghost.SetActive(true);

    // Determine validity (check grid occupancy)
    var cell = GridManager.Instance.Grid.WorldToCell(ghostPos);
    bool valid = !GridManager.Instance.HasBuildingAt(cell, _surfaceY);
    SetGhostColor(valid ? ValidColor : InvalidColor);

    // Place on click
    if (mouse.leftButton.wasPressedThisFrame && valid)
    {
        var category = ToolToCategory(_currentTool);
        int rotDeg = Mathf.RoundToInt(ghostRot.eulerAngles.y);
        GridManager.Instance.CmdPlace(cell, _surfaceY, rotDeg, CurrentVariant, category);
        Debug.Log($"build: placed {category} at ({cell.x},{cell.y}) y={_surfaceY:F1}");
    }

    // Remove on right click
    if (mouse.rightButton.wasPressedThisFrame)
    {
        GridManager.Instance.CmdDelete(
            GridManager.Instance.Grid.WorldToCell(hit.point), _surfaceY);
    }
}

private void HideGhost()
{
    if (_ghost != null) _ghost.SetActive(false);
}
```

**Step 3: Delete old per-tool handlers**

Remove these methods:
- `HandleFoundationInput` and all foundation ghost helpers (`ShowFoundationGhostSingle`, `UpdateFoundationZoopPreview`, `PlaceFoundationRect`, `HideFoundationGhosts`, `CreateFoundationGhost`)
- `HandleWallInput` and wall ghost helpers (`UpdateWallGhosts`, `UpdateWallZoopPreview`)
- `HandleRampInput` and ramp ghost helpers (`UpdateRampGhost`)
- `HandleMachineInput` and `HandleStorageInput`

Keep: `HandleBeltInput`, `HandleDeleteMode`, all zoop infrastructure (we'll reconnect zoop in Task 7).

**Step 4: Update CmdPlace in GridManager to use unified position**

In `GridManager.CmdPlace`, replace the foundation vs else branch with:

```csharp
// All categories use the same position formula
var (worldPos, rot) = GetGridPlacementPosition(
    Grid.CellToWorld(cell, surfaceY), prefab, rotation);

if (category == BuildingCategory.Foundation)
{
    int fs = FactoryGrid.FoundationSize;
    var size = new Vector2Int(fs, fs);
    if (!_grid.CanPlace(cell, size, surfaceY)) return;
    _grid.Place(cell, size, surfaceY, new BuildingData("foundation", cell, size, 0, 0));
}

var go = Instantiate(prefab, worldPos, rot);
```

**Step 5: Remove old placement methods from GridManager**

Delete:
- `GetPlacementPos`
- `GetFoundationPlacementPos`
- `GetDirectionalPlacement`

Update `CmdPlaceDirectional` and `CmdPlaceBelt` to use `GetGridPlacementPosition` or `GetSnapPlacementPosition` as appropriate.

**Step 6: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs Assets/_Slopworks/Scripts/Network/GridManager.cs
git commit -m "Unify build input handlers into single HandleBuildInput"
```

---

### Task 6: Auto-generate snap points on building spawn

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`

**Context:** After `CmdPlace` and `CmdPlaceDirectional` instantiate and spawn a building, call `BuildingSnapPoint.GenerateFromBounds(go)` so the placed building immediately has snap points for subsequent placements.

**Step 1: Add GenerateFromBounds call after spawn**

In `CmdPlace`, after `ServerManager.Spawn(go)`:

```csharp
BuildingSnapPoint.GenerateFromBounds(go);
```

Same in `CmdPlaceDirectional`, after `ServerManager.Spawn(go)`:

```csharp
BuildingSnapPoint.GenerateFromBounds(go);
```

This is safe because `GenerateFromBounds` checks for existing snap points first -- if a prefab already has manual `BuildingSnapPoint` children, it skips auto-generation.

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs
git commit -m "Auto-generate snap points on spawned buildings"
```

---

### Task 7: Reconnect zoop to unified placement

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Context:** Zoop (batch placement with Z key) was disconnected when the per-tool handlers were removed. Reconnect it to use `HandleBuildInput`'s position formula. Zoop only works in grid mode (not snap mode). Two-click: first click sets start, ghost shows preview of the line, second click places the batch.

**Step 1: Add zoop support to HandleBuildInput**

Inside `HandleBuildInput`, after calculating `ghostPos` and before the place-on-click block, add zoop logic:

```csharp
if (_zoopMode)
{
    // Only zoop in grid mode
    if (_placementMode != PlacementMode.Grid)
    {
        HideGhost();
        return;
    }

    var cell = GridManager.Instance.Grid.WorldToCell(ghostPos);

    if (!_zoopStartSet)
    {
        // Show single ghost at current position
        _ghost.transform.position = ghostPos;
        _ghost.transform.rotation = ghostRot;
        _ghost.SetActive(true);
        SetGhostColor(ValidColor);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _zoopStartSet = true;
            _zoopStartCell = cell;
            _zoopStartSurfaceY = EffectiveY;
        }
    }
    else
    {
        // Show preview line from start to current
        UpdateZoopPreview(cell, ghostRot);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlaceZoopLine(_zoopStartCell, cell, ghostRot);
            CancelZoop();
        }
        if (mouse.rightButton.wasPressedThisFrame)
            CancelZoop();
    }
    return;
}
```

**Step 2: Implement UpdateZoopPreview and PlaceZoopLine**

```csharp
private void UpdateZoopPreview(Vector2Int endCell, Quaternion rot)
{
    var category = ToolToCategory(_currentTool);
    var prefab = GetSelectedPrefab();
    // Walk from start to end along the dominant axis
    // ... (ghost pool management similar to existing foundation zoop)
}

private void PlaceZoopLine(Vector2Int startCell, Vector2Int endCell, Quaternion rot)
{
    var category = ToolToCategory(_currentTool);
    // Place buildings along the line from start to end
    // Each uses CmdPlace with the same surfaceY and rotation
}
```

The exact zoop cell-walking logic can be adapted from the existing `PlaceFoundationRect` -- just generalized to work with any building category using the unified position formula.

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Reconnect zoop to unified placement system"
```

---

### Task 8: Remove old snap point system

**Files:**
- Delete: `Assets/_Slopworks/Scripts/Building/SnapPoint.cs`
- Delete: `Assets/_Slopworks/Scripts/Building/SnapPointType.cs`
- Delete: `Assets/_Slopworks/Scripts/Building/SnapPointRegistry.cs`
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs` (remove `_snapRegistry` field and `SnapRegistry` property)
- Modify: `Assets/_Slopworks/Scripts/Building/StructuralPlacementService.cs` (remove SnapPointRegistry dependency if present)

**Context:** The old `SnapPoint`/`SnapPointRegistry`/`SnapPointType` system is fully replaced by `BuildingSnapPoint`. Clean up all references.

**Step 1: Remove references from GridManager**

In `GridManager.cs`:
- Remove `private SnapPointRegistry _snapRegistry;` field
- Remove `public SnapPointRegistry SnapRegistry => _snapRegistry;` property
- Remove `_snapRegistry = new SnapPointRegistry();` from Awake
- Remove the SnapPointRegistry parameter from `StructuralPlacementService` constructor call if present

**Step 2: Update or remove StructuralPlacementService**

Check if `StructuralPlacementService` still has value after the snap point removal. If it only existed to manage snap point registration during placement, it can be removed. If it has other responsibilities, just remove the SnapPointRegistry dependency.

**Step 3: Delete old files**

```bash
git rm Assets/_Slopworks/Scripts/Building/SnapPoint.cs
git rm Assets/_Slopworks/Scripts/Building/SnapPoint.cs.meta
git rm Assets/_Slopworks/Scripts/Building/SnapPointType.cs
git rm Assets/_Slopworks/Scripts/Building/SnapPointType.cs.meta
git rm Assets/_Slopworks/Scripts/Building/SnapPointRegistry.cs
git rm Assets/_Slopworks/Scripts/Building/SnapPointRegistry.cs.meta
```

**Step 4: Fix any remaining compile errors**

Search for all references to `SnapPoint`, `SnapPointType`, `SnapPointRegistry` across the codebase and update or remove them:

```bash
grep -rn "SnapPoint\|SnapPointType\|SnapPointRegistry" Assets/_Slopworks/Scripts/ --include="*.cs"
```

**Step 5: Run all EditMode tests**

Expected: All pass with no references to removed types.

**Step 6: Commit**

```bash
git add -A
git commit -m "Remove old SnapPoint/SnapPointRegistry system, replaced by BuildingSnapPoint"
```

---

### Task 9: Update OnGUI and delete mode for unified system

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Context:** Update the OnGUI debug display to show placement mode (Grid/Snap) and snap point info. Delete mode already works via PlacementInfo raycast and doesn't need changes, but verify it still works.

**Step 1: Update OnGUI**

In the `OnGUI` method, update the display to show:

```csharp
// In OnGUI, add after existing Surface display:
if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
    GUI.Label(NextLine(), $"Snap: {_activeSnapPoint.Normal} on {_activeSnapPoint.transform.parent?.name}");
else
    GUI.Label(NextLine(), $"Mode: Grid");
```

**Step 2: Verify delete mode compiles and works**

Delete mode uses `PlacementInfo` which is unchanged. No code changes needed -- just verify it compiles.

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Update OnGUI to show placement mode and snap info"
```

---

### Task 10: Manual playtest verification

**Files:** None (testing only)

**Context:** Playtest in the HomeBase scene to verify all placement modes work correctly.

**Verification checklist:**

1. **Grid mode -- all tools place at the same position:**
   - Select Foundation tool, aim at terrain, note ghost position
   - Switch to Wall tool, aim at same spot -- ghost should be at same XZ, appropriate height
   - Switch to Ramp tool -- same XZ, appropriate height
   - Switch to Machine, Storage -- same grid snap behavior

2. **Grid mode -- thin objects:**
   - Select Wall tool, aim at terrain
   - Wall back face should be flush with the nearest grid line
   - Rotate with R -- wall snaps to grid correctly at 90-degree increments

3. **Snap mode -- foundation to foundation:**
   - Place a foundation
   - Aim at its north face -- new foundation ghost snaps adjacent
   - Aim at its east face -- snaps to east
   - Aim at top -- stacks on top

4. **Snap mode -- wall on foundation:**
   - Place a foundation
   - Switch to Wall, aim at foundation edge
   - Wall ghost should appear flush against the edge

5. **Snap mode -- ramp on foundation:**
   - Switch to Ramp, aim at foundation edge
   - Ramp ghost should appear at the edge

6. **Nudge in both modes:**
   - PgUp/PgDn adjusts height in grid mode
   - PgUp/PgDn adjusts height in snap mode

7. **Delete mode:**
   - Place several buildings
   - X to enter delete mode
   - Click to remove -- works as before

8. **Zoop:**
   - Z to enable zoop
   - Click start, click end -- batch places along line

9. **Belt placement:**
   - Belt tool works independently (unchanged)
