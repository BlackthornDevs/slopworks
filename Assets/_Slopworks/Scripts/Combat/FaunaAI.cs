// plain C# simulation logic for fauna behavior — testable in EditMode without MonoBehaviour
// FaunaController is the thin MonoBehaviour wrapper that owns this and feeds it perception data

public class FaunaAI
{
    private readonly float _attackCooldown;
    private readonly float _fleeConfidenceThreshold;
    private readonly float _alertRange;
    private readonly float _alertStaleTime;
    private readonly float _forceFleeDuration;
    private readonly float _aggressionBoostDuration;
    private readonly float _aggressionBoostMultiplier;

    private float _lastAttackTime = float.MinValue;
    private float _lastReactedAllyDeathTime;
    private float _forceFleeUntil;
    private float _aggressionBoostUntil;
    private bool _aggressionBoosted;
    private bool _strafeClockwise;

    public FaunaAI(float attackCooldown, float fleeConfidenceThreshold, float alertRange,
                   float alertStaleTime = 15f, float forceFleeDuration = 5f,
                   float aggressionBoostDuration = 3f, float aggressionBoostMultiplier = 1.3f)
    {
        _attackCooldown = attackCooldown;
        _fleeConfidenceThreshold = fleeConfidenceThreshold;
        _alertRange = alertRange;
        _alertStaleTime = alertStaleTime;
        _forceFleeDuration = forceFleeDuration;
        _aggressionBoostDuration = aggressionBoostDuration;
        _aggressionBoostMultiplier = aggressionBoostMultiplier;
    }

    // ── attack timing ─────────────────────────────────────

    public bool CanAttack(float currentTime) => currentTime - _lastAttackTime >= _attackCooldown;

    public void RecordAttack(float currentTime) => _lastAttackTime = currentTime;

    // ── threat evaluation ─────────────────────────────────

    public bool ShouldFlee(float healthPercent, float currentTime)
        => healthPercent <= 0.2f || currentTime < _forceFleeUntil;

    // ── pack coordination ─────────────────────────────────

    public bool HasUnreactedAllyDeath(float allyDeathTime)
        => allyDeathTime > _lastReactedAllyDeathTime && allyDeathTime > 0f;

    public enum AllyDeathReaction { Flee, Boost }

    public AllyDeathReaction ReactToAllyDeath(float allyDeathTime, float packConfidence, float currentTime)
    {
        _lastReactedAllyDeathTime = allyDeathTime;

        if (packConfidence < _fleeConfidenceThreshold)
        {
            _forceFleeUntil = currentTime + _forceFleeDuration;
            return AllyDeathReaction.Flee;
        }

        _aggressionBoostUntil = currentTime + _aggressionBoostDuration;
        _aggressionBoosted = true;
        return AllyDeathReaction.Boost;
    }

    // ── alert evaluation ──────────────────────────────────

    public bool IsAlertRelevant(bool alertValid, float alertTime, float currentTime,
                                float distanceToAlert)
    {
        if (!alertValid) return false;
        if (currentTime - alertTime > _alertStaleTime) return false;
        return distanceToAlert <= _alertRange;
    }

    // ── aggression ────────────────────────────────────────

    public bool IsAggressionActive(float currentTime)
        => _aggressionBoosted && currentTime <= _aggressionBoostUntil;

    public float GetSpeedMultiplier(float currentTime)
        => IsAggressionActive(currentTime) ? _aggressionBoostMultiplier : 1f;

    public bool TryRevertAggression(float currentTime)
    {
        if (_aggressionBoosted && currentTime > _aggressionBoostUntil)
        {
            _aggressionBoosted = false;
            return true;
        }
        return false;
    }

    // ── strafe direction ──────────────────────────────────

    public bool ToggleStrafeDirection()
    {
        _strafeClockwise = !_strafeClockwise;
        return _strafeClockwise;
    }
}
