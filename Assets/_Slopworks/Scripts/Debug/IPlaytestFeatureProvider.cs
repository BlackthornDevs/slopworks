using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Interface for dev-specific playtest feature providers. Each dev's bootstrapper
/// implements this so MasterPlaytestSetup can discover and orchestrate all features
/// from a single scene. Providers are called in phases during Awake, then receive
/// runtime callbacks for FixedUpdate/Update/OnGUI.
/// </summary>
public interface IPlaytestFeatureProvider
{
    string ProviderName { get; }

    // Phase 1: Create SO definitions (turret defs, building defs, etc.)
    void CreateDefinitions(PlaytestContext ctx);

    // Phase 2: Add entries to the build page (slot 7+ for dev-specific tools)
    void ConfigureBuildPage(HotbarPage buildPage);

    // Phase 3: Register custom tool handlers on the tool controller
    void RegisterToolHandlers(PlaytestToolController toolCtrl);

    // Phase 4: Create world objects (buildings, portals, supply docks, etc.)
    void CreateWorldObjects(PlaytestContext ctx, PlaytestToolController toolCtrl);

    // Phase 5: Create combat setup (wave controller, spawners)
    // Returns a WaveControllerBehaviour if this provider owns home-base waves, null otherwise
    WaveControllerBehaviour CreateCombatSetup(PlaytestContext ctx);

    // Phase 6: Pre-seed factory (optional, called only when _preSeedFactory is true)
    void PreSeed(PlaytestToolController toolCtrl);

    // Phase 7: Wire HUD extensions (called via coroutine, 2-frame delay already handled)
    IEnumerator WireHUD(PlaytestContext ctx);

    // Runtime callbacks
    void FixedTick(float deltaTime);
    void UpdateInput(Keyboard kb);
    void DrawGUI(PlaytestToolController toolCtrl, ref float y, float x, float w, float h);

    // Cleanup
    void Cleanup();
}
