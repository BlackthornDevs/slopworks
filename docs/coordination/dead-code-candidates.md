# Dead code candidates

Files and systems that exist only for the single-player vertical slice playtest bootstrapper. None of this runs in the multiplayer build. Archive or delete when the playtest system is officially retired.

Last updated: 2026-03-13

---

## PlayerHUD system (replaced by VisorHUD)

PlayerHUD created runtime UI elements (crosshair, hotbar, health bar, ammo text, wave text, damage flash, build mode indicator). None of it ever worked in multiplayer -- only the crosshair and empty hotbar rendered. VisorHUD now handles all HUD duties.

| File | Notes |
|------|-------|
| `Scripts/UI/PlayerHUD.cs` | Start() gutted, all methods are no-ops. Delete when playtest refs removed. |
| `Scripts/UI/HotbarSlotUI.cs` | Only used by PlayerHUD.CreateHotbar(). |
| `Scripts/UI/HotbarPage.cs` | Only used by PlayerHUD hotbar page system. |
| `Scripts/UI/HealthBarUI.cs` | Only used by PlayerHUD.CreateHealthBar(). |
| `Scripts/UI/InteractionPromptUI.cs` | Created by PlayerHUD. Multiplayer uses InteractionController instead. |

## Playtest bootstrapper system

The entire Debug/ bootstrapper system was built for the single-player vertical slice. It creates a grid, simulation, player, HUD, and combat setup from scratch at runtime. Multiplayer uses scene-based setup with NetworkManager, prefabs, and FishNet.

| File | Notes |
|------|-------|
| `Scripts/Debug/PlaytestBootstrap.cs` | Creates PlayerHUD, grid, simulation, player -- all replaced by multiplayer scene. |
| `Scripts/Debug/PlaytestContext.cs` | Data class holding refs from bootstrap. Has `PlayerHUD` field. |
| `Scripts/Debug/PlaytestToolController.cs` | Shared tool handling, heavily references PlayerHUD for hotbar/page switching. |
| `Scripts/Debug/KevinPlaytestSetup.cs` | Kevin's single-player bootstrapper (buildings, supply chain, overworld). |
| `Scripts/Debug/JoePlaytestSetup.cs` | Joe's single-player bootstrapper (turrets). |
| `Scripts/Debug/MasterPlaytestSetup.cs` | Orchestrator that discovers providers and calls in phase order. |
| `Scripts/Debug/IPlaytestFeatureProvider.cs` | Interface for the provider pattern. |
| `Scripts/Debug/PlaytestValidator.cs` | Checks PlayerHUD exists, validates playtest scene setup. |
| `Scripts/Debug/BeltPlaytestSetup.cs` | Belt-specific playtest bootstrapper. |

## Playtest scene files

| File | Notes |
|------|-------|
| `Scenes/Playtest/` (entire directory) | Single-player playtest scenes. Not used in multiplayer. |

## Supporting types used only by playtest

| File | Notes |
|------|-------|
| `Scripts/Debug/PlaytestEnvironment.cs` | Procedural arena generator for playtest scenes. |

---

## NOT dead code (keep)

These Debug/ files are used in multiplayer or are the visor system:

| File | Reason to keep |
|------|----------------|
| `Scripts/Debug/ReticleTestSetup.cs` | Spawns VisorHUD via VisorAutoBootstrap. Active in multiplayer. |
| `Scripts/Debug/VisorAutoBootstrap.cs` | RuntimeInitializeOnLoadMethod, spawns visor in all scenes. |
| `Scripts/UI/VisorHUD.cs` | Joe's visor HUD -- the replacement. |
| `Scripts/UI/ReticleController.cs` | Visor crosshair system. |
| `Scripts/UI/BuildTooltipUI.cs` | Visor build tooltips. |
| `Scripts/UI/ReticleStyle.cs` | Reticle style data. |
