# Existing Slopworks Belt Codebase

Research compiled 2026-03-09. Inventory of all belt-related code currently in the project.

## Architecture Summary

The simulation layer is mature and multiplayer-ready. The visual/placement layer needs significant work for curved belts.

## Simulation (Pure C# -- D-004 Pattern)

### BeltItem.cs
- Pure struct: `itemId` (string) + `distanceToNext` (ushort)
- 100 subdivisions per tile for deterministic integer math
- Distance-offset model: items stored as gaps, not absolute positions

### BeltSegment.cs (169 lines)
- Core simulation class, no MonoBehaviour
- Data: `List<BeltItem>` + `ushort terminalGap`
- Items ordered input-to-output
- O(1) tick: only terminalGap and first item's distance change
- Key methods: TryInsertAtStart(), TryExtractFromEnd(), Tick(speed), GetItemPositions()
- Invariant: total belt length = sum of all distanceToNext + terminalGap (constant)

### BeltNetwork.cs (114 lines)
- Manages belt-to-belt connections with item transfer in transit
- Data: `List<BeltConnection>` with source, destination, held item
- Handles backpressure: if destination rejects, item held and retried next tick
- Default insert spacing: 50 subdivisions

### BeltPlacer.cs (132 lines)
- 2-click placement state machine (click start, drag, release end)
- Currently constrains to straight lines only (same X or same Z)
- Tracks: _startCell, _endCell, _isPlacing
- Methods: StartPlacement(), UpdateDrag(), FinishPlacement(), Cancel()

### BeltInputAdapter.cs (35 lines)
- IItemDestination wrapper around BeltSegment.TryInsertAtStart()
- Checks first item's distanceToNext >= minSpacing

### BeltOutputAdapter.cs (37 lines)
- IItemSource wrapper around BeltSegment.TryExtractFromEnd()
- Peeks at last item via GetItems()[Count-1].itemId

### FactorySimulation.cs (162 lines)
- Tick order: power -> belt segments -> belt network -> inserters -> machines
- RegisterBelt() / UnregisterBelt() maintains belt list
- BeltSpeed: ushort, default 2, configurable

## MonoBehaviour Wrappers

### BeltSegmentBehaviour.cs (121 lines)
- Thin wrapper around BeltSegment
- Creates BeltSegment in Awake
- Visual rendering via object pool (List<GameObject>)
- Updates visuals each frame using GetItemPositions()
- Incomplete multiplayer integration

### BeltNetworkBehaviour.cs (18 lines)
- Thin wrapper around BeltNetwork
- Creates BeltNetwork in Awake, exposes via public Network property

## Multiplayer (FishNet)

### NetworkBeltSegment.cs (82 lines)
- NetworkBehaviour with FishNet
- Server-authoritative: _segment runs server only
- Synced via SyncList<BeltItem> _syncItems + SyncVar<ushort> _syncTerminalGap
- ServerInit() called by GridManager after placement
- ServerSyncState() called periodically to sync to clients
- GetItemWorldPositions() transforms normalized positions to world space

### BeltItemVisualizer.cs (71 lines)
- Client-side rendering from NetworkBeltSegment synced data
- Creates visual cubes from Graphics.CreatePrimitive
- Updates positions each LateUpdate
- Simple color: (0.9, 0.6, 0.2) orange
- Object pool grows dynamically

## Port & Connection System

### PortOwnerType.cs
- Enum: Machine, Storage, Belt, Turret

### PortNode.cs (72 lines)
- Spatial connection point on factory grid
- Properties: Cell, Direction, Type (Input/Output), OwnerType, Owner, SlotIndex, Level, Connection

### ConnectionResolver.cs (174 lines)
- Auto-wires compatible ports when buildings placed
- Belt-to-belt: uses BeltNetwork.Connect()
- Other combinations: adapter pair + Inserter
- Creates BeltNetworkConnection tracking object

## Placement & Grid Integration

### BuildingPlacementService.cs
- PlaceBelt(startCell, endCell, surfaceY) method
- Creates BeltSegment, registers with FactorySimulation
- Creates two PortNodes (input at start, output at end)
- Calls ConnectionResolver for auto-wiring

### GridManager.cs (CmdPlaceBelt method)
- ServerRpc: instantiates Belt prefab, scales between cells
- Creates NetworkBeltSegment: netBelt.ServerInit(segment, startPos, endPos)
- Registers with FactorySimulation
- Adds BeltItemVisualizer
- Records in _placedRecords for deletion
- Calls AutoWire()

## Prefab

### Belt.prefab
- NetworkObject + NetworkBeltSegment
- Snap points: 4 cardinal x 3 tiers (Bot/Mid/Top) + center variants
- SnapPoints use sphere colliders on SnapPoints layer (20)
- Normal overrides on each snap point for directional matching

## Tests

- BeltSegmentTests.cs -- covers insert/extract, tick, spacing, invariants
- BeltNetworkTests.cs -- connection, transfer, backpressure
- BeltPlacerTests.cs -- placement state machine
- ConnectionResolverTests.cs -- auto-wiring

## Status Summary

| Component | Status | Multiplayer | Notes |
|-----------|--------|-------------|-------|
| BeltItem struct | Complete | Yes | Pure data |
| BeltSegment | Complete | Yes | Server-only, pure C# |
| BeltNetwork | Complete | Yes | Server-only, pure C# |
| BeltSegmentBehaviour | Complete | Partial | Single-player visuals |
| BeltNetworkBehaviour | Complete | Yes | Server-only |
| NetworkBeltSegment | Complete | Yes | SyncList/SyncVar |
| BeltItemVisualizer | Complete | Yes | Client-side rendering |
| BeltInputAdapter | Complete | Yes | IItemDestination |
| BeltOutputAdapter | Complete | Yes | IItemSource |
| BeltPlacer | Complete | No | Straight lines only |
| PortNode + PortOwnerType | Complete | Yes | Belt enum value |
| ConnectionResolver | Complete | Yes | Belt cases present |
| BuildingPlacementService | Complete | Partial | Single-player only |
| GridManager.CmdPlaceBelt | Complete | Yes | Server RPC |
| Belt.prefab | Complete | Yes | Snap points present |

## What Needs to Change for Curved Belts

| System | Current | Needed |
|--------|---------|--------|
| BeltPlacer | Straight-line grid cells | Port-to-port with spline preview |
| GridManager.CmdPlaceBelt | Scale box between two points | Spawn prefab + generate mesh along spline |
| NetworkBeltSegment | Two world positions | Spline control points + pre-sampled polyline |
| BeltItemVisualizer | Lerp between two positions | Lookup in polyline by distance |
| Belt.prefab | Static mesh | Procedurally generated mesh |
| BeltSegment | Length = Manhattan distance | Length = arc length of spline |
| Network sync | Start/end cells | Endpoints + tangent directions |
