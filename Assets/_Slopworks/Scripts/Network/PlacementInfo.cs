using UnityEngine;

/// <summary>
/// Attached to every spawned building so raycasts can identify what was hit.
/// </summary>
public class PlacementInfo : MonoBehaviour
{
    public enum PlacementType { Foundation, Wall, Ramp, Machine, Storage, Belt }

    public PlacementType Type;
    public Vector2Int Cell;
    public Vector2Int Size;
    public int Level;
    public Vector2Int EdgeDirection; // for walls/ramps
}
