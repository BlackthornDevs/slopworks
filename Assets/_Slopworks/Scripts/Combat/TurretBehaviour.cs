using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around TurretController (D-004 pattern).
/// Bridges Unity physics (OverlapSphere) to the pure C# simulation layer.
/// No NetworkBehaviour -- local playtest only for now.
/// </summary>
public class TurretBehaviour : MonoBehaviour
{
    private TurretDefinitionSO _definition;
    private TurretController _controller;
    private Transform _barrelPivot;

    // Reusable buffers to avoid per-frame allocation
    private readonly Collider[] _overlapResults = new Collider[32];
    private readonly List<Vector3> _candidatePositions = new();
    private readonly List<HealthBehaviour> _candidateHealth = new();

    private float _barrelRotationSpeed = 360f;

    public TurretController Controller => _controller;
    public TurretDefinitionSO Definition => _definition;
    public bool HasTarget => _controller != null && _controller.HasTarget;

    /// <summary>
    /// Initialize with pre-created simulation objects (bootstrapper path).
    /// Must be called before the component is activated.
    /// </summary>
    public void Initialize(TurretDefinitionSO definition, TurretController controller, Transform barrelPivot)
    {
        _definition = definition;
        _controller = controller;
        _barrelPivot = barrelPivot;
    }

    private void FixedUpdate()
    {
        if (_controller == null)
            return;

        GatherCandidates();

        var fireEvent = _controller.Tick(Time.fixedDeltaTime, _candidatePositions);

        if (fireEvent.HasValue)
            ApplyFireEvent(fireEvent.Value);
    }

    private void Update()
    {
        if (_controller == null || _barrelPivot == null)
            return;

        if (_controller.HasTarget && _controller.CurrentTargetIndex < _candidateHealth.Count)
        {
            var target = _candidateHealth[_controller.CurrentTargetIndex];
            if (target != null)
            {
                var dir = target.transform.position - _barrelPivot.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    var targetRot = Quaternion.LookRotation(dir);
                    _barrelPivot.rotation = Quaternion.RotateTowards(
                        _barrelPivot.rotation, targetRot, _barrelRotationSpeed * Time.deltaTime);
                }
            }
        }
    }

    private void GatherCandidates()
    {
        _candidatePositions.Clear();
        _candidateHealth.Clear();

        var origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(
            origin, _controller.Range, _overlapResults, PhysicsLayers.FaunaMask);

        for (int i = 0; i < count; i++)
        {
            var col = _overlapResults[i];
            if (col == null) continue;

            var health = col.GetComponent<HealthBehaviour>();
            if (health == null || !health.Health.IsAlive) continue;

            // TurretController expects positions relative to turret origin
            var relativePos = col.transform.position - origin;
            _candidatePositions.Add(relativePos);
            _candidateHealth.Add(health);
        }
    }

    private void ApplyFireEvent(TurretFireEvent fireEvent)
    {
        if (fireEvent.targetIndex < 0 || fireEvent.targetIndex >= _candidateHealth.Count)
            return;

        var target = _candidateHealth[fireEvent.targetIndex];
        if (target == null || !target.Health.IsAlive)
            return;

        var damage = new DamageData(
            fireEvent.damage, fireEvent.sourceId, fireEvent.damageType, transform.position);
        target.Health.TakeDamage(damage);

        Debug.Log($"turret fired at {target.name}: {fireEvent.damage} {fireEvent.damageType} damage");
    }
}
