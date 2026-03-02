# Slopworks Vertical Slice — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a playable vertical slice: one building to breach/clear/restore, a home base with Satisfactory-style factory building, one supply line connecting them, and a basic defense wave.

**Architecture:** Unity URP project with FishNet networking (Tugboat transport for local dev, FishySteamworks for Steam). MonoBehaviour + ScriptableObjects. Additive scene loading with Core scenes always resident. Server-authoritative factory simulation. Local JSON saves (Supabase deferred).

**Tech Stack:** Unity 6 + URP 17, FishNet, FishySteamworks, FastNoiseLite, ParrelSync, MCP Unity, GameCI

---

## Phase 0: Project Setup

### Task 0.1: Create Unity project

**Files:**
- Create: Unity project at repo root via Unity Hub

**Steps:**

1. Open Unity Hub. Create new project:
   - Template: **3D (URP)**
   - Unity version: **6000.x** (latest LTS)
   - Location: `C:\Users\KevinAmditis\source\repos\Slopworks`
   - Project name: leave as-is (project files go in repo root)

2. Verify URP is active: Edit > Project Settings > Graphics > Scriptable Render Pipeline Settings should show a URP Pipeline Asset.

3. Set project settings:
   - Edit > Project Settings > Player > Company Name: `BlackthornDevs`
   - Product Name: `Slopworks`
   - Color Space: **Linear** (not Gamma)
   - Api Compatibility Level: **.NET Standard 2.1**

4. Enable SRP Batcher: Select the URP Pipeline Asset in Project window > Rendering > SRP Batcher: On (should be default).

5. Configure Forward+ rendering: Select the URP Renderer Asset > Rendering Path: **Forward+**

6. Commit.

---

### Task 0.2: Set up folder structure

**Files:**
- Create directories under `Assets/`

**Steps:**

1. Create the project folder structure:

```
Assets/
  _Slopworks/
    Scripts/
      Automation/
      Combat/
      Network/
      Player/
      World/
      UI/
      Core/
    ScriptableObjects/
      Items/
      Recipes/
      Events/
      Buildings/
    Prefabs/
      Player/
      Machines/
      Buildings/
      UI/
      FX/
    Materials/
    Shaders/
    Audio/
  Scenes/
    Core/
    HomeBase/
    Buildings/
    Overworld/
  Plugins/
  StreamingAssets/
```

2. Add empty `.gitkeep` files in each leaf folder so git tracks them.

3. Commit.

---

### Task 0.3: Configure Git for Unity

**Files:**
- Create: `.gitattributes` at repo root
- Modify: `.gitignore` (verify Unity-specific entries)

**Steps:**

1. Create `.gitattributes` with Unity YAML merge, text/binary classification, and Git LFS rules per `docs/reference/team-workflow.md`.

2. Initialize Git LFS:
   ```bash
   git lfs install
   git lfs track "*.png" "*.jpg" "*.psd" "*.fbx" "*.obj" "*.blend" "*.mp3" "*.wav" "*.ogg" "*.dll"
   ```

3. Verify `.gitignore` includes:
   - `/[Ll]ibrary/`, `/[Tt]emp/`, `/[Oo]bj/`, `/[Bb]uild/`, `/[Bb]uilds/`
   - `/[Ll]ogs/`, `/[Uu]ser[Ss]ettings/`
   - `*.log`
   - `**/LightmapData-*`, `**/Lightmap-*`
   - `.claude/settings.local.json`

4. Commit.

---

### Task 0.4: Install FishNet

**Files:**
- Modify: `Packages/manifest.json` or import via Asset Store

**Steps:**

1. Install FishNet via Unity Package Manager:
   - Window > Package Manager > Add package from git URL:
   - `https://github.com/FirstGearGames/FishNet.git`
   - Or download from Asset Store (free) if git URL doesn't resolve

2. Verify import: `Assets/FishNet/` folder should exist with Runtime, Editor, etc.

3. Tugboat transport is included with FishNet (for local dev without Steam).

4. Install ParrelSync for multiplayer testing:
   - Package Manager > Add from git URL: `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`

5. Commit.

---

### Task 0.5: Create Core scenes

**Files:**
- Create: `Assets/Scenes/Core/Core_Network.unity`
- Create: `Assets/Scenes/Core/Core_GameManager.unity`

**Steps:**

1. Create `Core_Network.unity`:
   - Add empty GameObject: `NetworkManager`
   - Add component: `FishNet.Managing.NetworkManager`
   - Add component: Tugboat transport (for local dev)
   - Set as default transport on NetworkManager
   - Save scene

2. Create `Core_GameManager.unity`:
   - Add empty GameObject: `GameManager`
   - (Script will be added later)
   - Save scene

3. Set `Core_Network` as the default scene in Build Settings (index 0).

4. Create a bootstrap script that additively loads `Core_GameManager` on startup:

```csharp
// Assets/_Slopworks/Scripts/Core/Bootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private string _gameManagerScene = "Core_GameManager";

    private void Awake()
    {
        SceneManager.LoadScene(_gameManagerScene, LoadSceneMode.Additive);
    }
}
```

5. Attach `Bootstrap` to a GameObject in `Core_Network` scene.

6. Commit.

---

### Task 0.6: Create ScriptableObject event bus

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/GameEventSO.cs`
- Create: `Assets/_Slopworks/Scripts/Core/GameEventListener.cs`
- Create event assets in `Assets/_Slopworks/ScriptableObjects/Events/`

**Steps:**

1. Create the event ScriptableObject:

```csharp
// Assets/_Slopworks/Scripts/Core/GameEventSO.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    private readonly List<GameEventListener> _listeners = new();

    public void Raise()
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
            _listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener)
    {
        if (!_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public void UnregisterListener(GameEventListener listener)
    {
        _listeners.Remove(listener);
    }
}
```

2. Create the listener component:

```csharp
// Assets/_Slopworks/Scripts/Core/GameEventListener.cs
using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEventSO _event;
    [SerializeField] private UnityEvent _response;

    private void OnEnable() => _event.RegisterListener(this);
    private void OnDisable() => _event.UnregisterListener(this);

    public void OnEventRaised() => _response.Invoke();
}
```

3. Create initial event assets (right-click > Create > Events > Game Event):
   - `SceneTransitionRequested.asset`
   - `BuildingClaimed.asset`
   - `WaveStarted.asset`
   - `WaveEnded.asset`

4. Commit.

---

## Phase 1: Base Building (Satisfactory-Style Factory)

### Task 1.1: Factory grid system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/FactoryGrid.cs`
- Create: `Assets/Scenes/HomeBase/HomeBase_Terrain.unity`
- Create: `Assets/Scenes/HomeBase/HomeBase_Grid.unity`

**Steps:**

1. Create `HomeBase_Terrain.unity`:
   - Add a large flat plane (100x100 units) as ground
   - Add a directional light
   - Add basic URP skybox
   - Save scene

2. Create `HomeBase_Grid.unity` (loaded additively with terrain):
   - Add empty GameObject: `FactoryGridManager`

3. Implement the grid system:

```csharp
// Assets/_Slopworks/Scripts/Automation/FactoryGrid.cs
using UnityEngine;

public class FactoryGrid : MonoBehaviour
{
    public const float CellSize = 1.0f;

    private BuildingData[,] _cells;
    private int _gridWidth = 200;
    private int _gridHeight = 200;

    private void Awake()
    {
        _cells = new BuildingData[_gridWidth, _gridHeight];
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CellSize) + _gridWidth / 2,
            Mathf.FloorToInt(worldPos.z / CellSize) + _gridHeight / 2);
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            (cell.x - _gridWidth / 2) * CellSize + CellSize / 2f,
            0f,
            (cell.y - _gridHeight / 2) * CellSize + CellSize / 2f);
    }

    public bool CanPlace(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int cx = origin.x + x;
                int cy = origin.y + y;
                if (cx < 0 || cx >= _gridWidth || cy < 0 || cy >= _gridHeight)
                    return false;
                if (_cells[cx, cy] != null)
                    return false;
            }
        }
        return true;
    }

    public void Place(Vector2Int origin, Vector2Int size, BuildingData data)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                _cells[origin.x + x, origin.y + y] = data;
    }

    public void Remove(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                _cells[origin.x + x, origin.y + y] = null;
    }

    public BuildingData GetAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= _gridWidth || cell.y < 0 || cell.y >= _gridHeight)
            return null;
        return _cells[cell.x, cell.y];
    }
}

public class BuildingData
{
    public string BuildingId;
    public Vector2Int Origin;
    public Vector2Int Size;
    public int Rotation; // 0, 90, 180, 270
    public GameObject Instance;
}
```

4. Attach `FactoryGrid` to the `FactoryGridManager` in `HomeBase_Grid.unity`.

5. Commit.

---

### Task 1.2: Foundation placement system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BuildModeController.cs`
- Create: `Assets/_Slopworks/Scripts/Automation/FoundationDefinitionSO.cs`
- Create: `Assets/_Slopworks/Materials/Foundation_Default.mat`
- Create: Foundation SO asset

**Steps:**

1. Create the foundation definition:

```csharp
// Assets/_Slopworks/Scripts/Automation/FoundationDefinitionSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Buildings/Foundation")]
public class FoundationDefinitionSO : ScriptableObject
{
    public string foundationId;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;
}
```

2. Create a simple foundation prefab:
   - Cube scaled to (1, 0.2, 1) — 1 unit wide, 0.2 units tall
   - Apply a concrete-gray URP/Lit material
   - Save as `Assets/_Slopworks/Prefabs/Machines/Foundation_1x1.prefab`

3. Create SO asset: `Assets/_Slopworks/ScriptableObjects/Buildings/Foundation_1x1.asset`
   - Set prefab reference and size

4. Implement build mode controller:

```csharp
// Assets/_Slopworks/Scripts/Automation/BuildModeController.cs
using UnityEngine;

public class BuildModeController : MonoBehaviour
{
    [SerializeField] private FactoryGrid _grid;
    [SerializeField] private FoundationDefinitionSO _selectedFoundation;
    [SerializeField] private Camera _buildCamera;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Material _validPreviewMat;
    [SerializeField] private Material _invalidPreviewMat;

    private GameObject _preview;
    private bool _buildModeActive;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
            ToggleBuildMode();

        if (!_buildModeActive) return;

        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
            TryPlace();

        if (Input.GetMouseButtonDown(1))
            ToggleBuildMode();
    }

    private void ToggleBuildMode()
    {
        _buildModeActive = !_buildModeActive;
        if (_buildModeActive)
        {
            _preview = Instantiate(_selectedFoundation.prefab);
            SetPreviewMaterial(_validPreviewMat);
        }
        else if (_preview != null)
        {
            Destroy(_preview);
        }
    }

    private void UpdatePreview()
    {
        if (_preview == null) return;

        var ray = _buildCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 200f, _groundLayer))
        {
            var cell = _grid.WorldToCell(hit.point);
            _preview.transform.position = _grid.CellToWorld(cell);

            bool canPlace = _grid.CanPlace(cell, _selectedFoundation.size);
            SetPreviewMaterial(canPlace ? _validPreviewMat : _invalidPreviewMat);
        }
    }

    private void TryPlace()
    {
        var ray = _buildCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 200f, _groundLayer)) return;

        var cell = _grid.WorldToCell(hit.point);
        if (!_grid.CanPlace(cell, _selectedFoundation.size)) return;

        var data = new BuildingData
        {
            BuildingId = _selectedFoundation.foundationId,
            Origin = cell,
            Size = _selectedFoundation.size,
            Instance = Instantiate(_selectedFoundation.prefab,
                _grid.CellToWorld(cell), Quaternion.identity)
        };

        _grid.Place(cell, _selectedFoundation.size, data);
    }

    private void SetPreviewMaterial(Material mat)
    {
        if (_preview == null) return;
        var renderer = _preview.GetComponent<Renderer>();
        if (renderer != null) renderer.material = mat;
    }
}
```

5. Set up in scene:
   - Attach `BuildModeController` to a new `BuildSystem` GameObject in `HomeBase_Grid.unity`
   - Wire up references (grid, camera, materials, foundation SO)
   - Create ground plane on a "Ground" layer, set `_groundLayer` mask

6. **Test:** Press B to enter build mode. Mouse over ground to see preview snapping to grid. Click to place. Right-click to exit build mode. Verify you can't place overlapping foundations.

7. Commit.

---

### Task 1.3: Machine definition and placement

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/MachineDefinitionSO.cs`
- Create: `Assets/_Slopworks/Scripts/Automation/MachineComponent.cs`
- Modify: `BuildModeController.cs` to support machine placement
- Create: Machine SO assets and prefabs

**Steps:**

1. Create machine definition:

```csharp
// Assets/_Slopworks/Scripts/Automation/MachineDefinitionSO.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Buildings/Machine")]
public class MachineDefinitionSO : ScriptableObject
{
    public string machineId;
    public string displayName;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;
    public MachinePort[] ports;
}

[System.Serializable]
public struct MachinePort
{
    public Vector2Int LocalOffset;
    public Vector2Int Direction;
    public PortType Type;
}

public enum PortType { Input, Output }
```

2. Create machine component:

```csharp
// Assets/_Slopworks/Scripts/Automation/MachineComponent.cs
using UnityEngine;

public class MachineComponent : MonoBehaviour
{
    public MachineDefinitionSO Definition;
    public MachineStatus Status = MachineStatus.Idle;
}

public enum MachineStatus { Idle, Working, Blocked }
```

3. Create placeholder machine prefabs (cubes with different colors):
   - `Smelter.prefab` — orange cube (2x2)
   - `Assembler.prefab` — blue cube (2x2)
   - `StorageBin.prefab` — gray cube (1x1)

4. Create machine SO assets in `ScriptableObjects/Buildings/`:
   - `Smelter.asset`, `Assembler.asset`, `StorageBin.asset`

5. Extend `BuildModeController` to support cycling between buildable items (foundations + machines) with number keys or scroll wheel.

6. **Test:** Place foundations, then place machines on top. Verify grid collision prevents overlap.

7. Commit.

---

### Task 1.4: Item and recipe system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/ItemDefinitionSO.cs`
- Create: `Assets/_Slopworks/Scripts/Core/ItemInstance.cs`
- Create: `Assets/_Slopworks/Scripts/Core/ItemRegistry.cs`
- Create: `Assets/_Slopworks/Scripts/Core/RecipeSO.cs`
- Create: `Assets/_Slopworks/Scripts/Core/RecipeRegistry.cs`
- Create: Item and recipe SO assets

**Steps:**

1. Implement `ItemDefinitionSO`, `ItemInstance`, `ItemSlot`, `ItemRegistry` exactly as defined in `docs/reference/crafting-inventory.md`.

2. Implement `RecipeSO` and `RecipeRegistry` exactly as defined in `docs/reference/crafting-inventory.md`.

3. Create initial item SO assets:
   - `iron_scrap.asset` (stackable, max 64)
   - `iron_ingot.asset` (stackable, max 64)
   - `copper_scrap.asset` (stackable, max 64)
   - `copper_ingot.asset` (stackable, max 64)
   - `iron_plate.asset` (stackable, max 64)
   - `iron_gear.asset` (stackable, max 64)

4. Create initial recipe SO assets:
   - `smelt_iron.asset`: 2x iron_scrap -> 1x iron_ingot, 3s, requires Smelter
   - `smelt_copper.asset`: 2x copper_scrap -> 1x copper_ingot, 3s, requires Smelter
   - `iron_plate.asset`: 1x iron_ingot -> 2x iron_plate, 2s, requires Assembler
   - `iron_gear.asset`: 2x iron_plate -> 1x iron_gear, 4s, requires Assembler

5. Create a registry initializer that loads all SOs at startup:

```csharp
// Assets/_Slopworks/Scripts/Core/RegistryInitializer.cs
using UnityEngine;

public class RegistryInitializer : MonoBehaviour
{
    private void Awake()
    {
        ItemRegistry.Initialize(Resources.LoadAll<ItemDefinitionSO>("Items"));
        // RecipeRegistry similarly
    }
}
```

6. Move item SO assets to a `Resources/Items/` folder so `Resources.LoadAll` finds them.

7. Commit.

---

### Task 1.5: Machine simulation tick

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/MachineSimulation.cs`
- Create: `Assets/_Slopworks/Scripts/Automation/InventoryContainer.cs`
- Modify: `MachineComponent.cs` to include buffers

**Steps:**

1. Create inventory container (simplified for single player prototype):

```csharp
// Assets/_Slopworks/Scripts/Automation/InventoryContainer.cs
using System.Collections.Generic;

public class InventoryContainer
{
    private readonly List<ItemSlot> _slots;
    public int SlotCount => _slots.Count;

    public InventoryContainer(int slotCount)
    {
        _slots = new List<ItemSlot>(slotCount);
        for (int i = 0; i < slotCount; i++)
            _slots.Add(default);
    }

    public bool TryAdd(ItemInstance item, int count)
    {
        var def = ItemRegistry.Get(item.definitionId);
        if (def == null) return false;

        // find existing stack first
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.isEmpty && slot.item.definitionId == item.definitionId
                && def.isStackable && slot.count + count <= def.maxStackSize)
            {
                slot.count += count;
                _slots[i] = slot;
                return true;
            }
        }

        // find empty slot
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].isEmpty)
            {
                _slots[i] = new ItemSlot { item = item, count = count };
                return true;
            }
        }

        return false;
    }

    public bool TryRemove(string definitionId, int count)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.isEmpty && slot.item.definitionId == definitionId && slot.count >= count)
            {
                slot.count -= count;
                if (slot.count == 0) slot = default;
                _slots[i] = slot;
                return true;
            }
        }
        return false;
    }

    public int CountOf(string definitionId)
    {
        int total = 0;
        foreach (var slot in _slots)
            if (!slot.isEmpty && slot.item.definitionId == definitionId)
                total += slot.count;
        return total;
    }

    public ItemSlot GetSlot(int index) => _slots[index];
}
```

2. Implement machine simulation loop per `docs/reference/factory-automation.md`:
   - Fixed tick rate (FixedUpdate)
   - IDLE -> check inputs -> WORKING -> timer -> try output -> BLOCKED if full
   - Machine state machine: IDLE / WORKING / BLOCKED

3. Add input and output buffers to `MachineComponent`.

4. **Test:** Place a smelter, manually add iron_scrap to its input buffer (via inspector or debug key), watch it transition through IDLE -> WORKING -> BLOCKED states. Verify output buffer contains iron_ingot after craft time.

5. Commit.

---

### Task 1.6: Conveyor belt system (basic)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltSegment.cs`
- Create: `Assets/_Slopworks/Scripts/Automation/BeltItem.cs`
- Create: Belt prefab and material
- Modify: `BuildModeController.cs` to support belt placement

**Steps:**

1. Implement belt segment per `docs/reference/factory-automation.md` (distance-offset model with integer distances).

2. Create belt prefab: narrow plane (1x0.05x1) with scrolling UV material (Shader Graph: scroll Time * speed on U axis).

3. Implement belt placement in build mode: click-drag to place connected belt segments. Auto-merge straight runs into single segments.

4. Connect belt outputs to machine input ports when placed adjacent (check port direction match).

5. Implement visual item rendering: start with simple GameObjects (not GPU instanced yet — optimize later per the docs' guidance to profile first).

6. **Test:** Place a belt, manually add an item, watch it move along the belt. Connect belt to smelter input, verify item enters input buffer.

7. Commit.

---

## Phase 2: First-Person Controller

### Task 2.1: FPS character controller

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/PlayerController.cs`
- Create: `Assets/_Slopworks/Scripts/Player/PlayerCamera.cs`
- Create: `Assets/_Slopworks/Prefabs/Player/PlayerCharacter.prefab`

**Steps:**

1. Create player character prefab:
   - Capsule collider (radius 0.3, height 1.8)
   - Rigidbody (freeze rotation X/Z)
   - PlayerController script
   - Child camera at eye height (1.6 units)

2. Implement basic FPS controller:
   - WASD movement
   - Mouse look (lock cursor)
   - Jump (space)
   - Sprint (shift)

3. **Test:** Play the Home Base scene. Walk around, look around, jump. Verify movement feels responsive.

4. Commit.

---

### Task 2.2: Camera mode toggle (FPS / Isometric)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/CameraModeController.cs`
- Modify: Player prefab to include isometric camera setup

**Steps:**

1. Set up URP Camera Stacking per `docs/reference/render-pipeline.md`:
   - Base camera (isometric/perspective toggle)
   - Overlay camera for first-person elements

2. Implement toggle (Tab key):
   - FPS mode: perspective projection, cursor locked, WASD movement
   - Isometric mode: orthographic projection, cursor visible, click-to-interact

3. Toggle camera GameObjects, don't reconfigure a single camera (per render pipeline doc — reconfiguration causes stutter).

4. **Test:** Tab to toggle between FPS and isometric. Verify both views render correctly. Verify cursor lock/unlock works.

5. Commit.

---

### Task 2.3: Interaction system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/InteractionController.cs`
- Create: `Assets/_Slopworks/Scripts/Core/IInteractable.cs`

**Steps:**

1. Create interactable interface:

```csharp
public interface IInteractable
{
    string GetInteractionPrompt();
    void Interact(PlayerController player);
}
```

2. Implement raycast-based interaction:
   - Raycast from camera center each frame
   - If hit has `IInteractable`, show prompt
   - Press E to interact

3. Make machines implement `IInteractable` (opens recipe selection when interacted with).

4. **Test:** Walk up to a smelter, see "Press E to configure" prompt, press E, verify interaction fires.

5. Commit.

---

## Phase 3: Combat

### Task 3.1: Basic weapon system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Combat/WeaponController.cs`
- Create: `Assets/_Slopworks/Scripts/Combat/HealthComponent.cs`
- Create: `Assets/_Slopworks/Scripts/Combat/DamageData.cs`

**Steps:**

1. Implement hitscan weapon (raycast from camera center on left-click).
2. Create health component (current HP, max HP, TakeDamage method, OnDeath event).
3. Add muzzle flash VFX (simple particle system).
4. **Test:** Shoot at objects with HealthComponent, verify damage applies and death triggers.
5. Commit.

---

### Task 3.2: Enemy AI (basic fauna)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Combat/EnemyAI.cs`
- Create: `Assets/_Slopworks/Scripts/Combat/EnemySpawner.cs`
- Create: Enemy prefab

**Steps:**

1. Create enemy prefab: capsule with HealthComponent, NavMeshAgent, EnemyAI.
2. Implement basic AI:
   - Idle: wander randomly within territory
   - Alert: player enters detection range, move toward player
   - Attack: within melee range, deal damage on timer
   - Death: destroy after delay
3. Create enemy spawner: spawn N enemies at designated points.
4. Bake NavMesh on Home Base terrain.
5. **Test:** Enemies spawn, wander, chase player when close, attack, take damage, die.
6. Commit.

---

### Task 3.3: Wave defense system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Combat/WaveController.cs`
- Create: `Assets/_Slopworks/Scripts/Combat/ThreatMeter.cs`

**Steps:**

1. Implement wave controller:
   - Configurable wave definitions (enemy count, types, spawn delay)
   - Spawn enemies at designated points around base perimeter
   - Fire `WaveStarted` and `WaveEnded` events
2. Implement threat meter:
   - Tracks current threat level (int)
   - Increases when buildings connected (placeholder for now)
   - Drives wave intensity
3. **Test:** Trigger a wave, enemies spawn from perimeter, attack base/player. Wave ends when all enemies dead.
4. Commit.

---

## Phase 4: Turret Defenses

### Task 4.1: Auto-turret

**Files:**
- Create: `Assets/_Slopworks/Scripts/Combat/TurretController.cs`
- Create: Turret prefab and SO

**Steps:**

1. Create turret prefab: base + rotating barrel.
2. Implement turret logic:
   - Detect nearest enemy in range (OverlapSphere)
   - Rotate barrel toward target
   - Fire projectile/hitscan on interval
   - Requires power (check power network satisfaction)
   - Consumes ammo from internal inventory
3. Add turret as a buildable item in build mode.
4. **Test:** Place turret, trigger a wave, turret targets and shoots enemies. Verify it stops when unpowered.
5. Commit.

---

## Parallel Execution Schedule

Phases 1-3 are complete. Phase 4 is in progress (Joe). Remaining phases are assigned for parallel execution:

| Round | Kevin | Joe |
|-------|-------|-----|
| 1 | Phase 5 (Core UI + Inventory + Scene Mgmt) | Phase 4 (Turret Defenses) -- in progress |
| 2 | Phase 6 (Building Exploration) | Phase 7 (The Tower) |
| 3 | Phase 8 (Supply Chain Network) | Phase 9 (Save System + Full Loop) |

Round 2 has no file overlap: Kevin works in `Scripts/World/`, `Scripts/Core/`, `Scripts/UI/`; Joe works in `Scripts/World/Tower*`, `Scripts/Combat/InteriorFauna*`, `Scenes/Tower_Core.unity`. Round 3 has integration points that require coordination.

---

## Phase 5: Core UI + Player Inventory + Scene Management (Kevin)

> Prerequisites for every subsequent phase. HUD, inventory, and scene transitions must exist before building exploration, the tower, or supply chains can function.

### Task 5.1: Scene loader and transition system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/SceneLoader.cs`
- Modify: `Bootstrap.cs`

**Steps:**

1. Implement scene loader (additive loading, Core scenes always resident):
   - `LoadSceneAsync(string sceneName, LoadSceneMode.Additive)` with progress callback
   - `UnloadSceneAsync(string sceneName)` for cleanup
   - Transition flow: fade out -> unload old -> load new -> fade in
   - Core_Network and Core_GameManager are never unloaded
2. Create loading screen UI (simple panel with progress bar).
3. Add transition trigger interface: `ISceneTransitionTrigger` with destination scene name.
4. **Test:** Trigger a scene transition, verify additive load/unload, verify Core scenes persist.
5. Commit.

### Task 5.2: Basic HUD

**Files:**
- Create: `Assets/Scenes/HomeBase/HomeBase_UI.unity`
- Create: UI scripts in `Assets/_Slopworks/Scripts/UI/`

**Steps:**

1. Create HUD elements:
   - Health bar (bound to player HealthComponent)
   - Hotbar (9 slots, number keys to select)
   - Threat meter display
   - Build mode indicator
   - Crosshair
   - Interaction prompt ("Press E to ...")
   - Wave incoming warning
2. HUD scene loads additively via SceneLoader from 5.1.
3. Commit.

### Task 5.3: Player inventory

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/PlayerInventory.cs`
- Create: Inventory UI scripts

**Steps:**

1. Implement player inventory (36 slots + 9 hotbar) using existing `InventoryContainer`.
2. Implement pickup system (walk over items on ground, auto-collect).
3. Implement inventory UI (Tab to open/close).
4. Implement manual crafting at workstations (interact with machine, select recipe, craft from inventory).
5. **Test:** Pick up items, open inventory, see items. Craft at a workstation. Hotbar reflects inventory changes.
6. Commit.

---

## Phase 6: Building Exploration (Kevin)

> First BIM building interior. Establishes the pattern for clearing and claiming buildings that the tower and overworld build on.

### Task 6.1: Placeholder building scene

**Files:**
- Create: `Assets/Scenes/Buildings/Building_Warehouse.unity`
- Create: `Assets/_Slopworks/Scripts/World/BuildingManager.cs`

**Steps:**

1. Create a simple building interior scene:
   - Hallways, rooms, a mechanical room (all from ProBuilder or primitives)
   - NavMesh baked for fauna
   - Enemy spawners placed in dark corners
   - MEP interaction points (placeholder cubes that trigger "restore" on interact)
2. Implement building manager:
   - Track which MEP systems are restored
   - When all systems restored, fire `BuildingClaimed` event
   - Building produces resources after claiming
3. Scene transitions use SceneLoader from Phase 5 (enter building -> additive load, exit -> unload).
4. **Test:** Load building scene, navigate interior, fight fauna, interact with MEP points, claim building, return to home base.
5. Commit.

---

## Phase 7: The Tower (Joe)

> Repeatable FPS combat gauntlet for progression, rare materials, and factory upgrades. Full design in `docs/plans/2026-02-28-tower-design.md`. Joe's tasks are J-016 through J-021 in `docs/coordination/tasks-joe.md`.

### Task 7.1: Tower data model and simulation layer (J-016)

Plain C# classes following D-004 pattern. `TowerController` tracks run state (current building, cleared chunks, carried loot, banked fragments, tier). `FloorChunkDefinition` is a data class for spawn points, loot nodes, stair connections. `TowerBuildingDefinitionSO` is a read-only SO with chunk list and boss floor config. All classes are pure C# with no MonoBehaviour dependency. EditMode tests cover run init, chunk clearing, fragment banking, tier progression, loot-on-death clearing.

### Task 7.2: Tower loot system (J-017)

Data-driven loot system. `LootDropDefinition` is a configurable data class (itemId, rarity, dropWeight, min/maxAmount, floor/tier filters). `TowerLootTable` resolves drops from data -- all tuning happens in data, not code. Adding/removing/rebalancing loot requires zero code changes. Tests cover drop resolution with weight/rarity/floor/tier filters.

### Task 7.3: Tower MonoBehaviour wrapper + elevator (J-018)

`TowerBehaviour` wraps `TowerController`. `Tower_Core.unity` is the persistent scene with elevator, lobby geometry, and tower UI. Elevator panel shows floor buttons, triggers chunk swap (destroy current prefab, instantiate next). Depends on Phase 5 (scene management + inventory).

### Task 7.4: Tower enemy population (J-019)

Data-driven enemy spawn configuration per floor chunk. `FloorChunkDefinition` holds spawn entries (faunaDefinition, count, tierMultiplier). 1 new interior fauna type as proof of concept. Enemies spawn on chunk load, cleared floors stay cleared for the run.

### Task 7.5: Boss encounter (J-020)

Boss floor locked until required fragments banked (configurable, default 4). Hand-designed arena. Boss uses existing `FaunaDefinitionSO` with elevated stats. Boss kill rewards use `LootDropDefinition` system. Tier increment and fragment cycle reset on boss death.

### Task 7.6: Tower playtest scene (J-021)

End-to-end playtest: enter tower, explore floors, fight enemies, collect loot + fragments, extract or die. Verify loot banking on extract, loot loss on death, fragment persistence, boss unlock, boss fight, tier progression.

---

## Phase 8: Supply Chain Network (Kevin)

> Connects claimed buildings to home base via overworld supply lines. Resources flow passively, giving the player a reason to clear more buildings.

### Task 8.1: Overworld map

**Files:**
- Create: `Assets/Scenes/Overworld/Overworld_Map.unity`
- Create: `Assets/_Slopworks/Scripts/World/OverworldController.cs`

**Steps:**

1. Create simple isometric overworld scene:
   - Grid-based tile map
   - Home base icon at center
   - Building icons at fixed positions (3-4 buildings)
   - Tower node (requires power connection to activate elevator)
   - Supply line visuals (simple lines between connected nodes)
2. Implement click-to-select on buildings:
   - Show building info panel (type, status, production)
   - Show recipe configuration UI
   - Show supply line routing UI
3. **Test:** Navigate overworld, click buildings, see info panels. Click supply lines to see routing.
4. Commit.

### Task 8.2: Supply line resource flow

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/SupplyLineManager.cs`
- Create: `Assets/_Slopworks/Scripts/Automation/BuildingNode.cs`

**Steps:**

1. Implement building node (abstracted production):
   - Each claimed building is a node with configurable recipe
   - Produces output on a timer
   - Output routes to connected nodes per configured ratios
2. Implement supply line manager:
   - Tracks connections between building nodes and home base
   - Resources flow along supply lines at configured rates
   - Resources arrive at destination after transport delay (based on distance)
   - Supply lines have a vulnerability rating (used by wave system)
3. Connect supply line outputs to home base inventory (items appear in a "supply dock" storage container).
4. Tower node integration: power connection from player network activates the tower elevator.
5. **Test:** Claim a building, configure production, set output to home base. Wait for resources to arrive. Verify resource flow. Connect power to tower node, verify elevator activates.
6. Commit.

---

## Phase 9: Save System + Full Loop Assembly (Joe assists)

> Persistence layer and integration testing. Joe handles save/load implementation while Kevin connects all systems.

### Task 9.1: Local JSON save system

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/SaveSystem.cs`
- Create: `Assets/_Slopworks/Scripts/Core/SaveData.cs`

**Steps:**

1. Implement save/load to local JSON file:
   - Save: player inventory, placed buildings (factory grid state), claimed buildings, supply line config, threat level, tower progress (banked fragments, current tier)
   - Save location: `Application.persistentDataPath + "/saves/"`
   - Format per `docs/reference/crafting-inventory.md` (definitionId strings, save version)
2. Auto-save every 5 minutes and on quit.
3. Load on game start.
4. **Test:** Build a base, run the tower, save, quit, reload. Verify base, inventory, and tower progress persist.
5. Commit.

### Task 9.2: Connect the full loop

**Steps:**

1. Verify the full gameplay loop works end-to-end:
   - Start at home base
   - Build foundations, machines, turret defenses
   - Open overworld, scout a building
   - Travel to building, clear fauna, restore MEP systems, claim it
   - Return to overworld, configure building production and supply line to base
   - Resources flow to base, use them to craft equipment and turret ammo
   - Enter the tower, explore floors, fight enemies, collect loot
   - Extract with loot or die and lose it
   - Bank key fragments, unlock boss, beat boss for tier upgrade
   - Defense wave attacks base, turrets defend
   - Survive, expand to next building
2. Fix integration issues between systems.
3. Commit as "vertical slice complete".

---

## Phase 10: Multiplayer (Post-Vertical-Slice)

### Task 10.1: FishNet integration

Convert single-player systems to networked:
- Add NetworkObject and NetworkBehaviour to player, machines, belt segments
- Convert FactoryGrid to server-authoritative with SyncVar/SyncList
- Convert inventory to NetworkInventory per patterns doc
- Convert machine simulation to server-only (add `if (!IsServerInitialized) return;` guards)
- Test with ParrelSync (two editor instances)

### Task 10.2: FishySteamworks transport

- Install Facepunch.Steamworks
- Install FishySteamworks
- Configure Steam lobby creation/joining
- Test P2P connection between two machines

---

## Open Items (Backlog)

Items covered by phases above have been removed. Remaining backlog:

- Supabase integration (lobby discovery, persistent saves, cross-session state)
- BIM import pipeline (Revit FBX -> Unity, material remapping, LOD, navmesh)
- Art style pass (replace placeholder cubes with real assets)
- Audio and music
- Tech tree and progression curve (tower tiers drive this)
- GPU instanced belt item rendering (replace GameObjects with DrawMeshInstancedProcedural)
- Zoop-style batch placement for foundations
- Day/night cycle
- GameCI build pipeline
- MCP Unity integration for Claude Code workflow
- Review SebLague/Curve-Editor (https://github.com/SebLague/Curve-Editor) for belt path math
- Tower building pool expansion (additional BIM models for variety)
- Interior fauna variety (new enemy types for upper tower floors)
- Tower modifiers and blueprints (specific items and effects to design)
- Supply line vulnerability and defense events (waves target supply lines)
- Equipment and gear system (weapons, armor, tools with durability and upgrades)
