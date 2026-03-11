# Belt Design Decisions

Decisions made during research phase, 2026-03-09. These are settled and should be followed during implementation.

## D-BLT-001: Belt Segmentation is Click-to-Click

A belt segment is one continuous spline between two player clicks. The "turn then rise" decomposition (separating horizontal and vertical changes) is handled by internal knot placement on a SINGLE spline, NOT by splitting into multiple BeltSegment objects.

Rationale: deleting a belt should remove the whole belt between the two click points, not half of it.

## D-BLT-002: Supports and Belts Are Independent Network Objects

- Supports are placed during belt construction (auto-placed on empty ground) but exist as independent NetworkObjects
- Deleting a support does NOT delete connected belts (belt remains floating, still functional)
- Deleting a belt does NOT delete its supports (supports remain, reusable for new belts)
- Supports are purely for placement convenience and visual aesthetics, not structural

## D-BLT-003: Belt Items Are Non-Interactive by Default

- Belt item visuals have NO colliders (current implementation already does this)
- Walking over a belt does NOT trigger item pickup
- Players interact with belt items through intentional action only (TBD: press E on belt, or only via machine/storage interface)
- Belt items may need a dedicated physics layer (slot 21+) if raycasted interaction is added later

## D-BLT-004: Simulation Layer Is Geometry-Agnostic

- BeltSegment, BeltNetwork, BeltItem, adapters do NOT change for curved belts
- The only change is how `_totalLength` is computed (arc length instead of Manhattan distance)
- `GetItemPositions()` returns normalized [0,1] values; the visual layer maps these to spline positions
- 100 subdivisions per meter of arc length (same constant, different interpretation)

## D-BLT-005: Belt Item Interaction via Raycast-Belt-Resolve

- Intentional interaction (press E) raycasts the BELT collider, not individual items
- Nearest item resolved mathematically from hit point's parametric distance along the spline
- ServerRpc to extract item from belt at resolved index
- No per-item colliders, no dedicated physics layer needed
- Current no-collider visual approach is already correct for preventing accidental pickup

## D-BLT-006: Spline Sync via Deterministic Reconstruction

- Server syncs endpoint positions + tangent directions (4x Vector3 = 48 bytes) via SyncVar
- Clients reconstruct the identical spline deterministically from these values
- No need to sync the full SplineContainer or BezierKnot array
- BezierKnot custom serializer available as fallback if needed (~15 lines)
- FishNet's built-in Unity.Mathematics serializers auto-activate when com.unity.splines is installed
