using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Joe's exclusive playtest bootstrapper. Implements IPlaytestFeatureProvider so it
/// can run standalone (only component on the scene) or as a provider inside MasterPlaytestSetup.
///
/// Controls (shared):
///   WASD - Move, Mouse - Look, Space - Jump, Shift - Sprint
///   B - Toggle build/items hotbar page
///   1-8 - Select hotbar slot (items or build tool depending on page)
///   Tab - Open/close inventory
///   E - Interact with machines
///   R - Rotate (wall/ramp/machine placement)
///   Escape - Cancel / return to items page
///   PageUp/PageDown - Change active level
///   F - Fill storage with iron scrap
///   G - Spawn next wave
///   Left click - Fire weapon / place building
///
/// Joe-specific controls:
///   P - Pre-seed factory with turret chain
///   8 - Turret placement tool (on build page slot 7)
/// </summary>
public class JoePlaytestSetup : MonoBehaviour, IPlaytestFeatureProvider
{
    [Header("Pre-seed")]
    [SerializeField] private bool _preSeedFactory;

    [Header("Automation")]
    [SerializeField] private ushort _beltSpeed = 4;

    // Standalone vs provider mode
    private bool _isStandalone = true;

    // Shared context
    private PlaytestContext _ctx;
    private PlaytestToolController _toolCtrl;

    // -- Ground plane --
    private GameObject _groundPlane;

    // -- Combat --
    private WaveControllerBehaviour _waveController;
    private EnemySpawner _enemySpawner;

    // -- Turret --
    private TurretDefinitionSO _turretDef;
    private readonly List<TurretBehaviour> _turrets = new();
    private int _placeRotation;

    // Turret ghost preview (owned by this handler, not PlaytestToolController)
    private GameObject _turretGhost;
    private readonly List<GameObject> _turretGhostPorts = new();

    // ========== IPlaytestFeatureProvider ==========

    public string ProviderName => "Joe";

    public void CreateDefinitions(PlaytestContext ctx)
    {
        _ctx = ctx;
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
    }

    public void CreateWorldObjects(PlaytestContext ctx, PlaytestToolController toolCtrl)
    {
        // Arena environment replaces ground plane in standalone mode.
        // In master mode, Kevin or the orchestrator handles the ground plane.
    }

    public WaveControllerBehaviour CreateCombatSetup(PlaytestContext ctx)
    {
        if (_isStandalone)
        {
            CreateSpawnPointsAndWaves();
            return _waveController;
        }

        // Master mode: Kevin handles home-base waves
        return null;
    }

    public void PreSeed(PlaytestToolController toolCtrl)
    {
        if (_preSeedFactory)
            PreSeedTurretChain();
    }

    public IEnumerator WireHUD(PlaytestContext ctx)
    {
        return WireJoeHUDBody();
    }

    public void FixedTick(float deltaTime)
    {
        // TurretBehaviour handles its own FixedUpdate ticking via the simulation layer.
        // No additional tick needed from the provider.
    }

    public void UpdateInput(Keyboard kb)
    {
        if (kb.pKey.wasPressedThisFrame)
        {
            _toolCtrl.PreSeedFactory();
            PreSeedTurretChain();
            Debug.Log("turret chain pre-seeded via P key");
        }
    }

    public void DrawGUI(PlaytestToolController toolCtrl, ref float y, float x, float w, float h)
    {
        int turretCount = _turrets.Count;
        int activeTurrets = 0;
        int totalAmmo = 0;
        foreach (var t in _turrets)
        {
            if (t == null) continue;
            if (t.HasTarget) activeTurrets++;
            if (t.Controller != null)
                totalAmmo += t.Controller.AmmoStorage.GetTotalItemCount();
        }

        PlaytestToolController.DrawLine(ref y, x, w, h,
            $"Turrets: {turretCount}  |  Active: {activeTurrets}  |  Total ammo: {totalAmmo}");
    }

    public void Cleanup()
    {
        if (_turretDef != null)
            DestroyImmediate(_turretDef);
    }

    // ========== Standalone Awake ==========

    private void Awake()
    {
        if (GetComponent<MasterPlaytestSetup>() != null)
        {
            _isStandalone = false;
            return;
        }

        // 1. Shared bootstrap
        _ctx = new PlaytestBootstrap(this, _beltSpeed).Setup();

        // 2. Arena environment (replaces plain ground plane)
        var env = gameObject.AddComponent<PlaytestEnvironment>();
        _groundPlane = env.Generate();

        // 3. Turret definition
        CreateTurretDefinition();

        // 4. Shared tool controller
        var buildPage = PlaytestToolController.CreateSharedBuildPage();
        ConfigureBuildPage(buildPage);
        _toolCtrl = gameObject.AddComponent<PlaytestToolController>();
        _toolCtrl.Initialize(_ctx, buildPage, _groundPlane);
        RegisterToolHandlers(_toolCtrl);

        // 5. Waves
        CreateSpawnPointsAndWaves();
        _toolCtrl.SetWaveController(_waveController);

        // 6. NavMesh
        PlaytestToolController.BakeNavMesh(_groundPlane);

        // 7. Pre-seed (optional)
        if (_preSeedFactory)
        {
            _toolCtrl.PreSeedFactory();
            PreSeedTurretChain();
        }

        // 8. HUD wiring
        StartCoroutine(WireJoeHUDDelayed());

        Debug.Log("playtest: setup complete (Joe)");
        Debug.Log("controls: WASD=move, Mouse=look, Space=jump, Shift=sprint");
        Debug.Log("controls: B=toggle build/items, 1-8=select slot, Tab=inventory, E=interact");
        Debug.Log("controls: R=rotate, Esc=cancel, PgUp/PgDn=level, F=fill storage");
        Debug.Log("controls: G=spawn next wave, P=pre-seed turret chain, LMB=fire/place");
    }

    // ========== Turret definition ==========

    private void CreateTurretDefinition()
    {
        _turretDef = ScriptableObject.CreateInstance<TurretDefinitionSO>();
        _turretDef.turretId = "turret_basic";
        _turretDef.displayName = "Basic Turret";
        _turretDef.range = 20f;
        _turretDef.fireInterval = 0.5f;
        _turretDef.damagePerShot = 10f;
        _turretDef.damageType = DamageType.Kinetic;
        _turretDef.ammoItemId = "iron_scrap";
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

    // ========== Turret placement handler ==========

    private void HandleTurretPlaceInput(Keyboard kb, Mouse mouse)
    {
        var cell = _toolCtrl.GetCellUnderCursor();

        if (kb.rKey.wasPressedThisFrame)
        {
            _placeRotation = (_placeRotation + 90) % 360;
            Debug.Log($"turret rotation: {_placeRotation}");
        }

        if (cell.HasValue)
        {
            UpdateTurretGhost(cell.Value, true);
            UpdateTurretGhostPorts(cell.Value);
        }
        else
        {
            DestroyTurretGhost();
        }

        if (mouse.leftButton.wasPressedThisFrame && !cell.HasValue)
        {
            Debug.Log("turret: click ignored, no grid cell under cursor");
        }
        else if (mouse.leftButton.wasPressedThisFrame && cell.HasValue)
        {
            ClearTurretGhostPorts();
            var result = _ctx.AutomationService.PlaceTurret(
                _turretDef, cell.Value, _placeRotation, _toolCtrl.CurrentLevel);
            if (result != null)
            {
                _toolCtrl.AutomationBuildings.Add(result);
                SpawnTurretVisual(result, cell.Value);
                _toolCtrl.SpawnPortIndicators(result);

                var inputDir = GridRotation.Rotate(new Vector2Int(-1, 0), _placeRotation);
                Debug.Log($"turret placed at ({cell.Value.x},{cell.Value.y}) rotation {_placeRotation}, ammo input from {FormatDirection(inputDir)}");
            }
            else
            {
                Debug.Log($"cannot place turret at ({cell.Value.x},{cell.Value.y}): overlap");
            }
        }
    }

    // ========== Turret visual ==========

    private void SpawnTurretVisual(PlacementResult result, Vector2Int cell)
    {
        var worldPos = _ctx.Grid.CellToWorld(cell, _toolCtrl.CurrentLevel);
        var turretController = (TurretController)result.SimulationObject;

        // Base: dark red cylinder
        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = $"Turret_{cell.x}_{cell.y}";
        var defaultCollider = baseObj.GetComponent<Collider>();
        if (defaultCollider != null) Destroy(defaultCollider);
        baseObj.layer = PhysicsLayers.Interactable;
        baseObj.AddComponent<BoxCollider>();

        baseObj.transform.position = worldPos + Vector3.up * 0.4f;
        baseObj.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
        PlaytestToolController.SetColor(baseObj, new Color(0.5f, 0.15f, 0.15f));

        // Barrel pivot
        var pivot = new GameObject("BarrelPivot");
        pivot.transform.SetParent(baseObj.transform);
        pivot.transform.localPosition = Vector3.up * 0.4f;

        // Barrel: elongated cube
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Barrel";
        var barrelCollider = barrel.GetComponent<Collider>();
        if (barrelCollider != null) Destroy(barrelCollider);
        barrel.transform.SetParent(pivot.transform);
        barrel.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        barrel.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
        PlaytestToolController.SetColor(barrel, new Color(0.3f, 0.1f, 0.1f));

        // TurretBehaviour (inactive-then-activate pattern)
        baseObj.SetActive(false);
        var behaviour = baseObj.AddComponent<TurretBehaviour>();
        behaviour.Initialize(_turretDef, turretController, pivot.transform);
        baseObj.SetActive(true);

        _turrets.Add(behaviour);
        result.BuildingData.Instance = baseObj;

        // Pre-load 32 ammo so turret fires immediately
        turretController.AmmoStorage.TryInsertStack("iron_scrap", 32);
        Debug.Log("turret visual spawned, pre-loaded 32 ammo");
    }

    // ========== Turret ghost preview ==========

    private void UpdateTurretGhost(Vector2Int cell, bool valid)
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

        var color = valid
            ? new Color(0.8f, 0.3f, 0.3f, 0.4f)
            : new Color(1f, 0f, 0f, 0.4f);
        PlaytestToolController.SetColor(_turretGhost, color);
    }

    private void UpdateTurretGhostPorts(Vector2Int cell)
    {
        ClearTurretGhostPorts();

        float cellSize = FactoryGrid.CellSize;
        var worldPos = _ctx.Grid.CellToWorld(cell, _toolCtrl.CurrentLevel);

        foreach (var portDef in _turretDef.ports)
        {
            var rotatedDir = GridRotation.Rotate(portDef.direction, _placeRotation);
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

    // ========== Turret pre-seed chain ==========

    private void PreSeedTurretChain()
    {
        // Ammo storage at (5,5) with 200 iron scrap
        var ammoResult = _ctx.AutomationService.PlaceStorage(
            _ctx.StorageDef, new Vector2Int(5, 5), 0);
        if (ammoResult != null)
        {
            _toolCtrl.AutomationBuildings.Add(ammoResult);
            _toolCtrl.SpawnStorageVisual(ammoResult, new Vector2Int(5, 5));
            var ammoStorage = (StorageContainer)ammoResult.SimulationObject;
            for (int i = 0; i < 200; i++)
                ammoStorage.TryInsert("iron_scrap");
            Debug.Log("pre-seed: ammo storage at (5,5) with 200 iron scrap");
        }
        else
        {
            Debug.LogWarning("pre-seed: failed to place ammo storage at (5,5)");
        }

        // Belt from (6,5) to (8,5) — feeds east toward turret
        var ammoBeltResult = _ctx.AutomationService.PlaceBelt(
            new Vector2Int(6, 5), new Vector2Int(8, 5));
        if (ammoBeltResult != null)
        {
            _toolCtrl.AutomationBuildings.Add(ammoBeltResult);
            _toolCtrl.SpawnBeltVisual(ammoBeltResult, new Vector2Int(6, 5), new Vector2Int(8, 5));
            Debug.Log("pre-seed: ammo belt from (6,5) to (8,5)");
        }
        else
        {
            Debug.LogWarning("pre-seed: failed to place ammo belt from (6,5) to (8,5)");
        }

        // Turret at (9,5), rotation 0 (input port faces west toward belt)
        var turretResult = _ctx.AutomationService.PlaceTurret(
            _turretDef, new Vector2Int(9, 5), 0, 0);
        if (turretResult != null)
        {
            _toolCtrl.AutomationBuildings.Add(turretResult);
            SpawnTurretVisual(turretResult, new Vector2Int(9, 5));
            _toolCtrl.SpawnPortIndicators(turretResult);
            Debug.Log("pre-seed: turret at (9,5)");
        }
        else
        {
            Debug.LogWarning("pre-seed: failed to place turret at (9,5)");
        }

        Debug.Log("turret defense chain pre-seed complete");
    }

    // ========== Combat ==========

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

    // ========== HUD ==========

    private IEnumerator WireJoeHUDDelayed()
    {
        yield return null;
        yield return null;

        var body = WireJoeHUDBody();
        while (body.MoveNext())
            yield return body.Current;
    }

    private IEnumerator WireJoeHUDBody()
    {
        Debug.Log("playtest: Joe HUD extensions wired");
        yield break;
    }

    // ========== Helpers ==========

    private static string FormatDirection(Vector2Int dir)
    {
        if (dir == new Vector2Int(1, 0)) return "east";
        if (dir == new Vector2Int(-1, 0)) return "west";
        if (dir == new Vector2Int(0, 1)) return "north";
        if (dir == new Vector2Int(0, -1)) return "south";
        return $"({dir.x},{dir.y})";
    }

    // ========== Unity callbacks (standalone mode only) ==========

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
        DestroyTurretGhost();
        Cleanup();

        if (_isStandalone && _ctx != null && _ctx.RuntimeSOs != null)
        {
            foreach (var so in _ctx.RuntimeSOs)
            {
                if (so != null) DestroyImmediate(so);
            }
        }

        if (_isStandalone && _ctx != null && _ctx.EnemyTemplate != null)
            DestroyImmediate(_ctx.EnemyTemplate);
    }
}
