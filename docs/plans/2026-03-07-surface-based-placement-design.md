# Surface-Based Placement System

Date: 2026-03-07
Status: Approved
Branch: kevin/multiplayer-step1

## Problem

The current placement system uses integer levels (`int level`) tied to a fixed vertical grid (`level * LevelHeight`). This breaks with variable-height prefabs -- a 0.5m foundation floats above terrain because the system assumes 1m increments. Adding half-walls, shallow ramps, or any non-uniform vertical building requires fighting the level abstraction.

## Decision

Remove the integer level system entirely. Placement Y is determined by the surface the player is aiming at (terrain or top of placed structure) plus prefab half-height. No fixed vertical grid.

## Core Data Model

### FactoryGrid

- Remove `LevelHeight` and `MaxLevels` constants
- Cell dictionary key stays `Vector3Int(cellX, cellZ, yBucket)`
- `yBucket = Mathf.RoundToInt(surfaceY / BucketSize)`, `BucketSize = 0.5f`
- `CellToWorld(cell, float surfaceY)` replaces `CellToWorld(cell, int level)`
- `CanPlace(origin, size, float surfaceY)` replaces `CanPlace(origin, size, int level)`
- `CellSize`, `Width`, `Height`, `FoundationSize`, `WallWidth`, `WallHeight` unchanged

### PlacementInfo

- `float SurfaceY` replaces `int Level` -- the Y of the surface this object sits on
- `float ObjectHeight` added -- full height from renderer bounds, so stacked objects know where their bottom goes

### WallData / RampData

- `float SurfaceY` replaces `int Level`

## Placement Flow

1. Player aims at surface (terrain or placed structure)
2. Raycast returns hit
3. If hit is a `PlacementInfo` object: `surfaceY = info.SurfaceY + info.ObjectHeight`
4. If hit is terrain: `surfaceY = hit.point.y`
5. Effective Y = `surfaceY + nudgeOffset`
6. Ghost position = `cellWorldXZ` + effective Y + `prefabHalfHeight`
7. Server spawn uses identical formula

Foundations float/embed to stay flat on the XZ grid. They do not conform to terrain slope.

## Unified Placement API

### Three ServerRpcs (down from seven)

```
CmdPlace(Vector2Int cell, float surfaceY, int rotation, int variant, BuildingCategory category)
```
Used for: foundations, machines, storage

```
CmdPlaceDirectional(Vector2Int cell, float surfaceY, Vector2Int direction, int variant, BuildingCategory category)
```
Used for: walls, ramps

```
CmdPlaceBelt(Vector2Int fromCell, Vector2Int toCell, float surfaceY, int variant)
```
Used for: belts (kept separate -- belt pathing will expand significantly)

### BuildingCategory enum

`Foundation, Wall, Ramp, Machine, Storage, Belt`

### Two delete RPCs

```
CmdDelete(Vector2Int cell, float surfaceY)
CmdDeleteDirectional(Vector2Int cell, float surfaceY, Vector2Int direction)
```

## GridManager Simplification

### Prefab storage

Six separate arrays become a dictionary:
```csharp
private Dictionary<BuildingCategory, GameObject[]> _prefabArrays;
```
Adding a new category = add enum value + Resources folder. No new fields.

### Placed building tracking

Three collections (`_foundationObjects`, `_wallRecords`, `_placedBuildings`) merge into one:
```csharp
private Dictionary<Vector3Int, PlacedRecord> _placedRecords;
```
`PlacedRecord` stores: GameObject, category, direction, surfaceY, component refs for auto-wiring.

### Universal placement methods

Five methods collapse to two:
```csharp
public Vector3 GetPlacementPos(Vector2Int cell, float surfaceY, GameObject prefab)
public void GetDirectionalPlacement(Vector2Int cell, float surfaceY, Vector2Int direction,
    GameObject prefab, out Vector3 position, out Quaternion rotation)
```

## NetworkBuildController Changes

### State

- `_lastLevel` (int) replaced by `_surfaceY` (float) from raycast
- `_nudgeOffset` (float) added -- PgUp/PgDn adjustment
- `_levelOverrideFrames` removed
- `_lastHitFoundation` removed

### PgUp/PgDn nudge

- PgUp: `_nudgeOffset += shift ? 0.5f : 1f`
- PgDn: `_nudgeOffset -= shift ? 0.5f : 1f`
- Resets to 0 on: tool switch (1-6), toggle build mode, toggle delete mode
- Does NOT reset on Tab (variant cycling)

### OnGUI

- Shows `Surface: {_surfaceY + _nudgeOffset:F1}m` instead of level
- Shows `Nudge: +{_nudgeOffset:F1}m` when non-zero

## Component Self-Initialization

`NetworkMachine` and `NetworkStorage` self-initialize in `OnStartServer()` instead of GridManager manually creating definitions. GridManager just instantiates, positions, and spawns. `ServerInit()` stays available for bootstrapper/playtest injection.

## What Stays the Same

- XZ grid (CellSize, Width, Height, FoundationSize, WallWidth)
- Auto-wiring (cardinal neighbor check, inserter creation)
- Factory simulation (NetworkFactorySimulation, Machine, StorageContainer, BeltSegment)
- Variant system (Resources.LoadAll, Tab cycling, GetPrefabHalfHeight)
- Ghost preview system (EnsurePrefabGhost, CreateGhostFromPrefab, pools)
- Belt item visualization
- Zoop (uses surfaceY at start instead of level)
- Delete mode (raycast + despawn, PlacementInfo identifies target)
