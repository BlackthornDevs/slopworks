using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Settlement/Building Definition")]
public class SettlementBuildingDefinitionSO : ScriptableObject
{
    public string buildingId;
    public string displayName;
    public SettlementBuildingType buildingType;

    [Header("Repair")]
    public RepairStageDefinition[] repairStages;

    [Header("Production")]
    public ProductionDefinition production;

    [Header("Territory")]
    public float territoryRadius = 20f;
    public float connectionRange = 100f;

    [Header("Workers")]
    public int workerSlots = 2;
    public float workerBonusPerSlot = 0.25f;

    [Header("Upgrades")]
    public UpgradeTierDefinition[] upgradeTiers;

    public int MaxRepairLevel => repairStages != null ? repairStages.Length : 0;
}
