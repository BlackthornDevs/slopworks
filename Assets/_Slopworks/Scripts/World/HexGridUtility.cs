using UnityEngine;

/// <summary>
/// Pure C# hex grid math. Pointy-top axial coordinates (q, r).
/// </summary>
public static class HexGridUtility
{
    private static readonly Vector2Int[] NeighborOffsets = {
        new(1, 0), new(0, 1), new(-1, 1),
        new(-1, 0), new(0, -1), new(1, -1)
    };

    public static Vector3 HexToWorld(int q, int r, float size)
    {
        float x = size * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float z = size * (1.5f * r);
        return new Vector3(x, 0f, z);
    }

    public static Vector3[] HexCorners(Vector3 center, float size)
    {
        var corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60f * i - 30f; // pointy-top: first corner at -30 deg
            float angleRad = Mathf.PI / 180f * angleDeg;
            corners[i] = new Vector3(
                center.x + size * Mathf.Cos(angleRad),
                center.y,
                center.z + size * Mathf.Sin(angleRad));
        }
        return corners;
    }

    public static Vector2Int[] Neighbors(int q, int r)
    {
        var result = new Vector2Int[6];
        for (int i = 0; i < 6; i++)
            result[i] = new Vector2Int(q + NeighborOffsets[i].x, r + NeighborOffsets[i].y);
        return result;
    }

    public static int HexDistance(int q1, int r1, int q2, int r2)
    {
        int dq = q2 - q1;
        int dr = r2 - r1;
        return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
    }
}
