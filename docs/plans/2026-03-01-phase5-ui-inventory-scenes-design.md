# Phase 5 Design: Core UI + Player Inventory + Scene Management

**Date:** 2026-03-01
**Author:** Kevin's Claude (lead)
**Status:** Approved
**Prerequisites:** Phases 1-4 complete. Existing systems: InventoryContainer, Inventory, ItemDefinitionSO, ItemRegistry, RecipeRegistry, GameEventSO, PlayerController, HealthComponent, SlopworksControls input system.

---

## Overview

Phase 5 builds three foundational systems that every subsequent phase depends on:
1. Scene loader with fade transitions (unlocks building exploration, tower, overworld)
2. HUD with hotbar, health, and contextual prompts (unlocks all gameplay UI)
3. Player inventory with pickup and machine crafting (unlocks item-driven gameplay)

---

## 5.1 Scene Loader and Transition System

### Architecture

`SceneLoader` is a pure C# class (D-004 pattern). `SceneLoaderBehaviour` is a MonoBehaviour in Core_GameManager that owns the SceneLoader instance and provides the coroutine runner.

An `ISceneService` interface wraps all scene operations (per D-002) so Addressables can replace the implementation later without touching callers.

### Interface

```csharp
public interface ISceneService
{
    void LoadSceneGroup(string groupName, System.Action onComplete = null);
    void UnloadCurrentGroup(System.Action onComplete = null);
    string CurrentGroup { get; }
}
```

### Scene groups

Scene groups are defined as string arrays. The loader unloads the current group and loads the target group additively. Core scenes are never in a group -- they're always resident.

| Group name | Scenes |
|------------|--------|
| HomeBase | HomeBase_Terrain, HomeBase_Grid, HomeBase_UI |
| Building_Warehouse | Building_Warehouse |
| Overworld | Overworld_Map, Overworld_UI |
| Tower | Tower_Core |

### Transition flow

1. `SceneTransitionRequested` GameEventSO fires (or direct call)
2. Fade black panel to opaque (CanvasGroup alpha, 0.3s lerp)
3. Unload current scene group (SceneManager.UnloadSceneAsync for each)
4. Load target scene group (SceneManager.LoadSceneAsync additive for each)
5. Fade panel to transparent (0.3s lerp)

### Fade panel

A full-screen black Image with CanvasGroup lives on a Canvas in Core_GameManager. It starts transparent and is only visible during transitions. The Canvas uses Sort Order 999 to render above everything.

### Files

- Create: `Assets/_Slopworks/Scripts/Core/ISceneService.cs`
- Create: `Assets/_Slopworks/Scripts/Core/SceneLoader.cs` (pure C#)
- Create: `Assets/_Slopworks/Scripts/Core/SceneLoaderBehaviour.cs` (MonoBehaviour wrapper)
- Create: `Assets/_Slopworks/Scripts/Core/SceneGroupDefinition.cs` (data class)
- Modify: `Bootstrap.cs` (wire SceneLoaderBehaviour)

---

## 5.2 HUD

### Scene

`HomeBase_UI.unity` loaded additively as part of the HomeBase scene group. Contains a single Screen Space Overlay Canvas.

### Elements (all uGUI)

| Element | Location | Binding |
|---------|----------|---------|
| Health bar | Top-left | Filled Image, HealthComponent.CurrentHealth / MaxHealth |
| Hotbar | Bottom-center | 9 slot images, PlayerInventory slots 0-8, number keys 1-9 |
| Crosshair | Center | Static small cross image |
| Interaction prompt | Below crosshair | Text, shown when raycast hits IInteractable |
| Build mode indicator | Top-center | Text/icon, shown when BuildModeController.IsInBuildMode |
| Wave warning | Center-top | Large text, appears on WaveStarted event, hides on WaveEnded |
| Threat meter | Top-right corner | Small filled bar or numeric display |

### Binding pattern

`HUDController` MonoBehaviour on the Canvas. Finds player components at Start (player spawns before UI scene loads). Updates UI elements each frame from component state. Listens to GameEventSO events for wave warnings.

Hotbar slots subscribe to `Inventory.OnSlotChanged` callback (add this to Inventory class if missing). Each slot displays the item icon from ItemDefinitionSO and the stack count.

### Files

- Create: `Assets/Scenes/HomeBase/HomeBase_UI.unity`
- Create: `Assets/_Slopworks/Scripts/UI/HUDController.cs`
- Create: `Assets/_Slopworks/Scripts/UI/HotbarSlotUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/HealthBarUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/InteractionPromptUI.cs`

---

## 5.3 Player Inventory

### PlayerInventory

MonoBehaviour wrapper around the existing `Inventory` class. Owns a 45-slot Inventory (slots 0-8 = hotbar, slots 9-44 = main inventory). Tracks selected hotbar index (0-8).

### Pickup system

`WorldItem` MonoBehaviour placed on ground items. Fields: `ItemDefinitionSO`, `int count`. Sits on the `Interactable` physics layer. Player has a trigger collider (sphere, radius ~1.5). On trigger enter with a WorldItem, attempt `PlayerInventory.TryAdd()`. Success: destroy the WorldItem and log. Failure (inventory full): ignore.

### Inventory UI

Full-screen panel toggled with Tab key (or Escape to close). Shows:
- 36 main inventory slots in a 9x4 grid
- 9 hotbar slots at bottom (mirrored from HUD hotbar)
- Item tooltip on hover (name, description from ItemDefinitionSO)

Interaction: click slot to pick up stack (attaches to cursor), click another slot to place. Shift-click to split. The UI reads/writes directly to the Inventory instance.

### Machine crafting

When player presses E on a machine (IInteractable):
1. Open recipe selection panel (list of recipes from RecipeRegistry.GetForMachine(machineType))
2. Each recipe shows: inputs needed, outputs produced, whether player has ingredients (grayed out if not)
3. Player clicks a recipe to set the machine's active recipe
4. Machine pulls ingredients from player inventory and starts crafting
5. Panel closes, machine runs its existing simulation tick

### Files

- Create: `Assets/_Slopworks/Scripts/Player/PlayerInventory.cs`
- Create: `Assets/_Slopworks/Scripts/Player/WorldItem.cs`
- Create: `Assets/_Slopworks/Scripts/UI/InventoryUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/InventorySlotUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/RecipeSelectionUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/ItemTooltipUI.cs`
- Modify: `Inventory.cs` (add OnSlotChanged callback if missing)

---

## Playtest Scene

`Phase5Playtest.unity` with a `Phase5PlaytestSetup.cs` bootstrapper. Following the established playtest pattern (PortNodePlaytestSetup, StructuralPlaytestSetup):

- Creates player with PlayerInventory containing test items (iron scrap, copper scrap)
- Spawns WorldItems on the ground to pick up
- Places one smelter machine with recipes configured
- Creates a scene transition trigger zone (transitions to a minimal second scene and back)
- All HUD elements active and bound
- Logs every action to console

### Verification checklist

- [ ] Walk over WorldItem, item enters inventory, WorldItem disappears
- [ ] Press Tab, inventory UI opens showing items
- [ ] Click slots to move items around
- [ ] Number keys 1-9 select hotbar slots, HUD highlights selected
- [ ] Press E on smelter, recipe panel opens
- [ ] Select smelt recipe, ingredients deducted from inventory, machine starts crafting
- [ ] Health bar reflects player health
- [ ] Walk into transition zone, screen fades, new scene loads, Core scenes persist
- [ ] Walk back, original scene reloads

---

## Design decisions

- **Simple black fade** for transitions (not loading screen). Upgrade later if scenes get heavy.
- **Prefab-based UI** in HomeBase_UI.unity scene. Production-path, not throwaway.
- **Machine-only crafting** for now. No personal crafting menu.
- **D-004 pattern** maintained: SceneLoader is pure C# with MonoBehaviour wrapper.
- **D-002 compliance**: ISceneService interface allows future Addressables swap.
- **No new dependencies** needed. Uses existing uGUI, Input System, and project patterns.
