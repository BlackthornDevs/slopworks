# Multiplayer networking reference

**Stack:** FishNet + FishySteamworks transport

---

## Framework choice: FishNet

FishNet over Mirror or Unity NGO. Key reasons for Slopworks specifically:
- Server-authoritative by design — critical for factory simulation consistency
- Network LOD (interest management) reduces bandwidth up to 95% for objects outside player view range — matters for a game where players spread across a large base + multiple buildings
- Best performance of free options (~5x Mirror under load)
- FishySteamworks transport handles Steam NAT punchthrough + free relay fallback

**Install:** Asset Store or `https://github.com/FirstGearGames/FishNet`

---

## Transport

Use **FishySteamworks** for Steam distribution. It handles:
1. Direct P2P connection attempt via NAT punchthrough
2. Automatic fallback to Steam relay servers if NAT fails
3. Zero bandwidth cost — Steam covers relay

For local dev/testing without Steam: swap to **Tugboat** (KCP/UDP), included with FishNet.

**FishySteamworks repo:** `https://github.com/FirstGearGames/FishySteamworks`
Requires Facepunch.Steamworks or Steamworks.NET in the project.

---

## Authority model

**Server-authoritative for all simulation objects.** Clients send input/commands; server validates and executes; clients display results.

| Object type | Owner | Pattern |
|---|---|---|
| Player character | Client | Client predicts movement; server validates |
| Factory machines | Server | Clients send config commands via ServerRpc |
| Belt items | Server | Simulation runs server-side only; clients display |
| Building placement | Server | Client requests; server validates space + resources |
| Inventory | Server | Client requests pickup/craft; server modifies |

For a co-op game with no competitive cheating concerns, client prediction on player movement is the only place you need latency compensation. Everything else can afford server-round-trip latency.

---

## Session structure (Slopworks-specific)

Slopworks has three map types: Home Base, Reclaimed Buildings, Overworld. Each is a separate Unity scene. The host runs a listen server.

```
Host starts session
  → Creates game_sessions row in Supabase (lobby status)
  → Starts FishNet ServerManager + ClientManager ("localhost")
  → Other players query Supabase for lobby sessions
  → Players join via FishySteamworks (Steam lobby ID in connection_info JSONB)

Scene transitions:
  → Host initiates scene change (NetworkManager.SceneManager.LoadScene)
  → All connected clients load the same scene simultaneously
  → Factory simulation pauses during transition, resumes on load
```

---

## State sync patterns

### Inventory

```csharp
public class NetworkInventory : NetworkBehaviour
{
    // Delta sync: only changed slots are sent, not the full list
    [SyncObject]
    private readonly SyncList<ItemSlot> _slots = new();

    [ServerRpc(RequireOwnership = true)]
    public void RequestPickupServerRpc(ulong itemId) {
        // Server validates, modifies _slots, SyncList auto-replicates
    }
}
```

### Factory machine state

```csharp
public class MachineNetworkState : NetworkBehaviour
{
    [SyncVar] public MachineStatus Status;       // IDLE/WORKING/BLOCKED
    [SyncVar] public float CraftProgress;        // 0.0 – 1.0
    [SyncVar] public string ActiveRecipeId;

    [ServerRpc]
    public void ConfigureRecipeServerRpc(string recipeId) { ... }
}
```

### Belt items (do NOT use one NetworkObject per item)

Belt contents are a struct list on the belt segment's NetworkObject:

```csharp
[SyncObject]
private readonly SyncList<BeltItem> _items = new();

// BeltItem is a plain struct: itemType + ushort distance offset
// Server runs belt simulation; clients read this list and interpolate positions visually
// Server never sends individual item positions — only the delta-updated list
```

### World state (buildings placed, terrain modified)

Flush to Supabase on: autosave interval, player disconnect, session end.
During play: FishNet NetworkObjects handle in-session sync (placed buildings are spawned NetworkObjects owned by the server).

---

## Network LOD (interest management)

Configure per-scene based on view distance:

```csharp
// On NetworkManager
networkManager.ObserverManager.DefaultCondition = new DistanceCondition(viewDistanceTiles);
```

Home Base: large view distance (players need to see their whole factory)
Reclaimed Buildings: smaller (interior space, close combat)
Overworld: very large (isometric, need to see territory)

---

## Multiplayer pitfalls to avoid

1. **Don't run factory simulation on clients.** Server only. Float drift + timing differences = desync within seconds.
2. **Don't spawn a NetworkObject per belt item.** Model belt contents as a `SyncList<BeltItem>` on the belt segment entity.
3. **Don't set SyncVars before `NetworkObject.Spawn()`.** State set before spawn is not sent to clients.
4. **Don't use RPCs for persistent state.** Late-joining clients don't receive RPCs. Use SyncVar/SyncList for anything a new client needs on join.
5. **Test with simulated latency.** Unity Multiplayer Tools package includes a Network Simulator. Use it. A factory game that works at 0ms LAN latency may break at 100ms internet latency.

---

## LAN testing during development

Use **ParrelSync** to run two Unity editor instances from the same project on one machine:
- Clone 1: Host mode
- Clone 2: Client mode, connect to localhost

This is the recommended dev workflow. No builds needed to test multiplayer.

**ParrelSync:** `https://github.com/VeriorPies/ParrelSync`

---

## Supabase integration points (BACKLOGGED)

> **Status: Deferred.** Supabase integration is backlogged until core gameplay is working. Use local JSON save files for persistence during prototype/vertical slice development. Add Supabase when lobby discovery and cross-session persistence become necessary.

FishNet and Supabase are separate systems. They only touch at:

| Event | FishNet action | Supabase action |
|---|---|---|
| Player creates session | Start ServerManager | Insert `game_sessions` row |
| Player joins | ClientManager.StartConnection | Insert `session_players` row |
| Player disconnects | OnClientDisconnect | Update `session_players.status = 'disconnected'` |
| Autosave trigger | — | Upsert `world_state` + `player_saves` |
| Session ends | StopServer | Update `game_sessions.status = 'ended'` |

The `connection_info` JSONB column on `game_sessions` stores the Steam lobby ID, so clients can find and join the FishNet session via Supabase lobby discovery.
