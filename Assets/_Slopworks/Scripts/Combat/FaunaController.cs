using UnityEngine;
using UnityEngine.AI;
using NPBehave;

public class FaunaController : MonoBehaviour
{
    [SerializeField] private FaunaDefinitionSO _def;
    [SerializeField] private GameEventSO _onDeathEvent;

    private NavMeshAgent _agent;
    private HealthComponent _health;
    private Root _tree;
    private Collider _collider;

    private Transform _currentTarget;
    private float _lastAttackTime = float.MinValue;

    // pack coordination
    private PackCoordinator _pack;
    private Blackboard _ownBlackboard;
    private float _lastReactedAllyDeathTime;
    private float _forceFleeUntil;
    private float _aggressionBoostUntil;
    private bool _aggressionBoosted;
    private bool _strafeClockwise;

    private const float WANDER_RADIUS = 10f;
    private const float FLEE_DISTANCE = 12f;
    private const float ALERT_STALE_TIME = 15f;
    private const float FORCE_FLEE_DURATION = 5f;
    private const float AGGRESSION_BOOST_DURATION = 3f;
    private const float AGGRESSION_BOOST_MULTIPLIER = 1.3f;

    public FaunaDefinitionSO Definition => _def;
    public HealthComponent Health => _health;
    public bool IsDead => _health != null && !_health.IsAlive;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();
    }

    private void Start()
    {
        var healthBehaviour = GetComponent<HealthBehaviour>();
        if (healthBehaviour == null)
        {
            Debug.LogError("FaunaController on " + name + " requires a HealthBehaviour component", this);
            enabled = false;
            return;
        }
        _health = healthBehaviour.Health;

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
                        () => HasUnreactedAllyDeath(),
                        Stops.LOWER_PRIORITY, 0.5f, 0.1f,
                        new Sequence(
                            new Action(ReactToAllyDeath),
                            new Wait(0.5f)
                        )
                    ),

                    // 3. flee when near death or morale-forced flee
                    new Condition(
                        () => _health.IsAlive && (HealthPercent() <= 0.2f || Time.time < _forceFleeUntil),
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
                                () => DistanceToTarget() <= _def.attackRange && CanAttack(),
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
                        () => HasPackAlert(),
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
        UpdatePerception();
        UpdatePackState();
    }

    private void UpdatePerception()
    {
        // TODO: add if (!IsServerInitialized) return; for networking
        var target = FindBestTarget();

        if (target != null)
        {
            _currentTarget = target.transform;
            _tree.Blackboard["has_target"] = true;
            _tree.Blackboard["target_position"] = _currentTarget.position;

            if (_pack != null)
                _pack.ReportPlayerSighted(_currentTarget.position);
        }
        else
        {
            _currentTarget = null;
            _tree.Blackboard["has_target"] = false;
        }
    }

    private void UpdatePackState()
    {
        if (_pack == null)
            return;

        // revert aggression boost when expired
        if (_aggressionBoosted && Time.time > _aggressionBoostUntil)
        {
            _agent.speed = _def.moveSpeed;
            _aggressionBoosted = false;
        }
    }

    // ── perception ─────────────────────────────────────────

    private GameObject FindBestTarget()
    {
        int playerMask = 1 << PhysicsLayers.Player;

        // sight: frontal cone + line-of-sight
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

        // hearing: no LOS needed
        var hearingHits = Physics.OverlapSphere(transform.position, _def.hearingRange, playerMask);
        if (hearingHits.Length > 0)
            return hearingHits[0].gameObject;

        return null;
    }

    // ── pack awareness ─────────────────────────────────────

    private bool HasUnreactedAllyDeath()
    {
        if (_pack == null)
            return false;

        float allyDeathTime = (float)_pack.SharedBlackboard["ally_death_time"];
        return allyDeathTime > _lastReactedAllyDeathTime && allyDeathTime > 0f;
    }

    private void ReactToAllyDeath()
    {
        _lastReactedAllyDeathTime = (float)_pack.SharedBlackboard["ally_death_time"];

        if (_pack.Confidence < _def.fleeConfidenceThreshold)
            _forceFleeUntil = Time.time + FORCE_FLEE_DURATION;
        else
            BoostAggression();
    }

    private bool HasPackAlert()
    {
        if (_pack == null)
            return false;

        bool alertValid = (bool)_pack.SharedBlackboard["alert_valid"];
        if (!alertValid)
            return false;

        float alertTime = (float)_pack.SharedBlackboard["alert_time"];
        if (Time.time - alertTime > ALERT_STALE_TIME)
            return false;

        Vector3 alertPos = (Vector3)_pack.SharedBlackboard["alert_position"];
        float distance = Vector3.Distance(transform.position, alertPos);
        return distance <= _def.alertRange;
    }

    private void AdoptAlertAsTarget()
    {
        Vector3 alertPos = (Vector3)_pack.SharedBlackboard["alert_position"];
        _tree.Blackboard["target_position"] = alertPos;
    }

    private void BoostAggression()
    {
        _agent.speed = _def.moveSpeed * AGGRESSION_BOOST_MULTIPLIER;
        _aggressionBoostUntil = Time.time + AGGRESSION_BOOST_DURATION;
        _aggressionBoosted = true;
    }

    // ── combat movement ────────────────────────────────────

    private float DistanceToTarget()
    {
        if (_currentTarget == null)
            return float.MaxValue;

        return Vector3.Distance(transform.position, _currentTarget.position);
    }

    private bool CanAttack()
    {
        return Time.time - _lastAttackTime >= _def.attackCooldown;
    }

    private void MeleeAttack()
    {
        if (_currentTarget == null)
            return;

        if (!CanAttack())
            return;

        _lastAttackTime = Time.time;

        var healthBehaviour = _currentTarget.GetComponent<HealthBehaviour>();
        if (healthBehaviour == null)
            return;

        var damage = new DamageData(
            _def.attackDamage,
            _def.faunaId,
            _def.attackDamageType
        );
        healthBehaviour.Health.TakeDamage(damage);
    }

    private void PickStrafeTarget()
    {
        if (_currentTarget == null)
            return;

        _agent.speed = _def.strafeSpeed;
        _strafeClockwise = !_strafeClockwise;

        Vector3 strafePoint = CombatMovement.CalculateStrafeTarget(
            transform.position, _currentTarget.position, _def.strafeRadius, _strafeClockwise);
        _tree.Blackboard["strafe_target"] = strafePoint;
    }

    private void PickFlankTarget()
    {
        if (_currentTarget == null)
            return;

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

        // try cover-seeking first
        Vector3? coverPoint = CombatMovement.FindCoverPoint(
            transform.position, threatDir, _def.coverSearchRadius);

        if (coverPoint.HasValue)
        {
            _tree.Blackboard["flee_target"] = coverPoint.Value;
            return;
        }

        // fall back to original flee logic
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
        // report to pack before cleanup
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

        Destroy(gameObject, 2f);
    }
}
