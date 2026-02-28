# Fauna AI reference

NPBehave behavior trees running server-side for all fauna in Slopworks. This covers the package, tree structure, perception system, wave controller integration, and performance budgets.

---

## Package choice: NPBehave

Options evaluated:
- **NPBehave** — open-source C# behavior tree (github.com/snozbot/npbehave), no runtime cost, pure C# (testable), Unity-native
- **Behavior Designer** — paid asset ($95), visual editor, actively maintained
- **Unity Behavior** — Unity's new built-in system, experimental

**Recommendation: NPBehave.** Free, no external dependencies, runs as pure C# — which means it's testable without entering Play Mode. The server-only execution constraint means a visual editor provides limited value anyway.

Install via `git clone https://github.com/snozbot/npbehave` into `Assets/_Slopworks/Plugins/`.

---

## Server authority for AI

All fauna AI runs on the server. No exceptions.

```csharp
public class FaunaController : NetworkBehaviour
{
    private BehaviorTree _tree;

    public override void OnStartServer()
    {
        base.OnStartServer();
        BuildBehaviorTree();
        _tree.Start();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        _tree?.Stop();
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        _tree.Clock.Tick(Time.deltaTime);
    }
}
```

Clients never tick the behavior tree. Fauna position and state are SyncVars that clients receive and interpolate.

---

## Behavior tree structure

### Base fauna (melee, ground-based)

```
Root (Selector)
├── Sequence [take damage response]
│   ├── BlackboardCondition("is_hurt", true)
│   ├── Action(PlayHurtAnimation)
│   └── Wait(0.3)
├── Sequence [flee when near death]
│   ├── Condition(health_pct <= 0.2)
│   ├── Action(FindFleeTarget)
│   └── Action(MoveToTarget)
├── Sequence [attack in range]
│   ├── BlackboardCondition("has_target", true)
│   ├── Condition(distance_to_target <= attack_range)
│   └── Action(MeleeAttack)
├── Sequence [chase target]
│   ├── BlackboardCondition("has_target", true)
│   └── Action(ChaseTarget)
├── Sequence [detect players]
│   ├── Action(ScanForTargets)
│   └── BlackboardCondition("has_target", true)
└── Action(Wander)
```

### Ranged fauna

Same tree; replace `MeleeAttack` with `RangedAttack` and add a maintain-distance step before attacking.

---

## Perception system

Three perception layers run in priority order. All checks are server-side.

```csharp
public class FaunaPerception : MonoBehaviour
{
    [SerializeField] private float sightRange = 15f;
    [SerializeField] private float sightAngle = 120f;    // degrees, frontal cone
    [SerializeField] private float hearingRange = 8f;
    [SerializeField] private float smellRange = 4f;

    private Blackboard _blackboard;

    // Call from behavior tree Action node, not every frame
    public void Scan()
    {
        if (!IsServerInitialized) return;

        var target = FindBestTarget();
        _blackboard["has_target"] = target != null;
        if (target != null)
            _blackboard["target"] = target;
    }

    private GameObject FindBestTarget()
    {
        // Sight: frontal cone + line-of-sight check
        var sightHits = Physics.OverlapSphere(transform.position, sightRange, LayerMask.GetMask("Player"));
        foreach (var hit in sightHits)
        {
            var dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) > sightAngle * 0.5f) continue;
            // LOS check: blocked by BIM walls and built structures
            if (Physics.Linecast(transform.position, hit.transform.position,
                LayerMask.GetMask("BIM_Static", "Structures"))) continue;
            return hit.gameObject;
        }

        // Hearing: player within range (doesn't require LOS)
        var hearingHits = Physics.OverlapSphere(transform.position, hearingRange, LayerMask.GetMask("Player"));
        if (hearingHits.Length > 0) return hearingHits[0].gameObject;

        // Smell: very close proximity
        var smellHits = Physics.OverlapSphere(transform.position, smellRange, LayerMask.GetMask("Player"));
        if (smellHits.Length > 0) return smellHits[0].gameObject;

        return null;
    }
}
```

Throttle perception scans — run every 0.2s via the behavior tree's `Wait` node, not every `Update` frame.

---

## Wave controller

Lives in `Core_GameManager.unity`, server-only.

```csharp
public class WaveController : NetworkBehaviour
{
    [SyncVar] private float _threatLevel = 0f;
    private float _waveTimer;

    private void FixedUpdate()
    {
        if (!IsServerInitialized) return;
        TickWave(Time.fixedDeltaTime);
    }

    // Called when player connects a building or increases throughput
    [Server]
    public void IncreaseThreat(float amount)
    {
        _threatLevel = Mathf.Clamp01(_threatLevel + amount);
    }

    [Server]
    private void TickWave(float dt)
    {
        _waveTimer -= dt;
        if (_waveTimer > 0f) return;

        SpawnWave();
        _waveTimer = GetNextWaveInterval();    // shorter at higher threat
    }

    [Server]
    private void SpawnWave()
    {
        int count = Mathf.RoundToInt(Mathf.Lerp(3, 20, _threatLevel));
        // Addressables.LoadAssetAsync → Instantiate → ServerManager.Spawn
    }

    private float GetNextWaveInterval()
    {
        return Mathf.Lerp(120f, 30f, _threatLevel);    // 2 min at 0 threat → 30s at max
    }
}
```

---

## Performance budget

| Fauna count | AI per tick | Notes |
|-------------|-------------|-------|
| ≤ 20 (normal wave) | 2ms | Comfortable |
| ≤ 40 (max wave) | 4ms | Acceptable |
| > 40 | Skip distant AI | Required |

LOD AI: fauna more than 60m from all players skip perception and run only the Wander node.

```csharp
private void Update()
{
    if (!IsServerInitialized) return;

    // Skip expensive perception for distant fauna
    if (!IsAnyPlayerWithin(60f))
    {
        _tree.Clock.Tick(Time.deltaTime);
        return;
    }

    _perception.Scan();
    _tree.Clock.Tick(Time.deltaTime);
}
```

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| AI ticking on clients | `if (!IsServerInitialized) return;` in every Update |
| Behavior tree not stopped on server stop | `OnStopServer` → `_tree.Stop()` |
| Perception scan in Update every frame | Throttle via `Wait` node in behavior tree |
| Sight check ignoring LOS through walls | Linecast with `BIM_Static, Structures` mask |
| Not unregistering OnStopServer | Always stop and null the tree in `OnStopServer` |
