using System;
using UnityEngine;

[Serializable]
public class RepairStageDefinition
{
    public string[] requiredItemIds;
    public int[] requiredAmounts;
    public GameObject[] addedPiecePrefabs;
    public SettlementCapability unlockedCapability = SettlementCapability.None;
}
