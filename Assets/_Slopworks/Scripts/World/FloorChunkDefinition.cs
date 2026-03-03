using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data class for a single floor chunk in a tower building.
/// Defines spawn points, loot nodes, and stair connections.
/// </summary>
[Serializable]
public class FloorChunkDefinition
{
    public List<Vector3> spawnPoints = new List<Vector3>();
    public List<Vector3> lootNodes = new List<Vector3>();
    public List<int> stairConnections = new List<int>();
}
