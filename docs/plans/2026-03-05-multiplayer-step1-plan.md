# Multiplayer Step 1: Scene + Network + Player

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a real terrain scene with FishNet networking where two players can connect, spawn, walk around, and see each other.

**Architecture:** FishNet host-client model with Tugboat transport. Player is a proper prefab with NetworkObject and NetworkTransform. Host starts server+client, second player joins as client. PlayerController converted to NetworkBehaviour with owner-only input. Scene is a real Unity Terrain, not a bootstrapper-generated ground plane.

**Tech Stack:** Unity 6, FishNet (Tugboat transport), Unity Terrain, New Input System

---

### Task 1: Create the multiplayer scene with terrain

**Files:**
- Create: `Assets/_Slopworks/Scenes/Multiplayer/HomeBase.unity` (via editor)

This task is done in the Unity Editor, not code. The scene needs:

**Step 1: Create scene and terrain**

1. File > New Scene (Basic)
2. Save as `Assets/_Slopworks/Scenes/Multiplayer/HomeBase.unity`
3. Add Terrain: GameObject > 3D Object > Terrain
4. Terrain settings: Size 500x500, height 100. Flatten to a mostly level area for the home base with gentle hills around the edges.
5. Add a basic terrain texture (grass or dirt layer)
6. Add directional light if not present
7. Set RenderSettings: skybox, ambient lighting, fog (optional)

**Step 2: Add NetworkManager**

1. Create empty GameObject named `NetworkManager`
2. Add component: `FishNet.Managing.NetworkManager`
3. FishNet auto-adds a `Tugboat` transport component -- verify it's there
4. On the NetworkManager component, verify Transport is set to the Tugboat component
5. Verify `DefaultPrefabObjects` is assigned (FishNet auto-generates this asset)

**Step 3: Add PlayerSpawner**

1. On the same `NetworkManager` GameObject, add component: `PlayerSpawner` (FishNet.Component.Spawning)
2. Leave `_playerPrefab` empty for now (we'll create the prefab in Task 2)
3. Create 2 empty GameObjects as spawn points: `SpawnPoint_0` at (10, 1, 10) and `SpawnPoint_1` at (15, 1, 10)
4. Assign both to the PlayerSpawner's `Spawns` array

**Step 4: Set as build scene**

1. File > Build Settings > Add Open Scenes (HomeBase should be index 0)
2. Remove any other scenes from the build list for now

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scenes/Multiplayer/
git commit -m "Create HomeBase multiplayer scene with terrain and NetworkManager"
```

---

### Task 2: Create the player prefab with NetworkObject

**Files:**
- Create: `Assets/_Slopworks/Prefabs/Player/NetworkPlayer.prefab`
- Create: `Assets/_Slopworks/Scripts/Player/NetworkPlayerController.cs`

The existing `PlayerController.cs` is a MonoBehaviour. We create a new `NetworkPlayerController` that extends `NetworkBehaviour` and handles owner-only input. The old `PlayerController` stays untouched (bootstrapper scenes still use it).

**Step 1: Create NetworkPlayerController script**

```csharp
// Assets/_Slopworks/Scripts/Player/NetworkPlayerController.cs
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;
    [SerializeField] private float _jumpForce = 7f;
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _groundCheckRadius = 0.25f;
    [SerializeField] private float _groundCheckDistance = 0.15f;

    private SlopworksControls _controls;
    private Rigidbody _rb;
    private Transform _cameraTransform;
    private Camera _camera;
    private AudioListener _audioListener;

    private float _pitch;
    private bool _isGrounded;

    private static readonly int GroundMask =
        (1 << PhysicsLayers.Terrain) |
        (1 << PhysicsLayers.BIM_Static) |
        (1 << PhysicsLayers.Structures) |
        (1 << PhysicsLayers.GridPlane);

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            // Disable camera and audio listener on non-owned players
            if (_camera != null) _camera.enabled = false;
            if (_audioListener != null) _audioListener.enabled = false;
            enabled = false;
            return;
        }

        _controls = new SlopworksControls();
        _controls.Exploration.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (IsOwner && _controls != null)
        {
            _controls.Exploration.Disable();
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _camera = GetComponentInChildren<Camera>();
        _cameraTransform = _camera.transform;
        _audioListener = GetComponentInChildren<AudioListener>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        gameObject.layer = PhysicsLayers.Player;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Look();
        CheckJump();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        CheckGround();
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }
        Move();
    }

    private void Look()
    {
        Vector2 look = _controls.Exploration.Look.ReadValue<Vector2>();

        float yaw = look.x * _mouseSensitivity;
        _pitch -= look.y * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        transform.Rotate(0f, yaw, 0f);
        _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = _controls.Exploration.Move.ReadValue<Vector2>();
        bool sprinting = _controls.Exploration.Sprint.IsPressed();
        float speed = sprinting ? _sprintSpeed : _walkSpeed;

        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        Vector3 targetVelocity = direction * speed;

        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
    }

    private void CheckGround()
    {
        float skinOffset = 0.1f;
        Vector3 origin = transform.position + Vector3.up * (_groundCheckRadius + skinOffset);
        _isGrounded = Physics.SphereCast(origin, _groundCheckRadius, Vector3.down,
            out _, _groundCheckDistance + skinOffset, GroundMask);
    }

    private void CheckJump()
    {
        if (_isGrounded && _controls.Exploration.Jump.WasPressedThisFrame())
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _jumpForce, _rb.linearVelocity.z);
        }
    }
}
```

**Step 2: Create the prefab in the editor**

1. In the HomeBase scene, create a new empty GameObject named `NetworkPlayer`
2. Add components in this order:
   - `CapsuleCollider` (radius 0.3, height 1.8, center (0, 0.9, 0))
   - `Rigidbody` (freeze rotation checked, interpolation: Interpolate, collision detection: Continuous)
   - `NetworkObject` (FishNet component)
   - `NetworkTransform` (FishNet component -- syncs position/rotation to all clients)
   - `NetworkPlayerController` (our script from Step 1)
3. Create child GameObject `FPSCamera`:
   - Position: (0, 1.6, 0) (eye height)
   - Add `Camera` component (tag: MainCamera, clear flags: Skybox)
   - Add `AudioListener` component
4. Set the root layer to `Player` (layer 8)
5. Drag the `NetworkPlayer` GameObject from Hierarchy to `Assets/_Slopworks/Prefabs/Player/` to create the prefab
6. Delete the instance from the scene

**Step 3: Wire prefab to PlayerSpawner**

1. Select the `NetworkManager` GameObject in the HomeBase scene
2. On the `PlayerSpawner` component, drag `NetworkPlayer.prefab` to the `Player Prefab` field
3. Verify the prefab appears in FishNet's `DefaultPrefabObjects` asset (FishNet should auto-detect it, but check Window > FishNet > Prefab Objects if not)
4. Save the scene

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkPlayerController.cs
git add Assets/_Slopworks/Prefabs/Player/
git add Assets/_Slopworks/Scenes/Multiplayer/
git commit -m "Create NetworkPlayer prefab with NetworkObject and owner-only input"
```

---

### Task 3: Create connection UI

**Files:**
- Create: `Assets/_Slopworks/Scripts/Network/ConnectionUI.cs`

A simple runtime UI for host/join. No fancy layout needed -- just functional buttons.

**Step 1: Create ConnectionUI script**

```csharp
// Assets/_Slopworks/Scripts/Network/ConnectionUI.cs
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionUI : MonoBehaviour
{
    private NetworkManager _networkManager;
    private string _joinAddress = "localhost";
    private bool _connected;

    private void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;
    }

    private void OnEnable()
    {
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState += OnServerState;
            _networkManager.ClientManager.OnClientConnectionState += OnClientState;
        }
    }

    private void OnDisable()
    {
        if (_networkManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState -= OnServerState;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientState;
        }
    }

    private void OnServerState(ServerConnectionStateArgs args)
    {
        _connected = args.ConnectionState == LocalConnectionState.Started;
        Debug.Log($"network: server state changed to {args.ConnectionState}");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        _connected = args.ConnectionState == LocalConnectionState.Started;
        Debug.Log($"network: client state changed to {args.ConnectionState}");
    }

    private void OnGUI()
    {
        if (_connected)
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 30));
            GUILayout.Label($"Connected ({(_networkManager.IsServerStarted ? "Host" : "Client")})");
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));

        if (GUILayout.Button("Host", GUILayout.Height(30)))
        {
            _networkManager.ServerManager.StartConnection();
            _networkManager.ClientManager.StartConnection();
            Debug.Log("network: starting as host");
        }

        GUILayout.Space(10);
        GUILayout.Label("Join Address:");
        _joinAddress = GUILayout.TextField(_joinAddress);

        if (GUILayout.Button("Join", GUILayout.Height(30)))
        {
            _networkManager.ClientManager.StartConnection(_joinAddress);
            Debug.Log($"network: joining {_joinAddress}");
        }

        GUILayout.EndArea();
    }
}
```

**Step 2: Add to scene**

1. In the HomeBase scene, create empty GameObject named `ConnectionUI`
2. Add `ConnectionUI` component
3. Save scene

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/ConnectionUI.cs
git add Assets/_Slopworks/Scenes/Multiplayer/
git commit -m "Add connection UI for host/join"
```

---

### Task 4: Delete the default Main Camera

**Files:**
- Modify: HomeBase.unity scene

The player prefab has its own camera. A default scene camera will conflict.

**Step 1: Clean up scene**

1. Open HomeBase scene
2. Delete any `Main Camera` GameObject that exists in the scene (not in the player prefab)
3. The player prefab's FPSCamera becomes the active camera when the player spawns
4. Save scene

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scenes/Multiplayer/
git commit -m "Remove default camera from HomeBase scene"
```

---

### Task 5: Test with ParrelSync

**Steps:**

1. Open ParrelSync: Window > ParrelSync > Clones Manager
2. Create a clone if one doesn't exist
3. Open the clone project in a second Unity editor
4. In both editors, open `HomeBase.unity`
5. In editor 1: Hit Play, click "Host"
6. In editor 2: Hit Play, click "Join" (address should be "localhost")
7. Verify:
   - Both players spawn at the spawn points
   - Player 1 can see Player 2's capsule moving
   - Player 2 can see Player 1's capsule moving
   - Each player only controls their own character
   - Camera only renders from the owning player's perspective
   - Walking, sprinting, jumping all sync
8. Check console logs for any errors

**If two-player testing isn't possible right now:** Test single-player host mode. Click "Host", verify player spawns, movement works, no FishNet errors in console.

**Step 6: Commit any fixes**

```bash
git add -A
git commit -m "Step 1 complete: two players can connect and walk around on terrain"
```

---

## Remaining Steps (high-level -- detailed plans written when ready)

### Step 2: Factory Grid + Placement
- Convert `FactoryGrid` to exist on a server-owned `GridManager` NetworkBehaviour
- Client sends placement requests via `[ServerRpc]`
- Server validates, spawns foundation/wall/ramp prefabs via `ServerManager.Spawn()`
- Foundation, wall, ramp become prefabs with `NetworkObject`
- Build mode UI works client-side, placement confirmed server-side

### Step 3: Inventory + Items
- `PlayerInventory` becomes `NetworkBehaviour` with `SyncList<ItemSlot>`
- `WorldItem` pickup: client walks over, server validates proximity and adds to inventory
- Hotbar selection synced via `SyncVar`
- Item definitions stay as ScriptableObjects (shared data, no networking)

### Step 4: Machines + Belts + Simulation
- `FactorySimulation.Tick()` runs server-only in `FixedUpdate`
- Machine/storage/belt become prefabs with `NetworkObject`
- Machine status, craft progress synced via `SyncVar`
- Belt contents synced via `SyncList` on belt segment's `NetworkBehaviour`
- Port connections validated and managed server-side

### Step 5: Combat
- `WeaponBehaviour` already has `[ServerRpc]` pattern -- verify it works
- Enemy prefab with `NetworkObject`, server spawns via `EnemySpawner`
- `HealthBehaviour` syncs HP via `SyncVar`
- `FaunaController` runs server-only (AI), clients see synced transform via `NetworkTransform`
- `WaveController` server-only, fires events when waves start/end

### Step 6: Tower + Buildings
- Tower entry uses FishNet `SceneManager.LoadScene` (additive, for all clients)
- `TowerManager` NetworkBehaviour syncs run state (current floor, cleared chunks, fragment count)
- Both players in tower together, same floor
- Building exploration uses same scene management pattern

### Step 7: Persistence (Supabase)
- Server serializes world state to Supabase on autosave and quit
- Tables: `game_sessions`, `world_state` (JSONB), `player_saves`
- Server loads from Supabase on session start
- Uses existing `supabase-config.template.json` pattern
