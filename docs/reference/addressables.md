# Addressables reference

Unity Addressables for all large asset loading in Slopworks. This covers group structure, async loading patterns, building scenes, and the two-developer workflow.

---

## Why Addressables from day one

Building scenes contain Revit-imported FBX geometry, textures, and lightmaps — 50–100MB each. Without Addressables:
- All buildings load with the initial build, bloating install size
- No way to add buildings as DLC without a full game update
- Referencing a scene from code loads it synchronously into memory

With Addressables:
- Buildings load on demand, async, with proper unloading when the player leaves
- New buildings can ship as remote DLC post-launch
- Memory management: unload building when returning to overworld

**Package:** `com.unity.addressables 1.21+`

---

## Group structure

```
Addressables Groups:
  Always Loaded (Local, Preloaded)
    — ItemRegistry.asset
    — RecipeRegistry.asset
    — Core prefabs (NetworkManager, GameManager)

  HomeBase (Local, Load on Demand)
    — HomeBase_Terrain.unity
    — HomeBase_Grid.unity
    — HomeBase_UI.unity
    — HomeBase_Lighting.unity

  Overworld (Local, Load on Demand)
    — Overworld_Map.unity
    — Overworld_UI.unity

  Buildings (Local initially → Remote for DLC)
    — Building_Template.unity
    — [BuildingName].unity per building

  Machines (Local, Pooled)
    — Smelter.prefab
    — Assembler.prefab
    — [etc.]

  BIM_Geometry (Local initially → Remote for DLC)
    — [BuildingName]_BIM.fbx assets
```

---

## Address naming convention

Use consistent address keys and define them as constants:

```csharp
public static class AssetAddresses
{
    // Buildings
    public const string BuildingFoundry = "building/foundry";
    public const string BuildingMachineShop = "building/machine-shop";
    public const string BuildingWaterTreatment = "building/water-treatment";

    // Machines
    public const string MachineSmelter = "machine/smelter";
    public const string MachineAssembler = "machine/assembler";
    public const string MachineSplitter = "machine/splitter";

    // Scenes
    public const string SceneHomeBase = "scene/home-base";
    public const string SceneOverworld = "scene/overworld";
}
```

Never use raw address strings in game code. If an address changes in the Addressables group, fixing the constant is one change, not a codebase search.

---

## Async scene loading

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public class SceneLoader : MonoBehaviour
{
    private AsyncOperationHandle<SceneInstance> _buildingHandle;

    // Call when player travels to a building
    public async UniTask LoadBuildingAsync(string address)
    {
        if (_buildingHandle.IsValid())
        {
            await UnloadBuildingAsync();
        }

        var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Additive);
        _buildingHandle = handle;
        await handle.ToUniTask();

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"building load failed: {address}");
            Addressables.Release(handle);
            return;
        }

        Debug.Log($"building loaded: {address}");
    }

    // Call when player returns to overworld
    public async UniTask UnloadBuildingAsync()
    {
        if (!_buildingHandle.IsValid()) return;
        await Addressables.UnloadSceneAsync(_buildingHandle).ToUniTask();
        _buildingHandle = default;
    }
}
```

---

## Async prefab loading (machines)

```csharp
// Server spawns machine prefabs via Addressables
[ServerRpc]
private void PlaceMachineServerRpc(string address, Vector3 position)
{
    _ = SpawnMachineAsync(address, position);
}

private async UniTask SpawnMachineAsync(string address, Vector3 position)
{
    var handle = Addressables.LoadAssetAsync<GameObject>(address);
    var prefab = await handle.ToUniTask();

    if (prefab == null)
    {
        Addressables.Release(handle);
        return;
    }

    var instance = Instantiate(prefab, position, Quaternion.identity);
    ServerManager.Spawn(instance);  // FishNet: replicate to all clients
}
```

---

## Remote catalog (DLC buildings)

When adding a building post-launch:
1. Create a new Addressables group: `DLC_[BuildingName]`
2. Set Build Path to `[RemoteBuildPath]`
3. Set Load Path to `[RemoteLoadPath]` (your CDN)
4. Build remote content, upload to CDN
5. Update the remote catalog URL in the game settings

The game fetches the updated catalog at startup and can load the new building without a patch.

---

## Two-developer workflow

Addressables groups live in `Assets/AddressableAssetsData/AssetGroups/`. These are Unity YAML assets — use UnityYAMLMerge. Assign ownership:

- Joe owns: Player, UI, Weapons, Combat effect groups
- Kevin owns: Buildings, Machines, Belt, BIM_Geometry groups

Don't edit the same group file simultaneously.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Building scenes in direct Build Settings list | Only Core scenes in Build Settings; all others via Addressables |
| Synchronous `SceneManager.LoadScene` for large scenes | Use `Addressables.LoadSceneAsync` |
| Forgetting to release handles | Release in `OnDisable` or when scene unloads |
| Raw address strings in code | Use `AssetAddresses` constants class |
| Both devs editing same group file | Assign ownership per asset category |
