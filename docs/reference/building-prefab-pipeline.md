# Building Prefab Pipeline

Living reference for how building prefabs are constructed, configured, and placed. Update this document whenever the pipeline changes or a new building category is added.

Last updated: 2026-03-09

---

## Quick reference

| Category | Folder | Layer | Snap count | Snap filter mode | Required components |
|----------|--------|-------|------------|------------------|---------------------|
| Foundation | `Resources/Prefabs/Buildings/Foundations/` | 13 (Structures) | 14 | CENTER / EDGE (scroll) | NetworkObject |
| Wall | `Resources/Prefabs/Buildings/Walls/` | 13 (Structures) | 14 | CENTER / EDGE (scroll) | NetworkObject |
| Ramp | `Resources/Prefabs/Buildings/Ramps/` | 13 (Structures) | 6 | CENTER / EDGE (scroll) | NetworkObject |
| Machine | `Resources/Prefabs/Buildings/Machines/` | 14 (Interactable) | 5 | FOUNDATION / PEER (scroll) | NetworkObject, NetworkMachine |
| Storage | `Resources/Prefabs/Buildings/Storage/` | 14 (Interactable) | 6 | FOUNDATION / PEER (scroll) | NetworkObject, NetworkStorage |
| Belt | `Resources/Prefabs/Buildings/Belts/` | 14 (Interactable) | TBD | TBD | NetworkObject, NetworkBeltSegment |

---

## 1. FBX model requirements

### Origin and orientation

- **Pivot at geometric center.** Unity places the object at the pivot point. Snap points are computed relative to renderer bounds center, so a centered pivot keeps snap offsets symmetric.
- **Forward = +Z (north).** Ramp slopes go uphill toward +Z. Wall faces toward +Z. This matches Unity convention and snap normal directions.
- **Up = +Y.**
- **Scale = 1 unit = 1 meter.** Export at real-world scale. Do not rely on Unity import scale to fix sizing.

### Mesh structure

- Single mesh is simplest. Multi-mesh FBX (e.g., extrusions) works -- the snap system combines bounds from all `Renderer` children via `Encapsulate()`.
- Each mesh child gets its own `MeshRenderer` and `MeshCollider` automatically on import.
- Keep collider geometry simple. Complex concave meshes should use convex decomposition or a separate low-poly collision mesh.

### Materials

- Assign materials in the 3D app. Unity imports them as sub-assets.
- URP Lit shader is the target. If importing from apps that export Standard shader, convert manually or at runtime.

### Naming

- Use UPPER_CASE with underscores for the FBX filename: `SLAB_1m.fbx`, `WALL_0.5m.fbx`, `RAMP 4x1.fbx`, `CONSTRUCTOR.fbx`, `STORAGE CONTAINER.fbx`.
- The filename becomes the prefab name in Unity and shows in build mode HUD.

---

## 2. Prefab setup

### Folder placement

Prefabs MUST live under `Assets/_Slopworks/Resources/Prefabs/Buildings/<Category>/`. GridManager loads them at runtime via `Resources.LoadAll<GameObject>("Prefabs/Buildings/<Category>")`. A prefab in the wrong folder will not appear in the build menu.

```
Resources/Prefabs/Buildings/
    Foundations/    -- SLAB_1m, SLAB_2m, SLAB_4m
    Walls/          -- WALL_0.5m
    Ramps/          -- RAMP 4x1, RAMP 4x2
    Machines/       -- CONSTRUCTOR
    Storage/        -- STORAGE CONTAINER
    Belts/          -- Belt
```

### Required components

Every building prefab needs a `NetworkObject` component (FishNet). FishNet auto-collects prefabs with NetworkObject into DefaultPrefabObjects at compile time -- no manual registration needed.

| Category | Additional components |
|----------|---------------------|
| Foundation | None |
| Wall | None |
| Ramp | None |
| Machine | `NetworkMachine` |
| Storage | `NetworkStorage` |
| Belt | `NetworkBeltSegment` |

### Layer assignment

**Do NOT set layers on the prefab asset.** `GridManager.SetBuildingLayer()` assigns layers at spawn time based on category:

| Category | Root + mesh children layer | Snap point children layer |
|----------|---------------------------|--------------------------|
| Foundation | 13 (Structures) | 20 (SnapPoints) -- preserved |
| Wall | 13 (Structures) | 20 (SnapPoints) -- preserved |
| Ramp | 13 (Structures) | 20 (SnapPoints) -- preserved |
| Machine | 14 (Interactable) | 20 (SnapPoints) -- preserved |
| Storage | 14 (Interactable) | 20 (SnapPoints) -- preserved |
| Belt | 14 (Interactable) | 20 (SnapPoints) -- preserved |

`SetBuildingLayer` iterates all children but **skips any child already on layer 20 (SnapPoints)**. This prevents baked snap point colliders from being overwritten. If this skip is missing, snap point raycasts silently fail because the colliders end up on the wrong layer.

### Variant support

All prefabs in a category folder are loaded as variants. **Tab key** cycles through variants for the current tool. `GridManager.GetPrefab(category, variantIndex)` returns the selected one. Variants are sorted by Unity's `Resources.LoadAll` order (alphabetical). Each tool type tracks its own variant index independently.

---

## 3. Snap point baking

### Editor tool

**Menu: Tools > Slopworks > Add Snap Points to Prefabs**

`SnapPointPrefabSetup.cs` scans all prefabs under `Resources/Prefabs/Buildings/`, detects category from subfolder name, and generates snap point child GameObjects with `BuildingSnapPoint` components.

**The tool skips prefabs that already have snap points.** To re-bake: delete all `SnapPoint_*` children from the prefab first, then re-run.

### Category detection

| Subfolder contains | Detected category |
|--------------------|-------------------|
| `/Ramps/` | Ramp |
| `/Walls/` | Wall |
| `/Machines/` | Machine |
| `/Storage/` | Storage |
| (default) | Foundation |

### Bounds calculation

The tool combines world-space bounds from ALL `Renderer` children on the prefab via `Encapsulate()`. This handles multi-mesh FBX models (e.g., Storage Container with 3 extrusion meshes). Single-renderer bounds would only capture the first child, producing wrong extents.

### Runtime fallback

`BuildingSnapPoint.GenerateFromBounds(go, category)` is called after server spawn (`GridManager.CmdPlace`). It skips if snap points already exist (baked). This is a safety net -- baked snap points from the editor tool are preferred because they can be manually adjusted.

---

## 4. Snap point layouts

### Naming convention

All snap points follow `SnapPoint_<Cardinal>_<Tier>` or `SnapPoint_<Special>`:

- Cardinals: `North`, `South`, `East`, `West`, `Center`
- Tiers: `Top`, `Mid`, `Bot`
- Special: `HighEdge`, `LowEdge`

Examples: `SnapPoint_North_Top`, `SnapPoint_Center_Bot`, `SnapPoint_HighEdge`

### Each snap point child has

- `BuildingSnapPoint` component with `_normalOverride` (local-space outward direction) and `SurfaceSize`
- `SphereCollider` (radius 0.5, isTrigger = true) for raycast detection
- Layer 20 (SnapPoints)

### Foundation / Wall (14 snap points)

Structural buildings get the full 3-tier layout on all 4 cardinal faces plus top and bottom center.

```
Cardinal faces (4 directions x 3 heights = 12):
    North_Top, North_Mid, North_Bot    normal: (0, 0, 1)
    South_Top, South_Mid, South_Bot    normal: (0, 0, -1)
    East_Top,  East_Mid,  East_Bot     normal: (1, 0, 0)
    West_Top,  West_Mid,  West_Bot     normal: (-1, 0, 0)

Face centers (2):
    Center_Top    normal: (0, 1, 0)    -- stack on top
    Center_Bot    normal: (0, -1, 0)   -- hang from underside
```

**Height positions:**
- `_Top` = bounds center + extents.y (top edge of face)
- `_Mid` = bounds center (middle of face)
- `_Bot` = bounds center - extents.y (bottom edge of face)

### Ramp (6 snap points)

Ramps have reduced snaps. No South cardinal (the slope surface), no _Mid or _Top tiers, no Center_Top. Plus slope-aware edge snaps.

```
Cardinal faces (3 x Bot only = 3):
    North_Bot    normal: (0, 0, 1)     -- front face, bottom
    East_Bot     normal: (1, 0, 0)     -- side face, bottom
    West_Bot     normal: (-1, 0, 0)    -- side face, bottom

Slope edges (2):
    HighEdge     normal: (0, 0, 1)     -- top of slope (high Y, +Z)
    LowEdge      normal: (0, 0, -1)    -- bottom of slope (low Y, -Z)

Face center (1):
    Center_Bot   normal: (0, -1, 0)    -- underside
```

**HighEdge/LowEdge Y positions** are derived from the renderer's actual bounds max/min Y, not the bounding box center. This ensures ramp-to-ramp and ramp-to-foundation connections match height correctly.

### Machine (5 snap points)

Machines have bottom-only cardinal snaps for side-by-side alignment. No stacking (no Center_Top).

```
Cardinal faces (4 x Bot only = 4):
    North_Bot    normal: (0, 0, 1)
    South_Bot    normal: (0, 0, -1)
    East_Bot     normal: (1, 0, 0)
    West_Bot     normal: (-1, 0, 0)

Face center (1):
    Center_Bot   normal: (0, -1, 0)    -- sits on foundation/grid
```

### Storage (6 snap points)

Storage is identical to Machine but adds Center_Top for vertical stacking.

```
Cardinal faces (4 x Bot only = 4):
    North_Bot    normal: (0, 0, 1)
    South_Bot    normal: (0, 0, -1)
    East_Bot     normal: (1, 0, 0)
    West_Bot     normal: (-1, 0, 0)

Face centers (2):
    Center_Top   normal: (0, 1, 0)     -- stack another storage on top
    Center_Bot   normal: (0, -1, 0)    -- sits on foundation/grid
```

### Belt (TBD)

Belt snap points are not yet implemented. Will be defined when belt placement is converted to snap-based system.

---

## 5. Snap filter modes

The scroll wheel toggles snap filters. Which filter is active depends on the current build tool.

### Structural tools (Foundation, Wall, Ramp)

| Mode | Scroll state | Target snaps allowed |
|------|-------------|---------------------|
| **CENTER** (default) | Initial | `_Mid` and `Center` snaps only |
| **EDGE** | Scrolled | Everything except `_Mid` and `Center` (i.e., `_Top`, `_Bot`, `HighEdge`, `LowEdge`) |

CENTER mode is less cluttered for typical side-by-side building. EDGE mode is needed for stacking (top), hanging walls from edges (bot), and ramp slope connections.

### Machine / Storage tools

| Mode | Scroll state | Target snaps allowed |
|------|-------------|---------------------|
| **FOUNDATION** (default) | Initial | Only `_Top` snaps on Foundation buildings |
| **PEER** | Scrolled | Any snap on other Machine or Storage buildings |

FOUNDATION mode places equipment on top of foundations only (not walls or ramps). PEER mode snaps equipment side-by-side or stacks storage.

### HUD display

The current filter mode always shows in the build HUD: `Filter: CENTER (scroll to swap)`, `Filter: EDGE`, `Filter: FOUNDATION`, `Filter: PEER`.

---

## 6. Snap-to-snap placement

### How it works

1. `Physics.RaycastAll` with `StructuralMask` (includes layers 12, 13, 15, 20) finds all hits along the crosshair ray.
2. Hits on layer 20 (SnapPoints) are filtered by `MatchesSnapFilter()` based on current tool and scroll mode.
3. Closest passing snap becomes `_activeSnapPoint`.
4. `GridManager.GetSnapPlacementPosition()` finds the ghost prefab's matching snap via `FindGhostAttachSnap()`.
5. Ghost position = `targetSnapWorldPos - ghostRotation * ghostSnapLocalPos`. No offset math -- snap point positions encode all geometry.

### FindGhostAttachSnap tier pairing

The ghost snap is chosen by opposite normal (dot product > 0.5 after rotation) and tier pairing:

| Target snap tier | Ghost snap tier | Context |
|-----------------|-----------------|---------|
| `_Top` | `_Bot` | Wall hangs down from foundation top edge |
| `_Bot` | `_Top` | Building sits on top of foundation bottom edge |
| `_Bot` | `_Bot` | Machine/Storage peer snap only (side-by-side) |
| `_Mid` | `_Mid` | Same-height center attachment |
| `HighEdge` | `LowEdge` | Ramp chains uphill |
| `LowEdge` | `HighEdge` | Ramp chains downhill |
| `Center_Top` | `Center_Bot` | Vertical stacking (storage on storage) |

**Machine/Storage on structural**: Always uses ghost's `Center_Bot` regardless of which structural snap is targeted. The machine sits on the surface.

**Bot-to-Bot pairing is ONLY for machine/storage peers.** For structural placement, `_Bot` always pairs with `_Top` (wall hangs down from foundation edge). This is enforced by checking both ghost and target categories.

### Grid placement fallback

When the raycast hits terrain (layer 12) or grid plane (layer 15) instead of a building, `GetGridPlacementPosition()` snaps to the nearest 1m grid intersection and offsets Y by `GetPrefabBaseOffset(prefab)`.

---

## 7. Zoop (batch placement)

Zoop places a chain of buildings in a line. First click sets the anchor, then drag to extend the chain. Each ghost snaps to the previous ghost's snap point -- no cell math or footprint calculations. `MaxZoopCount = 5`.

### How it works

1. First click uses normal snap/grid placement to set `_zoopStartPos` and `_zoopStartRot`.
2. After first click, `Plane.Raycast` at anchor Y height determines direction and distance (works even when looking at sky).
3. `UpdateZoopPreview` builds a chain: `FindZoopSnapPoint` on each ghost finds the snap whose rotated normal aligns with the zoop direction.
4. `PlaceZoopLine` reads positions directly from ghost transforms.

### Per-category zoop behavior

| Category | Zoop axis | Snap used for chaining |
|----------|-----------|----------------------|
| Foundation | Any cardinal direction | Cardinal _Mid or _Bot depending on filter mode |
| Wall | Length axis only (perpendicular to facing) | Prevents face-to-face stacking |
| Ramp | Uphill/downhill along slope | HighEdge/LowEdge pairing (0.1 bonus over cardinal _Bot) |
| Ramp | Side-by-side (East/West) | Cardinal _Bot snaps for wider ramp paths |
| Machine/Storage | Any cardinal direction | Cardinal _Bot snaps for side-by-side rows |

---

## 8. Placement flow (server side)

When the client clicks to place:

1. Client calls `CmdPlace(cell, surfaceY, rotation, variant, category, worldPos)` or `CmdPlaceDirectional(...)` for walls/ramps.
2. Server validates: checks prefab exists, cell not occupied.
3. `Instantiate(prefab, worldPos, rotation)`
4. `SetBuildingLayer(go, category)` -- assigns physics layer, preserves snap point layer.
5. `PlacementInfo` component added with category, cell, surfaceY, objectHeight.
6. `ServerManager.Spawn(go)` -- FishNet replicates to all clients.
7. `BuildingSnapPoint.GenerateFromBounds(go, category)` -- runtime fallback if no baked snaps.
8. Record stored in `_placedRecords` for occupancy checks and removal.
9. `AutoWire(record, cell)` -- connects adjacent machines/belts (automation wiring).

---

## 9. PlacementInfo component

Every spawned building gets a `PlacementInfo` component at spawn time. This is how the system identifies what a building is after placement.

| Field | Purpose |
|-------|---------|
| `Category` | `BuildingCategory` enum -- used by snap filter to determine valid targets |
| `Cell` | Grid cell position |
| `SurfaceY` | Y coordinate of the surface this building sits on |
| `ObjectHeight` | Full height of the building (2x half-height from renderer bounds) |
| `EdgeDirection` | (Walls/Ramps only) Which edge of the cell this directional building faces |

`MatchesSnapFilter` uses `GetComponentInParent<PlacementInfo>()` on the target snap to determine the target building's category. If PlacementInfo is missing, the building won't be recognized as a valid snap target for category-filtered modes.

---

## 10. Physics layers reference

| Layer | Number | Used for |
|-------|--------|----------|
| Player | 8 | Player character |
| Fauna | 9 | Enemies |
| Projectile | 10 | Bullets, projectiles |
| BIM_Static | 11 | Imported BIM geometry |
| Terrain | 12 | Unity Terrain, ground |
| Structures | 13 | Foundations, walls, ramps |
| Interactable | 14 | Machines, storage (clickable) |
| GridPlane | 15 | Invisible grid for placement raycasts |
| VolumeTrigger | 16 | Trigger volumes |
| NavMeshAgent | 17 | AI navigation agents |
| Decal | 18 | Decal projectors |
| FogOfWar | 19 | Fog of war system |
| SnapPoints | 20 | Snap point sphere colliders |

### Key raycast masks

| Mask | Layers included | Used by |
|------|----------------|---------|
| `StructuralPlacementMask` | Terrain + BIM_Static + GridPlane + Structures | Build mode raycast (includes SnapPoints via RaycastAll QueryTriggerInteraction) |
| `PlacementMask` | Terrain + BIM_Static + GridPlane | Ground-only placement |
| `DeleteMask` | Structures + Interactable | Delete tool raycast (hits all building types, excludes snap triggers) |

---

## 11. Adding a new building category

Checklist for adding a new type (e.g., Turret, Conveyor):

1. **Add enum value** to `BuildingCategory` in `BuildingCategory.cs`
2. **Create prefab folder** at `Resources/Prefabs/Buildings/<NewType>/`
3. **Add folder mapping** in `GridManager.cs` (the `folders` dictionary in prefab loading)
4. **Define snap layout** in `BuildingSnapPoint.GenerateFromBounds()` -- add a branch for the new category
5. **Define snap layout** in `SnapPointPrefabSetup.GenerateSnapPoints()` -- mirror the runtime logic for editor baking
6. **Add category detection** in `SnapPointPrefabSetup.DetectCategory()` -- map subfolder name to category
7. **Add layer assignment** in `GridManager.SetBuildingLayer()` if the new type needs a specific layer (structural = 13, equipment = default)
8. **Add build tool** in `NetworkBuildController.BuildTool` enum
9. **Add tool-to-category mapping** in `NetworkBuildController.ToolToCategory()`
10. **Define snap filter behavior** in `NetworkBuildController.MatchesSnapFilter()` -- what can this type snap to?
11. **Add tier pairing rules** in `GridManager.FindGhostAttachSnap()` if the new type has special pairing (e.g., peer Bot-to-Bot)
12. **Add required components** to the prefab (NetworkObject + type-specific NetworkBehaviour)
13. **Run the editor tool** (Tools > Slopworks > Add Snap Points to Prefabs) to bake snap points
14. **Test**: place on grid, place on foundation, place next to same type, verify snap filter modes

---

## 12. Troubleshooting

### Snap points not being hit by raycast

- **Check layer**: Snap point children must be on layer 20 (SnapPoints). If `SetBuildingLayer` overwrites them, the skip condition for layer 20 is missing or broken.
- **Check collider**: Each snap point needs a `SphereCollider` with `isTrigger = true` and `radius = 0.5`.
- **Check mask**: The raycast must include layer 20 or use `QueryTriggerInteraction.Collide`.

### Ghost floating above / below target

- **Check tier pairing** in `FindGhostAttachSnap`. Bot-to-Bot is only for machine/storage peers. Structural uses Bot-to-Top (wall hangs down from edge).
- **Check snap point positions on prefab**: Open the prefab, select each SnapPoint_* child, verify local position matches expected bounds offset.
- **Check combined bounds**: Multi-mesh FBX must use `GetComponentsInChildren<Renderer>()` + `Encapsulate()`, not single renderer bounds.

### Prefab not appearing in build menu

- **Check folder**: Must be under `Resources/Prefabs/Buildings/<Category>/`. Not `Prefabs/Buildings/` (no Resources), not a subfolder deeper than one level.
- **Check console**: GridManager logs `grid: <Category> variants: <count>` at startup. If count is 0, the folder is wrong or empty.

### Snap points not generated by editor tool

- **Existing snaps**: Tool skips prefabs that already have `BuildingSnapPoint` on any child. Delete old `SnapPoint_*` children first.
- **No renderer**: Tool skips prefabs with no `Renderer` component. FBX import must include mesh renderers.
- **Wrong subfolder**: Category detected from path. If the subfolder name doesn't match (`/Machines/`, `/Storage/`, etc.), it defaults to Foundation.

### SetBuildingLayer overwriting snap points

- `SetBuildingLayer` must check `if (t.gameObject.layer == PhysicsLayers.SnapPoints) continue;` before setting layer. Without this, baked snap colliders get moved to layer 13 or 0, and snap raycasts stop finding them.

### Machine/Storage not snapping to each other

- Verify target building has `PlacementInfo` component with correct `Category`.
- Verify scroll wheel is in PEER mode (HUD shows "MACHINE/STORAGE" or "PEER").
- In FOUNDATION mode, machine/storage tools only target structural buildings.

---

## 13. Known limitations and future work

### Belt snap integration (not yet implemented)

Belt placement currently uses a 2-click system (start cell, end cell) and does not use snap points. Future: input/output port snap points on machines/storage for belt connection endpoints.

### I/O port snaps (deferred)

Machine input/output ports for belt auto-wiring are not yet snap points. When belts are converted to snap-based placement, machines will need additional `SnapPoint_Input1`, `SnapPoint_Output1` children at belt connection faces.

---

## Changelog

- **2026-03-09**: Initial version. Documents Foundation, Wall, Ramp, Machine, Storage snap layouts. Snap filter modes. Tier pairing rules. FBX requirements. Prefab setup checklist.
