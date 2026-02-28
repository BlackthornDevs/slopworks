---
name: slopworks-architecture
description: This skill should be used when making design decisions about system placement, networking architecture, or Supabase vs FishNet data ownership in Slopworks. Load it when deciding where a new system belongs, how two scenes should communicate, which data goes to Supabase vs stays in FishNet, or when setting up the parallel joe/kevin development workflow. Reference before adding new managers, services, or data stores.
version: 0.1.0
---

# Slopworks architecture

Reference for design decisions — where things live, what owns what, and how the pieces connect.

---

## The three world spaces

These are separate loaded scenes, not a streaming open world. No seamless transitions.

| Space | Scene group | Camera | Purpose |
|-------|-------------|--------|---------|
| Home Base | `HomeBase/` | Isometric + first-person | Factory, crafting, defense |
| Reclaimed Buildings | `Buildings/` | First-person | Exploration, combat, MEP restoration |
| Overworld / Network Map | `Overworld/` | Isometric only | Territory, supply lines, scouting |

`Core/` scenes (NetworkManager, GameManager) are always loaded and never unloaded.

### Scene loading model

The host initiates all scene transitions via `NetworkManager.SceneManager.LoadScene`. All clients load the same scene simultaneously. The factory simulation (in `HomeBase_Grid.unity`) pauses during transition and resumes on load.

Scenes load additively. The NetworkManager scene is always the base.

---

## System placement — which systems belong where

### Always in Core (never unloaded)

- `NetworkManager` + FishNet setup
- `GameManager` (session state, threat level, wave controller)
- `SceneLoader`
- `ItemRegistry` + `RecipeRegistry` (loaded at startup, never again)
- `SaveSystem` (Supabase client wrapper)
- ScriptableObject event assets (loaded by reference, not scene-bound)

### Home Base only

- Factory grid (`FactoryGrid`) — the grid exists only while Home Base is loaded
- Belt simulation and machine tick loop
- Power grid manager
- Defense turret manager
- Supply line abstraction (receives resources from connected buildings as a flow, not physical belts)
- Build mode controller

### Buildings only

- Fauna AI + navmesh
- MEP restoration system (tracks which systems are restored)
- Building completion handler (fires `BuildingClaimed` event on full restoration)
- Loot distribution

### Overworld only

- Territory map renderer
- Threat level display
- Building discovery system
- Supply line route visualization (visual representation of the resource network)

---

## FishNet + FishySteamworks networking stack

**FishNet** is the networking framework. Chosen over Mirror and Unity NGO for:
- Server-authoritative by design
- Network LOD (interest management) — reduces bandwidth up to 95% for objects outside player range
- Best performance of free options

**FishySteamworks** is the transport layer for Steam distribution:
1. Attempts direct P2P connection via NAT punchthrough
2. Falls back to Steam relay servers if NAT fails (zero bandwidth cost)

**For local dev and testing without Steam:** swap to Tugboat (KCP/UDP), included with FishNet.

**ParrelSync** for multiplayer testing without builds: two Unity editor instances from the same project directory, one as host, one as client connecting to localhost.

### Authority model summary

| Object type | Owner | Sync mechanism |
|-------------|-------|----------------|
| Player character | Client | ClientRpc prediction + server validation |
| Factory machines | Server | SyncVar for status, ServerRpc for config |
| Belt items | Server | SyncList on segment entity |
| Building placement | Server | Client requests, server validates + spawns |
| Inventory | Server | SyncList for slots, ServerRpc for operations |
| World chunks | Server | Generated server-side, sent to clients on demand |

---

## Supabase integration points (BACKLOGGED)

> **Status: Deferred.** Use local JSON save files during prototype phase. Add Supabase when lobby discovery and cross-session persistence are needed.

FishNet and Supabase are separate systems. They only touch at discrete events.

| Event | FishNet action | Supabase action |
|-------|---------------|----------------|
| Player creates session | Start ServerManager | Insert `game_sessions` row |
| Player joins | ClientManager.StartConnection | Insert `session_players` row |
| Player disconnects | OnClientDisconnect callback | Update `session_players.status = 'disconnected'` |
| Autosave trigger | — | Upsert `world_state` + `player_saves` |
| Session ends | StopServer | Update `game_sessions.status = 'ended'` |

**The `connection_info` JSONB column** on `game_sessions` stores the Steam lobby ID. Clients query Supabase to find open sessions, then use the Steam lobby ID to connect via FishySteamworks.

### What lives in Supabase vs FishNet

**Supabase (persistent world state):**
- `world_state` — placed buildings, claimed territory, resource node states
- `player_saves` — inventory, discovered recipes, player stats
- `game_sessions` — lobby discovery, session metadata
- `session_players` — who is in each session

**FishNet (in-session live state):**
- Machine status (IDLE/WORKING/BLOCKED)
- Belt item contents
- Player positions + inventories
- Craft progress
- Active wave state

**Rule:** If the data must survive a server crash and be available to a new session, it goes to Supabase. If it only matters while the session is live, it stays in FishNet.

---

## Parallel development setup (joe vs kevin)

The project uses a parallel experiment: both developers build their own version from the same design doc, then combine the best parts.

**Branches:**
- `joe/main` — Joe's version
- `kevin/main` — Kevin's version
- `main` — combined/shared

**Scene ownership during parallel development:** Each developer works on different scene files to avoid merge conflicts. When merging, use `git difftool` with UnityYAMLMerge to compare scenes side-by-side.

**Folder convention:** All game code under `Assets/_Slopworks/` (underscore sorts to top in Project window).

**Binary assets via Git LFS:** `.png`, `.fbx`, `.blend`, `.wav`, `.dll`. Scene files via UnityYAMLMerge.

**Short-lived branches only.** Feature branches off your main branch. Merge back within a day or two. Long-lived branches create merge hell with Unity scene files.

---

## BIM pipeline (Kevin's contribution)

Real building models from Revit/Navisworks → explorable Unity levels.

```
Revit/Navisworks model
  → IFC or FBX export
  → Unity FBX importer
  → Mesh cleanup (combine materials, LOD generation)
  → MEP system identification (pipes, ducts = restorable)
  → Collision mesh generation
  → Navmesh bake for fauna AI
  → Fauna + hazard placement
```

Each building becomes a self-contained scene with its own fauna, MEP systems, loot, and AI-generated dossier.

Point cloud data generates damaged/overgrown variants: collapsed sections, vegetation, flooding.

---

## MCP Unity integration

`mcp-unity` bridges Claude Code to the Unity Editor. Claude can create GameObjects, add components, run tests, and read the console without manual copy-paste.

**Install:** Unity Package Manager → git URL `https://github.com/CoderGamester/mcp-unity`

**Setup:**
1. Tools > MCP Unity > Server Window → Configure for Claude Code
2. Start the WebSocket server in Unity
3. Add to `~/.claude.json` MCP config

Use this for scaffolding new scenes, configuring NetworkObject components, running Unit Tests, and reading console errors without leaving the conversation.

---

## Architecture quick reference

**Adding a new game system — checklist:**
1. Which scene does it belong in? (Core/HomeBase/Buildings/Overworld)
2. Is it server-authoritative? Add `if (!IsServerInitialized) return;` guard
3. Does it need cross-scene communication? Use GameEventSO, not direct reference
4. Does state need to survive session end? Write to Supabase at autosave + disconnect
5. Is it a static definition (item type, recipe, building type)? Make it a ScriptableObject

## Additional references

- **`references/scene-structure.md`** — Full scene hierarchy, additive loading order, scene manager patterns
- **`references/supabase-schema.md`** — Database schema, table definitions, and query patterns
