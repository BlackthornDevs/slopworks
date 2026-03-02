using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Joe's exclusive playtest bootstrapper. Implements IPlaytestFeatureProvider so it
/// can run standalone (only component on the scene) or as a provider inside MasterPlaytestSetup.
///
/// Joe: port your turret code here. See docs/coordination/tasks-joe.md J-023 for details.
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
/// Joe-specific controls (add as you wire them):
///   P - Pre-seed factory with turret chain
///   8 - Turret placement tool (on build page)
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

    // Joe adds: turret fields here
    // private TurretDefinitionSO _turretDef;

    // ========== IPlaytestFeatureProvider ==========

    public string ProviderName => "Joe";

    public void CreateDefinitions(PlaytestContext ctx)
    {
        _ctx = ctx;
        // Joe adds: CreateTurretDefinition() here
    }

    public void ConfigureBuildPage(HotbarPage buildPage)
    {
        // Joe adds: slot 7 for turret
        // buildPage.Entries[7] = new HotbarEntry { Id = "turret", DisplayName = "Turret", Color = new Color(0.9f, 0.3f, 0.3f, 0.8f) };
    }

    public void RegisterToolHandlers(PlaytestToolController toolCtrl)
    {
        _toolCtrl = toolCtrl;
        // Joe adds: toolCtrl.RegisterToolHandler(PlaytestToolController.ToolMode.TurretPlace, HandleTurretPlaceInput);
    }

    public void CreateWorldObjects(PlaytestContext ctx, PlaytestToolController toolCtrl)
    {
        // _toolCtrl already set by RegisterToolHandlers (phase 3)
        // Joe adds: arena objects, turret placements, etc.
    }

    public WaveControllerBehaviour CreateCombatSetup(PlaytestContext ctx)
    {
        if (_isStandalone)
        {
            // Standalone mode: Joe owns the wave controller
            CreateSpawnPointsAndWaves();
            return _waveController;
        }

        // Master mode: Kevin handles home-base waves. Joe adds tower-specific waves later.
        return null;
    }

    public void PreSeed(PlaytestToolController toolCtrl)
    {
        // Joe adds: turret chain pre-seed
    }

    public IEnumerator WireHUD(PlaytestContext ctx)
    {
        // No yields here -- caller handles the 2-frame delay
        return WireJoeHUDBody();
    }

    public void FixedTick(float deltaTime)
    {
        // Joe adds: turret tick logic
    }

    public void UpdateInput(Keyboard kb)
    {
        // Joe adds: P key for turret pre-seed, turret placement input
    }

    public void DrawGUI(PlaytestToolController toolCtrl, ref float y, float x, float w, float h)
    {
        // Joe adds: turret stats
    }

    public void Cleanup()
    {
        // Joe adds: turret SO cleanup
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

        // 2. Ground plane (Joe replaces with PlaytestEnvironment later)
        _groundPlane = PlaytestToolController.CreateGroundPlane();

        // 3. Shared tool controller
        var buildPage = PlaytestToolController.CreateSharedBuildPage();
        ConfigureBuildPage(buildPage);
        _toolCtrl = gameObject.AddComponent<PlaytestToolController>();
        _toolCtrl.Initialize(_ctx, buildPage, _groundPlane);

        // Joe adds: turret definition and RegisterToolHandler here
        RegisterToolHandlers(_toolCtrl);

        // 4. Waves
        CreateSpawnPointsAndWaves();
        _toolCtrl.SetWaveController(_waveController);

        // 5. NavMesh
        PlaytestToolController.BakeNavMesh(_groundPlane);

        // 6. Pre-seed (optional)
        if (_preSeedFactory)
        {
            _toolCtrl.PreSeedFactory();
            // Joe adds: turret chain on top of pre-seed
        }

        // 7. Joe-specific HUD wiring
        StartCoroutine(WireJoeHUDDelayed());

        Debug.Log("playtest: setup complete (Joe)");
        Debug.Log("controls: WASD=move, Mouse=look, Space=jump, Shift=sprint");
        Debug.Log("controls: B=toggle build/items, 1-8=select slot, Tab=inventory, E=interact");
        Debug.Log("controls: R=rotate, Esc=cancel, PgUp/PgDn=level, F=fill storage");
        Debug.Log("controls: G=spawn next wave, LMB=fire weapon (items page)");
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

    // -- HUD wiring (Joe-specific) --

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
        // Joe adds: turret-specific HUD wiring here

        Debug.Log("playtest: Joe HUD extensions wired");
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

        if (_isStandalone && _ctx != null && _ctx.EnemyTemplate != null)
            DestroyImmediate(_ctx.EnemyTemplate);
    }
}
