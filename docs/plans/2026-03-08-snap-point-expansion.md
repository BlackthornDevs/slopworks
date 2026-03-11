# Snap Point Expansion Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Expand snap points from 5 per prefab to 14-16, adding multi-height cardinal snaps, bottom face snaps, and ramp slope-aware snaps. Add R-key rotation override in snap mode.

**Architecture:** The editor tool `SnapPointPrefabSetup.cs` generates snap points per prefab category (foundation/wall/ramp). `GetSnapPlacementPosition` in GridManager handles vertical vs horizontal normals. `RaycastPlacement` in NetworkBuildController sets `_surfaceY` based on snap direction. Tests use Unity primitives with `BuildingSnapPoint.GenerateFromBounds` or manually placed snap points.

**Tech Stack:** Unity 2022 LTS, FishNet, C# EditMode tests (NUnit), MCP Unity for recompile/test

**Design doc:** `docs/reference/snap-point-system.md`

---

### Task 1: Rewrite SnapPointPrefabSetup for multi-height cardinal snaps

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/SnapPointPrefabSetup.cs`

**Step 1: Rewrite AddSnapPointsToPrefabs to generate per-category snap layouts**

Replace the single 5-snap loop with category-aware generation. Detect category from path substring.

Foundation/Ramp layout (14 base points):
- 4 cardinal directions x 3 heights (top edge, center, bottom edge) = 12
- Top_Center + Bot_Center = 2

Wall layout (14 points):
- Front/Back (thin axis) x 3 heights = 6
- Left/Right (wide axis) x 3 heights = 6
- Top_Center + Bot_Center = 2

For each cardinal face, compute three positions:
```csharp
// For north face (+Z):
var topY = center.y + ext.y;      // top edge of face
var midY = center.y;               // center of face
var botY = center.y - ext.y;       // bottom edge of face
AddSnapPoint(root, "North_Top", new Vector3(center.x, topY, center.z + ext.z), Vector3.forward, surfaceSize);
AddSnapPoint(root, "North_Mid", new Vector3(center.x, midY, center.z + ext.z), Vector3.forward, surfaceSize);
AddSnapPoint(root, "North_Bot", new Vector3(center.x, botY, center.z + ext.z), Vector3.forward, surfaceSize);
```

Repeat for South (-Z), East (+X), West (-X). Top_Center and Bot_Center use `Vector3.up` and `Vector3.down` normals.

The `AddSnapPoint` helper already handles world-to-local conversion and rounding. No changes needed there.

**Step 2: Run the tool via `Tools > Slopworks > Add Snap Points to Prefabs`**

Recompile, then execute menu item. Verify console logs show 14 snap points per foundation/wall, 14 per ramp (slope snaps added in Task 4).

**Step 3: Commit**

```
feat: expand snap points to 14 per prefab with multi-height cardinal layout
```

---

### Task 2: Update GenerateFromBounds fallback to match new layout

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/BuildingSnapPoint.cs:30-72`

**Step 1: Update GenerateFromBounds to create 14-point layout**

This is the runtime fallback for objects without prefab snap points (e.g., test cubes). Update the `faces` array to generate the same multi-height pattern:

```csharp
public static void GenerateFromBounds(GameObject go)
{
    if (go.GetComponentInChildren<BuildingSnapPoint>() != null)
        return;

    var renderer = go.GetComponentInChildren<Renderer>();
    if (renderer == null) return;

    var lb = renderer.localBounds;
    var s = renderer.transform.lossyScale;
    var worldExtents = new Vector3(
        lb.extents.x * Mathf.Abs(s.x),
        lb.extents.y * Mathf.Abs(s.y),
        lb.extents.z * Mathf.Abs(s.z));
    var center = renderer.transform.TransformPoint(lb.center);
    var localCenter = go.transform.InverseTransformPoint(center);
    var ext = lb.extents;

    float topY = ext.y;
    float botY = -ext.y;

    // Cardinal directions with 3 heights each
    var cardinals = new[]
    {
        (dir: Vector3.forward, offset: new Vector3(0, 0, ext.z)),
        (dir: Vector3.back,    offset: new Vector3(0, 0, -ext.z)),
        (dir: Vector3.right,   offset: new Vector3(ext.x, 0, 0)),
        (dir: Vector3.left,    offset: new Vector3(-ext.x, 0, 0)),
    };

    foreach (var (dir, offset) in cardinals)
    {
        bool isXFace = Mathf.Abs(dir.x) > 0.5f;
        float faceW = isXFace ? worldExtents.z : worldExtents.x;
        float faceH = worldExtents.y;
        var size = new Vector2(faceW * 2, faceH * 2);

        string name = dir == Vector3.forward ? "North"
                    : dir == Vector3.back    ? "South"
                    : dir == Vector3.right   ? "East" : "West";

        AddPoint(go, $"{name}_Top", localCenter + offset + new Vector3(0, topY, 0), dir, size);
        AddPoint(go, $"{name}_Mid", localCenter + offset, dir, size);
        AddPoint(go, $"{name}_Bot", localCenter + offset + new Vector3(0, botY, 0), dir, size);
    }

    // Top and bottom face centers
    var topSize = new Vector2(worldExtents.x * 2, worldExtents.z * 2);
    AddPoint(go, "Top_Center", localCenter + new Vector3(0, topY, 0), Vector3.up, topSize);
    AddPoint(go, "Bot_Center", localCenter + new Vector3(0, botY, 0), Vector3.down, topSize);
}

private static void AddPoint(GameObject parent, string name, Vector3 localPos, Vector3 normal, Vector2 size)
{
    var child = new GameObject($"SnapPoint_{name}");
    child.transform.SetParent(parent.transform, false);
    child.transform.localPosition = localPos;
    var snap = child.AddComponent<BuildingSnapPoint>();
    snap._normalOverride = normal;
    snap.SurfaceSize = size;
}
```

**Step 2: Commit**

```
feat: update GenerateFromBounds to produce 14-point multi-height layout
```

---

### Task 3: Update RaycastPlacement for bottom snap surfaceY

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:685-692`

**Step 1: Add bottom-snap surfaceY case**

Current code at line 689-692:
```csharp
bool isTop = Mathf.Abs(nearest.Normal.y) > 0.9f;
_surfaceY = isTop
    ? placement.SurfaceY + placement.ObjectHeight
    : placement.SurfaceY;
```

Replace with:
```csharp
float normalY = nearest.Normal.y;
if (normalY > 0.9f)
    _surfaceY = placement.SurfaceY + placement.ObjectHeight;  // top: above existing
else if (normalY < -0.9f)
    _surfaceY = placement.SurfaceY - placement.ObjectHeight;  // bottom: below existing
else
    _surfaceY = placement.SurfaceY;                            // side: same level
```

This gives buildings attached to the underside the correct reference Y. `GetSnapPlacementPosition` already handles `normal.y < 0` with `-baseOffset`, so no changes needed there.

**Step 2: Commit**

```
feat: handle bottom snap surfaceY in RaycastPlacement
```

---

### Task 4: Add R-key rotation override in snap mode

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:175-204`
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:202-207`

**Step 1: Add rotationDeg parameter to snap placement**

In `GetSnapPlacementPosition` at line 194, the autoYaw is always used for horizontal snaps. Change to use `rotationDeg` when non-zero:

```csharp
// Horizontal normal: auto-align yaw from normal direction
float autoYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
float finalYaw = rotationDeg != 0 ? rotationDeg : autoYaw;
float halfDepth = extents.z;
```

And use `finalYaw` in the return:
```csharp
return (pos, Quaternion.Euler(0f, finalYaw, 0f));
```

Wait -- this is wrong. `halfDepth = extents.z` assumes the prefab faces along autoYaw. If the player overrides rotation, the depth axis changes. The offset direction (along the snap normal) stays the same, but the depth extent to use depends on the final rotation relative to the normal.

Better approach: always offset along the snap normal, but compute `halfDepth` from the rotated extents. When autoYaw is used, the prefab's Z axis aligns with the normal, so `extents.z` is the depth. When the player rotates 90 degrees off the autoYaw, the prefab's X axis aligns with the normal, so `extents.x` is the depth.

```csharp
float autoYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
float finalYaw = rotationDeg != 0 ? (float)rotationDeg : autoYaw;

// Compute which extent faces the normal after rotation
float yawDiff = (finalYaw - autoYaw) * Mathf.Deg2Rad;
float halfDepth = Mathf.Abs(extents.z * Mathf.Cos(yawDiff)) + Mathf.Abs(extents.x * Mathf.Sin(yawDiff));
```

This rotates the bounding box extent projection onto the normal direction. For 0-degree diff (auto), it's `extents.z`. For 90-degree diff, it's `extents.x`.

**Step 2: Commit**

```
feat: R-key rotation override in snap mode with correct depth projection
```

---

### Task 5: Update tests for new snap point layout

**Files:**
- Modify: `Assets/_Slopworks/Tests/Editor/EditMode/SnapPlacementTests.cs`
- Modify: `Assets/_Slopworks/Tests/Editor/EditMode/UnifiedPlacementTests.cs`

**Step 1: Update FindSnapByNormal helper in both test files**

`FindSnapByNormal` finds the snap point whose normal most closely matches a direction. With 3 snaps per face (all sharing the same normal), it now returns whichever is closest by dot product -- they're all equal. Add position filtering to pick the mid-height snap by default:

```csharp
private BuildingSnapPoint FindSnapByNormal(GameObject go, Vector3 normalDir)
{
    var points = go.GetComponentsInChildren<BuildingSnapPoint>();
    BuildingSnapPoint best = null;
    float bestDot = float.MinValue;
    float bestDistFromCenter = float.MaxValue;

    // Get bounds center for tie-breaking
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
            // Same normal -- pick the one closest to bounds center (mid snap)
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
```

This ensures tests that use `FindSnapByNormal(go, Vector3.forward)` get the `North_Mid` snap (closest to center), maintaining existing test expectations.

**Step 2: Run all placement tests**

```
SnapPlacementTests: 4 tests
UnifiedPlacementTests: 43 tests
GridPlacementTests: 7 tests
```

All should pass without changing test assertions since the mid snap has the same position as the old single snap.

**Step 3: Add new tests for bottom snap and multi-height selection**

Add to `UnifiedPlacementTests.cs`:

```csharp
[Test]
public void Snap_BottomNormal_PlacesBelowExisting()
{
    var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), new Vector3(0f, 2f, 0f));
    var prefab = CreatePrefab(new Vector3(4f, 0.5f, 4f));

    var bottomSnap = FindSnapByNormal(existing, Vector3.down);
    Assert.IsNotNull(bottomSnap, "Should have a bottom snap point");

    var (pos, _) = GridManager.GetSnapPlacementPosition(bottomSnap, prefab, 0, 1.5f);

    // Bottom snap Y = existing bottom. surfaceY = 2 - 0.5 = 1.5.
    // New foundation: posY = snapPos.y - baseOffset.
    // For center-origin cube: baseOffset = 0.25. snapPos.y = bottom of existing = 1.75.
    // posY = 1.75 - 0.25 = 1.5. Center of new foundation hangs below.
    Assert.AreEqual(1.5f, pos.y, 0.01f, "Y places new foundation below existing");
}

[Test]
public void Snap_FindNearest_ReturnsTopEdgeWhenHitNearTop()
{
    var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
    // Hit near the top edge of the north face
    var hitPoint = new Vector3(0f, 0.24f, 2f);
    var nearest = BuildingSnapPoint.FindNearest(existing, hitPoint);
    Assert.IsNotNull(nearest);
    // Should be the North_Top snap (closest to hit point near top)
    Assert.IsTrue(nearest.transform.position.y > 0.1f, "Should select top-edge snap");
}
```

**Step 4: Run tests, verify pass**

**Step 5: Commit**

```
test: add bottom snap and multi-height selection tests
```

---

### Task 6: Add ramp slope-aware snap points (HighEdge / LowEdge)

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Editor/SnapPointPrefabSetup.cs`

**Step 1: Add ramp-specific slope snaps**

After generating the standard 14 cardinal + face center snaps for ramps, add two slope-aware snaps. The Y positions depend on the actual ramp mesh geometry. We need to inspect the ramp meshes to determine the high and low edge heights.

This requires loading each ramp prefab and sampling the mesh vertices at the +Z and -Z edges to find the actual surface height. Add a helper:

```csharp
private static (float highY, float lowY) GetRampEdgeHeights(Renderer renderer)
{
    var mf = renderer.GetComponent<MeshFilter>();
    if (mf == null || mf.sharedMesh == null)
        return (renderer.bounds.max.y, renderer.bounds.min.y);

    var mesh = mf.sharedMesh;
    var verts = mesh.vertices;
    float maxZ = float.MinValue, minZ = float.MaxValue;
    float highY = 0f, lowY = 0f;

    // Find the max and min Z vertices, get their Y
    foreach (var v in verts)
    {
        if (v.z > maxZ) { maxZ = v.z; highY = v.y; }
        if (v.z < minZ) { minZ = v.z; lowY = v.y; }
    }

    // Convert to world space Y
    var transform = renderer.transform;
    highY = transform.TransformPoint(new Vector3(0, highY, maxZ)).y;
    lowY = transform.TransformPoint(new Vector3(0, lowY, minZ)).y;

    return (highY, lowY);
}
```

Then in the ramp section:
```csharp
if (isRamp)
{
    var (highY, lowY) = GetRampEdgeHeights(renderer);
    AddSnapPoint(root, "SnapPoint_HighEdge",
        new Vector3(center.x, highY, center.z + ext.z), Vector3.forward, ...);
    AddSnapPoint(root, "SnapPoint_LowEdge",
        new Vector3(center.x, lowY, center.z - ext.z), Vector3.back, ...);
}
```

Note: The actual vertex sampling may need adjustment depending on how the ramp FBX is oriented. Run the tool, check console output for the detected heights, and verify they match expectations. If the ramp slopes along a different axis, adjust the vertex scan accordingly.

**Step 2: Run the editor tool, verify 16 snap points on ramp prefabs**

**Step 3: Commit**

```
feat: add HighEdge and LowEdge slope-aware snaps to ramp prefabs
```

---

### Task 7: Remove CLICK DIAG logging

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:240-257`

**Step 1: Remove the diagnostic logging block**

Delete the entire CLICK DIAG block inside `HandleBuildInput`:

```csharp
// DELETE: lines 240-257 (the diagRay/diagHit logging block)
```

This was temporary debugging. The snap point system is now well-tested.

**Step 2: Commit**

```
chore: remove CLICK DIAG temporary logging
```

---

### Task 8: Final test run and playtest

**Step 1: Run all EditMode tests**

All SnapPlacementTests, UnifiedPlacementTests, GridPlacementTests must pass.

**Step 2: Playtest verification**

1. Place foundation on terrain (grid mode)
2. Snap second foundation to first foundation's side (should be edge-to-edge, same height)
3. Snap third foundation to second (rotated building -- normals should be correct)
4. Place wall on foundation edge
5. Extend wall run (snap wall to wall left/right)
6. Stack foundation on top of foundation
7. Place from below: stand under elevated foundation, look up, snap a wall to underside
8. Place ramp on foundation edge, verify R-key rotates it
9. Delete buildings to verify cleanup

**Step 3: Commit all remaining changes**

```
feat: complete snap point expansion -- 14-16 points per prefab
```

---

## Summary

| Task | Points per prefab | What changes |
|------|------------------|--------------|
| 1 | 5 -> 14 | Editor tool generates multi-height layout |
| 2 | (runtime fallback) | GenerateFromBounds matches new layout |
| 3 | -- | Bottom snap surfaceY in RaycastPlacement |
| 4 | -- | R-key rotation override in snap mode |
| 5 | -- | Tests updated for new layout + new test cases |
| 6 | 14 -> 16 (ramps) | Slope-aware HighEdge/LowEdge |
| 7 | -- | Remove temp debug logging |
| 8 | -- | Full test run + manual playtest |
