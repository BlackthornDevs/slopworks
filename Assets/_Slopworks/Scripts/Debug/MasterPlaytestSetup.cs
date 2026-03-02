using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Master playtest orchestrator. Discovers all IPlaytestFeatureProvider components on
/// this GameObject and calls them in phased order. Neither dev works in this scene
/// directly -- it serves as a merge gate where ALL features must coexist.
///
/// Usage: Add MasterPlaytestSetup, KevinPlaytestSetup, JoePlaytestSetup, PlaytestLogger,
/// and PlaytestValidator as components on the same root GameObject. Hit Play.
/// The providers detect MasterPlaytestSetup and defer their standalone Awake.
/// </summary>
public class MasterPlaytestSetup : MonoBehaviour
{
    [SerializeField] private bool _preSeedFactory;
    [SerializeField] private ushort _beltSpeed = 4;

    private PlaytestContext _ctx;
    private PlaytestToolController _toolCtrl;
    private GameObject _groundPlane;
    private IPlaytestFeatureProvider[] _providers;

    private void Awake()
    {
        // 1. Shared bootstrap (called ONCE)
        _ctx = new PlaytestBootstrap(this, _beltSpeed).Setup();

        // 2. Ground plane
        _groundPlane = PlaytestToolController.CreateGroundPlane();

        // 3. Discover all feature providers on this GameObject
        _providers = GetComponents<IPlaytestFeatureProvider>();
        Debug.Log($"playtest: master scene discovered {_providers.Length} providers");

        // 4. Phase 1: definitions
        foreach (var p in _providers)
        {
            p.CreateDefinitions(_ctx);
            Debug.Log($"playtest: {p.ProviderName} definitions created");
        }

        // 5. Phase 2: build page
        var buildPage = PlaytestToolController.CreateSharedBuildPage();
        foreach (var p in _providers)
            p.ConfigureBuildPage(buildPage);

        // 6. Tool controller
        _toolCtrl = gameObject.AddComponent<PlaytestToolController>();
        _toolCtrl.Initialize(_ctx, buildPage, _groundPlane);

        // 7. Phase 3: tool handlers
        foreach (var p in _providers)
            p.RegisterToolHandlers(_toolCtrl);

        // 8. Phase 4: world objects
        foreach (var p in _providers)
        {
            p.CreateWorldObjects(_ctx, _toolCtrl);
            Debug.Log($"playtest: {p.ProviderName} world objects created");
        }

        // 9. Phase 5: combat -- first non-null wave controller wins
        WaveControllerBehaviour wc = null;
        foreach (var p in _providers)
        {
            var result = p.CreateCombatSetup(_ctx);
            if (result != null && wc == null)
                wc = result;
        }
        if (wc != null)
            _toolCtrl.SetWaveController(wc);

        // 10. NavMesh
        PlaytestToolController.BakeNavMesh(_groundPlane);

        // 11. Pre-seed
        if (_preSeedFactory)
        {
            _toolCtrl.PreSeedFactory();
            foreach (var p in _providers)
                p.PreSeed(_toolCtrl);
        }

        // 12. HUD wiring
        StartCoroutine(WireAllHUDs());

        Debug.Log("playtest: master setup complete");
        Debug.Log("controls: WASD=move, Mouse=look, Space=jump, Shift=sprint");
        Debug.Log("controls: B=toggle build/items, 1-9=select slot, Tab=inventory, E=interact");
        Debug.Log("controls: R=rotate, Esc=cancel, PgUp/PgDn=level, F=fill storage");
        Debug.Log("controls: G=spawn next wave, LMB=fire weapon (items page)");
    }

    private IEnumerator WireAllHUDs()
    {
        // Wait 2 frames -- after tool controller's 1-frame HUD wiring
        yield return null;
        yield return null;

        // Provider WireHUD bodies contain no yields (end with yield break),
        // so they execute synchronously within this already-delayed coroutine
        foreach (var p in _providers)
        {
            var enumerator = p.WireHUD(_ctx);
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (_providers == null) return;
        foreach (var p in _providers)
            p.FixedTick(dt);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || _providers == null) return;
        foreach (var p in _providers)
            p.UpdateInput(kb);
    }

    private void OnGUI()
    {
        if (_toolCtrl == null || _providers == null) return;

        float x = 10;
        float y = _toolCtrl.GuiNextY;
        float w = 420;
        float h = 22;

        foreach (var p in _providers)
        {
            p.DrawGUI(_toolCtrl, ref y, x, w, h);
            _toolCtrl.GuiNextY = y;
        }
    }

    private void OnDestroy()
    {
        if (_providers != null)
        {
            foreach (var p in _providers)
                p.Cleanup();
        }

        // Destroy shared SOs from bootstrap
        if (_ctx != null && _ctx.RuntimeSOs != null)
        {
            foreach (var so in _ctx.RuntimeSOs)
            {
                if (so != null) DestroyImmediate(so);
            }
        }

        if (_ctx != null && _ctx.EnemyTemplate != null)
            DestroyImmediate(_ctx.EnemyTemplate);
    }
}
