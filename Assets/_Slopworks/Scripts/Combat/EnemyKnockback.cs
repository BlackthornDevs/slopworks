using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyKnockback : MonoBehaviour
{
    [SerializeField] private float _knockbackMultiplier = 0.03f;
    [SerializeField] private float _knockbackDuration = 0.2f;
    [SerializeField] private float _minKnockbackDistance = 0.5f;
    [SerializeField] private float _maxKnockbackDistance = 1.5f;

    private NavMeshAgent _agent;
    private HealthBehaviour _healthBehaviour;
    private Coroutine _knockbackCoroutine;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        _healthBehaviour = GetComponent<HealthBehaviour>();

        if (_healthBehaviour == null)
        {
            Debug.LogError("EnemyKnockback on " + name + " requires a HealthBehaviour component", this);
            enabled = false;
            return;
        }

        if (_agent == null)
        {
            Debug.LogError("EnemyKnockback on " + name + " requires a NavMeshAgent component", this);
            enabled = false;
            return;
        }

        _healthBehaviour.Health.OnDamaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (_healthBehaviour != null)
            _healthBehaviour.Health.OnDamaged -= OnDamaged;
    }

    private void OnDamaged(DamageData damage)
    {
        Vector3 direction = ResolveKnockbackDirection(damage);
        float distance = Mathf.Clamp(damage.amount * _knockbackMultiplier, _minKnockbackDistance, _maxKnockbackDistance);

        if (_knockbackCoroutine != null)
            StopCoroutine(_knockbackCoroutine);

        _knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, distance));
    }

    private Vector3 ResolveKnockbackDirection(DamageData damage)
    {
        if (damage.sourcePosition != Vector3.zero)
            return (transform.position - damage.sourcePosition).normalized;

        return -transform.forward;
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float distance)
    {
        if (!_agent.isOnNavMesh)
            yield break;

        _agent.isStopped = true;
        _agent.ResetPath();

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * distance;

        // snap the target onto the NavMesh so Warp doesn't strand the agent
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, distance + 1f, NavMesh.AllAreas))
            targetPosition = hit.position;
        else
            targetPosition = startPosition;

        float elapsed = 0f;
        while (elapsed < _knockbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _knockbackDuration;

            // ease-out: fast at start, decelerates to a stop
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 lerpedPosition = Vector3.Lerp(startPosition, targetPosition, eased);

            if (_agent.isOnNavMesh)
                _agent.Warp(lerpedPosition);

            yield return null;
        }

        if (_agent.isOnNavMesh)
        {
            _agent.Warp(targetPosition);
            _agent.isStopped = false;
            _agent.ResetPath();
        }

        _knockbackCoroutine = null;
    }
}
