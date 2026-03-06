# Multiplayer Foundation -- Design

## Goal

Convert the single-player bootstrapper prototype into a networked multiplayer game where two players can connect via host-client model, build a factory, fight enemies, run the tower, and persist world state to Supabase.

## Architecture

FishNet host-authoritative model. One player hosts (acts as server + client in one process), the other joins as client via Tugboat transport. All game objects are proper prefabs registered with FishNet's SpawnablePrefabs. Server owns world state (factory grid, machines, belts, enemies, tower). Clients own their player input and request actions via ServerRpc.

Day-to-day dev: hit Play in the editor, FishNet boots as host, you play locally. No external server needed. ParrelSync for two-editor multiplayer testing.

Later: deploy a headless build to the Ubuntu mini PC as a dedicated server. FishNet supports both host-client and dedicated server without code changes.

## Terrain

Unity Terrain with heightmap. Real scene file with lighting, skybox, and spatial layout. Home base area for factory building, paths to building sites, tower entrance location. Replaces the procedural ground plane from the bootstrapper.

## Transport

Tugboat (TCP/UDP, included with FishNet) for all dev and play. FishySteamworks deferred to post-foundation -- can be added later without changing game code.

## Conversion Sequence

Each step is playable and testable with two connected clients before moving to the next.

### Step 1: Scene + Network + Player

Real terrain scene with NetworkManager and Tugboat transport. Player character becomes a proper prefab with NetworkObject. Two players spawn, walk around, see each other move. FPS camera, mouse look, jump all synced.

### Step 2: Factory Grid + Placement

Server-authoritative FactoryGrid. Client sends placement request via ServerRpc, server validates grid state, calls ServerManager.Spawn() to place the prefab. All structural elements (foundations, walls, ramps) become prefabs with NetworkObject. Clients see placements appear in real-time.

### Step 3: Inventory + Items

PlayerInventory uses SyncList for slot data. WorldItem pickup is server-validated (client walks over item, server checks proximity and adds to inventory). Hotbar state synced. Item definitions (ScriptableObjects) are shared data -- no networking needed for definitions.

### Step 4: Machines + Belts + Simulation

Server runs the simulation tick (FixedUpdate). Machine, belt, and storage are NetworkObject prefabs. Machine status and craft progress synced via SyncVar. Belt item positions synced via SyncList on the belt segment. Clients render visuals from synced state. Port connections validated server-side.

### Step 5: Combat

WeaponBehaviour: client fires ServerRpc with ray origin/direction, server validates and applies damage. EnemySpawner: server spawns NetworkObject enemy prefabs. HealthBehaviour: SyncVar for current health, ObserversRpc for hit effects. WaveController: server-only, clients see enemies appear. FaunaController: server-only AI, clients see synced position/rotation via NetworkTransform.

### Step 6: Tower + Buildings

Tower entry/exit uses FishNet's NetworkManager.SceneManager for additive scene loading (loads same scene for all connected clients). Tower run state synced via SyncVars on a TowerManager NetworkBehaviour. Both players can enter the tower together. Building exploration uses the same scene management pattern.

### Step 7: Persistence (Supabase)

Server serializes world state on autosave (every 5 minutes) and on quit:
- Factory grid: placed buildings with position, rotation, type, recipe config
- Player inventories: slot contents per player
- Tower progress: banked fragments, current tier, cleared buildings
- Supply chain: claimed buildings, supply line config, production state

Server loads world state on session start. Supabase config already templated in `Assets/StreamingAssets/supabase-config.template.json`.

## What Carries Over

Pure C# simulation classes transfer directly -- they don't know about networking:
- Machine, BeltSegment, StorageContainer, FactoryGrid
- TowerController, TowerLootTable, FloorChunkDefinition
- HealthComponent, WeaponController, FaunaAI, WaveController
- Inventory, ItemSlot, ItemInstance
- BuildingPlacementService, ConnectionResolver, PortNode

## What Gets Replaced

Bootstrapper files become reference-only (not deleted, just not used in the real game):
- PlaytestBootstrap.cs, PlaytestContext.cs
- PlaytestToolController.cs
- KevinPlaytestSetup.cs, JoePlaytestSetup.cs
- MasterPlaytestSetup.cs

These are replaced by:
- Real prefabs in Assets/_Slopworks/Prefabs/
- Real scenes in Assets/_Slopworks/Scenes/
- NetworkBehaviour wrappers that own simulation objects
- A GameManager that handles session lifecycle

## Dev Workflow After Conversion

Same as today: open a scene, hit Play, test locally. FishNet boots as host automatically. New scripts work the same -- MonoBehaviour for local-only, NetworkBehaviour for synced. The only new discipline: SyncVar for state that clients need to see, ServerRpc for client-to-server requests, IsServerInitialized guards on server-only logic.
