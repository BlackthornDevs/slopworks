using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltSplineBuilderTests
{
    private const float Tolerance = 0.01f;

    [Test]
    public void StraightBelt_StartAndEnd_MatchInputPositions()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);

        AssertVec3Near(start, data.Evaluate(0f));
        AssertVec3Near(end, data.Evaluate(1f));
    }

    [Test]
    public void StraightBelt_Midpoint_IsHalfway()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);
        var mid = data.Evaluate(0.5f);

        AssertVec3Near(new Vector3(5, 1, 0), mid);
    }

    [Test]
    public void CurvedBelt_StartAndEnd_MatchInputPositions()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(5, 1, 5);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.forward);

        AssertVec3Near(start, data.Evaluate(0f));
        AssertVec3Near(end, data.Evaluate(1f));
    }

    [Test]
    public void ArcLength_StraightBelt_MatchesDistance()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);

        Assert.AreEqual(10f, data.ArcLength, 0.1f);
    }

    [Test]
    public void ArcLength_CurvedBelt_LongerThanStraightLine()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(5, 0, 5);
        var straightDist = Vector3.Distance(start, end);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.forward);

        Assert.Greater(data.ArcLength, straightDist);
    }

    [Test]
    public void TangentMagnitude_ClampedToRange()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.5f, 0, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);
        var mid = data.Evaluate(0.5f);

        Assert.IsFalse(float.IsNaN(mid.x));
    }

    [Test]
    public void BezierControlPoints_MatchHermiteToBezierConversion()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(9, 0, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);
        var bezier = data.GetBezierControlPoints();

        // distance=9, tangentMag=9/3=3, T0=right*3=(3,0,0), T1=right*3=(3,0,0)
        // P1_bezier = P0 + T0/3 = (0,0,0) + (1,0,0) = (1,0,0)
        // P2_bezier = P1 - T1/3 = (9,0,0) - (1,0,0) = (8,0,0)
        AssertVec3Near(new Vector3(0, 0, 0), bezier.p0);
        AssertVec3Near(new Vector3(1, 0, 0), bezier.p1);
        AssertVec3Near(new Vector3(8, 0, 0), bezier.p2);
        AssertVec3Near(new Vector3(9, 0, 0), bezier.p3);
    }

    [Test]
    public void EvaluateTangent_AtStart_MatchesStartDirection()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);
        var tangent = data.EvaluateTangent(0f).normalized;

        AssertVec3Near(Vector3.right, tangent);
    }

    private void AssertVec3Near(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance, $"X mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.y, actual.y, Tolerance, $"Y mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.z, actual.z, Tolerance, $"Z mismatch: expected {expected}, got {actual}");
    }
}
