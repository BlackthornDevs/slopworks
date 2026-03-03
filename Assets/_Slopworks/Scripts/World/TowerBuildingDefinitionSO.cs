using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only tower building configuration. Never mutate at runtime.
/// Defines the floor chunk layout, boss floor, and fragment requirements.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/World/Tower Building Definition")]
public class TowerBuildingDefinitionSO : ScriptableObject
{
    public string buildingName;
    public List<FloorChunkDefinition> chunks = new List<FloorChunkDefinition>();
    public int bossChunkIndex;
    public int requiredFragments = 4;
}
