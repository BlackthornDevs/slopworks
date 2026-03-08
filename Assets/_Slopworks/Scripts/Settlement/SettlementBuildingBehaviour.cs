using System.Collections.Generic;
using UnityEngine;

public class SettlementBuildingBehaviour : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementBuildingDefinitionSO _definition;

    private SettlementBuilding _simulation;
    private readonly List<GameObject> _spawnedPieces = new();
    private int _lastVisualLevel;

    public SettlementBuilding Simulation => _simulation;
    public SettlementBuildingDefinitionSO Definition => _definition;

    public void Initialize(SettlementBuilding simulation)
    {
        _simulation = simulation;
        _simulation.OnRepaired += OnRepaired;
        _simulation.OnUpgraded += OnUpgraded;
        RefreshVisuals();
    }

    public string GetInteractionPrompt()
    {
        if (_simulation == null) return "";
        if (_simulation.IsClaimed)
            return $"press E to manage {_definition.displayName}";
        return $"press E to inspect {_definition.displayName} (repair {_simulation.RepairLevel}/{_definition.MaxRepairLevel})";
    }

    public void Interact(GameObject player)
    {
        if (_simulation == null) return;
        var ui = FindAnyObjectByType<SettlementInspectUI>();
        if (ui != null)
            ui.Open(_simulation);
        else
            Debug.Log($"settlement: interacted with {_definition.displayName}, no UI found");
    }

    public void RefreshVisuals()
    {
        if (_simulation == null || _definition == null) return;

        // spawn additive pieces for each completed repair stage
        for (int i = _lastVisualLevel; i < _simulation.RepairLevel; i++)
        {
            if (i >= _definition.repairStages.Length) break;
            var stage = _definition.repairStages[i];
            if (stage.addedPiecePrefabs == null) continue;
            foreach (var prefab in stage.addedPiecePrefabs)
            {
                if (prefab == null) continue;
                var piece = Instantiate(prefab, transform);
                _spawnedPieces.Add(piece);
            }
        }

        // spawn additive pieces for each completed upgrade tier
        int baseUpgradeVisual = Mathf.Max(0, _lastVisualLevel - _definition.MaxRepairLevel);
        for (int i = baseUpgradeVisual; i < _simulation.UpgradeTier; i++)
        {
            if (i >= _definition.upgradeTiers.Length) break;
            var tier = _definition.upgradeTiers[i];
            if (tier.addedPiecePrefabs == null) continue;
            foreach (var prefab in tier.addedPiecePrefabs)
            {
                if (prefab == null) continue;
                var piece = Instantiate(prefab, transform);
                _spawnedPieces.Add(piece);
            }
        }

        _lastVisualLevel = _simulation.RepairLevel + _simulation.UpgradeTier;
    }

    private void OnRepaired(string buildingId, int newLevel)
    {
        Debug.Log($"settlement: {_definition.displayName} repaired to level {newLevel}");
        RefreshVisuals();
    }

    private void OnUpgraded(string buildingId, int newTier)
    {
        Debug.Log($"settlement: {_definition.displayName} upgraded to tier {newTier}");
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        if (_simulation != null)
        {
            _simulation.OnRepaired -= OnRepaired;
            _simulation.OnUpgraded -= OnUpgraded;
        }
        foreach (var piece in _spawnedPieces)
            if (piece != null) Destroy(piece);
    }
}
