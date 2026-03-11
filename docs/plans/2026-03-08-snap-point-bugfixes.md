# Snap Point Bugfix Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 7 bugs found during snap point expansion playtest: snap selection, wall Y position, wall-to-wall direction, R-key rotation, nudge surfaceY, zoop regression, and ramp-to-ramp.

**Architecture:** Most bugs trace to two root causes: (1) FindNearest ignoring which face was hit, and (2) GetSnapPlacementPosition conflating "rotation 0" with "no rotation specified." Fix the selection and rotation foundations first, then the category-aware surfaceY, nudge storage, and zoop guard.

**Tech Stack:** Unity 2022 LTS, FishNet, C# EditMode tests (NUnit), MCP Unity for recompile/test

**Design doc:** `docs/plans/2026-03-08-snap-playtest-checklist.md` (playtest results)

---

### Task 1: Fix FindNearest to filter by hit normal (Bug A)

The root cause of Tests 8, 13. `FindNearest` uses raw distance -- from ground level, bottom snaps always win. Multi-height selection is dead.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/BuildingSnapPoint.cs:94-113`

**Step 1: Add hit-normal-aware overload of FindNearest**

Keep the existing `FindNearest(GameObject, Vector3)` for backward compatibility. Add a new overload:

```csharp
/// <summary>
/// Find the snap point on the given building closest to a world-space point,
/// filtering to only snap points on the same face as the raycast hit.
/// Falls back to any closest snap if no face match is found.
/// </summary>
public static BuildingSnapPoint FindNearest(GameObject building, Vector3 worldPoint, Vector3 hitNormal)
{
    var points = building.GetComponentsInChildren<BuildingSnapPoint>();
    if (points.Length == 0) return null;

    // First pass: only consider snaps whose normal matches the hit face
    BuildingSnapPoint nearest = null;
    float bestDist = float.MaxValue;

    foreach (var p in points)
    {
        if (Vector3.Dot(p.Normal, hitNormal) < 0.5f) continue;

        float dist = Vector3.Distance(p.transform.position, worldPoint);
        if (dist < bestDist)
        {
            bestDist = dist;
            nearest = p;
        }
    }

    // Fallback: if no normal match, use any closest
    if (nearest == null)
        return FindNearest(building, worldPoint);

    return nearest;
}
```

**Step 2: Update RaycastPlacement to pass hit normal**

In `NetworkBuildController.cs:663`, change:

```csharp
// Old:
var nearest = BuildingSnapPoint.FindNearest(placement.gameObject, hit.point);

// New:
var nearest = BuildingSnapPoint.FindNearest(placement.gameObject, hit.point, hit.normal);
```

**Step 3: Commit**

```
fix: filter snap selection by hit face normal
```

---

### Task 2: Fix GetSnapPlacementPosition rotation (Bug D)

`rotationDeg != 0` treats 0 as "use autoYaw." But 0 is a valid explicit rotation (north-facing). Change to always use the caller's rotation. The caller becomes responsible for computing the right value.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:193-207`

**Step 1: Always use rotationDeg for horizontal snaps**

Replace lines 193-207:

```csharp
        // Horizontal normal: offset along normal by rotated depth
        float autoYaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        float finalYaw = (float)rotationDeg;

        // Compute which extent faces the normal after rotation
        float yawDiff = (finalYaw - autoYaw) * Mathf.Deg2Rad;
        float halfDepth = Mathf.Abs(extents.z * Mathf.Cos(yawDiff)) + Mathf.Abs(extents.x * Mathf.Sin(yawDiff));

        Vector3 horizontalNormal = new Vector3(normal.x, 0f, normal.z).normalized;
        Vector3 pos = new Vector3(
            snapPos.x + horizontalNormal.x * halfDepth,
            surfaceY + baseOffset,
            snapPos.z + horizontalNormal.z * halfDepth);

        return (pos, Quaternion.Euler(0f, finalYaw, 0f));
```

The only change from current code: `float finalYaw = (float)rotationDeg;` instead of `float finalYaw = rotationDeg != 0 ? (float)rotationDeg : autoYaw;`.

**Step 2: Commit**

```
fix: always use caller rotation in GetSnapPlacementPosition
```

---

### Task 3: Fix snap rotation computation in NetworkBuildController (Bugs C + D)

The caller must now compute the full rotation before calling `GetSnapPlacementPosition`. For wall-to-wall, the base rotation is the existing wall's yaw (extend the run). For other horizontal snaps, it's the autoYaw from the snap normal. R-key adds offset from the base.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:202-207`

**Step 1: Compute effective rotation for snap mode**

Replace the snap mode block (lines 202-207):

```csharp
        if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
        {
            int effectiveRotation;
            if (Mathf.Abs(_activeSnapPoint.Normal.y) > 0.9f)
            {
                // Vertical snap: player rotation directly
                effectiveRotation = _placeRotation;
            }
            else
            {
                // Horizontal snap: compute base yaw then add R-key offset
                var targetInfo = _activeSnapPoint.GetComponentInParent<PlacementInfo>();
                bool isWallOnWall = targetInfo != null
                    && targetInfo.Category == BuildingCategory.Wall
                    && _currentTool == BuildTool.Wall;

                int baseYaw;
                if (isWallOnWall)
                {
                    // Wall-to-wall: match existing wall's rotation (extend the run)
                    baseYaw = Mathf.RoundToInt(_activeSnapPoint.transform.root.eulerAngles.y);
                }
                else
                {
                    // Other horizontal: auto-align from snap normal
                    float autoYaw = Mathf.Atan2(_activeSnapPoint.Normal.x, _activeSnapPoint.Normal.z) * Mathf.Rad2Deg;
                    baseYaw = Mathf.RoundToInt(autoYaw);
                }

                effectiveRotation = (baseYaw + _placeRotation) % 360;
            }

            var result = GridManager.GetSnapPlacementPosition(
                _activeSnapPoint, prefab, effectiveRotation, _surfaceY);
            ghostPos = result.position + new Vector3(0f, _nudgeOffset, 0f);
            ghostRot = result.rotation;
        }
```

This gives:
- Wall-to-wall, R=0: extends the run (same direction)
- Wall-to-wall, R=90: corner piece (perpendicular, meeting at edge)
- Wall-to-wall, R=180: extends (same visual -- wall is symmetric)
- Wall-to-wall, R=270: corner other direction
- Wall on foundation, R=0: faces outward from foundation edge
- Wall on foundation, R=90: rotated 90 from edge normal
- Foundation on foundation, R=0: autoYaw from normal
- Never a T-junction because extension uses existing wall's yaw, not the face normal

**Step 2: Commit**

```
fix: wall-to-wall extends run, R-key offsets from base yaw
```

---

### Task 4: Fix wall-on-foundation surfaceY (Bug B)

Walls on foundations have their bottom at terrain level instead of at foundation top. The horizontal snap sets `_surfaceY = placement.SurfaceY` (terrain). Walls (and ramps) placed on foundations should sit ON TOP of the slab.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:670-676`

**Step 1: Category-aware surfaceY for horizontal snaps**

Replace lines 675-676 (the `else` clause for horizontal):

```csharp
                else
                {
                    // Horizontal snap: walls/ramps on foundations sit on top of the slab.
                    // Same-type or wall-on-wall sits at the same base height.
                    bool sitsOnTop = (placement.Category == BuildingCategory.Foundation
                                     || placement.Category == BuildingCategory.Ramp)
                                    && (_currentTool == BuildTool.Wall
                                        || _currentTool == BuildTool.Ramp);
                    _surfaceY = sitsOnTop
                        ? placement.SurfaceY + placement.ObjectHeight
                        : placement.SurfaceY;
                }
```

This gives:
- Wall on foundation: surfaceY = foundationTop → wall bottom at foundation top. CORRECT.
- Wall on wall: surfaceY = wall.SurfaceY (same base) → same height. CORRECT.
- Foundation on foundation: surfaceY = foundation.SurfaceY → same height, flush. CORRECT.
- Ramp on foundation: surfaceY = foundationTop → ramp bottom at foundation top. CORRECT.
- Foundation on wall: surfaceY = wall.SurfaceY → not on top. CORRECT (rare case).

Note: For wall-on-wall to work correctly, the wall's `PlacementInfo.SurfaceY` must store the correct value. With this fix, when placing a wall on a foundation, `_surfaceY` = foundationTop. This gets passed to `CmdPlace` and stored as the wall's `PlacementInfo.SurfaceY`. Then wall-on-wall uses that stored value. Chain is consistent.

**Step 2: Commit**

```
fix: walls and ramps sit on top of foundations, not at terrain level
```

---

### Task 5: Fix nudge surfaceY storage (Bug E)

`PlacementInfo.SurfaceY` doesn't include the nudge offset. When snapping to a nudged building, the math uses wrong Y values. The actual world position is correct (ghostPos includes nudge) but the stored surfaceY is raw terrain height.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:234-251`

**Step 1: Use effective surfaceY (including nudge) for placement commands and validation**

In HandleBuildInput, around lines 234-251, compute `placeSurfaceY` and use it everywhere:

```csharp
        // Validity check
        var category = ToolToCategory(_currentTool);
        float placeSurfaceY = _surfaceY + _nudgeOffset;
        bool valid = !GridManager.Instance.HasBuildingAt(cell, placeSurfaceY);
        SetGhostColor(valid ? ValidColor : InvalidColor);

        // Place on left click
        if (mouse.leftButton.wasPressedThisFrame && valid)
        {
            int rotDeg = Mathf.RoundToInt(ghostRot.eulerAngles.y);

            if (category == BuildingCategory.Wall || category == BuildingCategory.Ramp)
            {
                var dir = RotationToDirection(rotDeg);
                GridManager.Instance.CmdPlaceDirectional(cell, placeSurfaceY, dir, CurrentVariant, category, ghostPos, ghostRot);
            }
            else
            {
                GridManager.Instance.CmdPlace(cell, placeSurfaceY, rotDeg, CurrentVariant, category, ghostPos);
            }
            Debug.Log($"build: placed {category} at ({cell.x},{cell.y}) y={placeSurfaceY:F1}");
        }

        // Delete on right click
        if (mouse.rightButton.wasPressedThisFrame)
        {
            var hitCell = GridManager.Instance.Grid.WorldToCell(hit.point);
            GridManager.Instance.CmdDelete(hitCell, placeSurfaceY);
        }
```

**Step 2: Commit**

```
fix: store effective surfaceY including nudge offset in PlacementInfo
```

---

### Task 6: Fix zoop snap regression (Bug F)

With 14 snap points on every placed foundation, raycasting during zoop hits existing foundations and enters snap mode. Zoop only fires when `_placementMode == PlacementMode.Grid`, so it stops working near existing buildings.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:660-663`

**Step 1: Skip snap detection during zoop**

In `RaycastPlacement`, skip the snap point check when zoop is active:

```csharp
        // Check if we hit an existing building with snap points
        // Skip during zoop -- zoop always uses grid placement
        var placement = hit.collider.GetComponentInParent<PlacementInfo>();
        if (placement != null && !_zoopMode)
        {
```

Add `&& !_zoopMode` to the existing `if (placement != null)` check at line 661.

**Step 2: Commit**

```
fix: force grid mode during zoop to prevent snap interference
```

---

### Task 7: Improve ramp-to-ramp snap (Bug G)

Ramp-to-ramp extension connects at wrong points. The HighEdge/LowEdge snaps exist on editor prefabs but FindNearest may not select them. With the face-normal filtering from Task 1, looking at the high edge (+Z face, forward normal) competes with North_Top/Mid/Bot snaps. Need to ensure the slope-height snaps are reachable.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs` (snap rotation section from Task 3)

**Step 1: Add ramp-to-ramp rotation logic**

In the snap rotation computation from Task 3, add a ramp-to-ramp case alongside the wall-to-wall case:

```csharp
                bool isWallOnWall = targetInfo != null
                    && targetInfo.Category == BuildingCategory.Wall
                    && _currentTool == BuildTool.Wall;
                bool isRampOnRamp = targetInfo != null
                    && targetInfo.Category == BuildingCategory.Ramp
                    && _currentTool == BuildTool.Ramp;

                int baseYaw;
                if (isWallOnWall || isRampOnRamp)
                {
                    // Match existing building's rotation (extend the run / continue the slope)
                    baseYaw = Mathf.RoundToInt(_activeSnapPoint.transform.root.eulerAngles.y);
                }
                else
                {
                    float autoYaw = Mathf.Atan2(_activeSnapPoint.Normal.x, _activeSnapPoint.Normal.z) * Mathf.Rad2Deg;
                    baseYaw = Mathf.RoundToInt(autoYaw);
                }
```

This ensures ramp-to-ramp inherits the existing ramp's rotation by default, with R-key to adjust. The HighEdge snap point's normal is forward, so it competes with other forward snaps. With face-normal filtering from Task 1, the closest forward-facing snap to the hit point wins. The HighEdge snap is at the actual slope surface height (from mesh vertex sampling), so it should be closest when looking at the high edge.

**Step 2: Commit**

```
fix: ramp-to-ramp inherits existing ramp rotation
```

---

### Task 8: Update tests for rotation API change

Task 2 changed `GetSnapPlacementPosition` to always use `rotationDeg` instead of falling back to autoYaw when 0. Tests that pass `rotationDeg: 0` expecting auto-alignment to east/south/west normals need to pass the autoYaw explicitly.

**Files:**
- Modify: `Assets/_Slopworks/Tests/Editor/EditMode/SnapPlacementTests.cs`
- Modify: `Assets/_Slopworks/Tests/Editor/EditMode/UnifiedPlacementTests.cs`

**Step 1: Update SnapPlacementTests**

`SnapPlacement_RotationAlignsToNormal` (line 140): East snap, autoYaw = 90. Change `0` to `90`:

```csharp
var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, _prefab, 90, 0f);
```

Test still expects rot.y = 90, pos.x = 2.25. Both correct with explicit 90.

**Step 2: Update UnifiedPlacementTests**

Tests that use east/south/west snaps with `rotationDeg: 0` need the autoYaw value:

1. `Snap_FoundationToFoundationEast_FlushAdjacent` (east snap, autoYaw=90):
   ```csharp
   var (pos, _) = GridManager.GetSnapPlacementPosition(eastSnap, prefab, 90, 0f);
   ```

2. `Snap_FoundationToFoundationSouth_FlushAdjacent` (south snap, autoYaw=180):
   ```csharp
   var (pos, _) = GridManager.GetSnapPlacementPosition(southSnap, prefab, 180, 0f);
   ```

3. `Snap_WallOnFoundationEastEdge_RotatesAutomatically` (east snap, autoYaw=90):
   ```csharp
   var (pos, rot) = GridManager.GetSnapPlacementPosition(eastSnap, wall, 90, 0f);
   ```

4. `Snap_FoundationInCorner_TwoAdjacentFoundations` (two east snap calls, autoYaw=90):
   ```csharp
   var (posC, _) = GridManager.GetSnapPlacementPosition(eastSnapA, prefab, 90, 0f);
   var (posD, _) = GridManager.GetSnapPlacementPosition(eastSnapB, prefab, 90, 0f);
   ```

Tests using north snaps (autoYaw=0) or vertical snaps are unaffected -- passing 0 is correct.

**Step 3: Add new test for wall-on-foundation Y**

Add to `UnifiedPlacementTests.cs` after the existing snap tests:

```csharp
[Test]
public void Snap_WallOnFoundationNorth_BottomAtFoundationTop()
{
    // Foundation 4x0.5x4 at origin. Top surface at Y=0.5 (center 0.25 + halfHeight 0.25).
    // Wall placed with surfaceY = foundationTop (0.5).
    // Wall halfHeight = 1.5. pos.y = 0.5 + 1.5 = 2.0. Wall bottom = 0.5 = foundation top.
    var existing = CreatePlacedBuilding(new Vector3(4f, 0.5f, 4f), Vector3.zero);
    var wall = CreatePrefab(new Vector3(4f, 3f, 0.5f));

    var northSnap = FindSnapByNormal(existing, Vector3.forward);
    // surfaceY = foundationTop = SurfaceY(0) + ObjectHeight(0.5) = 0.5
    var (pos, _) = GridManager.GetSnapPlacementPosition(northSnap, wall, 0, 0.5f);

    Assert.AreEqual(2.0f, pos.y, 0.01f, "Wall center Y = foundationTop (0.5) + wallHalfHeight (1.5)");
    Assert.AreEqual(0.5f, pos.y - 1.5f, 0.01f, "Wall bottom at foundation top");
}
```

**Step 4: Run all placement tests**

```
SnapPlacementTests: 4 tests
UnifiedPlacementTests: 50 tests (47 existing + 3 new from prior plan)
GridPlacementTests: 7 tests
```

All should pass.

**Step 5: Commit**

```
test: update rotation tests for explicit autoYaw, add wall-on-foundation Y test
```

---

### Task 9: Playtest verification

Rerun the playtest checklist from `docs/plans/2026-03-08-snap-playtest-checklist.md`.

**Step 1: Run `Tools > Slopworks > Add Snap Points to Prefabs`**

Verify console shows 14 for foundations/walls, 16 for ramps.

**Step 2: Retest each scenario**

| Test | Bug fixed | What to verify |
|------|-----------|----------------|
| 4 | E | Foundation below a nudged foundation hangs correctly |
| 5-6 | B | Wall bottom at foundation top, not terrain |
| 7 | C | Wall-to-wall extends the run by default, R makes corner |
| 8 | A | Top snap selectable when looking at top edge of wall |
| 9-10 | D | R-key rotation works at 0, 90, 180, 270 in snap mode |
| 11-12 | G | Ramp-to-ramp continues slope direction |
| 13 | A | Different snap selected when aiming at top vs mid vs bottom |
| 15 | F | Zoop works near existing buildings |
| 16 | E | Nudge applies in snap mode, ghost and placement match |

---

## Summary

| Task | Bug | Files modified | What changes |
|------|-----|----------------|--------------|
| 1 | A | BuildingSnapPoint.cs, NetworkBuildController.cs | Face-normal filtering on FindNearest |
| 2 | D | GridManager.cs | Always use caller's rotation |
| 3 | C+D | NetworkBuildController.cs | Wall-to-wall extends run, R offsets from base yaw |
| 4 | B | NetworkBuildController.cs | Walls/ramps sit on top of foundations |
| 5 | E | NetworkBuildController.cs | Store effective surfaceY with nudge |
| 6 | F | NetworkBuildController.cs | Skip snaps during zoop |
| 7 | G | NetworkBuildController.cs | Ramp-to-ramp inherits rotation |
| 8 | -- | SnapPlacementTests.cs, UnifiedPlacementTests.cs | Tests match new rotation API |
| 9 | -- | -- | Manual playtest verification |
