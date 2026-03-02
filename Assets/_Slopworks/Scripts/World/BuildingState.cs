using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# class tracking one building's runtime state (D-004).
/// Takes definition data in constructor -- does not hold SO reference.
/// </summary>
public class BuildingState
{
    private readonly string _buildingId;
    private readonly string _displayName;
    private readonly int _requiredMEPCount;
    private readonly string[] _producedItemIds;
    private readonly int[] _producedAmounts;
    private readonly float _productionInterval;
    private readonly List<MEPRestorePoint> _restorePoints = new();

    private float _productionTimer;
    private int _restoredCount;

    public string BuildingId => _buildingId;
    public string DisplayName => _displayName;
    public bool IsClaimed => _restoredCount >= _requiredMEPCount;
    public int RestoredCount => _restoredCount;
    public int RequiredMEPCount => _requiredMEPCount;
    public IReadOnlyList<MEPRestorePoint> RestorePoints => _restorePoints;

    public event Action OnPointRestored;
    public event Action OnBuildingClaimed;
    public event Action<string, int> OnItemProduced;

    public BuildingState(string buildingId, string displayName, int requiredMEPCount,
        string[] producedItemIds, int[] producedAmounts, float productionInterval)
    {
        _buildingId = buildingId;
        _displayName = displayName;
        _requiredMEPCount = requiredMEPCount;
        _producedItemIds = producedItemIds ?? Array.Empty<string>();
        _producedAmounts = producedAmounts ?? Array.Empty<int>();
        _productionInterval = productionInterval;
    }

    /// <summary>
    /// Convenience constructor from SO definition. Extracts fields -- does not hold reference.
    /// </summary>
    public BuildingState(BuildingDefinitionSO def)
        : this(def.buildingId, def.displayName, def.requiredMEPCount,
               def.producedItemIds, def.producedAmounts, def.productionInterval)
    {
    }

    public void AddRestorePoint(MEPRestorePoint point)
    {
        _restorePoints.Add(point);
    }

    /// <summary>
    /// Restores the point with the given ID. Returns true if newly restored.
    /// Fires OnPointRestored and potentially OnBuildingClaimed.
    /// </summary>
    public bool RestorePoint(string pointId)
    {
        for (int i = 0; i < _restorePoints.Count; i++)
        {
            if (_restorePoints[i].PointId != pointId)
                continue;

            if (!_restorePoints[i].Restore())
                return false;

            _restoredCount++;
            OnPointRestored?.Invoke();

            if (IsClaimed)
                OnBuildingClaimed?.Invoke();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Advances production timer on claimed buildings.
    /// Unclaimed buildings do nothing.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsClaimed || _productionInterval <= 0f)
            return;

        _productionTimer += deltaTime;

        while (_productionTimer >= _productionInterval)
        {
            _productionTimer -= _productionInterval;

            for (int i = 0; i < _producedItemIds.Length; i++)
            {
                int amount = i < _producedAmounts.Length ? _producedAmounts[i] : 1;
                OnItemProduced?.Invoke(_producedItemIds[i], amount);
            }
        }
    }
}
