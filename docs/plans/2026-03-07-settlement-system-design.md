# Settlement management system design

**Date:** 2026-03-07
**Author:** Joe (junior developer)
**Status:** Design approved, pending implementation plan

---

## Overview

A higher-level overworld settlement layer where players discover ruined structures across the HomeBaseTerrain, gradually repair them by delivering materials, and claim them to unlock production, capabilities, and territory control. Separate from Kevin's interior building exploration system (Phase 6).

## Core loop

```
Discover ruin on terrain
  -> Inspect (E key, see repair requirements)
  -> Deliver materials across multiple visits
  -> Visual pieces added to structure each stage (additive)
  -> Building claimed when fully repaired
  -> Assign NPC workers for production bonuses
  -> Upgrade through post-claim tiers
  -> Build roads to connect buildings into supply network
  -> Settlement production feeds the factory automation system
```

## Architecture: zone graph

Buildings are nodes in a graph. Player-built roads are edges. Supply lines require physical road connections between buildings. Territory is projected from each claimed building.

```
SettlementGraph
  nodes: Dictionary<string, SettlementBuilding>
  edges: List<(string a, string b)>  // roads
  AreConnected(a, b)                 // path exists via BFS
  HasFactoryConnection(id)           // can reach factory yard hub
  Tick(dt)                           // production for all claimed buildings
```

## Data model

### SettlementBuildingDefinitionSO (read-only config)

```
buildingId            : string
displayName           : string
buildingType          : enum (Farmstead, Workshop, Watchtower, Depot, Market, Barracks)
maxRepairLevel        : int (e.g., 3 stages)
repairStages[]        : RepairStageDefinition
  - requiredItems[]   : (itemId, amount) pairs
  - addedPiecePrefabs : list of prefab refs (roof panel, wall patch, door, etc.)
  - unlockedCapability: enum or null
territoryRadius       : float
connectionRange       : float (max road length to another building)
workerSlots           : int (max assignable NPCs)
workerBonusPerSlot    : float (production speed multiplier per worker)
upgradeTiers[]        : UpgradeTierDefinition
  - tierName          : string
  - requiredItems[]   : (itemId, amount) pairs
  - addedPiecePrefabs : list
  - productionOverride: ProductionDefinition or null
  - territoryBonus    : float
  - workerSlotsBonus  : int
  - unlockedCapability: enum or null
```

### ProductionDefinition

```
producedItemId        : string
producedAmount        : int
productionInterval    : float (seconds)
requiresSupplyLine    : bool (needs road connection to factory)
```

### RepairStageDefinition

```
requiredItems[]       : (itemId, amount) pairs
addedPiecePrefabs     : list of prefab refs
unlockedCapability    : enum or null
```

### UpgradeTierDefinition

```
tierName              : string
requiredItems[]       : (itemId, amount) pairs
addedPiecePrefabs     : list
productionOverride    : ProductionDefinition or null
territoryBonus        : float
workerSlotsBonus      : int
unlockedCapability    : enum or null
```

### SettlementBuilding (plain C# simulation object)

```
BuildingId            : string
RepairLevel           : int (0 = ruin, maxRepairLevel = claimed)
UpgradeTier           : int (0 = base claimed, 1+ = upgraded)
IsClaimed             : bool (derived: RepairLevel >= maxRepairLevel)
ProductionTimer       : float (server-only)
Position              : Vector3 (world position on terrain)
ConnectedBuildingIds  : List<string>
AssignedWorkerIds     : List<string>
WorkerCount           : int (derived)
EffectiveProductionRate : float (base rate * (1 + workerBonusPerSlot * workerCount))
```

## Building lifecycle

```
Ruin (repairLevel 0)
  -> Repair stage 1 (deliver materials, add roof)
  -> Repair stage 2 (deliver materials, add walls)
  -> Repair stage 3 = Claimed (building functions, assign workers)
    -> Upgrade tier 1 (deliver materials, enhanced production)
    -> Upgrade tier 2 (deliver materials, new capability unlock)
```

Visual approach: additive pieces. Start with a ruined shell mesh. Each repair stage spawns additional prefab pieces (roof panels, wall patches, doors, furniture) as children of the building GameObject. Pieces accumulate — they don't replace.

## Player interaction flow

### Discovering and repairing

1. Player walks across terrain, spots ruined structure
2. Trigger zone around building shows prompt: "press E to inspect [name]"
3. Inspection UI panel shows: name, type, current stage, materials needed (green/red by inventory), "deliver materials" button, preview of next stage additions
4. Player delivers materials. When stage requirements met: server advances repairLevel, new prefab pieces spawn, all clients see update via sync

### Managing a claimed building

Same IInteractable trigger, prompt changes to "press E to manage [name]". Panel shows:

- **Production tab** — what it produces, current rate, output buffer
- **Workers tab** — assign/unassign NPCs, slot bonuses shown
- **Upgrade tab** — next tier, materials needed, what it unlocks
- **Status** — supply line connected? territory radius on minimap

### Road building

1. Player opens settlement map (dedicated key)
2. Overhead view shows terrain with building icons
3. "Build road" mode: click source, click target
4. If within connectionRange: road path calculated, material cost shown
5. On confirmation: road appears on terrain, supply line enabled
6. Road visuals: dirt path, gravel, or paved depending on quality

### Territory and enemies

- Claimed buildings project territoryRadius sphere
- Inside territory: enemy spawn rate reduced/eliminated, NPCs work safely
- Unclaimed/disconnected buildings: normal enemy density
- Watchtowers specifically boost radius and add early-warning alerts

## Economy integration

Settlements feed the factory. Settlement production outputs use existing ItemDefinitionSO IDs from the shared item registry. Example flow:

```
Farmstead produces "raw_food" (every 30s)
  -> Road connects farmstead to factory yard
  -> Items appear in factory yard storage
  -> Belt delivers to processing machine
  -> Machine produces "preserved_food"
```

Settlement repair costs also come from factory output, creating a bidirectional dependency:
- Factory produces repair materials (metal plates, concrete, wiring)
- Settlements produce raw resources (food, fiber, chemicals)

## FishNet sync strategy

| Data | Mechanism | Why |
|------|-----------|-----|
| Repair level per building | SyncDictionary<string, int> | Late joiners get full state |
| Upgrade tier per building | Same SyncDictionary (composite value) | Single sync point |
| Road connections | SyncList<RoadConnection> | Late joiners see all roads |
| Worker assignments | SyncDictionary<string, workerData> | Both players see assignments |
| Production output | Server ticks, items deposited into shared inventory | No sync needed for timer |
| Territory radius | Derived from SO + repair level | Client computes locally |

Server-authoritative: all repair, upgrade, worker assignment, and production operations are ServerRpc requests validated by the server. Clients display state from sync callbacks.

## File structure

### Plain C# simulation (D-004 pattern)

```
Scripts/Settlement/
  SettlementBuilding.cs
  SettlementGraph.cs
  SettlementRoadDefinition.cs
  RepairStageDefinition.cs
  UpgradeTierDefinition.cs
  SettlementEvents.cs
```

### ScriptableObjects

```
Scripts/Settlement/
  SettlementBuildingDefinitionSO.cs
  SettlementRoadDefinitionSO.cs

ScriptableObjects/Settlement/
  Farmstead.asset
  Workshop.asset
  Watchtower.asset
  Depot.asset
  Market.asset
  Barracks.asset
  Greenhouse.asset   (upgrade from farmstead)
  FactoryYard.asset  (starting hub, pre-repaired)
```

### MonoBehaviour wrappers (thin)

```
Scripts/Settlement/
  SettlementManagerBehaviour.cs    — singleton, owns graph, FishNet sync, server-only tick
  SettlementBuildingBehaviour.cs   — per-building, IInteractable, spawns additive pieces
  SettlementRoadBehaviour.cs       — per-road, visual line/mesh, NavMeshModifier
```

### UI

```
Scripts/UI/
  SettlementInspectUI.cs           — repair/manage panel (runtime-created)
  SettlementMapUI.cs               — overhead map for road building
  SettlementProductionUI.cs        — production tab
  SettlementWorkerUI.cs            — worker assignment tab
```

### Tests

```
Tests/Editor/EditMode/
  SettlementBuildingTests.cs       — repair, production, workers, upgrades
  SettlementGraphTests.cs          — connectivity, supply lines, tick, territory
```

## Ownership boundaries

| System | Owner | Notes |
|--------|-------|-------|
| Scripts/Settlement/ (new) | Joe | Entire settlement layer |
| Scripts/World/BuildingState.cs | Kevin | Interior exploration, MEP restore |
| Scripts/Core/IInteractable.cs | Shared | Already exists, both systems use it |
| ScriptableObjects/Settlement/ | Joe | Settlement building configs |
| ScriptableObjects/Items/ | Shared | Settlement uses existing item IDs |

No overlap with Kevin's code. Only shared touchpoints are IInteractable (stable interface) and item IDs (additive, new items go through master PR).

## Building roster (first pass)

| # | Building | Type | Production | Unlock | Territory | Workers |
|---|----------|------|------------|--------|-----------|---------|
| 1 | Factory yard | Industrial | Parts, scrap metal | Crafting station (advanced) | 30m | 3 |
| 2 | Farmstead | Agricultural | Raw food, fiber | -- | 20m | 3 |
| 3 | Watchtower | Military | -- | Early warning, fast travel | 60m | 1 |
| 4 | Workshop | Industrial | Repair kits, ammo | Weapon/armor workbench | 25m | 2 |
| 5 | River depot | Logistics | -- | Supply range +50%, water purification | 20m | 2 |
| 6 | Market | Commerce | Trade tokens | Merchant NPC, buy/sell | 15m | 2 |
| 7 | Barracks | Military | -- | Recruit NPCs, defense patrols | 40m | 4 |
| 8 | Greenhouse | Agricultural | Medicine, chemicals | Research bench (tier 2) | 15m | 2 |

### Terrain placement (approximate world positions)

- Center (0, 0): Factory yard (existing hub)
- East (200, 50): Workshop
- North (50, 300): Farmstead
- Northwest (-100, 350): River depot
- South (0, -200): Watchtower
- West (-250, 0): Market
- Northeast (150, 250): Barracks
- North-far (0, 450): Greenhouse

Roads connect in a roughly radial pattern from the factory yard hub.

## Testing strategy

All simulation logic is pure C# with no MonoBehaviour dependency. Fully testable in EditMode:

- **SettlementBuildingTests**: repair advancement, material validation, production rate with workers, upgrade tier progression, capability unlocks
- **SettlementGraphTests**: add/remove nodes, road connectivity (BFS), supply line reachability to factory, tick distributes production correctly, territory radius computation, disconnection handling

Integration testing via playtest scene following phase completion standard.

## Open questions

- How do NPCs get recruited? (Tower runs, rescue events, quest rewards — details TBD)
- What happens to a building's workers if the building is somehow destroyed or disconnected?
- Should roads have maintenance costs or can they degrade over time?
- Exact item IDs for repair materials and production outputs (depends on item registry expansion)
