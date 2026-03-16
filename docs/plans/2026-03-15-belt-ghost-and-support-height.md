# Belt Ghost Preview and Support Height Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the belt line renderer preview with an actual mesh ghost, and add PgUp/PgDn support height adjustment during belt placement.

**Architecture:** Support height uses the existing `NudgeUp`/`NudgeDown` input actions, applying a `_beltSupportHeightOffset` during belt placement. Ghost mesh uses `BeltSplineMeshBaker.BakeMesh` on a ghost GameObject each frame. Both features are in `NetworkBuildController` with server-side support scaling in `GridManager.SpawnSupportAt`.

**Tech Stack:** Unity C#, BeltSplineMeshBaker, FishNet ServerRpc

---

## What already exists (DO NOT recreate)

- `NudgeUp`/`NudgeDown` input actions on the Build action map (`build.NudgeUp`, `build.NudgeDown`)
- `_nudgeOffset` field for structural placement height (separate from belt support height)
- `EnsureSupportGhost()` creates ghost support from prefab
- `BeltSplineMeshBaker.BakeMesh(target, waypoints, material)` generates mesh from waypoints
- `BeltRouteBuilder.Build(startPos, startDir, endPos, endDir, mode)` computes waypoints
- Line renderer preview at 30 points during belt dragging (lines 1557-1567)
- Support ghost positioning in `HandleBeltPickStart` and `HandleBeltDragging`
- `SpawnSupportAt` in GridManager spawns server-side support at ground position
- `SupportAnchorHeight` read from prefab's BeltSnapAnchor localPosition.y

## Key file paths

| File | Purpose |
|------|---------|
| `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs` | Belt placement, ghost preview, input handling |
| `Assets/_Slopworks/Scripts/Network/GridManager.cs` | Server-side support spawning (SpawnSupportAt) |
| `Assets/_Slopworks/Scripts/Automation/BeltSplineMeshBaker.cs` | Mesh generation from waypoints |

## Support prefab hierarchy

```
BELT SUPPORT (root, pivot at bottom)
  Extrusion [3029]  -- rectangular top piece (localPos Y=-0.075)
  Extrusion [3155]  -- cylinder pole (localPos Y=0, pivot at bottom, scales upward)
  BeltSnapAnchor    -- connection point (localPos Y=1)
```

---

### Task 1: Add belt support height offset field and input handling

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Step 1: Add field**

After `_beltRoutingMode` field (line 57), add:

```csharp
private float _beltSupportHeightOffset; // PgUp/PgDn height adjustment for belt supports
```

**Step 2: Handle PgUp/PgDn during belt placement**

In `HandleBeltTool` (the method that switches on `_beltState`), add height handling before the switch statement. Find the `HandleBeltTool` method and add after the method signature but before the switch:

```csharp
// Support height adjustment (PgUp/PgDn) during belt placement
if (_beltState != BeltPlacementState.Idle)
{
    bool shift = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    float step = shift ? 0.25f : 0.5f;
    if (build.NudgeUp.WasPressedThisFrame())
    {
        _beltSupportHeightOffset += step;
        Debug.Log($"belt: support height offset {_beltSupportHeightOffset:+0.0;-0.0}m");
    }
    if (build.NudgeDown.WasPressedThisFrame())
    {
        _beltSupportHeightOffset = Mathf.Max(0f, _beltSupportHeightOffset - step);
        Debug.Log($"belt: support height offset {_beltSupportHeightOffset:+0.0;-0.0}m");
    }
}
```

**Step 3: Reset offset when belt placement finishes**

In every place where `_beltState = BeltPlacementState.Idle` is set (placement complete, cancel), add `_beltSupportHeightOffset = 0f;` right after. Also reset in `SelectTool()`.

Search for all `_beltState = BeltPlacementState.Idle` and add the reset.

**Step 4: Use offset in belt start position calculation**

In `HandleBeltPickStart`, line 1314 currently reads:

```csharp
_beltStartPos = fromPort ? pos : new Vector3(pos.x, pos.y + GridManager.Instance.SupportAnchorHeight, pos.z);
```

Change to:

```csharp
_beltStartPos = fromPort ? pos : new Vector3(pos.x, pos.y + GridManager.Instance.SupportAnchorHeight + _beltSupportHeightOffset, pos.z);
```

**Step 5: Use offset in belt end position calculation**

In `HandleBeltDragging`, line 1413 currently reads:

```csharp
endPos.y += GridManager.Instance.SupportAnchorHeight;
```

Change to:

```csharp
endPos.y += GridManager.Instance.SupportAnchorHeight + _beltSupportHeightOffset;
```

**Step 6: Use offset in PickStart preview anchor position**

In `HandleBeltPickStart`, line 1279 currently reads:

```csharp
previewPos.y + GridManager.Instance.SupportAnchorHeight,
```

Change to:

```csharp
previewPos.y + GridManager.Instance.SupportAnchorHeight + _beltSupportHeightOffset,
```

**Step 7: Commit**

```
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Add PgUp/PgDn belt support height offset

Adjusts belt endpoint height in 0.5m steps (0.25m with Shift).
Clamped to minimum 0. Resets when placement finishes or tool changes."
```

---

### Task 2: Scale support ghost to match height offset

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Step 1: Add helper method to apply height offset to support ghost**

Add after `EnsureSupportGhost` method (after line ~2171):

```csharp
private void ApplySupportHeightOffset(GameObject supportGhost, float heightOffset)
{
    if (supportGhost == null || heightOffset == 0f) return;

    // Find children by mesh name pattern
    foreach (Transform child in supportGhost.transform)
    {
        var meshFilter = child.GetComponent<MeshFilter>();
        if (meshFilter == null) continue;

        string meshName = meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "";

        if (meshName.Contains("3155")) // Cylinder pole
        {
            // Pivot at bottom, Y scale stretches upward
            float defaultHeight = GridManager.Instance.SupportAnchorHeight;
            child.localScale = new Vector3(1f, 1f + (heightOffset / defaultHeight), 1f);
        }
        else if (meshName.Contains("3029")) // Top piece
        {
            var localPos = child.localPosition;
            child.localPosition = new Vector3(localPos.x, -0.075f + heightOffset, localPos.z);
        }
    }

    // Move anchor
    var anchor = supportGhost.GetComponentInChildren<BeltSnapAnchor>();
    if (anchor != null)
    {
        var localPos = anchor.transform.localPosition;
        anchor.transform.localPosition = new Vector3(localPos.x, 1f + heightOffset, localPos.z);
    }
}
```

**Step 2: Call helper after positioning support ghosts**

In `HandleBeltPickStart`, after the support ghost is positioned and shown (after line 1261 `ApplyGhostColor`), add:

```csharp
ApplySupportHeightOffset(_beltStartSupportGhost, _beltSupportHeightOffset);
```

Do the same after every place where a support ghost is positioned:
- `HandleBeltPickStart` preview block (~line 1261)
- `HandleBeltPickStart` confirmed placement block (~line 1338)
- `HandleBeltDragging` end support block (~line 1540)

**Step 3: Reset ghost child transforms when offset is zero**

The `ApplySupportHeightOffset` method skips when offset is 0, but if the ghost was previously scaled and offset resets, the children keep their modified transforms. Add an early block:

```csharp
private void ApplySupportHeightOffset(GameObject supportGhost, float heightOffset)
{
    if (supportGhost == null) return;

    foreach (Transform child in supportGhost.transform)
    {
        var meshFilter = child.GetComponent<MeshFilter>();
        if (meshFilter == null) continue;

        string meshName = meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "";

        if (meshName.Contains("3155"))
        {
            float defaultHeight = GridManager.Instance.SupportAnchorHeight;
            child.localScale = new Vector3(1f, 1f + (heightOffset / defaultHeight), 1f);
        }
        else if (meshName.Contains("3029"))
        {
            child.localPosition = new Vector3(child.localPosition.x, -0.075f + heightOffset, child.localPosition.z);
        }
    }

    var anchor = supportGhost.GetComponentInChildren<BeltSnapAnchor>();
    if (anchor != null)
        anchor.transform.localPosition = new Vector3(anchor.transform.localPosition.x, 1f + heightOffset, anchor.transform.localPosition.z);
}
```

(This version always sets the values, so resetting to 0 restores defaults.)

**Step 4: Commit**

```
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Scale support ghost to match height offset

Cylinder stretches via Y scale, top piece and anchor offset upward.
Always resets to defaults when offset is zero."
```

---

### Task 3: Scale server-side support to match height offset

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:381-385` (CmdPlaceBelt signature)
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:609-631` (SpawnSupportAt)
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs` (CmdPlaceBelt call)

**Step 1: Add supportHeightOffset parameter to CmdPlaceBelt**

```csharp
[ServerRpc(RequireOwnership = false)]
public void CmdPlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir,
    byte tier = 0, int variant = 0, byte routingMode = 0,
    bool startFromPort = true, bool endFromPort = true,
    bool flipBeltPorts = false,
    float supportHeightOffset = 0f,
    NetworkConnection sender = null)
```

**Step 2: Pass offset to SpawnSupportAt**

In CmdPlaceBelt, update the SpawnSupportAt calls (lines 396-398):

```csharp
if (!startFromPort)
    beltStartPos = SpawnSupportAt(startPos, Quaternion.LookRotation(startDir), supportHeightOffset, sender);
if (!endFromPort)
    beltEndPos = SpawnSupportAt(endPos, Quaternion.LookRotation(endDir), supportHeightOffset, sender);
```

**Step 3: Update SpawnSupportAt to accept and apply height offset**

```csharp
private Vector3 SpawnSupportAt(Vector3 groundPos, Quaternion rotation, float heightOffset = 0f, NetworkConnection sender = null)
{
    var prefab = GetPrefab(BuildingCategory.Support, 0);
    if (prefab == null)
    {
        Debug.LogWarning($"grid: no support prefab found at {groundPos}");
        return groundPos;
    }

    var instance = Instantiate(prefab, groundPos, rotation);

    var info = instance.AddComponent<PlacementInfo>();
    info.Category = BuildingCategory.Support;
    info.SurfaceY = groundPos.y;

    // Apply height offset to support children
    if (heightOffset > 0f)
    {
        foreach (Transform child in instance.transform)
        {
            var meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter == null) continue;
            string meshName = meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "";

            if (meshName.Contains("3155"))
                child.localScale = new Vector3(1f, 1f + (heightOffset / SupportAnchorHeight), 1f);
            else if (meshName.Contains("3029"))
                child.localPosition = new Vector3(child.localPosition.x, child.localPosition.y + heightOffset, child.localPosition.z);
        }

        var anchorTransform = instance.GetComponentInChildren<BeltSnapAnchor>()?.transform;
        if (anchorTransform != null)
            anchorTransform.localPosition = new Vector3(anchorTransform.localPosition.x, anchorTransform.localPosition.y + heightOffset, anchorTransform.localPosition.z);
    }

    ServerManager.Spawn(instance);

    var anchor = instance.GetComponentInChildren<BeltSnapAnchor>();
    var anchorPos = anchor != null ? anchor.WorldPosition : groundPos;

    Debug.Log($"grid: support at {groundPos}, anchor at {anchorPos} height+{heightOffset:F1} by {sender?.ClientId}");
    return anchorPos;
}
```

**Step 4: Pass offset from NetworkBuildController CmdPlaceBelt call**

In the CmdPlaceBelt call (~line 1574), add `supportHeightOffset: _beltSupportHeightOffset`:

```csharp
GridManager.Instance.CmdPlaceBelt(
    _beltStartFromPort ? _beltStartPos : _beltStartGroundPos,
    startDir,
    endFromPort ? endPos : endGroundPos,
    endDir,
    routingMode: (byte)_beltRoutingMode,
    startFromPort: _beltStartFromPort,
    endFromPort: endFromPort,
    flipBeltPorts: _beltFlipPorts,
    supportHeightOffset: _beltSupportHeightOffset);
```

**Step 5: Also update CmdPlaceSupport signature**

The standalone `CmdPlaceSupport` (line 601-607) should also accept height offset for consistency, but this can be deferred since belts use `SpawnSupportAt` directly.

**Step 6: Commit**

```
git add Assets/_Slopworks/Scripts/Network/GridManager.cs Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Server-side support height scaling

CmdPlaceBelt passes supportHeightOffset to SpawnSupportAt.
Server scales cylinder and offsets top piece to match client ghost."
```

---

### Task 4: Belt ghost mesh preview

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Step 1: Add ghost mesh fields**

After the line renderer fields (~line 59), add:

```csharp
// Belt ghost mesh preview
private GameObject _beltGhostMesh;
private Material _beltGhostMaterial;
```

**Step 2: Create ghost material on first use**

Add helper method after `ApplySupportHeightOffset`:

```csharp
private Material EnsureBeltGhostMaterial()
{
    if (_beltGhostMaterial != null) return _beltGhostMaterial;
    var shader = Shader.Find("Sprites/Default");
    _beltGhostMaterial = new Material(shader);
    _beltGhostMaterial.color = new Color(0f, 1f, 0f, 0.5f);
    return _beltGhostMaterial;
}
```

**Step 3: Replace line renderer preview with ghost mesh**

In `HandleBeltDragging`, replace the preview line block (lines 1557-1567):

```csharp
// Preview line (at anchor height) -- all modes use waypoints
{
    var waypoints = BeltRouteBuilder.Build(_beltStartPos, startDir, endPos, endDir, _beltRoutingMode);
    float routeLen = BeltRouteBuilder.ComputeRouteLength(waypoints);
    for (int i = 0; i < 30; i++)
    {
        float t = (float)i / 29;
        _beltLineRenderer.SetPosition(i,
            BeltRouteBuilder.EvaluateRoute(waypoints, routeLen, t));
    }
}
```

With:

```csharp
// Ghost mesh preview (at anchor height)
{
    var waypoints = BeltRouteBuilder.Build(_beltStartPos, startDir, endPos, endDir, _beltRoutingMode);

    if (_beltGhostMesh == null)
    {
        _beltGhostMesh = new GameObject("BeltGhostMesh");
        _beltGhostMesh.layer = PhysicsLayers.Decal;
    }

    var ghostMat = EnsureBeltGhostMaterial();
    ghostMat.color = isValid ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
    BeltSplineMeshBaker.BakeMesh(_beltGhostMesh, waypoints, ghostMat);

    // Also update line renderer for backup visibility
    float routeLen = BeltRouteBuilder.ComputeRouteLength(waypoints);
    for (int i = 0; i < 30; i++)
    {
        float t = (float)i / 29;
        _beltLineRenderer.SetPosition(i,
            BeltRouteBuilder.EvaluateRoute(waypoints, routeLen, t));
    }
}
```

**Step 4: Clean up ghost mesh on placement complete/cancel**

In `DestroyAllGhosts`, add:

```csharp
if (_beltGhostMesh != null) { Destroy(_beltGhostMesh); _beltGhostMesh = null; }
```

In every place where `_beltState = BeltPlacementState.Idle` is set and the preview line is hidden, also destroy the ghost mesh:

```csharp
if (_beltGhostMesh != null) { Destroy(_beltGhostMesh); _beltGhostMesh = null; }
```

**Step 5: Handle rejected endpoint (no valid route)**

In `HandleBeltDragging`, the early return block when endpoint is rejected (lines 1362-1378) should also hide the ghost mesh:

```csharp
if (_beltGhostMesh != null) _beltGhostMesh.SetActive(false);
```

And in the main block, make sure it's active:

```csharp
_beltGhostMesh.SetActive(true);
```

(Add after the `_beltGhostMesh` null check / creation.)

**Step 6: Commit**

```
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Belt ghost mesh preview during placement

Uses BeltSplineMeshBaker to render actual belt mesh as semi-transparent
ghost. Updates every frame during drag. Green for valid, red for invalid.
Line renderer kept as backup. Ghost destroyed on placement complete."
```

---

### Task 5: Playtest and push

1. Enter play mode, Host
2. Select belt tool
3. Click on ground to start belt -- verify support ghost appears
4. Press PgUp -- support ghost should stretch taller, belt endpoint rises
5. Press PgDn -- support shrinks back
6. Drag to endpoint -- verify ghost mesh shows actual belt shape (not just a line)
7. Verify curves match between ghost and placed belt
8. Place the belt -- verify server support matches the height
9. Press Shift+PgUp for 0.25m steps

```
git push origin kevin/belts
```

---

## What does NOT need to change

- **BeltSplineMeshBaker** -- already has the API we need, no modifications
- **BeltRouteBuilder** -- waypoint computation unchanged
- **BeltPort / BeltSnapAnchor** -- unchanged, anchor position is read from the spawned support
- **CmdPlaceBelt geometry** -- start/end positions already include the height, server uses them as-is for route building
- **Validation** -- BeltRouteBuilder.Validate uses the actual positions including height
