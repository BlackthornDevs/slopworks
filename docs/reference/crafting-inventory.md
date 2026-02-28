# Crafting and inventory reference

---

## Data model: the critical split

**Never mutate ScriptableObjects at runtime.** They are read-only static definitions. Per-instance state lives in a separate serializable struct.

```csharp
// Static definition — shared by all items of this type, read-only
[CreateAssetMenu(menuName = "Items/Item Definition")]
public class ItemDefinitionSO : ScriptableObject {
    public string itemId;          // stable string ID for serialization
    public string displayName;
    public Sprite icon;
    public bool isStackable;
    public int maxStackSize;
    public bool hasDurability;
    public float maxDurability;
    public ItemCategory category;
}

// Per-instance runtime state — this is what you serialize and sync
[Serializable]
public struct ItemInstance : INetworkSerializable, IEquatable<ItemInstance> {
    public string definitionId;   // lookup key into ItemRegistry
    public string instanceId;     // null/empty for stackable commodities (ore, wood)
    public float durability;
    public int quality;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
        s.SerializeValue(ref definitionId);
        s.SerializeValue(ref instanceId);
        s.SerializeValue(ref durability);
        s.SerializeValue(ref quality);
    }
    public bool Equals(ItemInstance other) => instanceId == other.instanceId;
}

// A slot is one entry in an inventory
[Serializable]
public struct ItemSlot : INetworkSerializable, IEquatable<ItemSlot> {
    public ItemInstance item;
    public int count;
    public bool isEmpty => count == 0;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter {
        item.NetworkSerialize(s);
        s.SerializeValue(ref count);
    }
    public bool Equals(ItemSlot other) => item.Equals(other.item) && count == other.count;
}
```

### Item registry (O(1) lookup)

```csharp
public static class ItemRegistry {
    private static Dictionary<string, ItemDefinitionSO> _items;

    public static void Initialize(IEnumerable<ItemDefinitionSO> defs) {
        _items = defs.ToDictionary(d => d.itemId);
    }

    public static ItemDefinitionSO Get(string id) =>
        _items.TryGetValue(id, out var def) ? def : null;
}
```

Load all `ItemDefinitionSO` assets at startup via `Resources.LoadAll<ItemDefinitionSO>()` or Addressables. Never scan at runtime.

---

## Recipe system

```csharp
[CreateAssetMenu(menuName = "Crafting/Recipe")]
public class RecipeSO : ScriptableObject {
    public string recipeId;
    public Ingredient[] inputs;
    public Ingredient[] outputs;
    public WorkstationTypeSO requiredWorkstation;  // null = hand-craftable
    public float craftTimeSeconds;
    public bool discoveredByDefault;

    [Serializable]
    public struct Ingredient {
        public ItemDefinitionSO item;
        public int count;
    }
}
```

### Recipe registry (pre-built lookup)

```csharp
public class RecipeRegistry {
    private Dictionary<string, List<RecipeSO>> _byWorkstation;
    private Dictionary<string, List<RecipeSO>> _byOutput;

    // Pre-build at startup — never scan all recipes at runtime
    public void Initialize(IEnumerable<RecipeSO> recipes) {
        foreach (var r in recipes) {
            // index by workstation type and by output item
        }
    }

    public List<RecipeSO> GetForWorkstation(string workstationType) =>
        _byWorkstation.TryGetValue(workstationType, out var list) ? list : null;
}
```

---

## Inventory types (Slopworks-specific)

| Inventory type | Pattern | Notes |
|---|---|---|
| Player backpack | Slot array, 36 slots | Owned by player's NetworkObject |
| Hotbar | Slot array, 9 slots | Subset of backpack or separate |
| Machine input buffer | Typed slots (filter per slot) | Server-owned, not accessible by player directly |
| Machine output buffer | Typed slots | Server-owned, players extract via inserter or manual |
| Storage container | Slot array, configurable size | Accessible to all players in session |

Machine buffers use a slot filter predicate:
```csharp
public class InventorySlot {
    public Func<ItemDefinitionSO, bool> acceptFilter;  // null = accept anything
    public ItemSlot contents;
}
```

---

## Multiplayer inventory sync

### Pattern: server-authoritative, client requests

```csharp
public class NetworkInventory : NetworkBehaviour {
    // NetworkList sends delta updates (changed slots only, not full array)
    [SyncObject]
    private readonly SyncList<ItemSlot> _slots = new();

    // Client requests → server validates and executes
    [ServerRpc(RequireOwnership = true)]
    public void RequestPickupServerRpc(ulong itemNetworkId) {
        // validate: item exists, in range, slot available
        // then modify _slots — SyncList auto-replicates
    }

    [ServerRpc(RequireOwnership = true)]
    public void RequestCraftServerRpc(string recipeId, ulong workstationId) {
        // validate: player near workstation, has ingredients
        // consume inputs, start craft timer
    }
}
```

### Visibility

Player backpacks: `NetworkVariableReadPermission.Owner` — only the owning player sees their inventory. Server can read/write for validation. Other clients never see it.

Storage containers: all players in the active session can read.

Machine buffers: readable by all players near the machine (for UI display), writable by server only.

### Late-joining clients

`SyncList` state is automatically sent to new clients on join (persistent state). No manual "request full state" RPC needed.

---

## Workstation crafting

```csharp
public class WorkstationComponent : NetworkBehaviour {
    public WorkstationTypeSO type;
    public InventoryContainer inputBuffer;
    public InventoryContainer outputBuffer;

    private SyncVar<string> _activeRecipeId;
    private SyncVar<float> _craftProgress;

    private void FixedUpdate() {
        if (!IsServerInitialized) return;
        if (_activeRecipe == null) return;

        _craftProgress.Value += Time.fixedDeltaTime;
        if (_craftProgress.Value >= _activeRecipe.craftTimeSeconds) {
            TryConsumeAndProduce();
            _craftProgress.Value = 0f;
        }
    }
}
```

For automated factory machines, this runs indefinitely without player interaction once configured. For manual crafting benches, requires player to initiate each craft.

---

## Serialization (save data)

Save to Supabase `player_saves.data` as JSON. Rules:
- Never serialize ScriptableObject references (they break across builds)
- Serialize `definitionId` strings + per-instance fields only
- Use a version number in the save document for migration

```json
{
  "saveVersion": 1,
  "slots": [
    { "definitionId": "iron_ore", "count": 64, "instanceId": null, "durability": 1.0 },
    { "definitionId": "iron_pickaxe", "count": 1, "instanceId": "abc-123", "durability": 0.73 }
  ]
}
```

---

## Recipe discovery

Keep discovered recipe state per-player in `player_saves.data`:

```json
{
  "discoveredRecipes": ["basic_plate", "iron_gear", "conveyor_belt"]
}
```

All recipes exist in the registry. The UI filters to show only discovered ones. Mark a recipe discovered when the player first picks up any ingredient, or on explicit research unlock.

In multiplayer, recipe discovery is per-player and should NOT be synced globally — each player progresses their own recipe knowledge.

---

## Pitfalls

1. **Mutating ScriptableObjects at runtime.** Corrupts the asset on disk in Editor, creates shared-state bugs in builds. SOs are read-only always.
2. **`NetworkVariable<Dictionary<...>>`** sends the full dict on any change. Use `SyncList<ItemSlot>` for inventories — only changed slots go over the wire.
3. **Client-side item pickup.** Always server-authoritative. Clients request; server validates and executes.
4. **Belt items in inventory containers.** In-transit items on conveyors are NOT in an inventory. They're entities in a belt segment buffer.
5. **Stacking bugs.** When stacking, increment `count` on the existing slot. Don't create two slots with the same `definitionId`. Keep `instanceId` null for stackable commodities.

---

## Reference implementations

- `https://github.com/adammyhre/Unity-Inventory-System` — clean 2024 implementation with UI Toolkit + Addressables
- `https://github.com/NaolShow/Sacados` — multiplayer inventory built on NGO, clean server-authority pattern
- `https://github.com/amineloop/unity-mirror-server-inventory` — Mirror-based strict server-authority example
