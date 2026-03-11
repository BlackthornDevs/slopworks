using UnityEngine;

/// <summary>
/// Attached to every spawned building so raycasts can identify what was hit.
/// </summary>
public class PlacementInfo : MonoBehaviour
{
    public BuildingCategory Category;
    public Vector2Int Cell;
    public Vector2Int Size;
    public float SurfaceY;
    public float ObjectHeight;
    public Vector2Int EdgeDirection;
}
