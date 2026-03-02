using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;
using NPBehave;

public class FaunaController : NetworkBehaviour
{
    [SerializeField] private FaunaDefinitionSO _def;
    [SerializeField] private GameEventSO _onDeathEvent;

    private NavMeshAgent _agent;
    private HealthComponent _health;
    private Root _tree;
    private Collider _collider;

    private FaunaAI _ai;
    private Transform _currentTarget;
    private HealthBehaviour _cachedTargetHealth;

    // pack coordination
    private PackCoordinator _pack;
    private Blackboard _ownBlackboard;

    private const float WANDER_RADIUS = 10f;
    private const float FLEE_DISTANCE = 12f;

    public FaunaDefinitionSO Definition => _def;
    public HealthComponent Health => _health;
    public FaunaAI AI => _ai;
    public bool IsDead => _health != null && !_health.IsAlive;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();
    }

    private void Start()
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        var healthBehaviour = GetComponent<HealthBehaviour>();
        if (healthBehaviour == null)
        {
            Debug.LogError("FaunaController on " + name + " requires a HealthBehaviour component", this);
            enabled = false;
            return;
        }
        _health = healthBehaviour.Health;

        _ai = new FaunaAI(
            _def.attackCooldown,
            _def.fleeConfidenceThreshold,
            _def.alertRange
        );

        _agent.speed = _def.moveSpeed;
        _agent.stoppingDistance = _def.attackRange * 0.5f;

        _health.OnDamaged += OnDamaged;
        _health.OnDeath += OnDeath;

        // register with pack coordinator, create own blackboard as child of shared
        _pack = PackCoordinator.GetOrCreate(_def.faunaId);
        _pack.Register(this);
        _ownBlackboard = new Blackboard(_pack.SharedBlackboard, UnityContext.GetClock());

        _tree = BuildBehaviorTree();

#if UNITY_EDITOR
        var debugger = gameObject.AddComponent<Debugger>();
        debugger.BehaviorTree = _tree;
#endif

        _tree.Start();
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDamaged -= OnDamaged;
            _health.OnDeath -= OnDeath;
        }

        _tree?.Stop();
        _tree = null;
    }

    private Root BuildBehaviorTree()
    {
        return new Root(_ownBlackboard,
            new Service(0.2f, ServiceTick,
                new Selector(

                    // 1. hurt response — immediately interrupt on damage
                    new BlackboardCondition("is_hurt", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Sequence(
                            new Action(() => _tree.Blackboard["is_hurt"] = false),
                            new Wait(0.3f)
                        )
                    ),

                    // 2. ally death reaction — flee if morale broken, else rage boost
                    new Condition(
                        () => CheckAllyDeathReaction(),
                        Stops.LOWER_PRIORITY, 0.5f, 0.1f,
                        new Sequence(
                            new Action(ExecuteAllyDeathReaction),
                            new Wait(0.5f)
                        )
                    ),

                    // 3. flee when near death or morale-forced flee
                    new Condition(
                        () => _health.IsAlive && _ai.ShouldFlee(HealthPercent(), Time.time),
                        Stops.LOWER_PRIORITY, 0.5f, 0.1f,
                        new Sequence(
                            new Action(PickFleeTarget),
                            new NavMoveTo(_agent, "flee_target", 1f, true)
                        )
                    ),

                    // 4. has target — attack, strafe, flank, or chase
                    new BlackboardCondition("has_target", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Selector(
                            // 4a. in range + can attack → melee
                            new Condition(
                                () => DistanceToTarget() <= _def.attackRange && _ai.CanAttack(Time.time),
                                Stops.NONE,
                                new Sequence(
                                    new Action(MeleeAttack),
                                    new Wait(0.3f)
                                )
                            ),
                            // 4b. in range + on cooldown → strafe around target
                            new Condition(
                                () => DistanceToTarget() <= _def.attackRange,
                                Stops.NONE,
                                new Sequence(
                                    new Action(PickStrafeTarget),
                                    new NavMoveTo(_agent, "strafe_target", 0.5f, true)
                                )
                            ),
                            // 4c. not in range + pack has allies → flanked approach
                            new Condition(
                                () => _pack != null && _pack.AliveCount >= 2,
                                Stops.NONE,
                                new Sequence(
                                    new Action(PickFlankTarget),
                                    new NavMoveTo(_agent, "flank_target", _def.attackRange * 0.8f, false)
                                )
                            ),
                            // 4d. direct chase
                            new NavMoveTo(_agent, "target_position", _def.attackRange * 0.8f, false)
                        )
                    ),

                    // 5. alert response — investigate last known player position
                    new Condition(
                        () => CheckPackAlert(),
                        Stops.NONE,
                        new Sequence(
                            new Action(AdoptAlertAsTarget),
                            new NavMoveTo(_agent, "target_position", 2f, true)
                        )
                    ),

                    // 6. wander
                    new Sequence(
                        new Action(PickWanderTarget),
                        new NavMoveTo(_agent, "wander_target", 1f, true)
                    )
                )
            )
        );
    }

    // ── service tick ────────────────────────────────────────

    private void ServiceTick()
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        UpdatePerception();
        UpdatePackState();
    }

    private void UpdatePerception()
    {
        var target = FindBestTarget();

        if (target != null)
        {
            // cache HealthBehaviour on target change
            if (_currentTarget != target.transform)
            {
                _currentTarget = target.transform;
                _cachedTargetHealth = _currentTarget.GetComponent<HealthBehaviour>();
            }
            _tree.Blackboard["has_target"] = true;
            _tree.Blackboard["target_position"] = _currentTarget.position;

            if (_pack != null)
                _pack.ReportPlayerSighted(_currentTarget.position);
        }
        else
        {
            _currentTarget = null;
            _cachedTargetHealth = null;
            _tree.Blackboard["has_target"] = false;
        }
    }

    private void UpdatePackState()
    {
        if (_pack == null) return;

        if (_ai.TryRevertAggression(Time.time))
            _agent.speed = _def.moveSpeed;
    }

    // ── perception ─────────────────────────────────────────

    private GameObject FindBestTarget()
    {
        int playerMask = 1 << PhysicsLayers.Player;

        var sightHits = Physics.OverlapSphere(transform.position, _def.sightRange, playerMask);
        foreach (var hit in sightHits)
        {
            var dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) > _def.sightAngle * 0.5f)
                continue;

            if (Physics.Linecast(transform.position, hit.transform.position, PhysicsLayers.FaunaLOSMask))
                continue;

            return hit.gameObject;
        }

        var hearingHits = Physics.OverlapSphere(transform.position, _def.hearingRange, playerMask);
        if (hearingHits.Length > 0)
            return hearingHits[0].gameObject;

        return null;
    }

    // ── pack awareness (delegates to FaunaAI) ─────────────

    private bool CheckAllyDeathReaction()
    {
        if (_pack == null) return false;
        float allyDeathTime = (float)_pack.SharedBlackboard["ally_death_time"];
        return _ai.HasUnreactedAllyDeath(allyDeathTime);
    }

    private void ExecuteAllyDeathReaction()
    {
        float allyDeathTime = (float)_pack.SharedBlackboard["ally_death_time"];
        var reaction = _ai.ReactToAllyDeath(allyDeathTime, _pack.Confidence, Time.time);

        if (reaction == FaunaAI.AllyDeathReaction.Boost)
            _agent.speed = _def.moveSpeed * _ai.GetSpeedMultiplier(Time.time);
    }

    private bool CheckPackAlert()
    {
        if (_pack == null) return false;

        bool alertValid = (bool)_pack.SharedBlackboard["alert_valid"];
        float alertTime = (float)_pack.SharedBlackboard["alert_time"];
        Vector3 alertPos = (Vector3)_pack.SharedBlackboard["alert_position"];
        float distance = Vector3.Distance(transform.position, alertPos);

        return _ai.IsAlertRelevant(alertValid, alertTime, Time.time, distance);
    }

    private void AdoptAlertAsTarget()
    {
        Vector3 alertPos = (Vector3)_pack.SharedBlackboard["alert_position"];
        _tree.Blackboard["target_position"] = alertPos;
    }

    // ── combat ─────────────────────────────────────────────

    private float DistanceToTarget()
    {
        if (_currentTarget == null) return float.MaxValue;
        return Vector3.Distance(transform.position, _currentTarget.position);
    }

    private void MeleeAttack()
    {
        if (NetworkObject != null && !IsServerInitialized) return;
        if (_currentTarget == null) return;
        if (!_ai.CanAttack(Time.time)) return;

        _ai.RecordAttack(Time.time);

        if (_cachedTargetHealth == null) return;

        var damage = new DamageData(
            _def.attackDamage,
            _def.faunaId,
            _def.attackDamageType,
            transform.position
        );
        _cachedTargetHealth.Health.TakeDamage(damage);
    }

    private void PickStrafeTarget()
    {
        if (_currentTarget == null) return;

        _agent.speed = _def.strafeSpeed;
        bool clockwise = _ai.ToggleStrafeDirection();

        Vector3 strafePoint = CombatMovement.CalculateStrafeTarget(
            transform.position, _currentTarget.position, _def.strafeRadius, clockwise);
        _tree.Blackboard["strafe_target"] = strafePoint;
    }

    private void PickFlankTarget()
    {
        if (_currentTarget == null) return;

        _agent.speed = _def.moveSpeed;
        float angle = _pack.GetFlankAngle(this);

        Vector3 flankPoint = CombatMovement.CalculateFlankTarget(
            transform.position, _currentTarget.position, angle, _def.attackRange);
        _tree.Blackboard["flank_target"] = flankPoint;
    }

    // ── flee + cover ───────────────────────────────────────

    private void PickFleeTarget()
    {
        _agent.speed = _def.moveSpeed;

        Vector3 threatDir;
        if (_currentTarget != null)
            threatDir = (_currentTarget.position - transform.position).normalized;
        else
            threatDir = transform.forward;

        Vector3? coverPoint = CombatMovement.FindCoverPoint(
            transform.position, threatDir, _def.coverSearchRadius);

        if (coverPoint.HasValue)
        {
            _tree.Blackboard["flee_target"] = coverPoint.Value;
            return;
        }

        Vector3 fleeDir = -threatDir;
        Vector3 fleePoint = transform.position + fleeDir * FLEE_DISTANCE;
        if (NavMesh.SamplePosition(fleePoint, out NavMeshHit hit, FLEE_DISTANCE, NavMesh.AllAreas))
            _tree.Blackboard["flee_target"] = hit.position;
        else
            _tree.Blackboard["flee_target"] = transform.position + fleeDir * 3f;
    }

    private float HealthPercent()
    {
        return _health.CurrentHealth / _health.MaxHealth;
    }

    // ── wander ─────────────────────────────────────────────

    private void PickWanderTarget()
    {
        _agent.speed = _def.moveSpeed;

        Vector3 randomDir = UnityEngine.Random.insideUnitSphere * WANDER_RADIUS;
        randomDir += transform.position;
        randomDir.y = transform.position.y;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, WANDER_RADIUS, NavMesh.AllAreas))
            _tree.Blackboard["wander_target"] = hit.position;
        else
            _tree.Blackboard["wander_target"] = transform.position;
    }

    // ── damage + death ─────────────────────────────────────

    private void OnDamaged(DamageData damage)
    {
        if (_tree != null)
            _tree.Blackboard["is_hurt"] = true;
    }

    private void OnDeath()
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        if (_pack != null)
        {
            _pack.ReportAllyDeath(transform.position);
            _pack.Unregister(this);
        }

        if (_tree != null)
        {
            _tree.Stop();
            _tree = null;
        }

        if (_agent != null && _agent.isOnNavMesh)
            _agent.isStopped = true;

        if (_collider != null)
            _collider.enabled = false;

        if (_onDeathEvent != null)
            _onDeathEvent.Raise();

        // TODO: replace with ServerManager.Despawn() when enemy has NetworkObject
        Destroy(gameObject, 2f);
    }
}
