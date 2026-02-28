# Tasks for Joe (junior developer)

Assigned by lead. Work on `joe/main`. Merge `master` first to pick up the project skeleton.

---

## TASK J-001: FPS Character Controller + Camera Toggle

**Status:** Complete (2026-02-28)
**Commits:** `c251da1` (main implementation), `ac55db6` (visual feedback on test interactable)

---

## TASK J-002: Clean up TMP extras and merge to master

**Status:** Complete (2026-02-28)
**Commits:** `d5dde89` (TMP cleanup), `c45d3be` (handoff note)

### Handoff notes (from Joe's Claude)

- Master is up to date with both Phase 1 factory systems and the full player controller stack.
- Player controller: FPS movement (WASD/sprint/jump/mouse look), camera mode toggle (V key, FPS/isometric), interaction system (raycast, E key, IInteractable), HUD canvas with interaction prompt text.
- Bug fix included: `InteractionController.OnDisable()` calls `ClearTarget()` so prompt clears when switching to isometric.
- TMP Examples & Extras is gitignored and removed from tracking. TMP runtime stays.
- Dev_Test scene has a test cube at (0, 0.5, 3) on layer 14 with `TestInteractable` -- toggles green on interaction.
- Plugins.meta GUID conflict resolved during merge -- kept Joe's GUID (`44395c0d`).

---

## TASK J-003: Health and damage system

**Priority:** High -- foundation for weapons, enemies, and turrets
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

### Before you start

Pull latest master into joe/main. Master now has all Phase 1 factory systems (grid, machines, belts, inventory, simulation tick).

```bash
git checkout joe/main
git fetch origin master
git merge origin/master
```

### What to build

A plain C# health/damage system following D-004 (simulation logic in plain C# classes, MonoBehaviours as thin wrappers). This is used by players, enemies, turrets, and destructible structures.

### Requirements

1. **DamageData struct** (`Scripts/Combat/DamageData.cs`):
   - `float amount` -- raw damage value
   - `string sourceId` -- who dealt the damage (player ID, turret ID, enemy ID)
   - `DamageType type` -- enum: Kinetic, Explosive, Fire, Toxic
   - Keep it a plain struct, no MonoBehaviour

2. **DamageType enum** (`Scripts/Combat/DamageType.cs`):
   - Kinetic, Explosive, Fire, Toxic

3. **HealthComponent** (`Scripts/Combat/HealthComponent.cs`) -- plain C# class:
   - Constructor takes `float maxHealth`
   - `float CurrentHealth { get; }` and `float MaxHealth { get; }`
   - `bool IsAlive { get; }`
   - `void TakeDamage(DamageData damage)` -- reduces health, clamps to 0
   - `void Heal(float amount)` -- clamps to maxHealth
   - `event Action<DamageData> OnDamaged` -- fires after taking damage
   - `event Action OnDeath` -- fires once when health reaches 0
   - Death event fires only once (no repeat triggers if TakeDamage called on a dead entity)

4. **HealthBehaviour** (`Scripts/Combat/HealthBehaviour.cs`) -- thin MonoBehaviour wrapper:
   - `[SerializeField] float _maxHealth = 100f`
   - Creates `HealthComponent` in `Awake`
   - Exposes `HealthComponent` property for other scripts to read
   - Set GameObject layer based on what it's attached to (don't change layer in this script)

5. **Tests** (`Tests/EditMode/HealthComponentTests.cs`):
   - Damage reduces health
   - Health doesn't go below 0
   - Heal restores health
   - Heal doesn't exceed max
   - OnDamaged fires with correct DamageData
   - OnDeath fires at 0 health
   - OnDeath fires only once
   - IsAlive returns false at 0 health
   - Overkill damage clamps to 0 (not negative)

### Architectural constraints

- **D-004:** Plain C# class, not MonoBehaviour. The MonoBehaviour wrapper is separate.
- **No FishNet yet.** This is single-player logic first. Networking gets layered on later.
- **No ScriptableObject mutation.** HealthComponent holds mutable state as instance fields.

### Why this is first

Weapons need something to damage. Enemies need health. Turrets need something to shoot at. This is the foundation for all of Phase 3 combat.

---

## TASK J-004: Basic hitscan weapon

**Status:** Complete (2026-02-28)
**Commits:** `7699c61`
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

### What to build

A hitscan weapon that fires from the player camera. This integrates with your existing PlayerController and the new HealthComponent.

### Requirements

1. **WeaponDefinitionSO** (`Scripts/Combat/WeaponDefinitionSO.cs`):
   - ScriptableObject (read-only at runtime, per the hard rules)
   - `string weaponId`
   - `float damage`
   - `float fireRate` -- shots per second
   - `float range`
   - `DamageType damageType`
   - `int magazineSize`
   - `float reloadTime`

2. **WeaponController** (`Scripts/Combat/WeaponController.cs`) -- plain C# class:
   - Constructor takes `WeaponDefinitionSO definition`
   - `bool CanFire { get; }` -- checks fire rate cooldown and ammo
   - `void TryFire()` -- returns fire data if cooldown elapsed and has ammo
   - `void Reload()` -- starts reload timer
   - `void Tick(float deltaTime)` -- updates cooldowns
   - `int CurrentAmmo { get; }`
   - `bool IsReloading { get; }`
   - Keep raycast logic OUT of this class -- the MonoBehaviour does the raycast because it needs Physics + Transform

3. **WeaponBehaviour** (`Scripts/Combat/WeaponBehaviour.cs`) -- MonoBehaviour:
   - Holds reference to camera transform for ray origin
   - On fire input: call `WeaponController.TryFire()`, if successful do `Physics.Raycast` with `PhysicsLayers.WeaponHitMask`
   - If hit has `HealthBehaviour`, call `TakeDamage` with correct `DamageData`
   - Wire to New Input System -- use `SlopworksControls` Exploration action map, fire action

4. **Input binding:**
   - Add a `Fire` action to the Exploration action map in `SlopworksInput.inputactions` if not already there
   - Left mouse button
   - Add a `Reload` action bound to R

5. **Tests** (`Tests/EditMode/WeaponControllerTests.cs`):
   - Can't fire when on cooldown
   - Ammo decrements on fire
   - Can't fire with 0 ammo
   - Reload restores ammo after delay
   - Fire rate respected (can't fire faster than definition allows)

### Architectural constraints

- **D-003:** New Input System only. Use `SlopworksControls` callbacks.
- **D-004:** WeaponController is plain C# (testable). WeaponBehaviour does the Unity-specific raycast.
- **Raycast mask:** Use `PhysicsLayers.WeaponHitMask` -- hits Player, Fauna, BIM_Static, Structures. Never magic numbers.
- **No muzzle flash VFX yet.** Just the raycast damage. Visual feedback comes in polish phase.

---

## TASK J-005: Enemy AI with NPBehave

**Status:** Complete (2026-02-28)
**Commits:** `d2b7281`
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`

### Notes
- NPBehave vendored from github.com/meniku/NPBehave (not snozbot — reference doc URL was wrong)
- Custom asmdef created for NPBehave to resolve assembly boundaries with Slopworks.Runtime
- `UnityEngine.Random` must be fully qualified due to NPBehave.Random name collision
- Enemy_Basic exists in Dev_Test scene with all components; prefab asset needs manual drag-to-project
- PlayMode tests noted but not written — behavior tree + NavMesh require running game loop

### Before you start

NPBehave needs to be installed. Clone it into `Assets/_Slopworks/Plugins/`:

```bash
cd Assets/_Slopworks/Plugins
git clone https://github.com/snozbot/npbehave.git NPBehave
```

Remove the `.git` folder inside it (we vendor it, not submodule it):
```bash
rm -rf NPBehave/.git
```

Decision D-007 requires NPBehave for all fauna AI.

### What to build

A basic ground enemy that wanders, detects the player, chases, attacks, takes damage, and dies. Uses NPBehave behavior trees running server-side per `docs/reference/fauna-ai.md`.

### Requirements

1. **FaunaDefinitionSO** (`Scripts/Combat/FaunaDefinitionSO.cs`):
   - ScriptableObject (read-only)
   - `string faunaId`
   - `float maxHealth`
   - `float moveSpeed`
   - `float attackDamage`
   - `float attackRange` -- melee range
   - `float attackCooldown`
   - `float sightRange`, `float sightAngle`, `float hearingRange`
   - `DamageType attackDamageType`

2. **FaunaController** (`Scripts/Combat/FaunaController.cs`) -- MonoBehaviour:
   - Holds `HealthComponent` (plain C# from J-003)
   - Holds `NavMeshAgent` reference
   - Builds NPBehave behavior tree per the structure in `docs/reference/fauna-ai.md`:
     - Hurt response -> Flee when low HP -> Attack in range -> Chase target -> Detect players -> Wander
   - Perception: `OverlapSphere` scan for Player layer, LOS check against `PhysicsLayers.FaunaLOSMask`
   - Throttle perception to every 0.2s (not every frame)
   - On death: play death (disable collider, wait, destroy)

3. **EnemySpawner** (`Scripts/Combat/EnemySpawner.cs`) -- MonoBehaviour:
   - Spawns enemy prefabs at designated points
   - `void SpawnWave(int count)` -- instantiates enemies from a pool/list
   - Set spawned enemies to Fauna layer (layer 9 / `PhysicsLayers.Fauna`)

4. **Enemy prefab** (`Prefabs/Combat/Enemy_Basic.prefab`):
   - Capsule mesh (stand-in visual)
   - CapsuleCollider
   - NavMeshAgent
   - FaunaController
   - HealthBehaviour
   - Layer: Fauna (9)

5. **Tests** (`Tests/EditMode/FaunaControllerTests.cs`):
   - Test what you can in EditMode (HealthComponent integration, damage response)
   - Behavior tree and NavMesh need PlayMode -- note which tests need PlayMode in comments

### Architectural constraints

- **D-007:** Use NPBehave behavior trees, not hand-rolled state machines.
- **Server-only AI:** In the final networked version, all AI ticks run inside `if (!IsServerInitialized) return;`. For now (single-player), skip the guard but structure the code so adding it later is a one-line change.
- **Perception uses PhysicsLayers:** `PhysicsLayers.Player` for OverlapSphere target, `PhysicsLayers.FaunaLOSMask` for line-of-sight blocking.
- **No `LayerMask.GetMask("Player")` string lookups.** Use `1 << PhysicsLayers.Player` constants from `PhysicsLayers.cs`.

### Reference

- `docs/reference/fauna-ai.md` -- full behavior tree structure, perception system, performance budgets
- `docs/reference/physics-layers.md` -- layer numbers, collision matrix, raycast masks

---

## TASK J-006: Wave defense system

**Status:** Complete (2026-02-28)
**Branch:** `joe/main`
**Ownership:** `Scripts/Combat/`
**Depends on:** J-003, J-005

### What to build

A wave controller that spawns groups of enemies at the Home Base perimeter. Waves escalate based on a threat meter that increases as the player claims buildings and expands.

### Requirements

1. **WaveDefinition** (`Scripts/Combat/WaveDefinition.cs`) -- serializable class:
   - `int enemyCount`
   - `float spawnDelay` -- seconds between individual spawns
   - `float timeBetweenWaves` -- rest period after wave clears
   - `string[] faunaIds` -- which enemy types to spawn (lookup via definition)

2. **WaveController** (`Scripts/Combat/WaveController.cs`) -- plain C# class:
   - Constructor takes list of `WaveDefinition`
   - `void StartNextWave()` -- begins spawning
   - `void OnEnemyKilled()` -- tracks remaining enemies
   - `bool IsWaveActive { get; }`
   - `int CurrentWave { get; }`
   - `int EnemiesRemaining { get; }`
   - `event Action OnWaveStarted`
   - `event Action OnWaveEnded` -- fires when all enemies in wave are dead
   - These events will later connect to `GameEventSO` assets (WaveStarted, WaveEnded)

3. **ThreatMeter** (`Scripts/Combat/ThreatMeter.cs`) -- plain C# class:
   - `float ThreatLevel { get; }` -- 0.0 to 1.0
   - `void AddThreat(float amount)` -- called when buildings claimed, supply lines connected
   - Threat level determines which wave definitions activate and enemy count scaling
   - Higher threat = more enemies, tougher variants

4. **WaveControllerBehaviour** (`Scripts/Combat/WaveControllerBehaviour.cs`) -- thin wrapper:
   - Manages timing (rest period between waves)
   - Connects to EnemySpawner for actual instantiation
   - Connects to GameEventSO assets for WaveStarted/WaveEnded

5. **Tests** (`Tests/EditMode/WaveControllerTests.cs`):
   - Wave starts, tracks enemy count
   - Enemy killed decrements remaining
   - Wave ends when all enemies dead
   - OnWaveStarted/OnWaveEnded events fire
   - ThreatMeter increases and clamps

### Architectural constraints

- **D-004:** WaveController and ThreatMeter are plain C# (testable in EditMode).
- **Cross-scene events:** When wiring to scenes later, use `GameEventSO` assets -- never direct references across scenes.

---

## Task order

Build these in sequence:
1. **J-003** first (health/damage) -- everything depends on it
2. **J-004** next (weapon) -- so you can test shooting things
3. **J-005** next (enemy AI) -- needs J-003 for health, needs NPBehave installed
4. **J-006** last (wave controller) -- orchestrates J-005 enemies

After J-006, merge joe/main to master. Kevin's turret system (Phase 4) will use your HealthComponent and DamageData -- keep the interfaces clean.

---

## Code review findings (2026-02-28)

Lead developer reviewed all J-003 through J-006 commits on master. Issues logged in `contradictions.md` as C-004 through C-007. These must be fixed before Phase 3 is considered complete.

### TASK J-007: Fix server authority violations (C-004)

**Priority:** Critical
**Branch:** `joe/main`

Add `if (!IsServerInitialized) return;` guards to:
- `EnemySpawner` -- all spawn/destroy methods
- `WaveControllerBehaviour` -- wave start, enemy tracking
- `FaunaController` -- AI tick, attack execution

Route damage through a `ServerRpc` in `WeaponBehaviour` instead of calling `TakeDamage()` directly on the client.

### TASK J-008: Extract FaunaAI from FaunaController (C-005)

**Priority:** High
**Branch:** `joe/main`

`FaunaController` violates D-004. Split into:
- `FaunaAI` (plain C#) -- threat evaluation, attack timing, state transitions, pack coordination
- `FaunaController` (thin MonoBehaviour) -- owns `FaunaAI`, feeds it perception data, executes movement

The simulation logic must be testable in EditMode without MonoBehaviour dependencies.

### TASK J-009: Remove GameObject.Find usage (C-006)

**Priority:** Medium
**Branch:** `joe/main`

Replace `FindAnyObjectByType<HealthComponent>()` in `PlayerHUD` and `GameObject.Find` patterns in `WeaponBehaviour` with proper dependency injection or `GameEventSO` event bus wiring. Player should receive its own references through spawn setup.

### TASK J-010: Cache GetComponent in FaunaController (C-007)

**Priority:** Low
**Branch:** `joe/main`

Cache the target's `HealthComponent` when target is acquired, clear on target change. Currently calls `GetComponent<HealthComponent>()` on every melee attack.
