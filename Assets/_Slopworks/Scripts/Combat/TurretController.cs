using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result of a turret firing. The MonoBehaviour wrapper applies damage to the target.
/// </summary>
public struct TurretFireEvent
{
    public int targetIndex;
    public float damage;
    public DamageType damageType;
    public string sourceId;
}

/// <summary>
/// Pure C# turret simulation. Detects nearest enemy in range, fires on interval,
/// consumes ammo from internal storage, and requires power to operate.
/// No MonoBehaviour or Unity physics dependency.
/// </summary>
public class TurretController
{
    private readonly TurretDefinitionSO _def;
    private readonly StorageContainer _ammoStorage;
    private float _fireCooldown;
    private int _currentTargetIndex = -1;

    public StorageContainer AmmoStorage => _ammoStorage;
    public int CurrentTargetIndex => _currentTargetIndex;
    public bool HasTarget => _currentTargetIndex >= 0;
    public float Range => _def.range;
    public string AmmoItemId => _def.ammoItemId;

    public TurretController(TurretDefinitionSO definition)
    {
        _def = definition;
        _ammoStorage = new StorageContainer(_def.ammoSlotCount, _def.ammoMaxStackSize);
    }

    /// <summary>
    /// Run one simulation tick. Returns a fire event if the turret fires this tick, null otherwise.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last tick.</param>
    /// <param name="candidates">Enemy positions to evaluate. Index corresponds to the external enemy list.</param>
    /// <param name="powerSatisfaction">Current power satisfaction (0-1). Below threshold, turret won't fire.</param>
    public TurretFireEvent? Tick(float deltaTime, IReadOnlyList<Vector3> candidates, float powerSatisfaction)
    {
        _fireCooldown = Math.Max(0f, _fireCooldown - deltaTime);

        SelectTarget(candidates);

        if (!HasTarget)
            return null;

        if (powerSatisfaction < _def.powerThreshold)
            return null;

        if (_fireCooldown > 0f)
            return null;

        if (!TryConsumeAmmo())
            return null;

        _fireCooldown = _def.fireInterval;

        return new TurretFireEvent
        {
            targetIndex = _currentTargetIndex,
            damage = _def.damagePerShot,
            damageType = _def.damageType,
            sourceId = _def.turretId
        };
    }

    /// <summary>
    /// Simplified tick with default full power. Useful for testing.
    /// </summary>
    public TurretFireEvent? Tick(float deltaTime, IReadOnlyList<Vector3> candidates)
    {
        return Tick(deltaTime, candidates, 1f);
    }

    private void SelectTarget(IReadOnlyList<Vector3> candidates)
    {
        _currentTargetIndex = -1;

        if (candidates == null || candidates.Count == 0)
            return;

        float closestDist = float.MaxValue;
        float rangeSq = _def.range * _def.range;

        for (int i = 0; i < candidates.Count; i++)
        {
            float distSq = candidates[i].sqrMagnitude; // turret assumed at origin for simulation
            if (distSq <= rangeSq && distSq < closestDist)
            {
                closestDist = distSq;
                _currentTargetIndex = i;
            }
        }
    }

    private bool TryConsumeAmmo()
    {
        if (string.IsNullOrEmpty(_def.ammoItemId))
            return true; // no ammo requirement

        return _ammoStorage.TryExtract(out _);
    }
}
