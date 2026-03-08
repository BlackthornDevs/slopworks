using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure C# simulation class for one settlement building (D-004).
/// No MonoBehaviour. Holds a reference to its definition SO for reading
/// repair stages, upgrades, and production config. All mutable state is
/// per-instance -- the SO is never mutated.
/// </summary>
public class SettlementBuilding
{
    private readonly string _buildingId;
    private readonly SettlementBuildingDefinitionSO _definition;
    private readonly Vector3 _position;
    private readonly List<string> _assignedWorkerIds = new();

    private int _repairLevel;
    private int _upgradeTier;
    private float _productionTimer;

    // -- public properties --

    public string BuildingId => _buildingId;
    public SettlementBuildingDefinitionSO Definition => _definition;
    public Vector3 Position => _position;
    public int RepairLevel => _repairLevel;
    public int UpgradeTier => _upgradeTier;
    public bool IsClaimed => _repairLevel >= _definition.MaxRepairLevel;
    public int WorkerCount => _assignedWorkerIds.Count;
    public IReadOnlyList<string> AssignedWorkerIds => _assignedWorkerIds;

    public int MaxWorkerSlots
    {
        get
        {
            int slots = _definition.workerSlots;
            if (_definition.upgradeTiers != null)
            {
                for (int i = 0; i < _upgradeTier && i < _definition.upgradeTiers.Length; i++)
                {
                    slots += _definition.upgradeTiers[i].workerSlotsBonus;
                }
            }
            return slots;
        }
    }

    public float EffectiveProductionMultiplier =>
        1f + _definition.workerBonusPerSlot * _assignedWorkerIds.Count;

    public float EffectiveTerritoryRadius
    {
        get
        {
            if (!IsClaimed)
                return 0f;

            float radius = _definition.territoryRadius;
            if (_definition.upgradeTiers != null)
            {
                for (int i = 0; i < _upgradeTier && i < _definition.upgradeTiers.Length; i++)
                {
                    radius += _definition.upgradeTiers[i].territoryBonus;
                }
            }
            return radius;
        }
    }

    // -- events --

    public event Action<string, int> OnRepaired;
    public event Action<string> OnClaimed;
    public event Action<string, int> OnUpgraded;
    public event Action<string, string, int> OnProduced;
    public event Action<string, string> OnWorkerAssigned;
    public event Action<string, string> OnWorkerUnassigned;

    // -- constructor --

    public SettlementBuilding(string buildingId, SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        _buildingId = buildingId;
        _definition = definition;
        _position = position;
    }

    // -- repair --

    public void AdvanceRepair()
    {
        if (_repairLevel >= _definition.MaxRepairLevel)
            return;

        _repairLevel++;
        OnRepaired?.Invoke(_buildingId, _repairLevel);

        if (IsClaimed)
            OnClaimed?.Invoke(_buildingId);
    }

    public (string itemId, int amount)[] GetRepairRequirements()
    {
        if (_repairLevel >= _definition.MaxRepairLevel)
            return Array.Empty<(string, int)>();

        var stage = _definition.repairStages[_repairLevel];
        int count = stage.requiredItemIds != null ? stage.requiredItemIds.Length : 0;
        var result = new (string itemId, int amount)[count];
        for (int i = 0; i < count; i++)
        {
            string itemId = stage.requiredItemIds[i];
            int amount = (stage.requiredAmounts != null && i < stage.requiredAmounts.Length)
                ? stage.requiredAmounts[i]
                : 1;
            result[i] = (itemId, amount);
        }
        return result;
    }

    // -- upgrades --

    public bool AdvanceUpgrade()
    {
        if (!IsClaimed)
            return false;

        if (_definition.upgradeTiers == null || _upgradeTier >= _definition.upgradeTiers.Length)
            return false;

        _upgradeTier++;
        _productionTimer = 0f;
        OnUpgraded?.Invoke(_buildingId, _upgradeTier);
        return true;
    }

    public (string itemId, int amount)[] GetUpgradeRequirements()
    {
        if (_definition.upgradeTiers == null || _upgradeTier >= _definition.upgradeTiers.Length)
            return Array.Empty<(string, int)>();

        var tier = _definition.upgradeTiers[_upgradeTier];
        int count = tier.requiredItemIds != null ? tier.requiredItemIds.Length : 0;
        var result = new (string itemId, int amount)[count];
        for (int i = 0; i < count; i++)
        {
            string itemId = tier.requiredItemIds[i];
            int amount = (tier.requiredAmounts != null && i < tier.requiredAmounts.Length)
                ? tier.requiredAmounts[i]
                : 1;
            result[i] = (itemId, amount);
        }
        return result;
    }

    // -- workers --

    public bool AssignWorker(string workerId)
    {
        if (_assignedWorkerIds.Count >= MaxWorkerSlots)
            return false;

        if (_assignedWorkerIds.Contains(workerId))
            return false;

        _assignedWorkerIds.Add(workerId);
        OnWorkerAssigned?.Invoke(_buildingId, workerId);
        return true;
    }

    public bool UnassignWorker(string workerId)
    {
        if (!_assignedWorkerIds.Remove(workerId))
            return false;

        OnWorkerUnassigned?.Invoke(_buildingId, workerId);
        return true;
    }

    // -- production tick --

    public void Tick(float deltaTime)
    {
        if (!IsClaimed)
            return;

        var production = GetActiveProduction();
        if (production == null || production.productionInterval <= 0f)
            return;

        float effectiveInterval = production.productionInterval / EffectiveProductionMultiplier;

        _productionTimer += deltaTime;

        while (_productionTimer >= effectiveInterval)
        {
            _productionTimer -= effectiveInterval;
            OnProduced?.Invoke(_buildingId, production.producedItemId, production.producedAmount);
        }
    }

    // -- network sync setters (no events) --

    public void SetRepairLevel(int level)
    {
        _repairLevel = Mathf.Clamp(level, 0, _definition.MaxRepairLevel);
    }

    public void SetUpgradeTier(int tier)
    {
        int max = _definition.upgradeTiers != null ? _definition.upgradeTiers.Length : 0;
        _upgradeTier = Mathf.Clamp(tier, 0, max);
    }

    // -- private helpers --

    /// <summary>
    /// Returns the active production definition, checking upgrade tiers
    /// from highest to lowest for overrides. Falls back to base definition.
    /// </summary>
    private ProductionDefinition GetActiveProduction()
    {
        if (_definition.upgradeTiers != null)
        {
            for (int i = _upgradeTier - 1; i >= 0; i--)
            {
                if (_definition.upgradeTiers[i].productionOverride != null
                    && !string.IsNullOrEmpty(_definition.upgradeTiers[i].productionOverride.producedItemId))
                {
                    return _definition.upgradeTiers[i].productionOverride;
                }
            }
        }
        return _definition.production;
    }
}
