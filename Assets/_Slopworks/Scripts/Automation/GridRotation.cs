using UnityEngine;

/// <summary>
/// Rotates Vector2Int values by 0, 90, 180, or 270 degrees.
/// Used to transform MachinePort offsets and directions when buildings are rotated.
/// </summary>
public static class GridRotation
{
    /// <summary>
    /// Rotates a Vector2Int by the given degrees clockwise (must be 0, 90, 180, or 270).
    /// Matches Unity's Quaternion.Euler(0, degrees, 0) viewed top-down.
    /// 0: (x,y), 90: (y,-x), 180: (-x,-y), 270: (-y,x)
    /// </summary>
    public static Vector2Int Rotate(Vector2Int v, int degrees)
    {
        switch (degrees)
        {
            case 0: return v;
            case 90: return new Vector2Int(v.y, -v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(-v.y, v.x);
            default:
                throw new System.ArgumentException(
                    $"Rotation must be 0, 90, 180, or 270. Got: {degrees}");
        }
    }
}
