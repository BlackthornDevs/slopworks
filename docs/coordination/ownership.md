# System and scene ownership

Who owns what. The owner is responsible for implementation on their branch. Ownership prevents merge conflicts -- don't edit files you don't own without coordinating.

**Hard rule (D-019): Never edit a `.unity` scene file you do not own.** If you need something changed in a scene you don't own, document it in `contradictions.md` and let the owner handle it. Prefabs and scripts are fine -- scene files are the conflict boundary.

---

## Scene ownership

HomeBase loads as an additive scene group via `SceneLoaderBehaviour.TransitionTo("HomeBase")`. All HomeBase subscenes load simultaneously.

| Scene | Owner | Notes |
|-------|-------|-------|
| `Core_Network.unity` | Shared (master) | NetworkManager config. Changes go through master. |
| `Core_GameManager.unity` | Shared (master) | Session state, registries, SceneLoaderBehaviour. Changes go through master. |
| `HomeBase.unity` | Kevin | Grid, machines, belts, network objects, spawn points, ConnectionUI |
| `HomeBase_Terrain.unity` | Joe | Terrain, lighting, atmosphere, environment art |
| `HomeBase_UI.unity` | Joe | World-space UI anchors (visor HUD is on the player prefab) |
| `Building_Template.unity` | Kevin | BIM pipeline |
| `Overworld_Map.unity` | Joe | Territory, supply lines |
| `Overworld_UI.unity` | Joe | Overworld HUD, dossier panel |

**Playtest scenes (`Scenes/Playtest/`) are retired per D-019.** Kevin will delete them. Do not create new playtest bootstrapper scenes.

## Script ownership (by folder)

| Folder | Owner | Notes |
|--------|-------|-------|
| `Scripts/Automation/` | Kevin | Belt, machine, grid, power |
| `Scripts/Combat/` | Joe | Weapons, damage, health, AI |
| `Scripts/Network/` | Kevin | FishNet setup, GridManager, NetworkInventory, NetworkBeltSegment |
| `Scripts/Player/` | Kevin | NetworkPlayerController, NetworkBuildController, input |
| `Scripts/World/` | Kevin | Terrain gen, BIM import, chunks, tower, buildings |
| `Scripts/UI/` | Joe | VisorHUD, ReticleController, BuildTooltipUI, VisorBuildAdapter, menus |
| `Scripts/Core/` | Shared (master) | Game manager, scene loader, registries, event bus, BuildStateSnapshot, IBuildStateReceiver |
| `Scripts/Debug/` | Kevin | Retired playtest bootstrapper (pending deletion) |

## Shared assets (master only)

These live on `master` and both branches merge from it:
- `Scripts/Core/` -- interfaces, base types, registries
- `Scripts/Core/PhysicsLayers.cs` -- layer constants and raycast masks (D-013: never edit on feature branches)
- `Scripts/Core/BuildStateSnapshot.cs` -- shared contract between NetworkBuildController and VisorBuildAdapter
- `Scripts/Core/IBuildStateReceiver.cs` -- interface Joe's adapter implements
- `ScriptableObjects/Items/` -- item definitions
- `ScriptableObjects/Recipes/` -- recipe definitions
- `ScriptableObjects/Events/` -- event bus assets
- `ProjectSettings/` -- physics layers, tags, input maps
- `Packages/manifest.json` -- package versions
- `docs/coordination/` -- this folder

## Prefab ownership

| Prefab folder | Owner |
|---------------|-------|
| `Prefabs/Machines/` | Kevin |
| `Prefabs/Belt/` | Kevin |
| `Prefabs/Player/` | Joe |
| `Prefabs/UI/` | Joe |
| `Prefabs/Buildings/` | Kevin |
| `Prefabs/FX/` | Joe |

**NetworkPlayer prefab** (`Prefabs/Player/NetworkPlayer.prefab`) is Joe's. UI components (VisorBuildAdapter, VisorHUD, ReticleController) attach here. Kevin's NetworkBuildController finds `IBuildStateReceiver` via `GetComponentInChildren` on the player -- no scene dependency.

## UI components (Joe owns)

Joe's visor HUD system replaces the old PlayerHUD (deleted per D-019):

| File | Purpose |
|------|---------|
| `VisorHUD.cs` | Runtime-generated persistent gameplay overlay |
| `ReticleController.cs` | TMP crosshair with mode label and fade |
| `ReticleStyle.cs` | Pure data struct for 6 reticle styles |
| `BuildTooltipUI.cs` | Keycap row and action stack for build mode |
| `VisorBuildAdapter.cs` | Implements `IBuildStateReceiver`, bridges snapshot to UI (pending J-030) |
| `VisorAutoBootstrap.cs` | Editor-only auto-spawner (pending J-031 guard) |
| `InventoryUI.cs` | Full inventory grid panel |
| `RecipeSelectionUI.cs` | Machine recipe selection modal |
| `StorageUI.cs` | Storage interaction split panel |
| `InventorySlotUI.cs` | Single inventory grid slot |

## Render pipeline assets

Joe owns URP Pipeline Asset and Renderer Asset configuration. Kevin requests render changes via `docs/render-requests.md` (per CLAUDE.md).
