using UnityEngine;

/// <summary>
/// Builds cubic Hermite spline data from two endpoints and directions.
/// Pure math class -- no MonoBehaviour, no Unity Splines dependency.
/// The output can be converted to Bezier control points for Unity Splines.
/// </summary>
public static class BeltSplineBuilder
{
    private const float MinTangentMagnitude = 0.5f;
    private const float MaxTangentMagnitude = 18.67f; // 56m / 3

    /// <summary>
    /// Build spline data from two endpoints and their tangent directions.
    /// Tangent magnitude is distance/3, clamped to [0.5, 18.67].
    /// </summary>
    public static BeltSplineData Build(Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir)
    {
        float distance = Vector3.Distance(startPos, endPos);
        float tangentMag = Mathf.Clamp(distance / 3f, MinTangentMagnitude, MaxTangentMagnitude);

        var t0 = startDir.normalized * tangentMag;
        var t1 = endDir.normalized * tangentMag;

        return new BeltSplineData(startPos, t0, endPos, t1);
    }
}

/// <summary>
/// Immutable cubic Hermite spline data for a single belt segment.
/// Stores the four Hermite parameters and provides evaluation and arc length.
/// </summary>
public class BeltSplineData
{
    public Vector3 P0 { get; }
    public Vector3 T0 { get; }
    public Vector3 P1 { get; }
    public Vector3 T1 { get; }

    private float _arcLength = -1f;
    private const int ArcLengthSamples = 64;

    public BeltSplineData(Vector3 p0, Vector3 t0, Vector3 p1, Vector3 t1)
    {
        P0 = p0;
        T0 = t0;
        P1 = p1;
        T1 = t1;
    }

    /// <summary>
    /// Evaluate the Hermite spline at parameter t in [0, 1].
    /// </summary>
    public Vector3 Evaluate(float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        return h00 * P0 + h10 * T0 + h01 * P1 + h11 * T1;
    }

    /// <summary>
    /// Evaluate the tangent (first derivative) at parameter t.
    /// </summary>
    public Vector3 EvaluateTangent(float t)
    {
        float t2 = t * t;

        float dh00 = 6f * t2 - 6f * t;
        float dh10 = 3f * t2 - 4f * t + 1f;
        float dh01 = -6f * t2 + 6f * t;
        float dh11 = 3f * t2 - 2f * t;

        return dh00 * P0 + dh10 * T0 + dh01 * P1 + dh11 * T1;
    }

    /// <summary>
    /// Total arc length of the spline in world units (meters).
    /// Computed once via numerical integration and cached.
    /// </summary>
    public float ArcLength
    {
        get
        {
            if (_arcLength < 0f)
                _arcLength = ComputeArcLength();
            return _arcLength;
        }
    }

    /// <summary>
    /// Convert Hermite to Bezier control points for Unity Splines.
    /// P1_bezier = P0 + T0/3, P2_bezier = P1 - T1/3
    /// </summary>
    public (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) GetBezierControlPoints()
    {
        return (P0, P0 + T0 / 3f, P1 - T1 / 3f, P1);
    }

    private float ComputeArcLength()
    {
        float length = 0f;
        var prev = Evaluate(0f);
        for (int i = 1; i <= ArcLengthSamples; i++)
        {
            float t = (float)i / ArcLengthSamples;
            var current = Evaluate(t);
            length += Vector3.Distance(prev, current);
            prev = current;
        }
        return length;
    }
}
