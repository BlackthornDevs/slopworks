# Factory automation reference

Slopworks has two automation layers: building-level (in-building sorting/routing) and hub-level (Home Base Satisfactory-style factory). This doc covers both.

---

## Core simulation model

Everything is a machine with input buffers, output buffers, and a production strategy:

```
MineStrategy:     no inputs → outputs resource on timer
RecipeStrategy:   inputs A + B → output C (after N ticks)
ForwardStrategy:  input X → output X (conveyor behavior)
SortStrategy:     input X → routes to output A or B based on filter
```

Run on a **fixed simulation tick** (16–20ms, decoupled from render frame rate). Server only. Clients display state synced from server; they don't simulate.

### Machine state machine

```
IDLE    → waiting for inputs (buffers empty or recipe not met)
WORKING → inputs consumed, timer counting down
BLOCKED → recipe complete, output buffer full, can't eject
```

### Tick structure

```
Pre-tick:   reset temp output buffers
Tick:       each machine tries grab() then produce()
Post-tick:  flush temp → final output buffers
```

The temp buffer prevents outputs from being consumed within the same tick as production. Flush happens after all machines tick — gives consistent frame boundaries.

---

## Belt system

### Data structure (distance-offset model)

Do NOT store absolute item positions. Store the gap between consecutive items:

```csharp
struct BeltItem {
    public ItemType Type;
    public ushort DistanceToNext;  // integer subdivisions, not float
}

// On a belt segment NetworkBehaviour (one NetworkObject per segment)
[SyncObject]
private readonly SyncList<BeltItem> _items = new();
ushort terminalGap;  // space from last item to end of segment
```

Each tick: only `terminalGap` changes when the belt flows freely. Everything else is static. This gives O(1) belt updates in steady-state flow.

**Why integer distances:** Float math is not deterministic across platforms. Using integer subdivisions (e.g., 100 subdivisions per tile) keeps simulation identical on all clients, which matters if you ever want deterministic lockstep.

### Performance at scale

- **Merge consecutive belts into one segment.** A straight run of 20 tiles is one entity with one item list, not 20 entities.
- **Split segments only at inserter attachment points** (inserters need a fixed position to read from).
- **GPU instancing for rendering.** Items on belts are not GameObjects. Use `Graphics.DrawMeshInstancedProcedural` with a `NativeArray<float3>` of positions computed from belt segment data each frame.
- **FishNet Network LOD.** Belt segments far from any player get update rate throttled to near-zero automatically.

Belt performance target: 60fps with 1M active items using DOTS + Burst. MonoBehaviour approach is fine up to ~500 active items before profiling shows issues.

---

## Building placement (grid system)

```csharp
public class FactoryGrid {
    public const float CellSize = 1.0f;  // 1 Unity unit = 1 cell

    private BuildingData[,] _cells;

    public Vector2Int WorldToCell(Vector3 world) =>
        new Vector2Int(
            Mathf.FloorToInt(world.x / CellSize),
            Mathf.FloorToInt(world.z / CellSize));

    public bool CanPlace(Vector2Int origin, Vector2Int size) {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                if (_cells[origin.x + x, origin.y + y] != null) return false;
        return true;
    }
}
```

**Key rules:**
- `CellSize = 1.0f` — matches Unity physics layer, makes math trivial
- Multi-cell buildings mark all occupied cells with the same building reference
- On removal, clear all occupied cells
- 4 rotations (0°/90°/180°/270°): swap width/height for odd-sized footprints
- Separate grid layers: terrain, belts, buildings — query the right layer per placement type

---

## Machine I/O port system

Each building defines ports relative to its origin cell and rotation:

```csharp
public struct MachinePort {
    public Vector2Int LocalOffset;   // e.g. (1, 0) = right edge
    public Vector2Int Direction;     // which way an incoming belt must face
    public PortType Type;            // Input or Output
    public Entity ConnectedBelt;
}
```

When a belt is placed adjacent to a machine, placement system checks nearby port definitions and connects if direction matches.

**Slopworks recommendation: use inserters** (Factorio pattern) rather than direct machine-belt connections (Satisfactory pattern). Inserters decouple belt throughput from machine processing rate, making the factory layout more flexible.

---

## Power system

Group machines into networks via BFS flood-fill on the connection graph. Per network:

```csharp
struct PowerNetwork {
    float TotalGeneration;
    float TotalConsumption;
    float Satisfaction;  // min(gen, consumption) / consumption — 0.0 to 1.0
}
```

- Recalculate only when network topology changes (building placed or removed) — not every tick
- Machines check `network.Satisfaction` to determine operating speed (at < 1.0, machines slow proportionally)
- Generators are critical infrastructure: losing power = defenses go dark. This is the design intent per the game doc.

---

## Multiplayer factory sync

The server runs the authoritative simulation. Clients display.

**What clients receive:**
- Machine status changes (IDLE/WORKING/BLOCKED) via SyncVar
- Belt item lists via SyncList (delta updates only)
- Power network satisfaction via SyncVar on network entity

**What clients do not receive:**
- Raw physics, item positions per-frame, simulation internals

**Machine config commands:**

```csharp
[ServerRpc]
public void SetRecipeServerRpc(string recipeId) {
    if (!IsServerInitialized) return;
    // validate recipe exists and player has access
    _activeRecipeId.Value = recipeId;
    _status.Value = MachineStatus.IDLE;
}
```

Players interact with machines by sending commands. The server processes them on the next tick.

---

## Slopworks-specific: two factory layers

### Building level (in-building automation)

Each reclaimed building has basic automation unlocked as you restore MEP systems:
- Power plant: auto-generates electricity on restore
- Warehouse: sorting lines route salvage types to output belts
- Hospital: auto-produces medical supplies from inputs

Model these as simple RecipeStrategy machines with fixed configurations set by the restoration event — players don't manually configure them.

### Hub level (Home Base)

Full Satisfactory-style factory with player-configured machines and conveyor layouts. This is where the complex belt graph, power grid, and recipe chains live.

**Scene separation:** Home Base and Reclaimed Buildings are separate scenes. The factory simulation only runs when the Home Base scene is loaded. Supply lines from connected buildings are abstracted to a network map resource flow (not physical belts between scenes).

---

## Open source reference

**Best implementation reference:** `https://github.com/theor/Automation`
Full Factorio belt system in Unity DOTS. Achieves 1M items at 60fps.
The distance-offset data structure used above is taken directly from this project.

**Simpler MonoBehaviour reference (for understanding basics):** `https://github.com/BrandonMCoffey/Factorio-Systems-Demo`
