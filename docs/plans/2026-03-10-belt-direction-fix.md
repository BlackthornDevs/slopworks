# Belt Direction Fix: Respect R-Key, Auto-Derive End Direction

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Start direction is locked by R-key at first click. End direction is auto-derived from spatial relationship. No direction overrides in the controller.

**Architecture:** Remove all `startDir` overrides from `HandleBeltDragging`. Unify direction derivation for all three modes. End direction is computed from the cross vector (perpendicular offset) relative to the locked start axis. Behind-start placement produces a U-turn with flipped end direction. Remove scroll wheel yaw offset (for now).

**Tech Stack:** C# / Unity / BeltRouteBuilder

---

## Context

`NetworkBuildController.HandleBeltDragging()` currently overrides `_beltStartDir` (the R-key direction) with `SnapToCardinal(toEnd)` for both default and straight/curved modes. This causes the placed support to face a different direction than the ghost preview showed. The end direction for default mode points backward toward start, creating twisted/looping belts.

### Current direction flow (BROKEN)

```
startDir = _beltStartDir                          // R-key direction
startDir = SnapToCardinal(endPos - startPos)       // OVERRIDE -- ignores R-key
endDir = SnapToCardinal(startPos - endPos)          // default: backward (WRONG)
endDir = SnapToCardinal(cross)                      // straight/curved: perpendicular (correct)
```

### Target direction flow (FIXED)

```
startDir = _beltStartDir                           // R-key direction, NEVER overridden
startAxis = SnapToCardinal(startDir)               // cardinal version for routing
delta = endPos - startPos (horizontal)
alongDist = dot(delta, startAxis)                  // how far forward/backward
cross = delta - alongDist * startAxis              // perpendicular offset

if behind (alongDist < 0):
    endDir = -startAxis                            // U-turn: end faces opposite
elif no cross offset:
    endDir = startAxis                             // straight: end faces same as start
else:
    endDir = SnapToCardinal(cross)                 // L/Z turn: end faces the offset direction
```

This logic is IDENTICAL for all three modes. The mode only affects routing (how the path is built), not the direction derivation.

---

## Files to Modify

| File | Change |
|------|--------|
| `Scripts/Player/NetworkBuildController.cs` | Rewrite direction logic in HandleBeltDragging, remove scroll wheel |
| `Scripts/Automation/BeltRouteBuilder.cs` | Add `DeriveEndDirection` static helper |

---

## Task 1: Add DeriveEndDirection to BeltRouteBuilder

Static helper that encapsulates the end direction logic. Lives in BeltRouteBuilder so it's testable and shared.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Automation/BeltRouteBuilder.cs`

**Step 1: Add the method after `SnapToCardinal`**

```csharp
/// <summary>
/// Derive end direction from start direction and endpoint positions.
/// Forward: end faces same as start. Offset: end faces the cross direction.
/// Behind: end faces opposite (U-turn). Used by all routing modes.
/// </summary>
public static Vector3 DeriveEndDirection(Vector3 startPos, Vector3 startDir,
    Vector3 endPos)
{
    var startAxis = SnapToCardinal(startDir);
    var delta = new Vector3(endPos.x - startPos.x, 0, endPos.z - startPos.z);
    float alongDist = Vector3.Dot(delta, startAxis);
    var cross = delta - alongDist * startAxis;
    float crossDist = cross.magnitude;

    if (alongDist < -0.1f)
    {
        // Behind start: U-turn, end faces opposite direction
        return -startAxis;
    }

    if (crossDist < 0.1f)
    {
        // Aligned: end faces same direction as start
        return startAxis;
    }

    // Offset: end faces the cross (perpendicular) direction
    return SnapToCardinal(cross);
}
```

**Step 2: Commit**

---

## Task 2: Rewrite HandleBeltDragging direction logic

Remove the per-mode direction overrides. All modes use the same flow: R-key startDir locked, endDir from `DeriveEndDirection`. Remove scroll wheel yaw offset entirely.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

### Step 1: Remove scroll wheel block

Delete lines 1177-1183 (the `_beltEndYawOffset` scroll wheel block). Also remove the `_beltEndYawOffset = 0f` resets at lines 1384 and 1395 (they become no-ops but clean them up).

### Step 2: Rewrite the direction setup

Replace the ENTIRE per-mode direction logic. The current code has two branches (default vs straight/curved) that each compute directions differently. Replace with one unified block.

**Replace lines 1187-1232 (the startDir/default block) AND lines 1233-1312 (the straight/curved block) with:**

```csharp
        if (TryResolveBeltEndpoint(ray, false, out var endPos, out var endDir, out var endFromPort))
        {
            var startDir = _beltStartDir;  // R-key direction, never overridden
            bool isValid;
            var endGroundPos = endPos;

            // Grid snap free endpoints
            if (!endFromPort)
            {
                endPos.x = Mathf.Round(endPos.x);
                endPos.z = Mathf.Round(endPos.z);
            }

            endGroundPos = endPos;
            if (!endFromPort)
                endPos.y += GridManager.Instance.SupportAnchorHeight;

            // Derive end direction from spatial relationship (all modes)
            if (!endFromPort)
                endDir = BeltRouteBuilder.DeriveEndDirection(_beltStartPos, startDir, endPos);

            // Validation
            var validation = BeltPlacementValidator.Validate(
                _beltStartPos, startDir, endPos, endDir);
            isValid = validation.IsValid || validation.Error == BeltValidationError.TurnTooSharp;

            // Additional straight/curved validation for turn geometry
            if (isValid && _beltRoutingMode != BeltRoutingMode.Default && !endFromPort)
            {
                var axis = BeltRouteBuilder.SnapToCardinal(startDir);
                var delta = new Vector3(endPos.x - _beltStartPos.x, 0, endPos.z - _beltStartPos.z);
                float alongDist = Mathf.Abs(Vector3.Dot(delta, axis));
                var cross = delta - Vector3.Dot(delta, axis) * axis;
                float crossDistVal = cross.magnitude;

                if (crossDistVal < 0.1f)
                {
                    isValid = alongDist >= BeltRouteBuilder.MinSegLength * 2;
                }
                else
                {
                    if (_beltRoutingMode == BeltRoutingMode.Curved)
                    {
                        isValid = alongDist >= BeltRouteBuilder.MinSegLength * 2
                               && crossDistVal >= BeltRouteBuilder.MinSegLength * 2;
                    }
                    else
                    {
                        float minLeg = Mathf.Min(BeltRouteBuilder.TurnRadius,
                                           alongDist - BeltRouteBuilder.MinSegLength)
                                     + BeltRouteBuilder.MinSegLength;
                        isValid = alongDist >= minLeg && crossDistVal >= minLeg;
                    }
                }

                float heightDiff = Mathf.Abs(endPos.y - _beltStartPos.y);
                if (isValid && heightDiff > 0.01f)
                {
                    float idealRamp = 1.5f * heightDiff / Mathf.Tan(BeltRouteBuilder.MaxRampAngle * Mathf.Deg2Rad);
                    float actualRadius = crossDistVal >= 0.1f
                        ? Mathf.Min(BeltRouteBuilder.TurnRadius,
                            alongDist - BeltRouteBuilder.MinSegLength,
                            crossDistVal - BeltRouteBuilder.MinSegLength)
                        : 0f;
                    if (_beltRoutingMode == BeltRoutingMode.Curved)
                        actualRadius = 0f;
                    float availableForRamp = alongDist - actualRadius;
                    float minAlongForElevation = idealRamp;
                    if (crossDistVal >= 0.1f && _beltRoutingMode != BeltRoutingMode.Curved)
                        minAlongForElevation += BeltRouteBuilder.MinPostRampLength;
                    if (availableForRamp < minAlongForElevation)
                        isValid = false;
                }
            }
```

Key changes:
- `startDir = _beltStartDir` -- R-key direction, NEVER overridden
- `endDir = BeltRouteBuilder.DeriveEndDirection(...)` -- unified for all modes
- No scroll wheel offset
- No separate default vs straight/curved direction branches
- Straight/curved still has the turn geometry validation (min leg checks etc.)
- Default mode skips the turn geometry validation (it uses free-form Hermite)

### Step 3: Clean up _beltEndYawOffset

Remove the `_beltEndYawOffset` field declaration (around line 57). Remove the `_beltEndYawOffset = 0f` resets in the placement confirm and right-click cancel blocks.

### Step 4: Commit

---

## Task 3: Fix BuildFreeform for backward endDir

When the end is behind start, `DeriveEndDirection` returns `-startAxis`. `BuildFreeform` uses this as the Hermite tangent T1. Since T1 = endDir * tangentMag, and endDir points backward (toward start), the curve will naturally make a U-loop. This should work correctly with no changes -- the Hermite math handles it. But verify during playtest.

No code changes needed. Just verify:
1. Place belt with end behind start -- should U-loop smoothly
2. Place belt with end forward + offset -- should L-curve smoothly
3. Place belt with end directly forward -- should go straight
4. Start support direction should ALWAYS match the ghost preview
5. Straight mode should still work with L/Z/U turns
6. Curved mode should allow short-distance turns

---

## Verification Checklist

- [ ] Ghost support preview direction matches placed support direction
- [ ] Default mode: forward placement = smooth curve
- [ ] Default mode: offset placement = L-curve
- [ ] Default mode: behind placement = U-loop (or invalid if broken)
- [ ] Straight mode: L/Z/U turns work as before
- [ ] Curved mode: short-distance turns allowed
- [ ] No scroll wheel interaction (removed)
- [ ] Port-to-port connections still work (directions come from ports, not overridden)
