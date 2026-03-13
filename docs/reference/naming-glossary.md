# Naming glossary

Single source of truth for all naming conventions in Slopworks. Both Claude instances (Joe's and Kevin's) must check this before creating or renaming anything.

---

## Class suffixes

Every C# class uses one of these suffixes based on its role. No exceptions.

| Suffix | Role | Example |
|--------|------|---------|
| *(none)* | Pure C# simulation (D-004). No Unity deps. EditMode-testable. | `Machine`, `Inserter`, `StorageContainer`, `BeltSegment` |
| `Behaviour` | MonoBehaviour wrapper. Owns a pure C# instance, syncs via FishNet. | `MachineBehaviour`, `TurretBehaviour`, `StorageBehaviour` |
| `Controller` | State manager or input handler. May be pure C# or MonoBehaviour. | `TurretController`, `PlayerController`, `ReticleController` |
| `DefinitionSO` | ScriptableObject. Read-only static data. Never mutated at runtime. | `MachineDefinitionSO`, `WeaponDefinitionSO`, `FaunaDefinitionSO` |
| `Manager` | Singleton/persistent coordinator. MonoBehaviour. | `PowerNetworkManager`, `BuildingManager` |
| `Service` | Stateless or scene-scoped utility. | `BuildingPlacementService`, `StructuralPlacementService` |
| `Adapter` | Bridge for port I/O. Implements `IItemSource` or `IItemDestination`. | `MachineInputAdapter`, `BeltOutputAdapter` |
| `Registry` | Central lookup table. Loaded once at startup. | `ItemRegistry`, `RecipeRegistry`, `PortNodeRegistry` |
| `PlaytestSetup` | Debug scene bootstrapper. Drop on empty GO, hit Play. | `StructuralPlaytestSetup`, `ReticleTestSetup` |
| `Tests` | NUnit test fixture. EditMode only for simulation, PlayMode for network. | `MachineTests`, `BuildingIntegrationTests` |
| `UI` | UI component (non-reticle). Builds on Canvas. | `InventoryUI`, `StorageUI`, `BuildTooltipUI` |
| `HUD` | Persistent screen-space HUD overlay. | `PlayerHUD`, `VisorHUD` |

### Interfaces

All use `I` prefix: `IItemSource`, `IItemDestination`, `IPlaceableDefinition`, `IInteractable`, `IPowerNode`, `ISceneService`.

### Data structs

No suffix. Marked `[Serializable]` when serialized: `ItemInstance`, `ItemSlot`, `BeltItem`, `MachinePort`, `DamageData`, `ReticleStyle`, `RecipeIngredient`, `BuildingData`.

---

## ID string conventions

All runtime IDs are **lowercase snake_case strings**. Never PascalCase, never camelCase.

| ID type | Field name | Examples |
|---------|-----------|----------|
| Item | `itemId` | `"iron_ore"`, `"copper_ingot"`, `"power_cell"` |
| Recipe | `recipeId` | `"smelt_iron"`, `"craft_copper"` |
| Machine | `machineId` | `"smelter_basic"`, `"furnace_industrial"` |
| Definition | `definitionId` | matches the above; used in `ItemInstance` for lookup |
| Instance | `instanceId` | GUID string for non-stackable items; empty for stackable |

---

## Asset naming

### ScriptableObject assets

| Folder | Type | Naming | Examples |
|--------|------|--------|---------|
| `ScriptableObjects/Items/` | `ItemDefinitionSO` | PascalCase descriptive | (created via menu) |
| `ScriptableObjects/Recipes/` | `RecipeSO` | PascalCase descriptive | (created via menu) |
| `ScriptableObjects/Events/` | `GameEventSO` | PascalCase verb phrase | `EnemyDied.asset` |
| `ScriptableObjects/Buildings/` | Various `*DefinitionSO` | PascalCase type name | (created via menu) |
| `ScriptableObjects/Weapons/` | `WeaponDefinitionSO` | `Test_[Name]` for dev | `Test_Rifle.asset` |
| `ScriptableObjects/Fauna/` | `FaunaDefinitionSO` | `Test_[Name]` for dev | `Test_Grunt.asset` |

### Prefabs

| Category | Pattern | Examples |
|----------|---------|---------|
| Network player | `NetworkPlayer` | `NetworkPlayer.prefab` |
| Player character | `PlayerCharacter` | `PlayerCharacter.prefab` |
| Enemies | `Enemy_[Type]` | `Enemy_Basic.prefab` |
| Tower floors | `TowerFloor_[FloorType]` | `TowerFloor_Lobby.prefab`, `TowerFloor_Boss.prefab` |
| Buildings (Resources) | `UPPER_CASE` with size | `SLAB_1m`, `CONSTRUCTOR`, `STORAGE CONTAINER` |
| World items | `WorldItem` | `WorldItem.prefab` (generic, ID-driven) |

### Scenes

| Pattern | When | Examples |
|---------|------|---------|
| `[Location]_[System].unity` | Production scenes | `HomeBase_Grid.unity`, `Core_Network.unity` |
| `[SystemName]Playtest.unity` | Test/debug scenes | `PortNodePlaytest.unity`, `MasterPlaytest.unity` |
| `[Name]Playtest.unity` | Personal test scenes | `JoePlaytest.unity`, `KevinPlaytest.unity` |

### Assembly definitions

| asmdef | GUID | Scope |
|--------|------|-------|
| `Slopworks.Runtime` | `d2454b8aaa04a894a9326d71bddaaaa1` | All Scripts/ |
| `Slopworks.Input` | — | Input System generated code |
| `Slopworks.Tests.EditMode` | — | EditMode tests, references Runtime |
| `Slopworks.Tests.PlayMode` | — | PlayMode tests |

---

## Enum catalog

### Automation / factory

```
MachineStatus      : Idle, Working, Blocked
PortType           : Input, Output
PortOwnerType      : Machine, Storage, Belt, Turret
BeltRoutingMode    : Curved, Straight
BuildMode          : None, Single, Batch, Belt, SnapAttach, Delete
ItemCategory       : None, RawMaterial, Component, Tool, Building, Consumable, Ammo
```

### Building / placement

```
BuildingCategory   : Foundation, Wall, Ramp, Machine, Storage, Belt, Support
SnapPointType      : FoundationEdge, FoundationCorner, WallEnd, RampBase, RampTop
```

### Combat

```
DamageType         : Kinetic, Explosive, Fire, Toxic
TargetingMode      : Closest, LowestHealth, HighestThreat
```

### World / overworld

```
OverworldBiomeType : Grassland, Forest, Wasteland, Swamp, Ruins, OvergrownRuins
OverworldNodeType  : HomeBase, Building, Tower
MEPSystemType      : Electrical, Plumbing, Mechanical, HVAC
```

### Settlement

```
SettlementBuildingType : Farmstead, Workshop, Watchtower, Depot, Market, Barracks, RiverDepot, Greenhouse
SettlementCapability   : None, CraftingStation, WeaponWorkbench, Merchant, FastTravel, EarlyWarning, WaterPurification, ResearchBench, RecruitNPCs, DefensePatrols
```

**Enum value style:** PascalCase always. No underscores, no prefixes.

---

## UI element naming

HUD elements use a prefix-based taxonomy. All lowercase with underscores.

| Prefix | Scope | Examples |
|--------|-------|---------|
| `hud_bar_*` | Progress/status bars | `hud_bar_raid`, `hud_bar_health`, `hud_bar_shield` |
| `hud_badge_*` | Info pills at top | `hud_badge_left`, `hud_badge_right` |
| `hud_strip_*` | Thin horizontal strips | `hud_strip_compass` |
| `hud_frame_*` | Bordered info panels | `hud_frame_status` |
| `hud_indicator_*` | Single-value readouts | `hud_indicator_ammo` |
| `hud_tray_*` | Slot containers | `hud_tray_hotbar` |
| `hud_selector_*` | Active selection markers | `hud_selector_tool` |
| `hud_grid_*` | Multi-slot grids | `hud_grid_loadout` |
| `hud_button_*` | Clickable buttons | `hud_button_gear` |
| `hud_reticle` | Center crosshair | (single element, no sub-prefix) |
| `hud_chat_*` | Chat system | `hud_chat_window` |
| `modal_*` | Full-screen overlays | `modal_slate`, `modal_manifest`, `modal_dossier` (backlogged) |
| `widget_*` | Shared building blocks | `widget_keycap`, `widget_action_key` |

### Reticle styles

| Style | Characters | Color | When |
|-------|-----------|-------|------|
| `Gameplay` | `[ + ]` | Cyan (0, 0.86, 1) | Default FPS |
| `BuildDefault` | `[ + ]` | Orange (1, 0.67, 0.19) | Standard placement |
| `BuildStraight` | `\| + \|` | Orange | Straight belt/wall |
| `BuildZoop` | `[ Z ]` | Orange (0.8 alpha) | Drag-to-extend |
| `BuildCurved` | `( * )` | Orange | Curved belt/path |
| `BuildVertical` | `[ ^ ]` | Orange | Vertical placement |

### Build mode keycaps

Row above hotbar: `R` (Rotate), `X` (Delete), `G` (Grid), `Z` (Zoop), `Tab` (Variant).
Action stack (right margin): `LMB` (Place, green), `RMB` (Remove, red), `B` (Exit, orange).
`B` is the sole build mode toggle. `Esc` is reserved for system/pause menu.

---

## Tower system

### Contract IDs

Pattern: `tower_contract_[NN]` where NN is zero-padded.

| ID | Name | Building | Tier | Type |
|----|------|----------|------|------|
| `tower_contract_01` | 30 Rock assessment | tower_30_rock | 1 | Spine |
| `tower_contract_02` | MetLife sweep | tower_metlife | 1 | Spine (gate) |
| `tower_contract_03` | Woolworth extraction | tower_woolworth | 2 | Spine |
| `tower_contract_04` | One World recovery | tower_one_world_trade | 2 | Spine (gate) |
| `tower_contract_05` | Night shift | tower_30_rock | 1 | Branch |
| `tower_contract_06` | Loading docks | tower_metlife | 1 | Branch |
| `tower_contract_07` | Penthouse | tower_metlife | 1 | Branch |
| `tower_contract_08` | Sublevel B | tower_30_rock | 2 | Branch |
| `tower_contract_09` | Empire State: Lobby | tower_empire_state | 2 | Branch (future) |
| `tower_contract_10` | Clock tower | tower_woolworth | 2 | Branch |
| `tower_contract_11` | Server farm | tower_metlife | 2 | Branch |
| `tower_contract_12` | Chrysler: Observatory | tower_chrysler | 2 | Branch (future) |
| `tower_contract_13` | The crypt | tower_woolworth | 3 | Branch |
| `tower_contract_14` | Sublevel zero | tower_one_world_trade | 3 | Branch |
| `tower_contract_15` | Empire State: Antenna | tower_empire_state | 3 | Branch (future) |

### Building IDs

| ID | Real building | Style | Status |
|----|--------------|-------|--------|
| `tower_30_rock` | 30 Rockefeller Plaza | Art deco, 1933 | Active BIM |
| `tower_metlife` | MetLife Building | Modernist, 1963 | Active BIM |
| `tower_woolworth` | Woolworth Building | Neo-gothic, 1913 | Active BIM |
| `tower_one_world_trade` | One World Trade Center | Modern, 2014 | Active BIM |
| `tower_empire_state` | Empire State Building | Art deco, 1931 | Planned |
| `tower_chrysler` | Chrysler Building | Art deco, 1930 | Planned |

### Fauna types

| ID | Role |
|----|------|
| `grunt` | Basic melee |
| `pack_runner` | Coordinated flankers |
| `spitter` | Ranged, exploits elevation |
| `spore_crawler` | Nests, spore clouds |
| `stalker` | Thrives in darkness |
| `biomech_hybrid` | Advanced EM/organic |
| `hive_queen` | Boss, spore colony leader |

### Environmental hazards

`darkness`, `em_interference`, `spore_clouds`, `wind_exposure`, `structural_instability`

### Tower loot

| ID | Category | Rarity |
|----|----------|--------|
| `power_cell` | Power | Uncommon |
| `capacitor_bank` | Power | Rare |
| `neural_processor` | Power | Epic |
| `signal_decoder` | Access | Rare |
| `key_fragment` | Access | Epic |
| `boss_blueprint` | Crafting | Legendary (kept on death) |
| `reinforced_plating` | Crafting | Rare |
| `tower_map_fragment` | Information | Uncommon |

---

## Physics layers

See `docs/reference/physics-layers.md` for full details. Quick reference:

```
 8  Player          12  Terrain         16  VolumeTrigger
 9  Fauna           13  Structures      17  NavMeshAgent
10  Projectile      14  Interactable    18  Decal
11  BIM_Static      15  GridPlane       19  FogOfWar
```

---

## Placement method naming

When adding a new building type, these methods must exist:

| Method | Location | Pattern |
|--------|----------|---------|
| `Place[Type]()` | `BuildingPlacementService` | Server-side spawn + wire ports |
| `Get[Type]WorldPos()` | `GridManager` | Universal Y-offset from prefab |
| `_[type]Prefab` field | `GridManager` | Serialized, exposed via getter |
| `FactoryPrefabSetup` | `FactoryPrefabSetup.cs` | Wires prefab reference at boot |

---

## File/folder conventions

- `Assets/_Slopworks/` — all game code (underscore sorts to top)
- `Scripts/[Domain]/` — Automation, Building, Combat, Core, Debug, Editor, Input, Network, Player, Settlement, UI, World
- `ScriptableObjects/[Type]/` — Buildings, Events, Fauna, Items, Recipes, Weapons
- `Prefabs/[Category]/` — Belt, Buildings, Combat, Enemies, FX, Items, Machines, Player, Tower, UI
- `Resources/Prefabs/Buildings/[SubType]/` — Belts, Foundations, Machines, Ramps, Storage, Supports, Walls
- `Tests/Editor/EditMode/` — simulation tests
- `Tests/PlayMode/` — network/integration tests
