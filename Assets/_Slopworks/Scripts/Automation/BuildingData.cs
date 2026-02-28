using UnityEngine;

/// <summary>
/// Data for a building placed on the factory grid.
/// Plain C# class -- no MonoBehaviour dependency.
/// </summary>
public class BuildingData
{
    public string BuildingId { get; set; }
    public Vector2Int Origin { get; set; }
    public Vector2Int Size { get; set; }
    public int Rotation { get; set; }
    public GameObject Instance { get; set; }

    public BuildingData(string buildingId, Vector2Int origin, Vector2Int size, int rotation = 0)
    {
        BuildingId = buildingId;
        Origin = origin;
        Size = size;
        Rotation = rotation;
    }
}
