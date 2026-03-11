# Belt System Design

Approved 2026-03-09. Satisfactory-style curved conveyor belts with Unity Splines, FishNet multiplayer, server-authoritative simulation.

References: `docs/research/belts/` (14 research files), `docs/research/belts/design-decisions.md` (D-BLT-001 through D-BLT-006).

---

## 1. BeltPort Component

New `MonoBehaviour` placed as child GameObjects on machine, storage, and belt prefabs.

**Fields:**
- `PortDirection` enum: `Input` or `Output`
- `SlotIndex` (int): which slot this port maps to for recipe routing
- `SlotLabel` (string): editor-only label for clarity (e.g., "Ore In", "Ingot Out")

**Placement on prefabs:**
- Machine/storage prefabs get BeltPort children positioned where belt connections should attach
- Belt prefab gets one Input (slot 0) and one Output (slot 0) at the segment endpoints
- Editor tooling: menu item creates child GameObject with BeltPort, user toggles Input/Output and positions manually

**Port routing rules:**
- **Input ports are fungible.** Any item can enter any input port. Machine pulls from all inputs equally. No per-port item filtering.
- **Output ports are recipe-routed.** Recipe defines which output slot index each product goes to. Belt connected to that slot receives the item.
- Splitters/mergers fit as regular machines with multiple BeltPorts -- no special system needed.

---

## 2. Placement State Machine

**States:** `Idle` -> `PickingStart` -> `Dragging` -> `Confirming`

**PickingStart:**
- Player enters belt build mode, raycast resolves first click
- Valid targets: BeltPort (on machine/storage/belt), BeltSnapAnchor (on support), open ground
- If open ground, auto-place a support at the click position
- Record `startPos` and `startTangent` from the target's transform

**Dragging:**
- LineRenderer preview with 30 sample points, updated each frame
- Hermite spline computed from `{startPos, startTangent, currentMousePos, derivedTangent}`
- Color indicates validity (green = valid, red = invalid)
- No clipping preview since LineRenderer doesn't represent final mesh width

**Confirming:**
- Second click resolves end target (same valid targets as start)
- Final validation: length 0.5-56m, slope <=45 degrees, turn radius >=2m
- If valid, send `CmdPlaceBelt` ServerRpc

**Build modes (Default first, others later):**
- **Default:** auto-route with turn-then-rise decomposition
- **Straight** and **Curve** modes deferred to future iteration

---

## 3. Server-Side Belt Placement

**CmdPlaceBelt flow:**
1. Client sends `CmdPlaceBelt(startPortId, endPortId, startPos, startTangent, endPos, endTangent)`
2. Server validates: length 0.5-56m, slope <=45 degrees, turn radius >=2m, no duplicate connection
3. Server spawns Belt NetworkObject with `NetworkBeltSegment`
4. `NetworkBeltSegment` stores spline data in SyncVars (4x Vector3 = 48 bytes)
5. Server computes arc length, creates `BeltSegment.FromArcLength(arcLength, tier)` for simulation
6. Server wires ports via `ConnectionResolver`: source output port -> belt input port, belt output port -> destination input port
7. Clients reconstruct spline deterministically from synced endpoint data, bake mesh via SplineExtrude

**CmdPlaceSupport flow:**
1. Client sends `CmdPlaceSupport(position, rotation)`
2. Server validates position
3. Server spawns Support NetworkObject with `BeltSnapAnchor` child
4. Support sits idle until a belt placement uses its snap anchor

**Auto-support during belt placement:**
When a belt endpoint lands on open ground (not a port or snap anchor), the server auto-spawns a support at that position. The support is a visual pole with a snap anchor -- not a network node.

---

## 4. Spline Construction and Mesh Generation

**Spline from two clicks:**
Cubic Hermite spline defined by `{P0, T0, P1, T1}` -- start position, start tangent, end position, end tangent.

- **Tangent derivation:** From BeltPort/BeltSnapAnchor, tangent = port's `transform.forward` (outward for Output, inward for Input). From open ground, tangent = direction from start to end projected onto horizontal plane.
- **Tangent magnitude:** `|T| = distance(P0, P1) / 3`, clamped to `[0.5, 18.67]`.
- **Turn-then-rise decomposition:** If belt has both horizontal turn AND vertical change, an internal knot is inserted at the turn-complete point. Single spline with two spans, single BeltSegment. Deletion removes the whole thing (D-BLT-001).

**Hermite-to-Bezier conversion (for Unity Splines):**
- `BezierP0 = P0`, `BezierP1 = P0 + T0/3`, `BezierP2 = P1 - T1/3`, `BezierP3 = P1`

**Mesh generation (bake-and-destroy pattern):**
1. Create temporary GameObject with SplineContainer + SplineExtrude (Road profile)
2. SplineExtrude generates belt mesh (~0.3ms)
3. Copy baked Mesh to MeshFilter on permanent belt GameObject
4. Destroy temporary SplineExtrude components
5. Mark belt renderer as static for batching

**Arc-length for simulation:**
`BeltSegment.FromArcLength(arcLength, tier)` -- arc length via `SplineUtility.CalculateLength()` or Gauss-Legendre quadrature. 100 subdivisions per meter of arc length. Simulation is geometry-agnostic (D-BLT-004).

**Belt item visual positioning:**
`GetItemPositions()` returns normalized `[0,1]` values. Visual layer evaluates spline at those parametric positions via cached arc-length LUT.

**Development fallback:**
If Unity Splines hits performance or quality issues during testing, swap to hand-rolled: Hermite evaluation + RMF cross-section sweep + custom arc-length LUT. Simulation and networking unchanged. This is a development-time decision, not a runtime switch.

---

## 5. Networking and Sync

**Belt NetworkObject SyncVars:**

| SyncVar | Type | Bytes | Purpose |
|---------|------|-------|---------|
| `_startPos` | Vector3 | 12 | Spline P0 |
| `_startTangent` | Vector3 | 12 | Spline T0 |
| `_endPos` | Vector3 | 12 | Spline P1 |
| `_endTangent` | Vector3 | 12 | Spline T1 |
| `_tier` | byte | 1 | Belt speed tier |
| `_startPortId` | NetworkObjectId + byte | ~5 | Source port reference |
| `_endPortId` | NetworkObjectId + byte | ~5 | Destination port reference |

Total: ~59 bytes per belt. Sent once at spawn.

**Belt items:** `SyncList<BeltItem>` on `NetworkBeltSegment`. Server ticks simulation, clients read synced list for visual positioning.

**Client-side reconstruction:** On spawn, client reads SyncVars, builds identical Hermite spline, converts to Bezier, runs SplineExtrude bake-and-destroy. Deterministic from identical inputs.

**Support NetworkObject:** Transform sync only (position + rotation) plus BeltSnapAnchor data. Minimal overhead.

**Late joiners:** All state in SyncVars and SyncLists. Full belt network state received automatically on connect. No RPCs for persistent state.

**Simulation authority:** Server-only tick. `if (!IsServerInitialized) return;` guard on `NetworkBeltSegment.Tick()`.

---

## 6. Simulation Integration

**Zero changes to BeltSegment/BeltNetwork core.** Existing gap-based item transport unchanged. Only difference is `_totalLength` sourced from arc length instead of Manhattan distance.

**Tick flow (server-only):**
1. `NetworkBeltSegment.Tick(deltaTime)` calls `_segment.Tick(deltaTime)`
2. `BeltSegment.Tick` advances items by gap-closing
3. At segment boundaries, items transfer via `BeltNetwork`
4. `BeltNetwork` resolves connections through port wiring (ConnectionResolver)

**Port wiring:**
- Belt output port -> destination input port (machine, storage, or another belt)
- Source output port -> belt input port
- `ConnectionResolver` gets new `PortOwnerType.Belt` case in `CreateSource` and `CreateDestination`

**Item transfer at ports:**
- **Into machine:** item enters whichever input port the belt is wired to. All inputs fungible.
- **Out of machine:** recipe routes output items to specific output port slot index. Belt on that port receives the item.
- **Belt-to-belt:** belt A output port wires directly to belt B input port. Support facilitated placement but is not in the connection chain.

**Speed tiers:**
`BeltTier` enum: Mk1 = 60 items/min, Mk2 = 120, Mk3 = 270. Tier stored as SyncVar, used by `BeltSegment` for tick rate.

---

## 7. Supports, Deletion, and Reconnection

**Supports are placement guides, not network nodes.**

`BeltSnapAnchor` component on support prefab:
- Position + direction from `transform.forward`
- Raycast target for belt placement
- No `PortOwnerType`, no ConnectionResolver case, no simulation involvement

**Support behavior:**
- Can be placed independently -- no belt required. Use to pre-plan belt routes.
- Exist only at belt start/end points (never intermediate along a belt path)
- Belt placement raycast hits snap anchor, reads position and direction for spline endpoint/tangent
- Once belt is placed, no ongoing relationship between belt and support

**Delete belt:**
1. Server disconnects belt ports from source and destination
2. Server destroys belt NetworkObject
3. Supports at either end remain as free-standing snap anchors

**Delete support:**
1. Server destroys support NetworkObject
2. Any belt placed using this support is completely unaffected -- belt keeps geometry and port connections

**Belt-to-belt at a support:**
Two belts placed to the same support connect port-to-port: belt A output -> belt B input. The support was the shared placement reference. Deleting the support leaves both belts connected and functional.

**Auto-support on open ground:**
When a belt endpoint lands on open ground (not a port or snap anchor), a support is auto-placed. Same rules: visual pole with snap anchor, not a network node.
