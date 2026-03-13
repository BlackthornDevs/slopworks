using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltSplineBuilderTests
{
    private const float Tolerance = 0.1f;

    [Test]
    public void DefaultMode_StartAndEnd_MatchInputPositions()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.right, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);

        AssertVec3Near(start, BeltRouteBuilder.EvaluateRoute(waypoints, len, 0f));
        AssertVec3Near(end, BeltRouteBuilder.EvaluateRoute(waypoints, len, 1f));
    }

    [Test]
    public void DefaultMode_StraightBelt_MidpointIsHalfway()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.right, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);
        var mid = BeltRouteBuilder.EvaluateRoute(waypoints, len, 0.5f);

        AssertVec3Near(new Vector3(5, 1, 0), mid);
    }

    [Test]
    public void DefaultMode_CurvedBelt_EndpointsMatch()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(5, 1, 5);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.forward, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);

        AssertVec3Near(start, BeltRouteBuilder.EvaluateRoute(waypoints, len, 0f));
        AssertVec3Near(end, BeltRouteBuilder.EvaluateRoute(waypoints, len, 1f));
    }

    [Test]
    public void DefaultMode_StraightBelt_RouteLengthMatchesDistance()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.right, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);

        Assert.AreEqual(10f, len, 0.5f);
    }

    [Test]
    public void DefaultMode_CurvedBelt_LongerThanStraightLine()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(5, 0, 5);
        var straightDist = Vector3.Distance(start, end);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.forward, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);

        Assert.Greater(len, straightDist);
    }

    [Test]
    public void DefaultMode_ShortBelt_NoNaN()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.5f, 0, 0);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.right, BeltRoutingMode.Default);
        float len = BeltRouteBuilder.ComputeRouteLength(waypoints);
        var mid = BeltRouteBuilder.EvaluateRoute(waypoints, len, 0.5f);

        Assert.IsFalse(float.IsNaN(mid.x));
    }

    [Test]
    public void DefaultMode_ProducesMultipleWaypoints()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 0, 0);

        var waypoints = BeltRouteBuilder.Build(start, Vector3.right, end, Vector3.right, BeltRoutingMode.Default);

        Assert.Greater(waypoints.Count, 2, "freeform should produce sampled waypoints");
    }

    [Test]
    public void AllModes_ProduceValidWaypoints()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(5, 0, 5);

        foreach (var mode in new[] { BeltRoutingMode.Default, BeltRoutingMode.Straight, BeltRoutingMode.Curved })
        {
            var waypoints = BeltRouteBuilder.Build(start, Vector3.forward, end, Vector3.right, mode);
            float len = BeltRouteBuilder.ComputeRouteLength(waypoints);

            Assert.Greater(waypoints.Count, 0, $"{mode} produced no waypoints");
            Assert.Greater(len, 0f, $"{mode} produced zero-length route");
            AssertVec3Near(start, waypoints[0].Position);
            AssertVec3Near(end, waypoints[waypoints.Count - 1].Position);
        }
    }

    private void AssertVec3Near(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance, $"X mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.y, actual.y, Tolerance, $"Y mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.z, actual.z, Tolerance, $"Z mismatch: expected {expected}, got {actual}");
    }
}
