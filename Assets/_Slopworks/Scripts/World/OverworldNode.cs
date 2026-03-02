/// <summary>
/// Data class for a single node on the overworld map.
/// Coordinates are normalized 0-1 for layout flexibility.
/// </summary>
public class OverworldNode
{
    public string NodeId { get; }
    public string DisplayName { get; }
    public OverworldNodeType NodeType { get; }
    public float MapX { get; }
    public float MapY { get; }
    public BuildingState BuildingState { get; }

    public bool IsActive
    {
        get
        {
            switch (NodeType)
            {
                case OverworldNodeType.HomeBase:
                    return true;
                case OverworldNodeType.Building:
                    return BuildingState != null && BuildingState.IsClaimed;
                case OverworldNodeType.Tower:
                    return false; // powered state wired later
                default:
                    return false;
            }
        }
    }

    public OverworldNode(string nodeId, string displayName, OverworldNodeType nodeType,
        float mapX, float mapY, BuildingState buildingState = null)
    {
        NodeId = nodeId;
        DisplayName = displayName;
        NodeType = nodeType;
        MapX = mapX;
        MapY = mapY;
        BuildingState = buildingState;
    }
}
