using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared tool handling for playtest bootstrappers. Added via AddComponent by each
/// dev's bootstrapper, initialized via Initialize() pattern (no Awake logic).
/// Handles all structural/automation tool input, ghost previews, visuals, belt items,
/// and shared OnGUI stats.
/// </summary>
public class PlaytestToolController : MonoBehaviour
{
    public enum ToolMode { None, Foundation, Wall, Ramp, Delete, Belt, MachinePlace, StoragePlace, TurretPlace }

    // -- Public API --

    public ToolMode CurrentTool => _currentTool;
    public int CurrentLevel => _currentLevel;
    public float GuiNextY { get; set; }
    public bool SuppressInput { get; set; }

    public List<BuildingData> Foundations => _foundations;
    public List<WallData> Walls => _walls;
    public List<RampData> Ramps => _ramps;
    public List<PlacementResult> AutomationBuildings => _automationBuildings;

    // -- Private state --

    private PlaytestContext _ctx;
    private HotbarPage _buildPage;
    private GameObject _groundPlane;
    private WaveControllerBehaviour _waveController;
    private readonly Dictionary<ToolMode, Action<Keyboard, Mouse>> _customToolHandlers = new();

    // -- Static data --

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    private static readonly string[] DirectionNames = { "North", "East", "South", "West" };

    private static readonly Key[] DigitKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Dictionary<string, ToolMode> BuildToolMap = new()
    {
        { "foundation", ToolMode.Foundation },
        { "wall", ToolMode.Wall },
        { "ramp", ToolMode.Ramp },
        { "belt", ToolMode.Belt },
        { "machine", ToolMode.MachinePlace },
        { "storage", ToolMode.StoragePlace },
        { "delete", ToolMode.Delete },
        { "turret", ToolMode.TurretPlace },
    };

    private static readonly Color GhostValidColor = new Color(0f, 1f, 0f, 0.4f);
    private static readonly Color GhostInvalidColor = new Color(1f, 0f, 0f, 0.4f);

    // -- Tracking --

    private readonly List<BuildingData> _foundations = new();
    private readonly List<WallData> _walls = new();
    private readonly List<RampData> _ramps = new();
    private readonly List<PlacementResult> _automationBuildings = new();

    // -- Tool state --

    private ToolMode _currentTool = ToolMode.None;
    private int _currentLevel;

    // Ramp 2-step
    private bool _pendingRamp;
    private Vector2Int _pendingRampCell;
    private int _pendingRampDirIndex;
    private GameObject _pendingRampPreview;

    // Foundation drag
    private bool _isDragging;
    private Vector2Int _dragStart;
    private Vector2Int _dragEnd;
    private readonly List<GameObject> _ghostPool = new();

    // Wall 2-step
    private bool _pendingWall;
    private Vector2Int _pendingWallCell;
    private int _pendingWallDirIndex;
    private GameObject _pendingWallPreview;

    // Belt 2-click
    private bool _beltStartSet;
    private Vector2Int _beltStartCell;
    private GameObject _beltGhostLine;

    // Machine/storage rotation
    private int _placeRotation;

    // Ghost preview
    private GameObject _placeGhost;
    private readonly List<GameObject> _ghostPortIndicators = new();

    // Port indicators on placed machines
    private readonly List<GameObject> _portIndicators = new();

    // Belt item visuals
    private readonly List<GameObject> _beltItemPool = new();
    private readonly List<float> _positionBuffer = new();

    // -- Setup --

    public void Initialize(PlaytestContext ctx, HotbarPage buildPage, GameObject groundPlane)
    {
        _ctx = ctx;
        _buildPage = buildPage;
        _groundPlane = groundPlane;
        StartCoroutine(WireHUD());
    }

    /// <summary>
    /// Creates a build page with the 7 shared tool slots (foundation through delete).
    /// Both standalone bootstrappers and MasterPlaytestSetup call this to avoid duplication.
    /// Dev-specific providers add their own entries starting at slot 7.
    /// </summary>
    public static HotbarPage CreateSharedBuildPage()
    {
        var page = new HotbarPage("Build", PlayerInventory.HotbarSlots);

        void Set(int slot, string id, string name, Color color)
        {
            page.Entries[slot] = new HotbarEntry { Id = id, DisplayName = name, Color = color };
        }

        Set(0, "foundation", "Found", new Color(0.4f, 0.4f, 0.4f, 0.8f));
        Set(1, "wall", "Wall", new Color(0.5f, 0.5f, 0.5f, 0.8f));
        Set(2, "ramp", "Ramp", new Color(0.5f, 0.6f, 0.5f, 0.8f));
        Set(3, "belt", "Belt", new Color(0.3f, 0.5f, 0.7f, 0.8f));
        Set(4, "machine", "Machine", new Color(0.7f, 0.5f, 0.2f, 0.8f));
        Set(5, "storage", "Storage", new Color(0.6f, 0.4f, 0.3f, 0.8f));
        Set(6, "delete", "Delete", new Color(0.8f, 0.2f, 0.2f, 0.8f));

        return page;
    }

    public static GameObject CreateGroundPlane()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "GridPlane";
        ground.layer = PhysicsLayers.GridPlane;
        ground.transform.position = new Vector3(
            FactoryGrid.Width * FactoryGrid.CellSize * 0.5f,
            -0.05f,
            FactoryGrid.Height * FactoryGrid.CellSize * 0.5f);
        ground.transform.localScale = new Vector3(
            FactoryGrid.Width * FactoryGrid.CellSize,
            0.1f,
            FactoryGrid.Height * FactoryGrid.CellSize);
        var renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
        return ground;
    }

    public static void BakeNavMesh(GameObject groundPlane)
    {
#if UNITY_EDITOR
        groundPlane.isStatic = true;
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        Debug.Log("playtest: navmesh baked");
#else
        Debug.LogWarning("playtest: navmesh baking not available outside editor");
#endif
    }

    public void SetWaveController(WaveControllerBehaviour wc)
    {
        _waveController = wc;
    }

    public void SetTool(ToolMode mode)
    {
        var old = _currentTool;
        CancelAllPending();
        _currentTool = mode;
        PlaytestLogger.Log($"event: tool changed {old} -> {mode}");
        if (mode == ToolMode.MachinePlace || mode == ToolMode.StoragePlace)
            _placeRotation = 0;
    }

    public void RegisterToolHandler(ToolMode mode, Action<Keyboard, Mouse> handler)
    {
        _customToolHandlers[mode] = handler;
    }

    // -- HUD wiring coroutine --

    private IEnumerator WireHUD()
    {
        yield return null;

        var player = _ctx.PlayerObject;
        var health = player.GetComponent<HealthBehaviour>();
        var inventory = player.GetComponent<PlayerInventory>();
        var weapon = player.GetComponent<WeaponBehaviour>();
        var cam = player.GetComponentInChildren<Camera>();

        var cameraShake = cam != null ? cam.GetComponent<CameraShake>() : null;
        var wc = _waveController != null ? _waveController.Controller : null;

        _ctx.PlayerHUD.Initialize(
            health != null ? health.Health : null,
            weapon != null ? weapon.Weapon : null,
            cameraShake, wc);
        _ctx.PlayerHUD.InitializeInventory(inventory, cam);

        var inventoryUI = _ctx.PlayerHUD.GetComponent<InventoryUI>();
        inventoryUI.Initialize(inventory);

        var itemsPage = new HotbarPage("Items", PlayerInventory.HotbarSlots);
        _ctx.PlayerHUD.SetPages(new[] { itemsPage, _buildPage });

        _ctx.PlayerHUD.OnBuildToolSelected += OnHotbarBuildToolSelected;
        _ctx.PlayerHUD.OnPageChanged += OnHotbarPageChanged;

        Debug.Log("playtest: HUD wired to player (combat + inventory)");
    }

    // -- Unity callbacks --

    private void FixedUpdate()
    {
        if (_ctx != null && _ctx.Simulation != null)
            _ctx.Simulation.Tick(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (SuppressInput) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        HandleToolSelection(kb);
        HandleDigitKeys(kb);
        HandleLevelChange(kb);
        HandleFillStorage(kb, mouse);
        HandleWaveTrigger(kb);

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
            default:
                if (_customToolHandlers.TryGetValue(_currentTool, out var handler))
                    handler(kb, mouse);
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

        int lineCount = 16;
        if (_currentTool == ToolMode.Wall && _pendingWall) lineCount++;
        if (_currentTool == ToolMode.Wall && !_pendingWall) lineCount++;
        if (_currentTool == ToolMode.Ramp) lineCount++;
        if (_currentTool == ToolMode.Belt) lineCount++;
        if (_isDragging) lineCount++;

        GUI.Box(new Rect(x - 4, y - 4, w + 8, h * lineCount + 8), "");

        DrawLine(ref y, x, w, h, "SLOPWORKS PLAYTEST", true);
        DrawLine(ref y, x, w, h, $"Tool: {_currentTool}  |  Level: {_currentLevel}");
        y += 4;
        DrawLine(ref y, x, w, h, $"Foundations: {_foundations.Count}  |  Walls: {_walls.Count}  |  Ramps: {_ramps.Count}");
        DrawLine(ref y, x, w, h, $"Snap points: {_ctx.SnapRegistry.Count}");

        int beltCount = _ctx.Simulation.BeltCount;
        int machineCount = _ctx.Simulation.MachineCount;
        int inserterCount = _ctx.Simulation.InserterCount;
        int storageCount = 0;
        foreach (var ab in _automationBuildings)
            if (ab.SimulationObject is StorageContainer) storageCount++;

        DrawLine(ref y, x, w, h, $"Belts: {beltCount}  |  Machines: {machineCount}  |  Storage: {storageCount}");
        DrawLine(ref y, x, w, h, $"Auto-inserters: {inserterCount}  |  Belt links: {_ctx.Simulation.BeltNetwork.ConnectionCount}");
        DrawLine(ref y, x, w, h, $"Port nodes: {_ctx.PortRegistry.Count}");

        var wc = _waveController != null ? _waveController.Controller : null;
        string waveInfo = wc != null
            ? $"Wave: {wc.CurrentWave}/{wc.TotalWaves}  |  Enemies: {wc.EnemiesRemaining}"
            : "Wave: --";
        var healthBeh = _ctx.PlayerObject != null ? _ctx.PlayerObject.GetComponent<HealthBehaviour>() : null;
        var healthComp = healthBeh != null ? healthBeh.Health : null;
        var weapon = _ctx.WeaponBehaviour != null ? _ctx.WeaponBehaviour.Weapon : null;
        string healthInfo = healthComp != null ? $"HP: {healthComp.CurrentHealth:F0}/{healthComp.MaxHealth:F0}" : "HP: --";
        string ammoInfo = weapon != null ? $"Ammo: {weapon.CurrentAmmo}/{_ctx.WeaponDef.magazineSize}" : "Ammo: --";
        DrawLine(ref y, x, w, h, $"{waveInfo}  |  {healthInfo}  |  {ammoInfo}");

        y += 4;
        DrawLine(ref y, x, w, h, "[B] Toggle build/items  [1-9] Select tool/slot  [V] FPS/Iso");
        DrawLine(ref y, x, w, h, "[PgUp/PgDn] Level  [R] Rotate  [Esc] Cancel  [F] Fill");
        DrawLine(ref y, x, w, h, "[Tab] Inventory  [E] Interact  [WASD] Move  [Space] Jump");
        DrawLine(ref y, x, w, h, "[G] Spawn wave  |  Port colors: BLUE=input RED=output");

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

        GuiNextY = y;
    }

    private void OnDestroy()
    {
        if (_ctx != null && _ctx.PlayerHUD != null)
        {
            _ctx.PlayerHUD.OnBuildToolSelected -= OnHotbarBuildToolSelected;
            _ctx.PlayerHUD.OnPageChanged -= OnHotbarPageChanged;
        }
    }

    // -- Hotbar events --

    private void OnHotbarBuildToolSelected(int pageIndex, int slotIndex, string entryId)
    {
        PlaytestLogger.Log($"input: build tool selected {entryId} slot {slotIndex}");
        if (BuildToolMap.TryGetValue(entryId, out var toolMode))
        {
            CancelAllPending();
            _currentTool = toolMode;
            if (toolMode == ToolMode.MachinePlace || toolMode == ToolMode.StoragePlace)
                _placeRotation = 0;
            Debug.Log($"build tool: {entryId} -> {toolMode}");
        }
    }

    private void OnHotbarPageChanged(int pageIndex)
    {
        PlaytestLogger.Log($"event: page changed to {pageIndex}");
        if (pageIndex == 0)
        {
            CancelAllPending();
            _currentTool = ToolMode.None;
            _ctx.PlayerHUD?.SetBuildModeVisible(false);
            if (_ctx.WeaponBehaviour != null)
            {
                _ctx.WeaponBehaviour.enabled = true;
                Debug.Log("weapon: re-enabled (items page)");
            }
        }
        else
        {
            _ctx.PlayerHUD?.SetBuildModeVisible(true);
            if (_ctx.WeaponBehaviour != null)
            {
                _ctx.WeaponBehaviour.enabled = false;
                Debug.Log("weapon: disabled (build page)");
            }
        }
    }

    // -- Tool selection --

    private void HandleToolSelection(Keyboard kb)
    {
        if (kb.bKey.wasPressedThisFrame && _ctx.PlayerHUD != null)
            _ctx.PlayerHUD.TogglePage();

        if (kb.escapeKey.wasPressedThisFrame)
        {
            CancelAllPending();
            _currentTool = ToolMode.None;
            _ctx.PlayerHUD?.SetPage(0);
        }
    }

    private void HandleDigitKeys(Keyboard kb)
    {
        if (_ctx.PlayerHUD == null) return;
        if (_ctx.PlayerHUD.CurrentPageIndex == 0) return;

        for (int i = 0; i < DigitKeys.Length; i++)
        {
            if (kb[DigitKeys[i]].wasPressedThisFrame)
            {
                _ctx.PlayerHUD.OnSlotPressed(i);
                break;
            }
        }
    }

    private void HandleLevelChange(Keyboard kb)
    {
        if (kb.pageUpKey.wasPressedThisFrame)
        {
            int old = _currentLevel;
            _currentLevel = Mathf.Min(_currentLevel + 1, FactoryGrid.MaxLevels - 1);
            UpdateGroundPlaneHeight();
            PlaytestLogger.Log($"input: level {old} -> {_currentLevel}");
            Debug.Log($"Level: {_currentLevel}");
        }
        else if (kb.pageDownKey.wasPressedThisFrame)
        {
            int old = _currentLevel;
            _currentLevel = Mathf.Max(_currentLevel - 1, 0);
            UpdateGroundPlaneHeight();
            PlaytestLogger.Log($"input: level {old} -> {_currentLevel}");
            Debug.Log($"Level: {_currentLevel}");
        }
    }

    private void HandleFillStorage(Keyboard kb, Mouse mouse)
    {
        if (!kb.fKey.wasPressedThisFrame)
            return;

        PlaytestLogger.Log("input: key F (fill storage)");

        var cell = GetCellUnderCursor(mouse);
        if (!cell.HasValue)
            return;

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
                while (storage.TryInsert(PlaytestContext.IronScrap))
                    added++;
                Debug.Log($"Filled storage at ({bd.Origin.x},{bd.Origin.y}) with {added} iron scrap (total: {storage.GetTotalItemCount()})");
                return;
            }
        }

        Debug.Log($"No storage at ({cell.Value.x},{cell.Value.y}) to fill");
    }

    private void HandleWaveTrigger(Keyboard kb)
    {
        if (kb.gKey.wasPressedThisFrame && _waveController != null)
        {
            PlaytestLogger.Log("input: key G (spawn wave)");
            _waveController.BeginNextWave();
            Debug.Log("playtest: wave triggered via G key");
        }
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
            if (mouse.leftButton.wasPressedThisFrame)
                Debug.Log("foundation: click ignored, no grid cell under cursor");
            HideGhosts();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlaytestLogger.Log($"input: LMB | tool=Foundation cell=({cell.Value.x},{cell.Value.y})");
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
                var data = _ctx.PlacementService.PlaceFoundation(_ctx.FoundationDef, cellPos, _currentLevel);
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
                bool valid = _ctx.Grid.CanPlace(cellPos, Vector2Int.one, _currentLevel);
                var worldPos = _ctx.Grid.CellToWorld(cellPos, _currentLevel) + Vector3.up * 0.05f;

                var ghost = _ghostPool[idx];
                ghost.SetActive(true);
                ghost.transform.position = worldPos;
                SetColor(ghost, valid ? GhostValidColor : GhostInvalidColor);
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
                {
                    Debug.Log("wall: click ignored, no grid cell under cursor");
                    return;
                }

                var building = _ctx.Grid.GetAt(cell.Value, _currentLevel);
                if (building == null || !building.IsStructural)
                {
                    Debug.Log("Walls must be placed on a foundation cell");
                    return;
                }

                PlaytestLogger.Log($"input: LMB | tool=Wall cell=({cell.Value.x},{cell.Value.y})");
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
                var wallData = _ctx.PlacementService.PlaceWall(_ctx.WallDef, _pendingWallCell, _currentLevel, dir);
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
        var cellCenter = _ctx.Grid.CellToWorld(_pendingWallCell, _currentLevel);
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
        SetColor(wall, GhostValidColor);

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
                {
                    Debug.Log("ramp: click ignored, no grid cell under cursor");
                    return;
                }

                var building = _ctx.Grid.GetAt(cell.Value, _currentLevel);
                if (building == null || !building.IsStructural)
                {
                    Debug.Log("Ramps must start from a foundation cell");
                    return;
                }

                PlaytestLogger.Log($"input: LMB | tool=Ramp cell=({cell.Value.x},{cell.Value.y})");
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
                var rampData = _ctx.PlacementService.PlaceRamp(_ctx.RampDef, _pendingRampCell, _currentLevel, dir);
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
            + new Vector3(dir2D.x, 0f, dir2D.y) * _ctx.RampDef.footprintLength * FactoryGrid.CellSize;
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
        SetColor(ramp, valid ? GhostValidColor : GhostInvalidColor);

        _pendingRampPreview = ramp;
    }

    private bool CanPlaceRampAt(Vector2Int sourceCell, int level, Vector2Int dir)
    {
        var start = sourceCell + dir;
        for (int i = 0; i < _ctx.RampDef.footprintLength; i++)
        {
            var cell = start + dir * i;
            if (!_ctx.Grid.IsInBounds(cell))
                return false;
            var existing = _ctx.Grid.GetAt(cell, level);
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
            if (cell.HasValue)
            {
                var existing = _ctx.Grid.GetAt(cell.Value, _currentLevel);
                bool hasFoundation = existing != null && existing.IsStructural;
                UpdatePlaceGhost(cell.Value, Vector2Int.one, hasFoundation,
                    new Color(0.3f, 0.3f, 0.3f, 0.6f), 0.15f);
            }
            else
            {
                DestroyPlaceGhost();
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!cell.HasValue)
                {
                    Debug.Log("belt: click ignored, no grid cell under cursor");
                }
                else
                {
                    var existing = _ctx.Grid.GetAt(cell.Value, _currentLevel);
                    if (existing == null || !existing.IsStructural)
                    {
                        Debug.Log("Belts must be placed on foundations");
                        return;
                    }
                    PlaytestLogger.Log($"input: LMB | tool=Belt start cell=({cell.Value.x},{cell.Value.y})");
                    _beltStartSet = true;
                    _beltStartCell = cell.Value;
                    DestroyPlaceGhost();
                    Debug.Log($"Belt start: ({_beltStartCell.x},{_beltStartCell.y}) -- click end cell");
                }
            }
        }
        else
        {
            if (cell.HasValue)
                UpdateBeltGhostLine(cell.Value);
            else
                DestroyBeltGhostLine();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!cell.HasValue)
                {
                    Debug.Log("belt: end click ignored, no grid cell under cursor");
                }
                else
                {
                    PlaytestLogger.Log($"input: LMB | tool=Belt end cell=({cell.Value.x},{cell.Value.y})");
                    var endCell = cell.Value;
                    var result = _ctx.AutomationService.PlaceBelt(_beltStartCell, endCell, _currentLevel);
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
    }

    private void UpdateBeltGhostLine(Vector2Int endCell)
    {
        DestroyBeltGhostLine();

        Vector2Int snappedEnd;
        var diff = endCell - _beltStartCell;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            snappedEnd = new Vector2Int(endCell.x, _beltStartCell.y);
        else
            snappedEnd = new Vector2Int(_beltStartCell.x, endCell.y);

        if (snappedEnd == _beltStartCell)
            return;

        var startWorld = _ctx.Grid.CellToWorld(_beltStartCell, _currentLevel);
        var endWorld = _ctx.Grid.CellToWorld(snappedEnd, _currentLevel);
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

        var dir = new Vector2Int(
            diff.x != 0 ? (snappedEnd.x > _beltStartCell.x ? 1 : -1) : 0,
            diff.y != 0 ? (snappedEnd.y > _beltStartCell.y ? 1 : -1) : 0);
        int length = Mathf.Abs(snappedEnd.x - _beltStartCell.x) + Mathf.Abs(snappedEnd.y - _beltStartCell.y);
        bool valid = true;
        for (int i = 0; i <= length; i++)
        {
            var checkCell = _beltStartCell + dir * i;
            var existing = _ctx.Grid.GetAt(checkCell, _currentLevel);
            if (existing == null || !existing.IsStructural)
            {
                valid = false;
                break;
            }
        }

        SetColor(_beltGhostLine, valid ? GhostValidColor : GhostInvalidColor);
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
            var existing = _ctx.Grid.GetAt(check, _currentLevel);
            if (existing == null || !existing.IsStructural)
            {
                Debug.Log($"Cannot place belt from ({start.x},{start.y}) to ({end.x},{end.y}): cell ({check.x},{check.y}) has no foundation");
                return;
            }
        }

        Debug.Log($"Cannot place belt from ({start.x},{start.y}) to ({end.x},{end.y}): path overlaps an existing building (belt/machine/storage)");
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
            var existing = _ctx.Grid.GetAt(cell.Value, _currentLevel);
            bool hasFoundation = existing != null && existing.IsStructural;
            UpdatePlaceGhost(cell.Value, _ctx.SmelterDef.size, hasFoundation,
                new Color(0.2f, 0.4f, 0.8f, 0.6f), 0.6f);
            UpdateGhostPortIndicators(cell.Value, _ctx.SmelterDef.ports, _placeRotation);
        }
        else
        {
            DestroyPlaceGhost();
            ClearGhostPortIndicators();
        }

        if (mouse.leftButton.wasPressedThisFrame && !cell.HasValue)
        {
            Debug.Log("machine: click ignored, no grid cell under cursor");
        }
        else if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            PlaytestLogger.Log($"input: LMB | tool=Machine cell=({cell.Value.x},{cell.Value.y})");
            ClearGhostPortIndicators();
            var result = _ctx.AutomationService.PlaceMachine(_ctx.SmelterDef, cell.Value, _placeRotation, _currentLevel);
            if (result != null)
            {
                _automationBuildings.Add(result);
                SpawnMachineVisual(result, cell.Value);
                var machine = (Machine)result.SimulationObject;
                machine.SetRecipe(PlaytestContext.SmeltIronRecipeId);
                int connections = CountConnections(result.Ports);
                var inputDir = GridRotation.Rotate(new Vector2Int(-1, 0), _placeRotation);
                var outputDir = GridRotation.Rotate(new Vector2Int(1, 0), _placeRotation);
                Debug.Log($"Smelter placed at ({cell.Value.x},{cell.Value.y}) rotation {_placeRotation} (input from {FormatDirection(inputDir)}, output to {FormatDirection(outputDir)}), {connections} connections formed, {_ctx.Simulation.InserterCount} total inserters");
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
            var existing = _ctx.Grid.GetAt(cell.Value, _currentLevel);
            bool hasFoundation = existing != null && existing.IsStructural;
            UpdatePlaceGhost(cell.Value, _ctx.StorageDef.size, hasFoundation,
                new Color(0.8f, 0.7f, 0.1f, 0.6f), 0.5f);
        }
        else
        {
            DestroyPlaceGhost();
        }

        if (mouse.leftButton.wasPressedThisFrame && !cell.HasValue)
        {
            Debug.Log("storage: click ignored, no grid cell under cursor");
        }
        else if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            PlaytestLogger.Log($"input: LMB | tool=Storage cell=({cell.Value.x},{cell.Value.y})");
            var result = _ctx.AutomationService.PlaceStorage(_ctx.StorageDef, cell.Value, _placeRotation, _currentLevel);
            if (result != null)
            {
                _automationBuildings.Add(result);
                SpawnStorageVisual(result, cell.Value);
                int connections = CountConnections(result.Ports);
                Debug.Log($"Storage placed at ({cell.Value.x},{cell.Value.y}) rotation {_placeRotation}, {connections} connections formed, {_ctx.Simulation.InserterCount} total inserters");
            }
            else
            {
                Debug.Log($"Cannot place storage at ({cell.Value.x},{cell.Value.y}): no foundation or overlap");
            }
        }
    }

    // -- Ghost preview --

    private void UpdatePlaceGhost(Vector2Int cell, Vector2Int size, bool valid, Color baseColor, float height)
    {
        if (_placeGhost == null)
        {
            _placeGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _placeGhost.name = "PlaceGhost";
            var collider = _placeGhost.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        var worldPos = _ctx.Grid.CellToWorld(cell, _currentLevel) + Vector3.up * height;
        _placeGhost.transform.position = worldPos;
        _placeGhost.transform.localScale = new Vector3(
            size.x * 0.9f * FactoryGrid.CellSize, height * 2f, size.y * 0.9f * FactoryGrid.CellSize);

        SetColor(_placeGhost, valid ? baseColor : GhostInvalidColor);
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
        {
            Debug.Log("delete: click ignored, no grid cell under cursor");
            return;
        }

        PlaytestLogger.Log($"input: LMB | tool=Delete cell=({cell.Value.x},{cell.Value.y})");

        // Priority 1: automation buildings
        for (int i = _automationBuildings.Count - 1; i >= 0; i--)
        {
            var ab = _automationBuildings[i];
            var bd = ab.BuildingData;
            if (bd.Level != _currentLevel)
                continue;

            bool covers = false;
            if (bd.BuildingId == "belt")
            {
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

            _ctx.AutomationService.Remove(bd);
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
                _ctx.PlacementService.RemoveWall(wall);
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
                _ctx.PlacementService.RemoveRamp(ramp);
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

            if (_ctx.PlacementService.RemoveFoundation(foundation))
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

    // -- Pre-seed factory --

    public void PreSeedFactory()
    {
        // Place a 10x5 foundation slab starting at (5,5)
        for (int x = 5; x < 15; x++)
        {
            for (int z = 5; z < 10; z++)
            {
                var cell = new Vector2Int(x, z);
                var data = _ctx.PlacementService.PlaceFoundation(_ctx.FoundationDef, cell, 0);
                if (data != null)
                {
                    _foundations.Add(data);
                    SpawnFoundationVisual(data, cell, 0);
                }
            }
        }

        // Source storage at (5,7)
        var srcResult = _ctx.AutomationService.PlaceStorage(_ctx.StorageDef, new Vector2Int(5, 7), 0);
        if (srcResult != null)
        {
            _automationBuildings.Add(srcResult);
            SpawnStorageVisual(srcResult, new Vector2Int(5, 7));
            var srcStorage = (StorageContainer)srcResult.SimulationObject;
            for (int i = 0; i < 50; i++)
                srcStorage.TryInsert(PlaytestContext.IronScrap);
            Debug.Log("Pre-seed: source storage placed at (5,7) with 50 iron scrap");
        }

        // Belt from (6,7) to (8,7)
        var belt1Result = _ctx.AutomationService.PlaceBelt(new Vector2Int(6, 7), new Vector2Int(8, 7));
        if (belt1Result != null)
        {
            _automationBuildings.Add(belt1Result);
            SpawnBeltVisual(belt1Result, new Vector2Int(6, 7), new Vector2Int(8, 7));
            Debug.Log("Pre-seed: belt placed from (6,7) to (8,7)");
        }

        // Smelter at (9,7)
        var smelterResult = _ctx.AutomationService.PlaceMachine(_ctx.SmelterDef, new Vector2Int(9, 7), 0);
        if (smelterResult != null)
        {
            _automationBuildings.Add(smelterResult);
            SpawnMachineVisual(smelterResult, new Vector2Int(9, 7));
            var machine = (Machine)smelterResult.SimulationObject;
            machine.SetRecipe(PlaytestContext.SmeltIronRecipeId);
            Debug.Log("Pre-seed: smelter placed at (9,7)");
        }

        // Belt from (10,7) to (12,7)
        var belt2Result = _ctx.AutomationService.PlaceBelt(new Vector2Int(10, 7), new Vector2Int(12, 7));
        if (belt2Result != null)
        {
            _automationBuildings.Add(belt2Result);
            SpawnBeltVisual(belt2Result, new Vector2Int(10, 7), new Vector2Int(12, 7));
            Debug.Log("Pre-seed: belt placed from (10,7) to (12,7)");
        }

        // Output storage at (13,7)
        var outResult = _ctx.AutomationService.PlaceStorage(_ctx.StorageDef, new Vector2Int(13, 7), 0);
        if (outResult != null)
        {
            _automationBuildings.Add(outResult);
            SpawnStorageVisual(outResult, new Vector2Int(13, 7));
            Debug.Log("Pre-seed: output storage placed at (13,7)");
        }

        Debug.Log($"Pre-seed complete: {_ctx.Simulation.InserterCount} auto-inserters, {_ctx.Simulation.BeltNetwork.ConnectionCount} belt links");
    }

    // -- Visual spawning (public for dev bootstrappers) --

    public void SpawnFoundationVisual(BuildingData data, Vector2Int cell, int level)
    {
        var worldPos = _ctx.Grid.CellToWorld(cell, level);
        var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = $"Foundation_{cell.x}_{cell.y}_L{level}";
        tile.transform.position = worldPos + Vector3.up * 0.05f;
        tile.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
        SetColor(tile, Color.white);
        data.Instance = tile;
    }

    public void SpawnWallVisual(WallData wallData)
    {
        var cellCenter = _ctx.Grid.CellToWorld(wallData.Cell, wallData.Level);
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

    public void SpawnRampVisual(RampData rampData)
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

    public void SpawnBeltVisual(PlacementResult result, Vector2Int startCell, Vector2Int endCell)
    {
        var startWorld = _ctx.Grid.CellToWorld(startCell, _currentLevel);
        var endWorld = _ctx.Grid.CellToWorld(endCell, _currentLevel);
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

    public void SpawnMachineVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _ctx.Grid.CellToWorld(cell, _currentLevel);
        var machine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        machine.name = $"Machine_{cell.x}_{cell.y}";

        var defaultCollider = machine.GetComponent<Collider>();
        if (defaultCollider != null) Destroy(defaultCollider);
        machine.layer = PhysicsLayers.Interactable;
        var boxCollider = machine.AddComponent<BoxCollider>();
        boxCollider.size = Vector3.one;

        machine.transform.position = worldPos + Vector3.up * 0.6f;
        machine.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        SetColor(machine, new Color(0.2f, 0.4f, 0.8f));

        machine.SetActive(false);
        var behaviour = machine.AddComponent<MachineBehaviour>();
        behaviour.Initialize(_ctx.SmelterDef, result.SimulationObject as Machine);
        machine.SetActive(true);

        result.BuildingData.Instance = machine;

        SpawnPortIndicators(result.Ports, worldPos, machine.transform);
    }

    public void SpawnStorageVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _ctx.Grid.CellToWorld(cell, _currentLevel);
        var storage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        storage.name = $"Storage_{cell.x}_{cell.y}";

        var defaultCollider = storage.GetComponent<Collider>();
        if (defaultCollider != null) Destroy(defaultCollider);
        storage.layer = PhysicsLayers.Interactable;
        var boxCollider = storage.AddComponent<BoxCollider>();
        boxCollider.size = Vector3.one;

        storage.transform.position = worldPos + Vector3.up * 0.5f;
        storage.transform.localScale = new Vector3(0.85f, 1.0f, 0.85f);
        SetColor(storage, new Color(0.8f, 0.7f, 0.1f));

        storage.SetActive(false);
        var behaviour = storage.AddComponent<StorageBehaviour>();
        behaviour.Initialize(_ctx.StorageDef, result.SimulationObject as StorageContainer);
        storage.SetActive(true);

        result.BuildingData.Instance = storage;
    }

    // -- Port indicators --

    public void SpawnPortIndicators(List<PortNode> ports, Vector3 buildingWorldPos, Transform parent)
    {
        float cellSize = FactoryGrid.CellSize;
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            bool isInput = port.Type == PortType.Input;
            var color = isInput ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.3f, 0.2f);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = isInput ? "PortIn" : "PortOut";
            var col = indicator.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var dir3 = new Vector3(port.Direction.x, 0, port.Direction.y);
            indicator.transform.position = buildingWorldPos + Vector3.up * 0.6f + dir3 * (cellSize * 0.45f);

            if (port.Direction.x != 0)
                indicator.transform.localScale = new Vector3(0.15f, 0.3f, 0.6f);
            else
                indicator.transform.localScale = new Vector3(0.6f, 0.3f, 0.15f);

            SetColor(indicator, color);
            indicator.transform.SetParent(parent, true);
            _portIndicators.Add(indicator);
        }
    }

    public void SpawnPortIndicators(PlacementResult result)
    {
        var worldPos = _ctx.Grid.CellToWorld(result.BuildingData.Origin, result.BuildingData.Level);
        if (result.BuildingData.Instance != null)
            SpawnPortIndicators(result.Ports, worldPos, result.BuildingData.Instance.transform);
    }

    private void UpdateGhostPortIndicators(Vector2Int cell, MachinePort[] portDefs, int rotation)
    {
        ClearGhostPortIndicators();

        float cellSize = FactoryGrid.CellSize;
        var worldPos = _ctx.Grid.CellToWorld(cell, _currentLevel);

        for (int i = 0; i < portDefs.Length; i++)
        {
            var def = portDefs[i];
            var rotatedDir = GridRotation.Rotate(def.direction, rotation);
            bool isInput = def.type == PortType.Input;
            var color = isInput ? new Color(0.2f, 0.6f, 1f, 0.8f) : new Color(1f, 0.3f, 0.2f, 0.8f);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = isInput ? "GhostPortIn" : "GhostPortOut";
            var col = indicator.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

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
        var belts = _ctx.Simulation.GetBelts();
        int totalItems = 0;
        for (int b = 0; b < belts.Count; b++)
            totalItems += belts[b].ItemCount;

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

            Vector3 dir3D = Vector3.right;
            if (beltResult.Ports.Count == 2)
            {
                var startWorld = _ctx.Grid.CellToWorld(beltResult.Ports[0].Cell, beltResult.BuildingData.Level);
                var endWorld = _ctx.Grid.CellToWorld(beltResult.Ports[1].Cell, beltResult.BuildingData.Level);
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

                bool isBeforeMachine = IsBeforeMachine(belt);
                SetColor(cube, isBeforeMachine
                    ? new Color(0.6f, 0.3f, 0.1f)
                    : new Color(0.7f, 0.7f, 0.8f));
                poolIdx++;
            }
        }

        for (int i = poolIdx; i < _beltItemPool.Count; i++)
            _beltItemPool[i].SetActive(false);
    }

    private bool IsBeforeMachine(BeltSegment belt)
    {
        foreach (var ab in _automationBuildings)
        {
            if (ab.SimulationObject != belt) continue;
            if (ab.Ports.Count < 2) return true;
            var outputPort = ab.Ports[1];
            if (outputPort.Connection is Inserter)
                return true;
        }
        return false;
    }

    // -- Helpers --

    public Vector2Int? GetCellUnderCursor()
    {
        var mouse = Mouse.current;
        if (mouse == null) return null;
        return GetCellUnderCursor(mouse);
    }

    private Vector2Int? GetCellUnderCursor(Mouse mouse)
    {
        var worldPos = GetWorldPosUnderCursor(mouse);
        if (!worldPos.HasValue)
            return null;
        return _ctx.Grid.WorldToCell(worldPos.Value);
    }

    private Vector3? GetWorldPosUnderCursor(Mouse mouse)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("build raycast: Camera.main is null, no camera tagged MainCamera is active");
            return null;
        }

        var mousePos = mouse.position.ReadValue();
        var ray = camera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, PhysicsLayers.PlacementMask))
            return hit.point;

        return null;
    }

    private Vector3 SnapPointToWorld(SnapPoint sp)
    {
        var cellCenter = _ctx.Grid.CellToWorld(sp.Cell, sp.Level);
        var edgeOffset = new Vector3(
            sp.EdgeDirection.x * 0.5f * FactoryGrid.CellSize, 0f,
            sp.EdgeDirection.y * 0.5f * FactoryGrid.CellSize);
        return cellCenter + edgeOffset;
    }

    public static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    public static void DrawLine(ref float y, float x, float w, float h, string text, bool bold = false)
    {
        var style = GUI.skin.label;
        if (bold)
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        GUI.Label(new Rect(x, y, w, h), text, style);
        y += h;
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
}
