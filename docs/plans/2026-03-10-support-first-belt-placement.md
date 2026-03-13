# Support-First Belt Placement Refactor

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor belt placement so supports are the source of truth. Server spawns supports first at the raw ground position, reads their actual snap anchor world positions, then uses those positions as belt endpoints.

**Architecture:** Client sends raw ground hit positions (no Y offset) and `fromPort` flags. Server receives these, spawns supports at ground level for free endpoints, reads the spawned support's `BeltSnapAnchor.WorldPosition` as the true belt endpoint, then runs validation and belt creation using those anchor-derived positions. This eliminates the lossy round-trip of client-adds-offset / server-subtracts-offset.

**Tech Stack:** Unity 2022.3, FishNet 4.x, C#

---

## Current state (what's wrong)

Client in `TryResolveBeltEndpoint` adds `SupportAnchorHeight` to `hit.point.y` (line 1349), sends this artificial position to server. Server in `SpawnSupportAt` subtracts it back (line 539). This is backwards:
- Grid snap can move X/Z to terrain with different Y
- Validation uses the artificial Y, not the actual anchor Y
- Second belt connecting to same support sees a different Y than the first

## Touch points (every line that changes)

| File | Method | Current behavior | New behavior |
|------|--------|-----------------|-------------|
| `NetworkBuildController.cs:1346-1349` | `TryResolveBeltEndpoint` | Adds `SupportAnchorHeight` to Y | Returns raw `hit.point` (no offset) |
| `NetworkBuildController.cs:1276-1281` | `HandleBeltDragging` (CmdPlaceBelt call) | Sends offset positions | Sends raw ground positions for free endpoints |
| `NetworkBuildController.cs:1095-1105` | `HandleBeltPickStart` | Stores offset position as `_beltStartPos` | Stores raw ground position; also stores `_beltStartGroundPos` |
| `NetworkBuildController.cs:1140-1245` | `HandleBeltDragging` (validation + preview) | Uses offset positions for everything | Uses anchor-height positions for preview/validation (client-side prediction) |
| `GridManager.cs:380-500` | `CmdPlaceBelt` | Receives offset positions, spawns supports by subtracting | Receives ground positions + fromPort flags, spawns supports at ground, reads anchor positions, uses those for belt |
| `GridManager.cs:528-544` | `SpawnSupportAt` | Subtracts `SupportAnchorHeight` from position | Takes ground position directly, returns spawned anchor position |

---

## Task 1: Refactor `SpawnSupportAt` to return the anchor position

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:528-544`

**Step 1: Change `SpawnSupportAt` to accept ground position and return anchor world position**

The method currently receives an offset belt position and reverse-engineers ground. Flip it: receive ground position, spawn support there, return the actual anchor world position.

```csharp
// REPLACE the entire SpawnSupportAt method (lines 528-544) with:
private Vector3 SpawnSupportAt(Vector3 groundPos, Quaternion rotation, NetworkConnection sender = null)
{
    var prefab = GetPrefab(BuildingCategory.Support, 0);
    if (prefab == null)
    {
        Debug.LogWarning($"grid: no support prefab found at {groundPos}");
        return groundPos; // fallback: belt at ground level
    }

    var instance = Instantiate(prefab, groundPos, rotation);
    ServerManager.Spawn(instance);

    // Read the actual anchor position from the spawned support
    var anchor = instance.GetComponentInChildren<BeltSnapAnchor>();
    var anchorPos = anchor != null ? anchor.WorldPosition : groundPos;

    Debug.Log($"grid: support at {groundPos}, anchor at {anchorPos} by {sender?.ClientId}");
    return anchorPos;
}
```

**Key change:** Returns `Vector3` (the anchor world position) instead of `void`. The belt will use this returned position as its true endpoint.

**Step 2: Verify `CmdPlaceSupport` still compiles**

`CmdPlaceSupport` calls `SpawnSupportAt` but doesn't use the return value. That's fine -- it's for independent support placement.

---

## Task 2: Refactor `CmdPlaceBelt` to use anchor-derived positions

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs:380-500`

**Step 1: Change the signature -- replace offset positions with ground positions**

The client will now send raw ground positions for free endpoints and actual port positions for port endpoints. The server resolves the true belt endpoints.

```csharp
// REPLACE the CmdPlaceBelt signature and the support-spawn block (lines 380-408) with:
[ServerRpc(RequireOwnership = false)]
public void CmdPlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir,
    byte tier = 0, int variant = 0, byte routingMode = 0,
    bool startFromPort = true, bool endFromPort = true,
    NetworkConnection sender = null)
{
    if (!IsServerInitialized) return;

    var mode = (BeltRoutingMode)routingMode;

    // For free endpoints, startPos/endPos are raw ground positions.
    // Spawn supports there and use the actual anchor positions for the belt.
    var beltStartPos = startPos;
    var beltEndPos = endPos;

    if (!startFromPort)
        beltStartPos = SpawnSupportAt(startPos, Quaternion.LookRotation(startDir), sender);
    if (!endFromPort)
        beltEndPos = SpawnSupportAt(endPos, Quaternion.LookRotation(endDir), sender);

    // Validate using the true anchor-derived positions
    var validation = BeltPlacementValidator.Validate(beltStartPos, startDir, beltEndPos, endDir);
    if (!validation.IsValid)
    {
        if (!(mode == BeltRoutingMode.Straight && validation.Error == BeltValidationError.TurnTooSharp))
        {
            Debug.Log($"grid: belt placement rejected: {validation.Error} by {sender?.ClientId}");
            return;
        }
    }
```

**Step 2: Replace all uses of `startPos`/`endPos` with `beltStartPos`/`beltEndPos` in the rest of CmdPlaceBelt**

Every reference to `startPos` and `endPos` after the support-spawn block must use the anchor-derived positions instead. There are 12 references total across the straight and curved branches:

Straight mode (find and replace within the straight block):
- Line 417: `BeltRouteBuilder.Build(startPos, startDir, endPos, endDir)` -> use `beltStartPos`, `beltEndPos`
- Line 433: `info.SurfaceY = startPos.y` -> use `beltStartPos.y`
- Line 437: `ServerInitStraight(segment, startPos, startDir, endPos, endDir, ...)` -> use `beltStartPos`, `beltEndPos`
- Line 453: `AddBeltPort(go, startPos, -startDir, ...)` -> use `beltStartPos`
- Line 454: `AddBeltPort(go, endPos, endDir, ...)` -> use `beltEndPos`
- Line 456: Debug.Log `startPos` and `endPos` -> use `beltStartPos`, `beltEndPos`

Curved mode (same pattern):
- Line 460: `BeltSplineBuilder.Build(startPos, startDir, endPos, endDir)` -> use `beltStartPos`, `beltEndPos`
- Line 475: `info.SurfaceY = startPos.y` -> use `beltStartPos.y`
- Line 479: `ServerInit(segment, splineData, tier)` -> unchanged (splineData already uses correct positions)
- Line 495: `AddBeltPort(go, startPos, ...)` -> use `beltStartPos`
- Line 496: `AddBeltPort(go, endPos, ...)` -> use `beltEndPos`
- Line 498: Debug.Log -> use `beltStartPos`, `beltEndPos`

---

## Task 3: Remove Y offset from client-side endpoint resolution

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:1346-1349`

**Step 1: Remove the `SupportAnchorHeight` offset from `TryResolveBeltEndpoint`**

The client should return the raw ground hit position. The server handles the Y via the actual support anchor.

```csharp
// REPLACE lines 1346-1349:
// FROM:
        // Ground/structure fallback -- support will be placed here.
        // Raise belt endpoint to where the support's snap anchor will be.
        pos = hit.point;
        pos.y += GridManager.Instance.SupportAnchorHeight;

// TO:
        // Ground/structure fallback -- server will spawn support here
        // and derive belt endpoint from the support's actual snap anchor.
        pos = hit.point;
```

---

## Task 4: Add client-side anchor height prediction for preview

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs:1091-1297`

**Problem:** After Task 3, the client's line renderer preview will draw at ground level instead of at anchor height. The preview needs to show where the belt WILL be (at anchor height) even though the position sent to the server is at ground level.

**Step 1: Store both ground and preview positions**

Add a helper that computes the preview position (ground + anchor height) for the line renderer, while keeping the ground position for the server call.

At the top of the class (near the other belt state fields), add:

```csharp
private Vector3 _beltStartGroundPos;  // raw ground position sent to server
```

**Step 2: In `HandleBeltPickStart`, store ground pos separately**

```csharp
// REPLACE lines 1095-1106:
        if (TryResolveBeltEndpoint(ray, true, out var pos, out var dir, out var fromPort))
        {
            if (!fromPort)
            {
                pos.x = Mathf.Round(pos.x);
                pos.z = Mathf.Round(pos.z);
            }
            _beltStartGroundPos = pos;
            // For preview and validation, use anchor height for free endpoints
            _beltStartPos = fromPort ? pos : new Vector3(pos.x, pos.y + GridManager.Instance.SupportAnchorHeight, pos.z);
            _beltStartDir = dir;
            _beltStartFromPort = fromPort;
            _beltState = BeltPlacementState.Dragging;
```

**Step 3: In `HandleBeltDragging`, compute preview vs ground positions separately**

After resolving the end endpoint and grid-snapping (the block starting at line 1135), add preview position computation. The validation and line renderer use preview positions (at anchor height). The CmdPlaceBelt call uses ground positions.

```csharp
// AFTER the grid snap block for endPos (after line 1147 for straight, 1222 for curved),
// ADD this before any validation/preview code:
            var endGroundPos = endPos;
            // Preview position: raise free endpoints to anchor height
            if (!endFromPort)
                endPos.y += GridManager.Instance.SupportAnchorHeight;
```

This must be added in BOTH the straight mode block (after line 1147) and the curved mode block (after line 1222).

**Step 4: Update the CmdPlaceBelt call to send ground positions**

```csharp
// REPLACE the CmdPlaceBelt call (lines 1276-1281):
// FROM:
                GridManager.Instance.CmdPlaceBelt(
                    _beltStartPos, startDir,
                    endPos, endDir,
                    routingMode: (byte)_beltRoutingMode,
                    startFromPort: _beltStartFromPort,
                    endFromPort: endFromPort);

// TO:
                GridManager.Instance.CmdPlaceBelt(
                    _beltStartFromPort ? _beltStartPos : _beltStartGroundPos,
                    startDir,
                    endFromPort ? endPos : endGroundPos,
                    endDir,
                    routingMode: (byte)_beltRoutingMode,
                    startFromPort: _beltStartFromPort,
                    endFromPort: endFromPort);
```

**IMPORTANT:** `endGroundPos` must be in scope at the CmdPlaceBelt call site. It's declared inside the `if (TryResolveBeltEndpoint(...))` block, so it IS in scope. But it's currently declared inside the `if (_beltRoutingMode == BeltRoutingMode.Straight)` block. Move it up:

Declare `endGroundPos` right after line 1138 (`bool isValid;`):

```csharp
            var startDir = _beltStartDir;
            bool isValid;
            var endGroundPos = endPos;  // raw ground position for server
```

Then in both routing mode blocks, update it after grid snap:

Straight mode (after line 1147):
```csharp
                endGroundPos = endPos;
                if (!endFromPort)
                    endPos.y += GridManager.Instance.SupportAnchorHeight;
```

Curved mode (after line 1222):
```csharp
                endGroundPos = endPos;
                if (!endFromPort)
                    endPos.y += GridManager.Instance.SupportAnchorHeight;
```

**Step 5: Fix the end-direction calculation for ground fallback**

In `TryResolveBeltEndpoint`, line 1359 uses `_beltStartPos` for the direction calculation. Since `_beltStartPos` is now at anchor height for free endpoints, this is correct -- the direction from anchor to anchor stays horizontal.

No change needed here.

---

## Task 5: Verify and commit

**Step 1: Review the data flow end-to-end**

Trace through a ground-to-ground belt placement:
1. Client clicks ground -> `TryResolveBeltEndpoint` returns raw `hit.point` (no offset)
2. `HandleBeltPickStart` stores `_beltStartGroundPos = hit.point`, `_beltStartPos = hit.point + anchorHeight`
3. Client drags to second ground point -> same split: `endGroundPos` (raw) and `endPos` (preview height)
4. Preview line renders at anchor height (correct visual)
5. Validation uses anchor-height positions (correct slope/distance checks)
6. `CmdPlaceBelt` sends `_beltStartGroundPos` and `endGroundPos` (raw ground positions)
7. Server receives ground positions, spawns supports at ground level
8. `SpawnSupportAt` returns actual anchor world positions
9. Server uses anchor positions for validation, routing, mesh baking, port creation

Trace through a port-to-ground belt placement:
1. Client clicks machine port -> `TryResolveBeltEndpoint` returns port's WorldPosition
2. `_beltStartFromPort = true`, `_beltStartPos = portPos`, `_beltStartGroundPos = portPos`
3. Client clicks ground -> `endGroundPos = hit.point`, `endPos = hit.point + anchorHeight`
4. `CmdPlaceBelt` sends `_beltStartPos` (port pos, unchanged) and `endGroundPos` (raw ground)
5. Server: `startFromPort=true` -> uses startPos as-is. `endFromPort=false` -> spawns support, reads anchor

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Refactor belt placement: supports are source of truth

Server spawns supports at ground level first, reads actual
snap anchor world positions, uses those as belt endpoints.
Client sends raw ground positions for free endpoints.
Preview line still renders at anchor height for visual accuracy."
```

---

## Task 6: Ghost support previews during belt placement

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Problem:** During belt placement the player sees only a line renderer. They should also see ghost supports at free endpoints so they know exactly where poles will spawn and whether placement is valid.

**Step 1: Add ghost support fields**

Near the other belt state fields (around line 43-62), add:

```csharp
// Belt support ghosts
private GameObject _beltStartSupportGhost;
private GameObject _beltEndSupportGhost;
```

**Step 2: Create a helper to ensure the support ghost exists**

Add near the other ghost helper methods (around line 1448):

```csharp
private GameObject EnsureSupportGhost(GameObject existing)
{
    if (existing != null) return existing;
    var supportPrefab = GridManager.Instance.GetPrefab(BuildingCategory.Support, 0);
    if (supportPrefab == null) return null;
    return CreateGhostFromPrefab(supportPrefab);
}
```

**Step 3: Show start ghost in `HandleBeltPickStart`**

After setting `_beltState = BeltPlacementState.Dragging` and before the line renderer setup, show the start support ghost if the endpoint is free (not from a port):

```csharp
            // Show ghost support at start if placing on ground
            if (!fromPort)
            {
                _beltStartSupportGhost = EnsureSupportGhost(_beltStartSupportGhost);
                if (_beltStartSupportGhost != null)
                {
                    _beltStartSupportGhost.transform.position = _beltStartGroundPos;
                    _beltStartSupportGhost.transform.rotation = Quaternion.LookRotation(dir);
                    _beltStartSupportGhost.SetActive(true);
                    ApplyGhostColor(_beltStartSupportGhost, ValidColor);
                }
            }
```

**Step 4: Show end ghost in `HandleBeltDragging`**

After computing `endGroundPos` and the validity check, show the end support ghost at the ground position. This goes right before the line renderer color assignment (the `var color = isValid ? ...` line):

```csharp
            // Show ghost support at end if placing on ground
            if (!endFromPort)
            {
                _beltEndSupportGhost = EnsureSupportGhost(_beltEndSupportGhost);
                if (_beltEndSupportGhost != null)
                {
                    _beltEndSupportGhost.transform.position = endGroundPos;
                    _beltEndSupportGhost.transform.rotation = Quaternion.LookRotation(endDir);
                    _beltEndSupportGhost.SetActive(true);
                }
            }
            else if (_beltEndSupportGhost != null)
            {
                _beltEndSupportGhost.SetActive(false);
            }
```

**Step 5: Tint both ghosts based on validity**

Right after setting the line renderer color (the `_beltLineRenderer.startColor = color` block), add:

```csharp
            // Tint support ghosts to match validity
            if (_beltStartSupportGhost != null && _beltStartSupportGhost.activeSelf)
                ApplyGhostColor(_beltStartSupportGhost, color);
            if (_beltEndSupportGhost != null && _beltEndSupportGhost.activeSelf)
                ApplyGhostColor(_beltEndSupportGhost, color);
```

**Step 6: Hide ghosts on confirm and cancel**

In the confirm block (after `_beltPreviewLine.SetActive(false)`):

```csharp
                HideSupportGhosts();
```

In the cancel block (right-click, after `_beltPreviewLine.SetActive(false)`):

```csharp
            HideSupportGhosts();
```

**Step 7: Add the hide helper**

```csharp
private void HideSupportGhosts()
{
    if (_beltStartSupportGhost != null) _beltStartSupportGhost.SetActive(false);
    if (_beltEndSupportGhost != null) _beltEndSupportGhost.SetActive(false);
}
```

**Step 8: Clean up ghosts on tool switch**

In the belt tool cleanup (wherever other belt state is reset, likely near `HideGhost()` calls or `DestroyAllGhosts()`), add:

```csharp
if (_beltStartSupportGhost != null) { Destroy(_beltStartSupportGhost); _beltStartSupportGhost = null; }
if (_beltEndSupportGhost != null) { Destroy(_beltEndSupportGhost); _beltEndSupportGhost = null; }
```

This should go in `DestroyAllGhosts()` so tool switching cleans them up.

---

## Task 7: Verify and commit

Replaces old Task 5.

**Step 1: Review the data flow end-to-end**

Trace through a ground-to-ground belt placement:
1. Client clicks ground -> `TryResolveBeltEndpoint` returns raw `hit.point` (no offset)
2. `HandleBeltPickStart` stores `_beltStartGroundPos = hit.point`, `_beltStartPos = hit.point + anchorHeight`
3. Start support ghost appears at ground hit, green tint
4. Client drags to second ground point -> same split: `endGroundPos` (raw) and `endPos` (preview height)
5. End support ghost follows cursor at ground level, tinted green/red with validity
6. Preview line renders at anchor height (correct visual), green/red
7. Both ghosts and line show the full picture: poles + belt path + validity
8. `CmdPlaceBelt` sends `_beltStartGroundPos` and `endGroundPos` (raw ground positions)
9. Server receives ground positions, spawns supports at ground level
10. `SpawnSupportAt` returns actual anchor world positions
11. Server uses anchor positions for validation, routing, mesh baking, port creation

Trace through a port-to-ground belt placement:
1. Client clicks machine port -> no start ghost (fromPort=true)
2. Client drags to ground -> end ghost appears, line renderer connects port to anchor height
3. `CmdPlaceBelt` sends port position (unchanged) and ground position
4. Server: port endpoint as-is, spawns support at ground, reads anchor for belt

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Support-first belt placement with ghost previews

Server spawns supports at ground level first, reads actual snap anchor
world positions, uses those as belt endpoints. Ghost support previews
show during belt placement with green/red validity tinting."
```

---

## Potential issues to watch for

1. **Support spawned but belt validation fails:** Support exists with no belt. Design doc says this is fine (supports are independent, can be deleted separately).

2. **`endGroundPos` scope:** Must be declared before the routing mode branch so it's accessible at the CmdPlaceBelt call site regardless of which branch executed.

3. **Curved mode start direction:** In curved mode, `_beltStartPos` (at anchor height) is used for `toEnd` direction calculation (line 1227). This is correct -- direction should be from anchor to anchor, not ground to ground.

4. **Grid snap then height offset order:** Grid snap X/Z first, THEN copy to `endGroundPos`, THEN add height offset to `endPos`. This order ensures the ground position has the snapped X/Z at the raw hit Y.
