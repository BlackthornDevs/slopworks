using System;
using UnityEngine;

[Serializable]
public class UpgradeTierDefinition
{
    public string tierName;
    public string[] requiredItemIds;
    public int[] requiredAmounts;
    public GameObject[] addedPiecePrefabs;
    public ProductionDefinition productionOverride;
    public float territoryBonus;
    public int workerSlotsBonus;
    public SettlementCapability unlockedCapability = SettlementCapability.None;
}
