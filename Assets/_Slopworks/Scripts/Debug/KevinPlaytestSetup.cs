using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kevin's exclusive playtest bootstrapper. Implements IPlaytestFeatureProvider so it
/// can run standalone (only component on the scene) or as a provider inside MasterPlaytestSetup.
///
/// Controls:
///   WASD - Move, Mouse - Look, Space - Jump, Shift - Sprint
///   B - Toggle build/items hotbar page
///   1-7 - Select hotbar slot (items or build tool depending on page)
///   Tab - Open/close inventory
///   E - Interact with machines
///   R - Rotate (wall/ramp/machine placement)
///   Escape - Cancel / return to items page
///   PageUp/PageDown - Change active level
///   F - Fill storage with iron scrap
///   M - Overworld map
///   G - Spawn next wave
///   Left click - Fire weapon / place building
///
/// Everything is created at runtime -- no prefabs or assets required.
/// </summary>
public class KevinPlaytestSetup : MonoBehaviour, IPlaytestFeatureProvider
{
    [Header("Pre-seed")]
    [SerializeField] private bool _preSeedFactory;

    [Header("Automation")]
    [SerializeField] private ushort _beltSpeed = 4;

    [Header("Inventory")]
    [SerializeField] private int _worldItemCount = 5;

    // Standalone vs provider mode
    private bool _isStandalone = true;

    // Shared context
    private PlaytestContext _ctx;
    private PlaytestToolController _toolCtrl;

    // -- Building exploration --
    private BuildingManager _buildingManager;
    private BuildingState _warehouseState;
    private BuildingDefinitionSO _warehouseDef;
    private GameEventSO _buildingClaimedEvent;
    private BuildingLayout _buildingLayout;
    private bool _insideBuilding;

    // -- Combat --
    private WaveControllerBehaviour _waveController;
    private EnemySpawner _enemySpawner;
    private WaveControllerBehaviour _buildingWaveController;
    private EnemySpawner _buildingEnemySpawner;

    // -- Supply chain --
    private SupplyLineManager _supplyLineManager;
    private SupplyLine _warehouseSupplyLine;
    private OverworldMap _overworldMap;
    private OverworldMapUI _overworldMapUI;
    private StorageContainer _supplyDockContainer;
    private StorageBehaviour _supplyDockBehaviour;
    private StorageDefinitionSO _supplyDockDef;

    // -- Ground plane --
    private GameObject _groundPlane;

    // -- Turret --
    private TurretDefinitionSO _turretDef;
    private readonly List<TurretBehaviour> _turrets = new();
    private int _turretRotation;
    private GameObject _turretGhost;
    private readonly List<GameObject> _turretGhostPorts = new();

    // -- Supply dock grid position --
    private static readonly Vector2Int SupplyDockCell = new Vector2Int(15, 7);

    // ========== IPlaytestFeatureProvider ==========

    public string ProviderName => "Kevin";

    public void CreateDefinitions(PlaytestContext ctx)
    {
        _ctx = ctx;
        CreateBuildingDefinitions();
        CreateTurretDefinition();
    }

    public void ConfigureBuildPage(HotbarPage buildPage)
    {
        buildPage.Entries[7] = new HotbarEntry
        {
            Id = "turret",
            DisplayName = "Turret",
            Color = new Color(0.8f, 0.3f, 0.3f, 0.8f)
        };
    }

    public void RegisterToolHandlers(PlaytestToolController toolCtrl)
    {
        _toolCtrl = toolCtrl;
        toolCtrl.RegisterToolHandler(PlaytestToolController.ToolMode.TurretPlace, HandleTurretPlaceInput);
        toolCtrl.RegisterToolCleanup(DestroyTurretGhost);
    }

    public void CreateWorldObjects(PlaytestContext ctx, PlaytestToolController toolCtrl)
    {
        // _toolCtrl already set by RegisterToolHandlers (phase 3)
        CreateBuildingLayout();
        CreateMEPRestorePoints();
        CreateBuildingEntryExit();
        CreateWorldItems();
        CreateSmelterInteractable();
        CreateSupplyDock();
        CreateSupplyChain();
    }

    public WaveControllerBehaviour CreateCombatSetup(PlaytestContext ctx)
    {
        CreateSpawnPointsAndWaves();
        CreateBuildingEnemies();
        return _waveController;
    }

    public void PreSeed(PlaytestToolController toolCtrl)
    {
        // No Kevin-specific pre-seed beyond the shared one
    }

    public IEnumerator WireHUD(PlaytestContext ctx)
    {
        // No yields here -- caller handles the 2-frame delay
        return WireKevinHUDBody();
    }

    public void FixedTick(float deltaTime)
    {
        if (_buildingManager != null)
            _buildingManager.TickAll(deltaTime);

        if (_supplyLineManager != null)
            _supplyLineManager.TickAll(deltaTime);
    }

    public void UpdateInput(Keyboard kb)
    {
        // Overworld map toggle
        if (kb[Key.M].wasPressedThisFrame && _overworldMapUI != null)
        {
            PlaytestLogger.Log("input: key M (overworld map)");
            _overworldMapUI.Toggle();
            Cursor.lockState = _overworldMapUI.IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _overworldMapUI.IsOpen;
        }

        // Close map with Escape
        if (kb[Key.Escape].wasPressedThisFrame && _overworldMapUI != null && _overworldMapUI.IsOpen)
        {
            _overworldMapUI.Toggle();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Tool input suppressed automatically via cursor lock check in PlaytestToolController.Update()
    }

    public void DrawGUI(PlaytestToolController toolCtrl, ref float y, float x, float w, float h)
    {
        // Building exploration status
        string buildingStatus;
        if (_warehouseState == null)
            buildingStatus = "Building: --";
        else if (_warehouseState.IsClaimed)
            buildingStatus = "Building: Claimed | Production active";
        else
            buildingStatus = $"Building: Unclaimed | MEP: {_warehouseState.RestoredCount}/{_warehouseState.RequiredMEPCount}";
        string locationInfo = _insideBuilding ? " [INSIDE]" : "";
        PlaytestToolController.DrawLine(ref y, x, w, h, $"{buildingStatus}{locationInfo}");

        // Supply chain status
        int inTransit = _supplyLineManager != null ? _supplyLineManager.TotalInFlight : 0;
        int delivered = _supplyLineManager != null ? _supplyLineManager.TotalDelivered : 0;
        PlaytestToolController.DrawLine(ref y, x, w, h, $"Supply: {inTransit} in transit | {delivered} delivered");

        PlaytestToolController.DrawLine(ref y, x, w, h, "[Portal] Enter/exit building  [M] Overworld map");
    }

    public void Cleanup()
    {
        _warehouseSupplyLine?.Dispose();
        DestroyTurretGhost();

        if (_warehouseDef != null) DestroyImmediate(_warehouseDef);
        if (_buildingClaimedEvent != null) DestroyImmediate(_buildingClaimedEvent);
        if (_supplyDockDef != null) DestroyImmediate(_supplyDockDef);
        if (_turretDef != null) DestroyImmediate(_turretDef);
    }

    // ========== Standalone Awake ==========

    private void Awake()
    {
        if (GetComponent<MasterPlaytestSetup>() != null)
        {
            _isStandalone = false;
            return; // MasterPlaytestSetup will call our interface methods
        }

        // 1. Shared bootstrap
        _ctx = new PlaytestBootstrap(this, _beltSpeed).Setup();

        // 2. Kevin-specific definitions
        CreateDefinitions(_ctx);

        // 3. Ground plane
        _groundPlane = PlaytestToolController.CreateGroundPlane();

        // 4. Building exploration + world objects (before tool controller, needs _buildingLayout)
        CreateBuildingLayout();
        CreateMEPRestorePoints();
        CreateBuildingEntryExit();

        // 5. Shared tool controller
        var buildPage = PlaytestToolController.CreateSharedBuildPage();
        ConfigureBuildPage(buildPage);
        _toolCtrl = gameObject.AddComponent<PlaytestToolController>();
        _toolCtrl.Initialize(_ctx, buildPage, _groundPlane);
        RegisterToolHandlers(_toolCtrl);

        // 6. Pre-seed (optional)
        if (_preSeedFactory)
            _toolCtrl.PreSeedFactory();

        // 7. World items + interactable smelter
        CreateWorldItems();
        CreateSmelterInteractable();

        // 8. Enemy template + waves (home base + building)
        CreateSpawnPointsAndWaves();
        CreateBuildingEnemies();
        _toolCtrl.SetWaveController(_waveController);

        // 9. NavMesh
        PlaytestToolController.BakeNavMesh(_groundPlane);

        // 10. Supply chain
        CreateSupplyDock();
        CreateSupplyChain();

        // 11. Kevin-specific HUD wiring (yield 2 frames -- after tool controller's HUD wiring)
        StartCoroutine(WireKevinHUDDelayed());

        Debug.Log("playtest: setup complete (Kevin)");
        Debug.Log("controls: WASD=move, Mouse=look, Space=jump, Shift=sprint");
        Debug.Log("controls: B=toggle build/items, 1-7=select slot, Tab=inventory, E=interact");
        Debug.Log("controls: R=rotate, Esc=cancel, PgUp/PgDn=level, F=fill storage");
        Debug.Log("controls: G=spawn next wave, LMB=fire weapon (items page)");
        Debug.Log("controls: [Portal] Enter/exit building, [M] Overworld map");
    }

    // -- Turret --

    private void CreateTurretDefinition()
    {
        _turretDef = ScriptableObject.CreateInstance<TurretDefinitionSO>();
        _turretDef.turretId = "turret_basic";
        _turretDef.displayName = "Basic Turret";
        _turretDef.range = 20f;
        _turretDef.fireInterval = 0.5f;
        _turretDef.damagePerShot = 10f;
        _turretDef.damageType = DamageType.Kinetic;
        _turretDef.ammoItemId = PlaytestContext.TurretAmmo;
        _turretDef.size = Vector2Int.one;
        _turretDef.ammoSlotCount = 1;
        _turretDef.ammoMaxStackSize = 64;
        _turretDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0),
                type = PortType.Input
            }
        };
        _ctx.RuntimeSOs.Add(_turretDef);
    }

    private void HandleTurretPlaceInput(Keyboard kb, Mouse mouse)
    {
        var cell = _toolCtrl.GetCellUnderCursor();

        if (kb.rKey.wasPressedThisFrame)
        {
            _turretRotation = (_turretRotation + 90) % 360;
            Debug.Log($"turret rotation: {_turretRotation}");
        }

        if (cell.HasValue)
        {
            UpdateTurretGhost(cell.Value);
            UpdateTurretGhostPorts(cell.Value);
        }
        else
        {
            DestroyTurretGhost();
        }

        if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            ClearTurretGhostPorts();
            var result = _ctx.AutomationService.PlaceTurret(
                _turretDef, cell.Value, _turretRotation, _toolCtrl.CurrentLevel);
            if (result != null)
            {
                _toolCtrl.AutomationBuildings.Add(result);
                SpawnTurretVisual(result, cell.Value);
                _toolCtrl.SpawnPortIndicators(result);

                var inputDir = GridRotation.Rotate(new Vector2Int(-1, 0), _turretRotation);
                Debug.Log($"turret placed at ({cell.Value.x},{cell.Value.y}) rotation {_turretRotation}");
            }
            else
            {
                Debug.Log($"cannot place turret at ({cell.Value.x},{cell.Value.y}): overlap");
            }
        }
    }

    private void SpawnTurretVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _ctx.Grid.CellToWorld(cell, _toolCtrl.CurrentLevel);
        var turretController = (TurretController)result.SimulationObject;

        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = $"Turret_{cell.x}_{cell.y}";
        var defaultCollider = baseObj.GetComponent<Collider>();
        if (defaultCollider != null) Destroy(defaultCollider);
        baseObj.layer = PhysicsLayers.Interactable;
        baseObj.AddComponent<BoxCollider>();

        baseObj.transform.position = worldPos + Vector3.up * 0.4f;
        baseObj.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
        PlaytestToolController.SetColor(baseObj, new Color(0.5f, 0.15f, 0.15f));

        var pivot = new GameObject("BarrelPivot");
        pivot.transform.SetParent(baseObj.transform);
        pivot.transform.localPosition = Vector3.up * 0.4f;

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Barrel";
        var barrelCollider = barrel.GetComponent<Collider>();
        if (barrelCollider != null) Destroy(barrelCollider);
        barrel.transform.SetParent(pivot.transform);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        barrel.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
        PlaytestToolController.SetColor(barrel, new Color(0.3f, 0.1f, 0.1f));

        baseObj.SetActive(false);
        var behaviour = baseObj.AddComponent<TurretBehaviour>();
        behaviour.Initialize(_turretDef, turretController, pivot.transform);
        baseObj.SetActive(true);

        _turrets.Add(behaviour);
        result.BuildingData.Instance = baseObj;

        turretController.AmmoStorage.TryInsertStack(PlaytestContext.TurretAmmo, 32);
        Debug.Log("turret visual spawned, pre-loaded 32 ammo");
    }

    private void UpdateTurretGhost(Vector2Int cell)
    {
        if (_turretGhost == null)
        {
            _turretGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _turretGhost.name = "TurretGhost";
            var col = _turretGhost.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        var worldPos = _ctx.Grid.CellToWorld(cell, _toolCtrl.CurrentLevel) + Vector3.up * 0.5f;
        _turretGhost.transform.position = worldPos;
        _turretGhost.transform.localScale = new Vector3(
            _turretDef.size.x * 0.9f * FactoryGrid.CellSize, 1f,
            _turretDef.size.y * 0.9f * FactoryGrid.CellSize);
        PlaytestToolController.SetColor(_turretGhost, new Color(0.8f, 0.3f, 0.3f, 0.4f));
    }

    private void UpdateTurretGhostPorts(Vector2Int cell)
    {
        ClearTurretGhostPorts();

        float cellSize = FactoryGrid.CellSize;
        var worldPos = _ctx.Grid.CellToWorld(cell, _toolCtrl.CurrentLevel);

        foreach (var portDef in _turretDef.ports)
        {
            var rotatedDir = GridRotation.Rotate(portDef.direction, _turretRotation);
            bool isInput = portDef.type == PortType.Input;
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

            PlaytestToolController.SetColor(indicator, color);
            _turretGhostPorts.Add(indicator);
        }
    }

    private void DestroyTurretGhost()
    {
        if (_turretGhost != null)
        {
            Destroy(_turretGhost);
            _turretGhost = null;
        }
        ClearTurretGhostPorts();
    }

    private void ClearTurretGhostPorts()
    {
        for (int i = 0; i < _turretGhostPorts.Count; i++)
        {
            if (_turretGhostPorts[i] != null)
                Destroy(_turretGhostPorts[i]);
        }
        _turretGhostPorts.Clear();
    }

    // -- Kevin-specific definitions --

    private void CreateBuildingDefinitions()
    {
        _warehouseDef = ScriptableObject.CreateInstance<BuildingDefinitionSO>();
        _warehouseDef.buildingId = "warehouse_01";
        _warehouseDef.displayName = "Abandoned Warehouse";
        _warehouseDef.requiredMEPCount = 4;
        _warehouseDef.producedItemIds = new[] { PlaytestContext.IronIngot };
        _warehouseDef.producedAmounts = new[] { 1 };
        _warehouseDef.productionInterval = 30f;

        _buildingClaimedEvent = ScriptableObject.CreateInstance<GameEventSO>();
    }

    // -- Building exploration --

    private void CreateBuildingLayout()
    {
        var buildingOrigin = new Vector3(200f, 0f, 200f);
        _buildingLayout = BuildingLayoutGenerator.GenerateWarehouse(buildingOrigin);

        _buildingManager = new BuildingManager();
        _warehouseState = new BuildingState(_warehouseDef);
        _buildingManager.Register(_warehouseState);

        _warehouseState.OnBuildingClaimed += () =>
        {
            Debug.Log("building: WAREHOUSE CLAIMED -- production starts");
            _buildingClaimedEvent.Raise();
        };

        Debug.Log("playtest: warehouse layout generated at (200, 0, 200)");
    }

    private void CreateMEPRestorePoints()
    {
        var types = new[] { MEPSystemType.Electrical, MEPSystemType.Plumbing,
                            MEPSystemType.Mechanical, MEPSystemType.HVAC };

        for (int i = 0; i < _buildingLayout.MEPPositions.Length; i++)
        {
            var mepTransform = _buildingLayout.MEPPositions[i];
            var pointId = $"mep_{i}";
            var point = new MEPRestorePoint(pointId, types[i]);
            _warehouseState.AddRestorePoint(point);

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"MEPPoint_{types[i]}";
            obj.transform.position = mepTransform.position;
            obj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            obj.layer = PhysicsLayers.Interactable;

            var behaviour = obj.AddComponent<MEPRestorePointBehaviour>();
            behaviour.Initialize(point, _warehouseState);
        }

        Debug.Log("playtest: 4 MEP restore points created in warehouse");
    }

    private void CreateBuildingEntryExit()
    {
        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;

        // Entry portal near the factory area
        var entryObj = new GameObject("BuildingEntryPortal");
        entryObj.layer = PhysicsLayers.VolumeTrigger;
        entryObj.transform.position = new Vector3(centerX + 15, 1f, centerZ + 15);

        var entryRb = entryObj.AddComponent<Rigidbody>();
        entryRb.isKinematic = true;
        var entryCollider = entryObj.AddComponent<BoxCollider>();
        entryCollider.isTrigger = true;
        entryCollider.size = new Vector3(3f, 3f, 3f);

        var entryTrigger = entryObj.AddComponent<BuildingEntryTrigger>();
        entryTrigger.Initialize(_buildingLayout.EntranceSpawn, OnPlayerEnterBuilding);

        // Visual marker for entry portal
        var entryVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        entryVisual.name = "EntryPortalVisual";
        entryVisual.transform.position = entryObj.transform.position;
        entryVisual.transform.localScale = new Vector3(2f, 3f, 0.3f);
        PlaytestToolController.SetColor(entryVisual, new Color(0.2f, 0.5f, 1f, 0.6f));
        var entryVisCol = entryVisual.GetComponent<Collider>();
        if (entryVisCol != null) Destroy(entryVisCol);

        // Exit destination
        var exitDestObj = new GameObject("ExitDestination");
        exitDestObj.transform.position = new Vector3(centerX + 15, 1.5f, centerZ + 12);

        // Exit portal inside the building
        var exitObj = new GameObject("BuildingExitPortal");
        exitObj.layer = PhysicsLayers.VolumeTrigger;
        exitObj.transform.position = _buildingLayout.EntranceSpawn.position + new Vector3(0, 1f, -2f);

        var exitRb = exitObj.AddComponent<Rigidbody>();
        exitRb.isKinematic = true;
        var exitCollider = exitObj.AddComponent<BoxCollider>();
        exitCollider.isTrigger = true;
        exitCollider.size = new Vector3(3f, 3f, 3f);

        var exitTrigger = exitObj.AddComponent<BuildingExitTrigger>();
        exitTrigger.Initialize(exitDestObj.transform, OnPlayerExitBuilding);

        // Visual marker for exit portal
        var exitVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitVisual.name = "ExitPortalVisual";
        exitVisual.transform.position = exitObj.transform.position;
        exitVisual.transform.localScale = new Vector3(2f, 3f, 0.3f);
        PlaytestToolController.SetColor(exitVisual, new Color(1f, 0.5f, 0.2f, 0.6f));
        var exitVisCol = exitVisual.GetComponent<Collider>();
        if (exitVisCol != null) Destroy(exitVisCol);

        Debug.Log("playtest: building entry/exit portals created");
    }

    private void OnPlayerEnterBuilding()
    {
        _insideBuilding = true;

        if (_buildingWaveController != null)
            _buildingWaveController.BeginNextWave();

        if (_ctx.PlayerHUD != null)
        {
            string status = _warehouseState.IsClaimed
                ? "Warehouse: Claimed"
                : $"Warehouse: MEP {_warehouseState.RestoredCount}/{_warehouseState.RequiredMEPCount}";
            _ctx.PlayerHUD.SetBuildingStatus(status);
        }
    }

    private void OnPlayerExitBuilding()
    {
        _insideBuilding = false;
        _ctx.PlayerHUD?.SetBuildingStatus(null);
    }

    // -- Combat --

    private void CreateSpawnPointsAndWaves()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;

        var spawnParent = new GameObject("SpawnPoints");
        Vector3[] positions =
        {
            new Vector3(centerX + 20, 0, centerZ + 20),
            new Vector3(Mathf.Max(1f, centerX - 20), 0, centerZ + 20),
            new Vector3(centerX + 20, 0, Mathf.Max(1f, centerZ - 20)),
            new Vector3(Mathf.Max(1f, centerX - 20), 0, Mathf.Max(1f, centerZ - 20)),
        };

        var spawnTransforms = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var point = new GameObject($"SpawnPoint_{i}");
            point.transform.SetParent(spawnParent.transform);
            point.transform.position = positions[i];
            point.transform.LookAt(new Vector3(centerX, 0, centerZ));
            spawnTransforms[i] = point.transform;
        }

        var waveObj = new GameObject("WaveController");
        waveObj.SetActive(false);

        _enemySpawner = waveObj.AddComponent<EnemySpawner>();
        typeof(EnemySpawner).GetField("_enemyPrefab", flags)?.SetValue(_enemySpawner, _ctx.EnemyTemplate);
        typeof(EnemySpawner).GetField("_spawnPoints", flags)?.SetValue(_enemySpawner, spawnTransforms);

        var waves = new List<WaveDefinition>
        {
            new WaveDefinition { enemyCount = 3, spawnDelay = 1f, timeBetweenWaves = 5f },
            new WaveDefinition { enemyCount = 5, spawnDelay = 0.8f, timeBetweenWaves = 5f },
            new WaveDefinition { enemyCount = 8, spawnDelay = 0.5f, timeBetweenWaves = 0f },
        };
        _waveController = waveObj.AddComponent<WaveControllerBehaviour>();
        typeof(WaveControllerBehaviour).GetField("_waves", flags)?.SetValue(_waveController, waves);
        typeof(WaveControllerBehaviour).GetField("_spawner", flags)?.SetValue(_waveController, _enemySpawner);
        typeof(WaveControllerBehaviour).GetField("_enemyDiedEvent", flags)?.SetValue(_waveController, _ctx.EnemyDiedEvent);
        typeof(WaveControllerBehaviour).GetField("_autoStartDelay", flags)?.SetValue(_waveController, -1f);

        waveObj.SetActive(true);

        Debug.Log("playtest: wave system created (3 waves: 3, 5, 8 enemies, press G to start)");
    }

    private void CreateBuildingEnemies()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var spawnTransforms = _buildingLayout.EnemySpawnPoints;

        var waveObj = new GameObject("BuildingWaveController");
        waveObj.SetActive(false);

        _buildingEnemySpawner = waveObj.AddComponent<EnemySpawner>();
        typeof(EnemySpawner).GetField("_enemyPrefab", flags)?.SetValue(_buildingEnemySpawner, _ctx.EnemyTemplate);
        typeof(EnemySpawner).GetField("_spawnPoints", flags)?.SetValue(_buildingEnemySpawner, spawnTransforms);

        var waves = new List<WaveDefinition>
        {
            new WaveDefinition { enemyCount = 3, spawnDelay = 1f, timeBetweenWaves = 5f },
            new WaveDefinition { enemyCount = 4, spawnDelay = 0.8f, timeBetweenWaves = 0f },
        };
        _buildingWaveController = waveObj.AddComponent<WaveControllerBehaviour>();
        typeof(WaveControllerBehaviour).GetField("_waves", flags)?.SetValue(_buildingWaveController, waves);
        typeof(WaveControllerBehaviour).GetField("_spawner", flags)?.SetValue(_buildingWaveController, _buildingEnemySpawner);
        typeof(WaveControllerBehaviour).GetField("_enemyDiedEvent", flags)?.SetValue(_buildingWaveController, _ctx.EnemyDiedEvent);
        typeof(WaveControllerBehaviour).GetField("_autoStartDelay", flags)?.SetValue(_buildingWaveController, -1f);

        waveObj.SetActive(true);

        Debug.Log("playtest: building enemies created (2 waves: 3, 4 enemies, auto-start on entry)");
    }

    // -- Supply chain --

    private void CreateSupplyDock()
    {
        _supplyDockDef = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        _supplyDockDef.storageId = "supply_dock";
        _supplyDockDef.displayName = "Supply Dock";
        _supplyDockDef.slotCount = 8;
        _supplyDockDef.maxStackSize = 64;
        _supplyDockDef.size = Vector2Int.one;
        _supplyDockDef.ports = new[]
        {
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(-1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, -1), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, 1), type = PortType.Output },
        };

        // Place foundation under the dock
        var foundationData = _ctx.PlacementService.PlaceFoundation(_ctx.FoundationDef, SupplyDockCell, 0);
        if (foundationData != null)
        {
            _toolCtrl.Foundations.Add(foundationData);
            _toolCtrl.SpawnFoundationVisual(foundationData, SupplyDockCell, 0);
        }

        // Place through the automation service
        var result = _ctx.AutomationService.PlaceStorage(_supplyDockDef, SupplyDockCell, 0);
        if (result == null)
        {
            Debug.LogError("playtest: failed to place supply dock at " + SupplyDockCell);
            return;
        }

        _toolCtrl.AutomationBuildings.Add(result);
        _supplyDockContainer = (StorageContainer)result.SimulationObject;

        // Spawn visual -- green to distinguish from normal storage
        var worldPos = _ctx.Grid.CellToWorld(SupplyDockCell, 0);
        var dockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dockObj.name = "SupplyDock";

        var defaultCollider = dockObj.GetComponent<Collider>();
        if (defaultCollider != null) Destroy(defaultCollider);
        dockObj.layer = PhysicsLayers.Interactable;
        var boxCollider = dockObj.AddComponent<BoxCollider>();
        boxCollider.size = Vector3.one;

        dockObj.transform.position = worldPos + Vector3.up * 0.5f;
        dockObj.transform.localScale = new Vector3(0.85f, 1.0f, 0.85f);
        PlaytestToolController.SetColor(dockObj, new Color(0.3f, 0.6f, 0.4f));

        dockObj.SetActive(false);
        _supplyDockBehaviour = dockObj.AddComponent<StorageBehaviour>();
        _supplyDockBehaviour.Initialize(_supplyDockDef, _supplyDockContainer);
        dockObj.SetActive(true);

        result.BuildingData.Instance = dockObj;

        _toolCtrl.SpawnPortIndicators(result.Ports, worldPos, dockObj.transform);

        Debug.Log($"playtest: supply dock placed at ({SupplyDockCell.x},{SupplyDockCell.y}) on grid with port nodes");
    }

    private void CreateSupplyChain()
    {
        _supplyLineManager = new SupplyLineManager();

        _warehouseSupplyLine = new SupplyLine(_warehouseState, _supplyDockContainer, 10f);
        _warehouseState.OnItemProduced += (itemId, amount) =>
            Debug.Log($"supply line: {amount} {itemId} produced, in transit (10s delay)");
        _warehouseSupplyLine.OnItemDelivered += (itemId, amount) =>
            Debug.Log($"supply line: delivered {amount} {itemId} to supply dock");
        _warehouseSupplyLine.OnItemLost += (itemId, amount) =>
            Debug.Log($"supply line: lost {amount} {itemId} (dock full)");
        _supplyLineManager.RegisterLine(_warehouseSupplyLine);

        _overworldMap = new OverworldMap();
        _overworldMap.RegisterNode(new OverworldNode(
            "home_base", "Home Base", OverworldNodeType.HomeBase, 0.5f, 0.5f));
        _overworldMap.RegisterNode(new OverworldNode(
            _warehouseState.BuildingId, _warehouseState.DisplayName,
            OverworldNodeType.Building, 0.3f, 0.7f, _warehouseState));
        _overworldMap.RegisterNode(new OverworldNode(
            "tower_01", "Broadcast Tower", OverworldNodeType.Tower, 0.7f, 0.3f));

        var mapUIObj = new GameObject("OverworldMapUI");
        _overworldMapUI = mapUIObj.AddComponent<OverworldMapUI>();
        _overworldMapUI.Initialize(_overworldMap, _supplyLineManager, () => 0f);

        Debug.Log("playtest: supply chain created (warehouse -> dock, 10s delay)");
    }

    // -- World items --

    private void CreateWorldItems()
    {
        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;

        for (int i = 0; i < _worldItemCount; i++)
        {
            float x = centerX + Random.Range(-8f, 8f);
            float z = centerZ + Random.Range(-8f, 8f);
            var pos = new Vector3(x, 0.3f, z);

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"WorldItem_IronScrap_{i}";
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var renderer = obj.GetComponent<Renderer>();
            renderer.material.color = new Color(0.6f, 0.4f, 0.2f);

            DestroyImmediate(obj.GetComponent<BoxCollider>());

            var worldItem = obj.AddComponent<WorldItem>();
            worldItem.Initialize(_ctx.IronScrapDef, Random.Range(1, 4));
        }
        Debug.Log($"playtest: {_worldItemCount} world items created");
    }

    private void CreateSmelterInteractable()
    {
        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;

        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "SmelterInteractable";
        obj.transform.position = new Vector3(centerX + 4, 0.5f, centerZ + 4);
        obj.transform.localScale = new Vector3(2, 1, 2);

        var smelterRenderer = obj.GetComponent<Renderer>();
        smelterRenderer.material.color = new Color(0.8f, 0.4f, 0.1f);

        obj.layer = PhysicsLayers.Interactable;

        obj.SetActive(false);
        var machineBehaviour = obj.AddComponent<MachineBehaviour>();
        var defField = typeof(MachineBehaviour).GetField("_definition",
            BindingFlags.NonPublic | BindingFlags.Instance);
        defField?.SetValue(machineBehaviour, _ctx.SmelterDef);
        obj.SetActive(true);

        Debug.Log("playtest: smelter interactable created");
    }

    // -- HUD wiring (Kevin-specific) --

    private IEnumerator WireKevinHUDDelayed()
    {
        yield return null;
        yield return null;

        var body = WireKevinHUDBody();
        while (body.MoveNext())
            yield return body.Current;
    }

    private IEnumerator WireKevinHUDBody()
    {
        if (_warehouseState != null)
        {
            _warehouseState.OnPointRestored += () =>
            {
                if (_insideBuilding && _ctx.PlayerHUD != null)
                {
                    string status = _warehouseState.IsClaimed
                        ? "Warehouse: Claimed"
                        : $"Warehouse: MEP {_warehouseState.RestoredCount}/{_warehouseState.RequiredMEPCount}";
                    _ctx.PlayerHUD.SetBuildingStatus(status);
                }
            };
        }

        Debug.Log("playtest: Kevin HUD extensions wired (building status)");
        yield break;
    }

    // -- Unity callbacks (standalone mode only) --

    private void FixedUpdate()
    {
        if (!_isStandalone) return;
        FixedTick(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (!_isStandalone) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        UpdateInput(kb);
    }

    private void OnGUI()
    {
        if (!_isStandalone) return;
        if (_toolCtrl == null) return;

        float x = 10;
        float y = _toolCtrl.GuiNextY;
        float w = 420;
        float h = 22;

        DrawGUI(_toolCtrl, ref y, x, w, h);
    }

    private void OnDestroy()
    {
        Cleanup();

        // Destroy shared SOs from bootstrap (only in standalone mode)
        if (_isStandalone && _ctx != null && _ctx.RuntimeSOs != null)
        {
            foreach (var so in _ctx.RuntimeSOs)
            {
                if (so != null) DestroyImmediate(so);
            }
        }

        // Destroy enemy template (only in standalone mode)
        if (_isStandalone && _ctx != null && _ctx.EnemyTemplate != null)
            DestroyImmediate(_ctx.EnemyTemplate);
    }
}
