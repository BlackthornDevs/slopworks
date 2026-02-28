using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unified playtest bootstrapper for structural building + factory automation.
/// Drop on an empty GameObject, hit Play, and test the full gameplay loop:
/// place foundations, then place belts/machines/storage ON those foundations.
///
/// Controls:
///   1 - Foundation tool (drag rectangle)
///   2 - Wall tool (click + R rotate + click)
///   3 - Ramp tool (click + R rotate + click)
///   4 - Delete tool (click to remove)
///   5 - Belt tool (2-click: start cell, end cell)
///   6 - Machine tool (1-click + R rotate)
///   7 - Storage tool (1-click + R rotate)
///   Escape - Cancel / deselect tool
///   PageUp / PageDown - Change active level
///   Right-click + mouse to look, WASD to fly
///
/// Everything is created at runtime -- no prefabs or assets required.
/// </summary>
public class StructuralPlaytestSetup : MonoBehaviour
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    [Header("Pre-seed")]
    [SerializeField] private bool _preSeedFactory;

    [Header("Automation")]
    [SerializeField] private ushort _beltSpeed = 4;

    // -- Structural infrastructure --
    private FactoryGrid _grid;
    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _placementService;

    // -- Automation infrastructure --
    private PortNodeRegistry _portRegistry;
    private ConnectionResolver _connectionResolver;
    private FactorySimulation _simulation;
    private BuildingPlacementService _automationService;

    // -- Structural definitions (created at runtime) --
    private FoundationDefinitionSO _foundationDef;
    private WallDefinitionSO _wallDef;
    private RampDefinitionSO _rampDef;

    // -- Automation definitions (created at runtime) --
    private MachineDefinitionSO _smelterDef;
    private StorageDefinitionSO _storageDef;
    private ItemDefinitionSO _ironOreDef;
    private ItemDefinitionSO _ironIngotDef;
    private RecipeSO _smeltRecipe;

    // -- Tracking --
    private readonly List<BuildingData> _foundations = new();
    private readonly List<WallData> _walls = new();
    private readonly List<RampData> _ramps = new();
    private readonly List<PlacementResult> _automationBuildings = new();

    // -- Tool state --
    private enum ToolMode { None, Foundation, Wall, Ramp, Delete, Belt, MachinePlace, StoragePlace }
    private ToolMode _currentTool = ToolMode.None;
    private int _currentLevel;

    // -- Ramp 2-step placement state --
    private bool _pendingRamp;
    private Vector2Int _pendingRampCell;
    private int _pendingRampDirIndex;
    private GameObject _pendingRampPreview;
    private static readonly string[] DirectionNames = { "North", "East", "South", "West" };

    // -- Foundation drag state --
    private bool _isDragging;
    private Vector2Int _dragStart;
    private Vector2Int _dragEnd;
    private readonly List<GameObject> _ghostPool = new();

    // -- Wall 2-step placement state --
    private bool _pendingWall;
    private Vector2Int _pendingWallCell;
    private int _pendingWallDirIndex;
    private GameObject _pendingWallPreview;

    // -- Belt 2-click placement state --
    private bool _beltStartSet;
    private Vector2Int _beltStartCell;
    private GameObject _beltGhostLine;

    // -- Machine/storage rotation state --
    private int _placeRotation;

    // -- Ghost for machine/storage preview --
    private GameObject _placeGhost;
    private readonly List<GameObject> _ghostPortIndicators = new();

    // -- Port direction indicators on placed machines --
    private readonly List<GameObject> _portIndicators = new();

    // -- Belt item visuals --
    private readonly List<GameObject> _beltItemPool = new();
    private readonly List<float> _positionBuffer = new();

    // -- Ground plane --
    private GameObject _groundPlane;

    // -- Colors --
    private static readonly Color _ghostValidColor = new Color(0f, 1f, 0f, 0.4f);
    private static readonly Color _ghostInvalidColor = new Color(1f, 0f, 0f, 0.4f);

    // -- Constants --
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const string SmeltIronRecipeId = "smelt_iron";
    private const string SmelterType = "smelter";

    private void Awake()
    {
        CreateDefinitions();
        CreateInfrastructure();
        CreateGroundPlane();
        SetupCamera();

        if (_preSeedFactory)
            PreSeedFactory();

        Debug.Log("Unified playtest started. [1] Foundation [2] Wall [3] Ramp [4] Delete [5] Belt [6] Machine [7] Storage");
    }

    private void FixedUpdate()
    {
        if (_simulation != null)
            _simulation.Tick(Time.fixedDeltaTime);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        HandleToolSelection(kb);
        HandleLevelChange(kb);
        HandleFillStorage(kb, mouse);

        switch (_currentTool)
        {
            case ToolMode.Foundation:
                HandleFoundationInput(mouse);
                break;
            case ToolMode.Wall:
                HandleWallInput(kb, mouse);
                break;
            case ToolMode.Ramp:
                HandleRampInput(kb, mouse);
                break;
            case ToolMode.Delete:
                HandleDeleteInput(mouse);
                break;
            case ToolMode.Belt:
                HandleBeltInput(kb, mouse);
                break;
            case ToolMode.MachinePlace:
                HandleMachinePlaceInput(kb, mouse);
                break;
            case ToolMode.StoragePlace:
                HandleStoragePlaceInput(kb, mouse);
                break;
        }

        UpdateBeltItemVisuals();
    }

    private void OnGUI()
    {
        float x = 10;
        float y = 10;
        float w = 420;
        float h = 22;

        int lineCount = 15;
        if (_currentTool == ToolMode.Wall && _pendingWall) lineCount++;
        if (_currentTool == ToolMode.Wall && !_pendingWall) lineCount++;
        if (_currentTool == ToolMode.Ramp) lineCount++;
        if (_currentTool == ToolMode.Belt) lineCount++;
        if (_isDragging) lineCount++;

        GUI.Box(new Rect(x - 4, y - 4, w + 8, h * lineCount + 8), "");

        DrawLine(ref y, x, w, h, "UNIFIED BUILDING + AUTOMATION PLAYTEST", true);
        DrawLine(ref y, x, w, h, $"Tool: {_currentTool}  |  Level: {_currentLevel}");
        y += 4;
        DrawLine(ref y, x, w, h, $"Foundations: {_foundations.Count}  |  Walls: {_walls.Count}  |  Ramps: {_ramps.Count}");
        DrawLine(ref y, x, w, h, $"Snap points: {_snapRegistry.Count}");

        // Automation stats
        int beltCount = _simulation.BeltCount;
        int machineCount = _simulation.MachineCount;
        int inserterCount = _simulation.InserterCount;
        int storageCount = 0;
        foreach (var ab in _automationBuildings)
            if (ab.SimulationObject is StorageContainer) storageCount++;

        DrawLine(ref y, x, w, h, $"Belts: {beltCount}  |  Machines: {machineCount}  |  Storage: {storageCount}");
        DrawLine(ref y, x, w, h, $"Auto-inserters: {inserterCount}  |  Belt links: {_simulation.BeltNetwork.ConnectionCount}");
        DrawLine(ref y, x, w, h, $"Port nodes: {_portRegistry.Count}");

        y += 4;
        DrawLine(ref y, x, w, h, "[1] Foundation  [2] Wall  [3] Ramp  [4] Delete");
        DrawLine(ref y, x, w, h, "[5] Belt  [6] Machine  [7] Storage  [F] Fill storage");
        DrawLine(ref y, x, w, h, "[PgUp/PgDn] Level  [R] Rotate  [Esc] Cancel");
        DrawLine(ref y, x, w, h, "Port colors: BLUE = input  RED = output");

        if (_currentTool == ToolMode.Wall)
        {
            if (_pendingWall)
                DrawLine(ref y, x, w, h, $"Wall: {DirectionNames[_pendingWallDirIndex]}  [R] rotate  [Click] confirm  [Esc] cancel");
            else
                DrawLine(ref y, x, w, h, "Click any foundation cell to preview wall");
        }
        if (_currentTool == ToolMode.Ramp)
        {
            if (_pendingRamp)
                DrawLine(ref y, x, w, h, $"Ramp: {DirectionNames[_pendingRampDirIndex]}  [R] rotate  [Click] confirm  [Esc] cancel");
            else
                DrawLine(ref y, x, w, h, "Click any foundation cell to preview ramp");
        }
        if (_currentTool == ToolMode.Belt)
        {
            if (_beltStartSet)
                DrawLine(ref y, x, w, h, $"Belt start: ({_beltStartCell.x},{_beltStartCell.y}) -- click end cell (straight line)");
            else
                DrawLine(ref y, x, w, h, "Click a foundation cell to set belt start");
        }

        if (_isDragging)
        {
            var min = Vector2Int.Min(_dragStart, _dragEnd);
            var max = Vector2Int.Max(_dragStart, _dragEnd);
            var size = max - min + Vector2Int.one;
            DrawLine(ref y, x, w, h, $"Dragging: {size.x}x{size.y} from ({min.x},{min.y})");
        }
    }

    private void OnDestroy()
    {
        if (_foundationDef != null) DestroyImmediate(_foundationDef);
        if (_wallDef != null) DestroyImmediate(_wallDef);
        if (_rampDef != null) DestroyImmediate(_rampDef);
        if (_smelterDef != null) DestroyImmediate(_smelterDef);
        if (_storageDef != null) DestroyImmediate(_storageDef);
        if (_ironOreDef != null) DestroyImmediate(_ironOreDef);
        if (_ironIngotDef != null) DestroyImmediate(_ironIngotDef);
        if (_smeltRecipe != null) DestroyImmediate(_smeltRecipe);
    }

    // -- Setup --

    private void CreateDefinitions()
    {
        // Structural
        _foundationDef = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundationDef.foundationId = "foundation_1x1";
        _foundationDef.displayName = "Foundation 1x1";
        _foundationDef.size = Vector2Int.one;
        _foundationDef.generatesSnapPoints = true;

        _wallDef = ScriptableObject.CreateInstance<WallDefinitionSO>();
        _wallDef.wallId = "wall_basic";
        _wallDef.displayName = "Basic Wall";

        _rampDef = ScriptableObject.CreateInstance<RampDefinitionSO>();
        _rampDef.rampId = "ramp_basic";
        _rampDef.displayName = "Basic Ramp";
        _rampDef.footprintLength = 3;

        // Automation
        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter_basic";
        _smelterDef.machineType = SmelterType;
        _smelterDef.displayName = "Basic Smelter";
        _smelterDef.size = Vector2Int.one;
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 2;
        _smelterDef.processingSpeed = 1f;
        _smelterDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0),
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(1, 0),
                type = PortType.Output
            }
        };

        _storageDef = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        _storageDef.storageId = "storage_bin";
        _storageDef.slotCount = 4;
        _storageDef.maxStackSize = 50;
        _storageDef.size = Vector2Int.one;
        // Storage accessible from all 4 sides (like Factorio chests)
        _storageDef.ports = new[]
        {
            // Input ports -- one per cardinal direction
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(-1, 0), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(1, 0), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, -1), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, 1), type = PortType.Input },
            // Output ports -- one per cardinal direction
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(-1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, -1), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, 1), type = PortType.Output },
        };

        _ironOreDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironOreDef.itemId = IronOre;
        _ironOreDef.displayName = "Iron Ore";

        _ironIngotDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironIngotDef.itemId = IronIngot;
        _ironIngotDef.displayName = "Iron Ingot";

        _smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltRecipe.recipeId = SmeltIronRecipeId;
        _smeltRecipe.displayName = "Smelt Iron";
        _smeltRecipe.inputs = new[] { new RecipeIngredient { itemId = IronOre, count = 1 } };
        _smeltRecipe.outputs = new[] { new RecipeIngredient { itemId = IronIngot, count = 1 } };
        _smeltRecipe.craftDuration = 2f;
        _smeltRecipe.requiredMachineType = SmelterType;
    }

    private void CreateInfrastructure()
    {
        _grid = new FactoryGrid();
        _snapRegistry = new SnapPointRegistry();
        _placementService = new StructuralPlacementService(_grid, _snapRegistry);

        RecipeSO LookupRecipe(string id) => id == SmeltIronRecipeId ? _smeltRecipe : null;
        _simulation = new FactorySimulation(LookupRecipe);
        _simulation.BeltSpeed = _beltSpeed;
        _portRegistry = new PortNodeRegistry();
        _connectionResolver = new ConnectionResolver(_portRegistry, _simulation);
        _automationService = new BuildingPlacementService(
            _grid, _portRegistry, _connectionResolver, _simulation);
    }

    private void CreateGroundPlane()
    {
        _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _groundPlane.name = "GridPlane";
        _groundPlane.layer = PhysicsLayers.GridPlane;
        _groundPlane.transform.position = new Vector3(
            FactoryGrid.Width * FactoryGrid.CellSize * 0.5f,
            -0.05f,
            FactoryGrid.Height * FactoryGrid.CellSize * 0.5f);
        _groundPlane.transform.localScale = new Vector3(
            FactoryGrid.Width * FactoryGrid.CellSize,
            0.1f,
            FactoryGrid.Height * FactoryGrid.CellSize);

        var renderer = _groundPlane.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;
        cam.transform.position = new Vector3(centerX, 20f, centerZ - 12f);
        cam.transform.LookAt(new Vector3(centerX, 0f, centerZ));

        if (cam.GetComponent<PlaytestCameraController>() == null)
            cam.gameObject.AddComponent<PlaytestCameraController>();
    }

    // -- Pre-seed factory --

    private void PreSeedFactory()
    {
        // Place a 10x5 foundation slab starting at (5,5)
        for (int x = 5; x < 15; x++)
        {
            for (int z = 5; z < 10; z++)
            {
                var cell = new Vector2Int(x, z);
                var data = _placementService.PlaceFoundation(_foundationDef, cell, 0);
                if (data != null)
                {
                    _foundations.Add(data);
                    SpawnFoundationVisual(data, cell, 0);
                }
            }
        }

        // Source storage at (5,7), pre-filled with 50 iron ore
        var srcResult = _automationService.PlaceStorage(_storageDef, new Vector2Int(5, 7), 0);
        if (srcResult != null)
        {
            _automationBuildings.Add(srcResult);
            SpawnStorageVisual(srcResult, new Vector2Int(5, 7));
            var srcStorage = (StorageContainer)srcResult.SimulationObject;
            for (int i = 0; i < 50; i++)
                srcStorage.TryInsert(IronOre);
            Debug.Log("Pre-seed: source storage placed at (5,7) with 50 iron ore");
        }

        // Belt from (6,7) to (8,7)
        var belt1Result = _automationService.PlaceBelt(new Vector2Int(6, 7), new Vector2Int(8, 7));
        if (belt1Result != null)
        {
            _automationBuildings.Add(belt1Result);
            SpawnBeltVisual(belt1Result, new Vector2Int(6, 7), new Vector2Int(8, 7));
            Debug.Log("Pre-seed: belt placed from (6,7) to (8,7)");
        }

        // Smelter at (9,7)
        var smelterResult = _automationService.PlaceMachine(_smelterDef, new Vector2Int(9, 7), 0);
        if (smelterResult != null)
        {
            _automationBuildings.Add(smelterResult);
            SpawnMachineVisual(smelterResult, new Vector2Int(9, 7));
            var machine = (Machine)smelterResult.SimulationObject;
            machine.SetRecipe(SmeltIronRecipeId);
            Debug.Log("Pre-seed: smelter placed at (9,7)");
        }

        // Belt from (10,7) to (12,7)
        var belt2Result = _automationService.PlaceBelt(new Vector2Int(10, 7), new Vector2Int(12, 7));
        if (belt2Result != null)
        {
            _automationBuildings.Add(belt2Result);
            SpawnBeltVisual(belt2Result, new Vector2Int(10, 7), new Vector2Int(12, 7));
            Debug.Log("Pre-seed: belt placed from (10,7) to (12,7)");
        }

        // Output storage at (13,7)
        var outResult = _automationService.PlaceStorage(_storageDef, new Vector2Int(13, 7), 0);
        if (outResult != null)
        {
            _automationBuildings.Add(outResult);
            SpawnStorageVisual(outResult, new Vector2Int(13, 7));
            Debug.Log("Pre-seed: output storage placed at (13,7)");
        }

        Debug.Log($"Pre-seed complete: {_simulation.InserterCount} auto-inserters, {_simulation.BeltNetwork.ConnectionCount} belt links");
    }

    // -- Input handling (New Input System) --

    private void HandleToolSelection(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.Foundation;
        }
        else if (kb.digit2Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.Wall;
        }
        else if (kb.digit3Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.Ramp;
        }
        else if (kb.digit4Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.Delete;
        }
        else if (kb.digit5Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.Belt;
        }
        else if (kb.digit6Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.MachinePlace;
            _placeRotation = 0;
        }
        else if (kb.digit7Key.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.StoragePlace;
            _placeRotation = 0;
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.None;
        }
    }

    private void HandleLevelChange(Keyboard kb)
    {
        if (kb.pageUpKey.wasPressedThisFrame)
        {
            _currentLevel = Mathf.Min(_currentLevel + 1, FactoryGrid.MaxLevels - 1);
            UpdateGroundPlaneHeight();
            Debug.Log($"Level: {_currentLevel}");
        }
        else if (kb.pageDownKey.wasPressedThisFrame)
        {
            _currentLevel = Mathf.Max(_currentLevel - 1, 0);
            UpdateGroundPlaneHeight();
            Debug.Log($"Level: {_currentLevel}");
        }
    }

    private void HandleFillStorage(Keyboard kb, Mouse mouse)
    {
        if (!kb.fKey.wasPressedThisFrame)
            return;

        var cell = GetCellUnderCursor(mouse);
        if (!cell.HasValue)
            return;

        // Find a storage building at the clicked cell
        for (int i = 0; i < _automationBuildings.Count; i++)
        {
            var ab = _automationBuildings[i];
            if (ab.SimulationObject is not StorageContainer storage)
                continue;
            var bd = ab.BuildingData;
            if (bd.Level != _currentLevel)
                continue;
            if (cell.Value.x >= bd.Origin.x && cell.Value.x < bd.Origin.x + bd.Size.x
                && cell.Value.y >= bd.Origin.y && cell.Value.y < bd.Origin.y + bd.Size.y)
            {
                int added = 0;
                while (storage.TryInsert(IronOre))
                    added++;
                Debug.Log($"Filled storage at ({bd.Origin.x},{bd.Origin.y}) with {added} iron ore (total: {storage.GetTotalItemCount()})");
                return;
            }
        }

        Debug.Log($"No storage at ({cell.Value.x},{cell.Value.y}) to fill");
    }

    private void CancelAllPending()
    {
        CancelDrag();
        CancelPendingWall();
        CancelPendingRamp();
        CancelBeltPlacement();
        DestroyPlaceGhost();
        ClearGhostPortIndicators();
    }

    private void UpdateGroundPlaneHeight()
    {
        if (_groundPlane != null)
        {
            var pos = _groundPlane.transform.position;
            pos.y = _currentLevel * FactoryGrid.LevelHeight - 0.05f;
            _groundPlane.transform.position = pos;
        }
    }

    // -- Foundation batch placement --

    private void HandleFoundationInput(Mouse mouse)
    {
        var cell = GetCellUnderCursor(mouse);
        if (!cell.HasValue)
        {
            HideGhosts();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _isDragging = true;
            _dragStart = cell.Value;
            _dragEnd = cell.Value;
        }

        if (_isDragging)
        {
            _dragEnd = cell.Value;
            UpdateFoundationGhosts();
        }

        if (mouse.leftButton.wasReleasedThisFrame && _isDragging)
        {
            PlaceFoundationRectangle();
            _isDragging = false;
            HideGhosts();
        }
    }

    private void PlaceFoundationRectangle()
    {
        var min = Vector2Int.Min(_dragStart, _dragEnd);
        var max = Vector2Int.Max(_dragStart, _dragEnd);

        int placed = 0;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var cellPos = new Vector2Int(x, z);
                var data = _placementService.PlaceFoundation(_foundationDef, cellPos, _currentLevel);
                if (data != null)
                {
                    _foundations.Add(data);
                    SpawnFoundationVisual(data, cellPos, _currentLevel);
                    placed++;
                }
            }
        }

        if (placed > 0)
            Debug.Log($"Placed {placed} foundations at level {_currentLevel}");
    }

    private void UpdateFoundationGhosts()
    {
        var min = Vector2Int.Min(_dragStart, _dragEnd);
        var max = Vector2Int.Max(_dragStart, _dragEnd);
        int needed = (max.x - min.x + 1) * (max.y - min.y + 1);

        while (_ghostPool.Count < needed)
        {
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "Ghost";
            ghost.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
            var collider = ghost.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            ghost.SetActive(false);
            _ghostPool.Add(ghost);
        }

        int idx = 0;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var cellPos = new Vector2Int(x, z);
                bool valid = _grid.CanPlace(cellPos, Vector2Int.one, _currentLevel);
                var worldPos = _grid.CellToWorld(cellPos, _currentLevel) + Vector3.up * 0.05f;

                var ghost = _ghostPool[idx];
                ghost.SetActive(true);
                ghost.transform.position = worldPos;
                SetColor(ghost, valid ? _ghostValidColor : _ghostInvalidColor);
                idx++;
            }
        }

        for (int i = idx; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private void HideGhosts()
    {
        for (int i = 0; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private void CancelDrag()
    {
        _isDragging = false;
        HideGhosts();
    }

    // -- Wall 2-step placement --

    private void HandleWallInput(Keyboard kb, Mouse mouse)
    {
        if (!_pendingWall)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var cell = GetCellUnderCursor(mouse);
                if (!cell.HasValue)
                    return;

                var building = _grid.GetAt(cell.Value, _currentLevel);
                if (building == null || !building.IsStructural)
                {
                    Debug.Log("Walls must be placed on a foundation cell");
                    return;
                }

                _pendingWall = true;
                _pendingWallCell = cell.Value;
                _pendingWallDirIndex = 0;
                UpdatePendingWallPreview();
                Debug.Log($"Wall at ({cell.Value.x},{cell.Value.y}): {DirectionNames[_pendingWallDirIndex]} -- R to rotate, click to confirm, Esc to cancel");
            }
        }
        else
        {
            if (kb.rKey.wasPressedThisFrame)
            {
                _pendingWallDirIndex = (_pendingWallDirIndex + 1) % 4;
                UpdatePendingWallPreview();
                Debug.Log($"Wall direction: {DirectionNames[_pendingWallDirIndex]}");
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                CancelPendingWall();
                Debug.Log("Wall placement cancelled");
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var dir = CardinalDirections[_pendingWallDirIndex];
                var wallData = _placementService.PlaceWall(_wallDef, _pendingWallCell, _currentLevel, dir);
                if (wallData != null)
                {
                    _walls.Add(wallData);
                    SpawnWallVisual(wallData);
                    Debug.Log($"Wall placed at ({_pendingWallCell.x},{_pendingWallCell.y}) edge {DirectionNames[_pendingWallDirIndex]}");
                }
                else
                {
                    Debug.Log($"Cannot place wall at ({_pendingWallCell.x},{_pendingWallCell.y}) edge {DirectionNames[_pendingWallDirIndex]}");
                }
                CancelPendingWall();
            }
        }
    }

    private void UpdatePendingWallPreview()
    {
        if (_pendingWallPreview != null)
            Destroy(_pendingWallPreview);

        if (!_pendingWall)
            return;

        var dir = CardinalDirections[_pendingWallDirIndex];
        var cellCenter = _grid.CellToWorld(_pendingWallCell, _currentLevel);
        var edgeOffset = new Vector3(
            dir.x * 0.5f * FactoryGrid.CellSize, 0f,
            dir.y * 0.5f * FactoryGrid.CellSize);
        var wallPos = cellCenter + edgeOffset + Vector3.up * FactoryGrid.LevelHeight * 0.5f;
        float yRotation = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "WallPreview";
        var collider = wall.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        wall.transform.position = wallPos;
        wall.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        wall.transform.localScale = new Vector3(0.95f, FactoryGrid.LevelHeight, 0.1f);
        SetColor(wall, _ghostValidColor);

        _pendingWallPreview = wall;
    }

    private void CancelPendingWall()
    {
        _pendingWall = false;
        if (_pendingWallPreview != null)
        {
            Destroy(_pendingWallPreview);
            _pendingWallPreview = null;
        }
    }

    // -- Ramp 2-step placement --

    private void HandleRampInput(Keyboard kb, Mouse mouse)
    {
        if (!_pendingRamp)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var cell = GetCellUnderCursor(mouse);
                if (!cell.HasValue)
                    return;

                var building = _grid.GetAt(cell.Value, _currentLevel);
                if (building == null || !building.IsStructural)
                {
                    Debug.Log("Ramps must start from a foundation cell");
                    return;
                }

                _pendingRamp = true;
                _pendingRampCell = cell.Value;
                _pendingRampDirIndex = 0;
                UpdatePendingRampPreview();
                Debug.Log($"Ramp at ({cell.Value.x},{cell.Value.y}): {DirectionNames[_pendingRampDirIndex]} -- R to rotate, click to confirm, Esc to cancel");
            }
        }
        else
        {
            if (kb.rKey.wasPressedThisFrame)
            {
                _pendingRampDirIndex = (_pendingRampDirIndex + 1) % 4;
                UpdatePendingRampPreview();
                Debug.Log($"Ramp direction: {DirectionNames[_pendingRampDirIndex]}");
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                CancelPendingRamp();
                Debug.Log("Ramp placement cancelled");
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var dir = CardinalDirections[_pendingRampDirIndex];
                var rampData = _placementService.PlaceRamp(_rampDef, _pendingRampCell, _currentLevel, dir);
                if (rampData != null)
                {
                    _ramps.Add(rampData);
                    SpawnRampVisual(rampData);
                    Debug.Log($"Ramp placed: {DirectionNames[_pendingRampDirIndex]} at ({rampData.BaseCell.x},{rampData.BaseCell.y}) level {rampData.BaseLevel}");
                }
                else
                {
                    Debug.Log($"Cannot place ramp {DirectionNames[_pendingRampDirIndex]}: cells blocked or out of bounds");
                }
                CancelPendingRamp();
            }
        }
    }

    private void UpdatePendingRampPreview()
    {
        if (_pendingRampPreview != null)
            Destroy(_pendingRampPreview);

        if (!_pendingRamp)
            return;

        var dir2D = CardinalDirections[_pendingRampDirIndex];
        var rampStart = _pendingRampCell + dir2D;

        var startPos = SnapPointToWorld(new SnapPoint(_pendingRampCell, _currentLevel, dir2D, SnapPointType.FoundationEdge, null));
        startPos.y = _currentLevel * FactoryGrid.LevelHeight;

        var endPos = startPos
            + new Vector3(dir2D.x, 0f, dir2D.y) * _rampDef.footprintLength * FactoryGrid.CellSize;
        endPos.y = (_currentLevel + 1) * FactoryGrid.LevelHeight;

        var midpoint = (startPos + endPos) * 0.5f;
        var dir3D = (endPos - startPos).normalized;
        var length = Vector3.Distance(startPos, endPos);

        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "RampPreview";
        var collider = ramp.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        ramp.transform.position = midpoint;
        ramp.transform.rotation = Quaternion.LookRotation(dir3D);
        ramp.transform.localScale = new Vector3(0.95f, 0.1f, length);

        bool valid = CanPlaceRampAt(_pendingRampCell, _currentLevel, dir2D);
        SetColor(ramp, valid ? _ghostValidColor : _ghostInvalidColor);

        _pendingRampPreview = ramp;
    }

    private bool CanPlaceRampAt(Vector2Int sourceCell, int level, Vector2Int dir)
    {
        var start = sourceCell + dir;
        for (int i = 0; i < _rampDef.footprintLength; i++)
        {
            var cell = start + dir * i;
            if (!_grid.IsInBounds(cell))
                return false;
            var existing = _grid.GetAt(cell, level);
            if (existing != null && !existing.IsStructural)
                return false;
            foreach (var r in _ramps)
            {
                if (r.BaseLevel == level && r.OccupiedCells.Contains(cell))
                    return false;
            }
        }
        return true;
    }

    private void CancelPendingRamp()
    {
        _pendingRamp = false;
        if (_pendingRampPreview != null)
        {
            Destroy(_pendingRampPreview);
            _pendingRampPreview = null;
        }
    }

    // -- Belt 2-click placement --

    private void HandleBeltInput(Keyboard kb, Mouse mouse)
    {
        var cell = GetCellUnderCursor(mouse);

        if (kb.escapeKey.wasPressedThisFrame)
        {
            CancelBeltPlacement();
            return;
        }

        if (!_beltStartSet)
        {
            // Show ghost on hover
            if (cell.HasValue)
            {
                var existing = _grid.GetAt(cell.Value, _currentLevel);
                bool hasFoundation = existing != null && existing.IsStructural;
                UpdatePlaceGhost(cell.Value, Vector2Int.one, hasFoundation,
                    new Color(0.3f, 0.3f, 0.3f, 0.6f), 0.15f);
            }
            else
            {
                DestroyPlaceGhost();
            }

            if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
            {
                var existing = _grid.GetAt(cell.Value, _currentLevel);
                if (existing == null || !existing.IsStructural)
                {
                    Debug.Log("Belts must be placed on foundations");
                    return;
                }
                _beltStartSet = true;
                _beltStartCell = cell.Value;
                DestroyPlaceGhost();
                Debug.Log($"Belt start: ({_beltStartCell.x},{_beltStartCell.y}) -- click end cell");
            }
        }
        else
        {
            // Show ghost belt line from start to hover cell
            if (cell.HasValue)
                UpdateBeltGhostLine(cell.Value);
            else
                DestroyBeltGhostLine();

            if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
            {
                var endCell = cell.Value;
                var result = _automationService.PlaceBelt(_beltStartCell, endCell, _currentLevel);
                if (result != null)
                {
                    _automationBuildings.Add(result);
                    SpawnBeltVisual(result, _beltStartCell, endCell);
                    int connections = CountConnections(result.Ports);
                    Debug.Log($"Belt placed from ({_beltStartCell.x},{_beltStartCell.y}) to ({endCell.x},{endCell.y}), {connections} connections formed");
                }
                else
                {
                    LogBeltPlacementFailure(_beltStartCell, endCell);
                }
                CancelBeltPlacement();
            }
        }
    }

    private void UpdateBeltGhostLine(Vector2Int endCell)
    {
        DestroyBeltGhostLine();

        // Snap to straight line (prefer longer axis)
        Vector2Int snappedEnd;
        var diff = endCell - _beltStartCell;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            snappedEnd = new Vector2Int(endCell.x, _beltStartCell.y);
        else
            snappedEnd = new Vector2Int(_beltStartCell.x, endCell.y);

        if (snappedEnd == _beltStartCell)
            return;

        var startWorld = _grid.CellToWorld(_beltStartCell, _currentLevel);
        var endWorld = _grid.CellToWorld(snappedEnd, _currentLevel);
        var center = (startWorld + endWorld) * 0.5f + Vector3.up * 0.15f;
        var d = endWorld - startWorld;
        float len = d.magnitude + FactoryGrid.CellSize;

        _beltGhostLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _beltGhostLine.name = "BeltGhost";
        var collider = _beltGhostLine.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        _beltGhostLine.transform.position = center;
        _beltGhostLine.transform.localScale = Mathf.Abs(d.x) > Mathf.Abs(d.z)
            ? new Vector3(len, 0.08f, 0.6f)
            : new Vector3(0.6f, 0.08f, len);

        // Check if all cells along path have foundations
        var dir = new Vector2Int(
            diff.x != 0 ? (snappedEnd.x > _beltStartCell.x ? 1 : -1) : 0,
            diff.y != 0 ? (snappedEnd.y > _beltStartCell.y ? 1 : -1) : 0);
        int length = Mathf.Abs(snappedEnd.x - _beltStartCell.x) + Mathf.Abs(snappedEnd.y - _beltStartCell.y);
        bool valid = true;
        for (int i = 0; i <= length; i++)
        {
            var checkCell = _beltStartCell + dir * i;
            var existing = _grid.GetAt(checkCell, _currentLevel);
            if (existing == null || !existing.IsStructural)
            {
                valid = false;
                break;
            }
        }

        SetColor(_beltGhostLine, valid ? _ghostValidColor : _ghostInvalidColor);
    }

    private void DestroyBeltGhostLine()
    {
        if (_beltGhostLine != null)
        {
            Destroy(_beltGhostLine);
            _beltGhostLine = null;
        }
    }

    private void CancelBeltPlacement()
    {
        _beltStartSet = false;
        DestroyBeltGhostLine();
        DestroyPlaceGhost();
    }

    private void LogBeltPlacementFailure(Vector2Int start, Vector2Int end)
    {
        if (start == end)
        {
            Debug.Log($"Cannot place belt: start and end are the same cell ({start.x},{start.y})");
            return;
        }
        if (start.x != end.x && start.y != end.y)
        {
            Debug.Log($"Cannot place belt from ({start.x},{start.y}) to ({end.x},{end.y}): not a straight line");
            return;
        }

        var diff = end - start;
        var dir = new Vector2Int(
            diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
            diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
        int len = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);

        for (int i = 0; i <= len; i++)
        {
            var check = start + dir * i;
            var existing = _grid.GetAt(check, _currentLevel);
            if (existing == null || !existing.IsStructural)
            {
                Debug.Log($"Cannot place belt from ({start.x},{start.y}) to ({end.x},{end.y}): cell ({check.x},{check.y}) has no foundation");
                return;
            }
        }

        // If all cells have foundations, the issue must be automation overlap
        Debug.Log($"Cannot place belt from ({start.x},{start.y}) to ({end.x},{end.y}): path overlaps an existing building (belt/machine/storage)");
    }

    private static int CountConnections(List<PortNode> ports)
    {
        int count = 0;
        for (int i = 0; i < ports.Count; i++)
        {
            if (ports[i].Connection != null)
                count++;
        }
        return count;
    }

    private static string FormatDirection(Vector2Int dir)
    {
        if (dir == new Vector2Int(1, 0)) return "east";
        if (dir == new Vector2Int(-1, 0)) return "west";
        if (dir == new Vector2Int(0, 1)) return "north";
        if (dir == new Vector2Int(0, -1)) return "south";
        return $"({dir.x},{dir.y})";
    }

    // -- Machine 1-click placement with R rotate --

    private void HandleMachinePlaceInput(Keyboard kb, Mouse mouse)
    {
        var cell = GetCellUnderCursor(mouse);

        if (kb.rKey.wasPressedThisFrame)
        {
            _placeRotation = (_placeRotation + 90) % 360;
            Debug.Log($"Machine rotation: {_placeRotation}");
        }

        if (cell.HasValue)
        {
            var existing = _grid.GetAt(cell.Value, _currentLevel);
            bool hasFoundation = existing != null && existing.IsStructural;
            UpdatePlaceGhost(cell.Value, _smelterDef.size, hasFoundation,
                new Color(0.2f, 0.4f, 0.8f, 0.6f), 0.6f);
            UpdateGhostPortIndicators(cell.Value, _smelterDef.ports, _placeRotation);
        }
        else
        {
            DestroyPlaceGhost();
            ClearGhostPortIndicators();
        }

        if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            ClearGhostPortIndicators();
            var result = _automationService.PlaceMachine(_smelterDef, cell.Value, _placeRotation, _currentLevel);
            if (result != null)
            {
                _automationBuildings.Add(result);
                SpawnMachineVisual(result, cell.Value);
                var machine = (Machine)result.SimulationObject;
                machine.SetRecipe(SmeltIronRecipeId);
                int connections = CountConnections(result.Ports);
                var inputDir = GridRotation.Rotate(new Vector2Int(-1, 0), _placeRotation);
                var outputDir = GridRotation.Rotate(new Vector2Int(1, 0), _placeRotation);
                Debug.Log($"Smelter placed at ({cell.Value.x},{cell.Value.y}) rotation {_placeRotation} (input from {FormatDirection(inputDir)}, output to {FormatDirection(outputDir)}), {connections} connections formed, {_simulation.InserterCount} total inserters");
            }
            else
            {
                Debug.Log($"Cannot place machine at ({cell.Value.x},{cell.Value.y}): no foundation or overlap");
            }
        }
    }

    // -- Storage 1-click placement with R rotate --

    private void HandleStoragePlaceInput(Keyboard kb, Mouse mouse)
    {
        var cell = GetCellUnderCursor(mouse);

        if (kb.rKey.wasPressedThisFrame)
        {
            _placeRotation = (_placeRotation + 90) % 360;
            Debug.Log($"Storage rotation: {_placeRotation}");
        }

        if (cell.HasValue)
        {
            var existing = _grid.GetAt(cell.Value, _currentLevel);
            bool hasFoundation = existing != null && existing.IsStructural;
            UpdatePlaceGhost(cell.Value, _storageDef.size, hasFoundation,
                new Color(0.8f, 0.7f, 0.1f, 0.6f), 0.5f);
        }
        else
        {
            DestroyPlaceGhost();
        }

        if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            var result = _automationService.PlaceStorage(_storageDef, cell.Value, _placeRotation, _currentLevel);
            if (result != null)
            {
                _automationBuildings.Add(result);
                SpawnStorageVisual(result, cell.Value);
                int connections = CountConnections(result.Ports);
                Debug.Log($"Storage placed at ({cell.Value.x},{cell.Value.y}) rotation {_placeRotation}, {connections} connections formed, {_simulation.InserterCount} total inserters");
            }
            else
            {
                Debug.Log($"Cannot place storage at ({cell.Value.x},{cell.Value.y}): no foundation or overlap");
            }
        }
    }

    // -- Ghost preview for machine/storage --

    private void UpdatePlaceGhost(Vector2Int cell, Vector2Int size, bool valid, Color baseColor, float height)
    {
        if (_placeGhost == null)
        {
            _placeGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _placeGhost.name = "PlaceGhost";
            var collider = _placeGhost.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        var worldPos = _grid.CellToWorld(cell, _currentLevel) + Vector3.up * height;
        _placeGhost.transform.position = worldPos;
        _placeGhost.transform.localScale = new Vector3(
            size.x * 0.9f * FactoryGrid.CellSize, height * 2f, size.y * 0.9f * FactoryGrid.CellSize);

        SetColor(_placeGhost, valid ? baseColor : _ghostInvalidColor);
    }

    private void DestroyPlaceGhost()
    {
        if (_placeGhost != null)
        {
            Destroy(_placeGhost);
            _placeGhost = null;
        }
        ClearGhostPortIndicators();
    }

    // -- Delete mode --

    private void HandleDeleteInput(Mouse mouse)
    {
        if (!mouse.leftButton.wasPressedThisFrame)
            return;

        var cell = GetCellUnderCursor(mouse);
        if (!cell.HasValue)
            return;

        // Priority 1: automation buildings (belts, machines, storage)
        for (int i = _automationBuildings.Count - 1; i >= 0; i--)
        {
            var ab = _automationBuildings[i];
            var bd = ab.BuildingData;
            if (bd.Level != _currentLevel)
                continue;

            // Check if this automation building covers the clicked cell
            bool covers = false;
            if (bd.BuildingId == "belt")
            {
                // Belts: check all port cells and cells between
                if (ab.Ports.Count == 2)
                {
                    var startCell = ab.Ports[0].Cell;
                    var endCell = ab.Ports[1].Cell;
                    var diff = endCell - startCell;
                    var dir = new Vector2Int(
                        diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
                        diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
                    int len = Mathf.Abs(diff.x) + Mathf.Abs(diff.y);
                    for (int j = 0; j <= len; j++)
                    {
                        if (startCell + dir * j == cell.Value)
                        {
                            covers = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                covers = cell.Value.x >= bd.Origin.x
                    && cell.Value.x < bd.Origin.x + bd.Size.x
                    && cell.Value.y >= bd.Origin.y
                    && cell.Value.y < bd.Origin.y + bd.Size.y;
            }

            if (!covers)
                continue;

            _automationService.Remove(bd);
            if (bd.Instance != null) Destroy(bd.Instance);
            _automationBuildings.RemoveAt(i);
            Debug.Log($"Automation building removed at ({cell.Value.x},{cell.Value.y})");
            return;
        }

        // Priority 2: walls
        for (int i = _walls.Count - 1; i >= 0; i--)
        {
            var wall = _walls[i];
            if (wall.Cell == cell.Value && wall.Level == _currentLevel)
            {
                _placementService.RemoveWall(wall);
                if (wall.Instance != null) Destroy(wall.Instance);
                _walls.RemoveAt(i);
                Debug.Log("Wall removed");
                return;
            }
        }

        // Priority 3: ramps
        for (int i = _ramps.Count - 1; i >= 0; i--)
        {
            var ramp = _ramps[i];
            if (ramp.OccupiedCells.Contains(cell.Value) && ramp.BaseLevel == _currentLevel)
            {
                _placementService.RemoveRamp(ramp);
                if (ramp.Instance != null) Destroy(ramp.Instance);
                _ramps.RemoveAt(i);
                Debug.Log("Ramp removed");
                return;
            }
        }

        // Priority 4: foundations
        for (int i = _foundations.Count - 1; i >= 0; i--)
        {
            var foundation = _foundations[i];
            if (foundation.Level != _currentLevel)
                continue;

            bool inFootprint = cell.Value.x >= foundation.Origin.x
                && cell.Value.x < foundation.Origin.x + foundation.Size.x
                && cell.Value.y >= foundation.Origin.y
                && cell.Value.y < foundation.Origin.y + foundation.Size.y;

            if (!inFootprint)
                continue;

            if (_placementService.RemoveFoundation(foundation))
            {
                if (foundation.Instance != null) Destroy(foundation.Instance);
                _foundations.RemoveAt(i);
                Debug.Log("Foundation removed");
            }
            else
            {
                Debug.Log("Cannot remove foundation: walls still attached");
            }
            return;
        }
    }

    // -- Visual spawning --

    private void SpawnFoundationVisual(BuildingData data, Vector2Int cell, int level)
    {
        var worldPos = _grid.CellToWorld(cell, level);
        var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = $"Foundation_{cell.x}_{cell.y}_L{level}";
        tile.transform.position = worldPos + Vector3.up * 0.05f;
        tile.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
        SetColor(tile, Color.white);
        data.Instance = tile;
    }

    private void SpawnWallVisual(WallData wallData)
    {
        var cellCenter = _grid.CellToWorld(wallData.Cell, wallData.Level);
        var edgeDir = wallData.EdgeDirection;
        var edgeOffset = new Vector3(
            edgeDir.x * 0.5f * FactoryGrid.CellSize, 0f,
            edgeDir.y * 0.5f * FactoryGrid.CellSize);
        var wallPos = cellCenter + edgeOffset + Vector3.up * FactoryGrid.LevelHeight * 0.5f;

        float yRotation = Mathf.Atan2(edgeDir.x, edgeDir.y) * Mathf.Rad2Deg;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{wallData.Cell.x}_{wallData.Cell.y}_L{wallData.Level}";
        wall.transform.position = wallPos;
        wall.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        wall.transform.localScale = new Vector3(0.95f, FactoryGrid.LevelHeight, 0.1f);
        SetColor(wall, new Color(0.6f, 0.6f, 0.6f));
        wallData.Instance = wall;
    }

    private void SpawnRampVisual(RampData rampData)
    {
        var dir2D = rampData.Direction;
        var snapCell = rampData.BaseCell - dir2D;
        var startPos = SnapPointToWorld(new SnapPoint(snapCell, rampData.BaseLevel, dir2D, SnapPointType.FoundationEdge, null));
        startPos.y = rampData.BaseLevel * FactoryGrid.LevelHeight;

        var endPos = startPos
            + new Vector3(dir2D.x, 0f, dir2D.y) * rampData.FootprintLength * FactoryGrid.CellSize;
        endPos.y = (rampData.BaseLevel + 1) * FactoryGrid.LevelHeight;

        var midpoint = (startPos + endPos) * 0.5f;
        var dir3D = (endPos - startPos).normalized;
        var length = Vector3.Distance(startPos, endPos);

        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = $"Ramp_{rampData.BaseCell.x}_{rampData.BaseCell.y}_L{rampData.BaseLevel}";
        ramp.transform.position = midpoint;
        ramp.transform.rotation = Quaternion.LookRotation(dir3D);
        ramp.transform.localScale = new Vector3(0.95f, 0.1f, length);
        SetColor(ramp, new Color(0.76f, 0.6f, 0.42f));
        rampData.Instance = ramp;
    }

    private void SpawnBeltVisual(PlacementResult result, Vector2Int startCell, Vector2Int endCell)
    {
        var startWorld = _grid.CellToWorld(startCell, _currentLevel);
        var endWorld = _grid.CellToWorld(endCell, _currentLevel);
        var center = (startWorld + endWorld) * 0.5f + Vector3.up * 0.15f;
        var diff = endWorld - startWorld;
        float len = diff.magnitude + FactoryGrid.CellSize;

        var belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        belt.name = $"Belt_{startCell.x}_{startCell.y}_to_{endCell.x}_{endCell.y}";
        var collider = belt.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        belt.transform.position = center;
        belt.transform.localScale = Mathf.Abs(diff.x) > Mathf.Abs(diff.z)
            ? new Vector3(len, 0.08f, 0.6f)
            : new Vector3(0.6f, 0.08f, len);
        SetColor(belt, new Color(0.3f, 0.3f, 0.3f));
        result.BuildingData.Instance = belt;
    }

    private void SpawnMachineVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _grid.CellToWorld(cell, _currentLevel);
        var machine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        machine.name = $"Machine_{cell.x}_{cell.y}";
        var collider = machine.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        machine.transform.position = worldPos + Vector3.up * 0.6f;
        machine.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        SetColor(machine, new Color(0.2f, 0.4f, 0.8f));
        result.BuildingData.Instance = machine;

        // Port direction indicators: blue arrow = input, red arrow = output
        SpawnPortIndicators(result.Ports, worldPos, machine.transform);
    }

    private void SpawnStorageVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _grid.CellToWorld(cell, _currentLevel);
        var storage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        storage.name = $"Storage_{cell.x}_{cell.y}";
        var collider = storage.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        storage.transform.position = worldPos + Vector3.up * 0.5f;
        storage.transform.localScale = new Vector3(0.85f, 1.0f, 0.85f);
        SetColor(storage, new Color(0.8f, 0.7f, 0.1f));
        result.BuildingData.Instance = storage;
    }

    // -- Port direction indicators --

    private void SpawnPortIndicators(List<PortNode> ports, Vector3 buildingWorldPos, Transform parent)
    {
        float cellSize = FactoryGrid.CellSize;
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            bool isInput = port.Type == PortType.Input;
            var color = isInput ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.3f, 0.2f);

            // Small indicator on the face of the building
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = isInput ? "PortIn" : "PortOut";
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var dir3 = new Vector3(port.Direction.x, 0, port.Direction.y);
            indicator.transform.position = buildingWorldPos + Vector3.up * 0.6f + dir3 * (cellSize * 0.45f);

            // Flatten into a stripe on the building face
            if (port.Direction.x != 0)
                indicator.transform.localScale = new Vector3(0.15f, 0.3f, 0.6f);
            else
                indicator.transform.localScale = new Vector3(0.6f, 0.3f, 0.15f);

            SetColor(indicator, color);
            indicator.transform.SetParent(parent, true);
            _portIndicators.Add(indicator);
        }
    }

    private void UpdateGhostPortIndicators(Vector2Int cell, MachinePort[] portDefs, int rotation)
    {
        ClearGhostPortIndicators();

        float cellSize = FactoryGrid.CellSize;
        var worldPos = _grid.CellToWorld(cell, _currentLevel);

        for (int i = 0; i < portDefs.Length; i++)
        {
            var def = portDefs[i];
            var rotatedDir = GridRotation.Rotate(def.direction, rotation);
            bool isInput = def.type == PortType.Input;
            var color = isInput ? new Color(0.2f, 0.6f, 1f, 0.8f) : new Color(1f, 0.3f, 0.2f, 0.8f);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = isInput ? "GhostPortIn" : "GhostPortOut";
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var dir3 = new Vector3(rotatedDir.x, 0, rotatedDir.y);
            indicator.transform.position = worldPos + Vector3.up * 0.6f + dir3 * (cellSize * 0.45f);

            if (rotatedDir.x != 0)
                indicator.transform.localScale = new Vector3(0.15f, 0.3f, 0.6f);
            else
                indicator.transform.localScale = new Vector3(0.6f, 0.3f, 0.15f);

            SetColor(indicator, color);
            _ghostPortIndicators.Add(indicator);
        }
    }

    private void ClearGhostPortIndicators()
    {
        for (int i = 0; i < _ghostPortIndicators.Count; i++)
        {
            if (_ghostPortIndicators[i] != null)
                Destroy(_ghostPortIndicators[i]);
        }
        _ghostPortIndicators.Clear();
    }

    // -- Belt item visuals --

    private void UpdateBeltItemVisuals()
    {
        var belts = _simulation.GetBelts();
        int totalItems = 0;
        for (int b = 0; b < belts.Count; b++)
            totalItems += belts[b].ItemCount;

        // Grow pool as needed
        while (_beltItemPool.Count < totalItems)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BeltItem";
            cube.transform.localScale = Vector3.one * 0.25f;
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            cube.SetActive(false);
            _beltItemPool.Add(cube);
        }

        int poolIdx = 0;
        for (int b = 0; b < belts.Count; b++)
        {
            var belt = belts[b];
            belt.GetItemPositions(_positionBuffer);

            // Find the visual for this belt via automation buildings
            PlacementResult beltResult = null;
            foreach (var ab in _automationBuildings)
            {
                if (ab.SimulationObject == belt)
                {
                    beltResult = ab;
                    break;
                }
            }

            if (beltResult == null || beltResult.BuildingData.Instance == null)
            {
                poolIdx += _positionBuffer.Count;
                continue;
            }

            var beltGO = beltResult.BuildingData.Instance;
            float halfLen = Mathf.Max(beltGO.transform.localScale.x, beltGO.transform.localScale.z) * 0.5f;
            Vector3 center = beltGO.transform.position;

            // Determine belt direction from ports
            Vector3 dir3D = Vector3.right;
            if (beltResult.Ports.Count == 2)
            {
                var startWorld = _grid.CellToWorld(beltResult.Ports[0].Cell, beltResult.BuildingData.Level);
                var endWorld = _grid.CellToWorld(beltResult.Ports[1].Cell, beltResult.BuildingData.Level);
                dir3D = (endWorld - startWorld).normalized;
            }

            Vector3 startPos = center - dir3D * halfLen;
            Vector3 endPos = center + dir3D * halfLen;

            for (int i = 0; i < _positionBuffer.Count; i++)
            {
                if (poolIdx >= _beltItemPool.Count) break;
                var cube = _beltItemPool[poolIdx];
                cube.SetActive(true);
                cube.transform.position = Vector3.Lerp(startPos, endPos, _positionBuffer[i])
                                          + Vector3.up * 0.2f;

                // Color by item type based on belt position in chain
                // Items on belts before machines are ore (brown), after are ingots (silver)
                bool isBeforeMachine = IsBeforeMachine(belt);
                SetColor(cube, isBeforeMachine
                    ? new Color(0.6f, 0.3f, 0.1f)  // brown = ore
                    : new Color(0.7f, 0.7f, 0.8f)); // silver = ingot
                poolIdx++;
            }
        }

        // Hide unused pool items
        for (int i = poolIdx; i < _beltItemPool.Count; i++)
            _beltItemPool[i].SetActive(false);
    }

    private bool IsBeforeMachine(BeltSegment belt)
    {
        // Check if this belt's output port connects to a machine input
        foreach (var ab in _automationBuildings)
        {
            if (ab.SimulationObject != belt) continue;
            if (ab.Ports.Count < 2) return true;
            var outputPort = ab.Ports[1]; // output port
            if (outputPort.Connection is Inserter)
                return true; // inserter to machine = this is an input belt
        }
        return false;
    }

    // -- Helpers --

    private Vector2Int? GetCellUnderCursor(Mouse mouse)
    {
        var worldPos = GetWorldPosUnderCursor(mouse);
        if (!worldPos.HasValue)
            return null;
        return _grid.WorldToCell(worldPos.Value);
    }

    private Vector3? GetWorldPosUnderCursor(Mouse mouse)
    {
        var camera = Camera.main;
        if (camera == null)
            return null;

        var mousePos = mouse.position.ReadValue();
        var ray = camera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, PhysicsLayers.PlacementMask))
            return hit.point;

        return null;
    }

    private Vector3 SnapPointToWorld(SnapPoint sp)
    {
        var cellCenter = _grid.CellToWorld(sp.Cell, sp.Level);
        var edgeOffset = new Vector3(
            sp.EdgeDirection.x * 0.5f * FactoryGrid.CellSize, 0f,
            sp.EdgeDirection.y * 0.5f * FactoryGrid.CellSize);
        return cellCenter + edgeOffset;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    private static void DrawLine(ref float y, float x, float w, float h, string text, bool bold = false)
    {
        var style = GUI.skin.label;
        if (bold)
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        GUI.Label(new Rect(x, y, w, h), text, style);
        y += h;
    }
}
