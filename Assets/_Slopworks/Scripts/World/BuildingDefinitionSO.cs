using UnityEngine;

/// <summary>
/// Read-only definition for an explorable building type.
/// Never mutate at runtime -- extract fields in BuildingState constructor.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/Buildings/Building Definition")]
public class BuildingDefinitionSO : ScriptableObject
{
    public string buildingId;
    public string displayName;
    public int requiredMEPCount;
    public string[] producedItemIds;
    public int[] producedAmounts;
    public float productionInterval;
}
