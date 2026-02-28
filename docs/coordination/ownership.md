# System and scene ownership

Who owns what. The owner is responsible for implementation on their branch. Ownership prevents merge conflicts -- don't edit files you don't own without coordinating.

---

## Scene ownership

| Scene | Owner | Notes |
|-------|-------|-------|
| `Core_Network.unity` | Shared (master) | NetworkManager config. Changes go through master. |
| `Core_GameManager.unity` | Shared (master) | Session state, registries. Changes go through master. |
| `HomeBase_Terrain.unity` | Kevin | Ground, resource nodes |
| `HomeBase_Grid.unity` | Kevin | Factory grid, belt network, machines |
| `HomeBase_UI.unity` | Joe | HUD, build menu, inventory |
| `HomeBase_Lighting.unity` | Kevin | Directional light, ambient, baked GI |
| `Building_Template.unity` | Kevin | BIM pipeline is Kevin's contribution |
| `Overworld_Map.unity` | Joe | Territory, supply lines |
| `Overworld_UI.unity` | Joe | Overworld HUD, dossier panel |

## Script ownership (by folder)

| Folder | Owner | Notes |
|--------|-------|-------|
| `Scripts/Automation/` | Kevin | Belt, machine, grid, power |
| `Scripts/Combat/` | Joe | Weapons, damage, health, AI |
| `Scripts/Network/` | Joe | FishNet setup, save system |
| `Scripts/Player/` | Joe | Character controller, camera, input |
| `Scripts/World/` | Kevin | Terrain gen, BIM import, chunks |
| `Scripts/UI/` | Joe | HUD, menus, panels |
| `Scripts/Core/` | Shared (master) | Game manager, scene loader, registries, event bus |

## Shared assets (master only)

These live on `master` and both branches merge from it:
- `Scripts/Core/` -- interfaces, base types, registries
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

## Render pipeline assets

Joe owns URP Pipeline Asset and Renderer Asset configuration. Kevin requests render changes via `docs/render-requests.md` (per CLAUDE.md).
