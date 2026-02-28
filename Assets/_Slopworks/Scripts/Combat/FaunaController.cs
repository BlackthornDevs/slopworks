using UnityEngine;
using UnityEngine.AI;
using NPBehave;

public class FaunaController : MonoBehaviour
{
    [SerializeField] private FaunaDefinitionSO _def;

    private NavMeshAgent _agent;
    private HealthComponent _health;
    private Root _tree;
    private Collider _collider;

    private Transform _currentTarget;

    private const float WANDER_RADIUS = 10f;
    private const float FLEE_DISTANCE = 12f;

    public FaunaDefinitionSO Definition => _def;
    public HealthComponent Health => _health;
    public bool IsDead => _health != null && !_health.IsAlive;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();

        var healthBehaviour = GetComponent<HealthBehaviour>();
        _health = healthBehaviour.Health;

        _agent.speed = _def.moveSpeed;
        _agent.stoppingDistance = _def.attackRange * 0.5f;
    }

    // TODO: convert to OnStartServer when adding NetworkBehaviour
    private void OnEnable()
    {
        _health.OnDamaged += OnDamaged;
        _health.OnDeath += OnDeath;

        _tree = BuildBehaviorTree();

#if UNITY_EDITOR
        var debugger = gameObject.AddComponent<Debugger>();
        debugger.BehaviorTree = _tree;
#endif

        _tree.Start();
    }

    // TODO: convert to OnStopServer when adding NetworkBehaviour
    private void OnDisable()
    {
        _health.OnDamaged -= OnDamaged;
        _health.OnDeath -= OnDeath;

        _tree?.Stop();
        _tree = null;
    }

    private Root BuildBehaviorTree()
    {
        return new Root(
            new Service(0.2f, UpdatePerception,
                new Selector(

                    // 1. hurt response — immediately interrupt on damage
                    new BlackboardCondition("is_hurt", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Sequence(
                            new Action(() => _tree.Blackboard["is_hurt"] = false),
                            new Wait(0.3f)
                        )
                    ),

                    // 2. flee when near death
                    new Condition(
                        () => _health.IsAlive && _health.CurrentHealth / _health.MaxHealth <= 0.2f,
                        Stops.LOWER_PRIORITY, 0.5f, 0.1f,
                        new Sequence(
                            new Action(PickFleeTarget),
                            new NavMoveTo(_agent, "flee_target", 1f, true)
                        )
                    ),

                    // 3. has target — attack if in range, otherwise chase
                    new BlackboardCondition("has_target", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Selector(
                            new Condition(
                                () => DistanceToTarget() <= _def.attackRange,
                                Stops.NONE,
                                new Sequence(
                                    new Action(MeleeAttack),
                                    new Wait(_def.attackCooldown)
                                )
                            ),
                            new NavMoveTo(_agent, "target_position", _def.attackRange * 0.8f, false)
                        )
                    ),

                    // 4. wander
                    new Sequence(
                        new Action(PickWanderTarget),
                        new NavMoveTo(_agent, "wander_target", 1f, true)
                    )
                )
            )
        );
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
        }
        else
        {
            _currentTarget = null;
            _tree.Blackboard["has_target"] = false;
        }
    }

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

    private float DistanceToTarget()
    {
        if (_currentTarget == null)
            return float.MaxValue;

        return Vector3.Distance(transform.position, _currentTarget.position);
    }

    private void MeleeAttack()
    {
        if (_currentTarget == null)
            return;

        var healthBehaviour = _currentTarget.GetComponent<HealthBehaviour>();
        if (healthBehaviour == null)
            return;

        var damage = new DamageData(
            _def.attackDamage,
            gameObject.name,
            _def.attackDamageType
        );
        healthBehaviour.Health.TakeDamage(damage);
    }

    private void PickFleeTarget()
    {
        Vector3 fleeDir;
        if (_currentTarget != null)
            fleeDir = (transform.position - _currentTarget.position).normalized;
        else
            fleeDir = -transform.forward;

        Vector3 fleePoint = transform.position + fleeDir * FLEE_DISTANCE;
        if (NavMesh.SamplePosition(fleePoint, out NavMeshHit hit, FLEE_DISTANCE, NavMesh.AllAreas))
            _tree.Blackboard["flee_target"] = hit.position;
        else
            _tree.Blackboard["flee_target"] = transform.position + fleeDir * 3f;
    }

    private void PickWanderTarget()
    {
        Vector3 randomDir = UnityEngine.Random.insideUnitSphere * WANDER_RADIUS;
        randomDir += transform.position;
        randomDir.y = transform.position.y;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, WANDER_RADIUS, NavMesh.AllAreas))
            _tree.Blackboard["wander_target"] = hit.position;
        else
            _tree.Blackboard["wander_target"] = transform.position;
    }

    private void OnDamaged(DamageData damage)
    {
        if (_tree != null)
            _tree.Blackboard["is_hurt"] = true;
    }

    private void OnDeath()
    {
        if (_tree != null)
        {
            _tree.Stop();
            _tree = null;
        }

        if (_agent != null && _agent.isOnNavMesh)
            _agent.isStopped = true;

        if (_collider != null)
            _collider.enabled = false;

        Destroy(gameObject, 2f);
    }
}
