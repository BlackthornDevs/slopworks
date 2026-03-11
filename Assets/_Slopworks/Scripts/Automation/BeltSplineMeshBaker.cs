using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Generates a belt mesh from spline data using SplineMesh.Extrude with a flat
/// rectangular cross-section. Knot rotations are set to force world-up so the
/// belt never twists on curves. Bake-and-destroy: temporary SplineContainer is
/// removed after mesh generation. No runtime overhead after baking.
/// </summary>
public static class BeltSplineMeshBaker
{
    private const float BeltWidth = 0.6f;
    private const float BeltThickness = 0.08f;
    private const int SegmentsPerMeter = 4;

    /// <summary>
    /// Generate a belt mesh from Hermite spline data (curved mode).
    /// </summary>
    public static void BakeMesh(GameObject target, BeltSplineData splineData, Material material)
    {
        var bezier = splineData.GetBezierControlPoints();

        EnsureMeshComponents(target, material, out var meshFilter);

        var splineContainer = target.AddComponent<SplineContainer>();
        var spline = splineContainer.Spline;
        spline.Clear();

        var worldToLocal = target.transform.worldToLocalMatrix;

        var localP0 = (float3)worldToLocal.MultiplyPoint3x4(bezier.p0);
        var localP1 = (float3)worldToLocal.MultiplyPoint3x4(bezier.p1);
        var localP2 = (float3)worldToLocal.MultiplyPoint3x4(bezier.p2);
        var localP3 = (float3)worldToLocal.MultiplyPoint3x4(bezier.p3);

        var tangentOut0 = localP1 - localP0;
        var tangentIn1 = localP2 - localP3;
        var localUp = (float3)worldToLocal.MultiplyVector(Vector3.up);

        var rot0 = ComputeKnotRotation(tangentOut0, localUp);
        var rot1 = ComputeKnotRotation(-tangentIn1, localUp);

        var invRot0 = math.inverse(rot0);
        var invRot1 = math.inverse(rot1);
        var localTanOut0 = math.mul(invRot0, tangentOut0);
        var localTanIn1 = math.mul(invRot1, tangentIn1);

        spline.Add(new BezierKnot(localP0, float3.zero, localTanOut0, rot0));
        spline.Add(new BezierKnot(localP3, localTanIn1, float3.zero, rot1));

        float arcLength = splineData.ArcLength;
        int segments = Mathf.Max(2, Mathf.RoundToInt(arcLength * SegmentsPerMeter));

        ExtrudeAndFinalize(target, splineContainer, spline, meshFilter, segments);
    }

    /// <summary>
    /// Generate a belt mesh from orthogonal route waypoints (straight mode).
    /// </summary>
    public static void BakeMesh(GameObject target, List<BeltRouteBuilder.Waypoint> waypoints, Material material)
    {
        EnsureMeshComponents(target, material, out var meshFilter);

        var splineContainer = target.AddComponent<SplineContainer>();
        var spline = splineContainer.Spline;
        spline.Clear();

        var worldToLocal = target.transform.worldToLocalMatrix;
        var localUp = (float3)worldToLocal.MultiplyVector(Vector3.up);

        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            var localPos = (float3)worldToLocal.MultiplyPoint3x4(wp.Position);
            var localTanIn = (float3)worldToLocal.MultiplyVector(wp.TangentIn);
            var localTanOut = (float3)worldToLocal.MultiplyVector(wp.TangentOut);

            // Forward direction for knot rotation: prefer outgoing, fall back to -incoming.
            // Project to horizontal so belt cross-section stays level on ramps
            // (like a real conveyor belt). Doesn't affect the spline path --
            // rot * invRot cancels out when evaluating control points.
            float3 forward;
            if (math.lengthsq(localTanOut) > 0.001f)
                forward = localTanOut;
            else if (math.lengthsq(localTanIn) > 0.001f)
                forward = -localTanIn;
            else
                forward = new float3(0, 0, 1);

            // Remove vertical component for level cross-section
            forward.y = 0;
            if (math.lengthsq(forward) < 0.001f)
                forward = new float3(0, 0, 1);
            else
                forward = math.normalize(forward);

            var rot = ComputeKnotRotation(forward, localUp);
            var invRot = math.inverse(rot);

            var knotTanIn = math.mul(invRot, localTanIn);
            var knotTanOut = math.mul(invRot, localTanOut);

            spline.Add(new BezierKnot(localPos, knotTanIn, knotTanOut, rot));
        }

        float arcLength = BeltRouteBuilder.ComputeRouteLength(waypoints);
        int segments = Mathf.Max(4, Mathf.RoundToInt(arcLength * SegmentsPerMeter));

        ExtrudeAndFinalize(target, splineContainer, spline, meshFilter, segments);
    }

    private static void EnsureMeshComponents(GameObject target, Material material, out MeshFilter meshFilter)
    {
        meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = target.AddComponent<MeshFilter>();

        var meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = target.AddComponent<MeshRenderer>();

        if (material != null)
            meshRenderer.sharedMaterial = material;
    }

    private static void ExtrudeAndFinalize(GameObject target, SplineContainer container,
        Spline spline, MeshFilter meshFilter, int segments)
    {
        var mesh = new Mesh { name = "BakedBeltMesh" };
        var shape = new BeltCrossSection();
        SplineMesh.Extrude(spline, mesh, 1f, segments, true, shape);

        Object.DestroyImmediate(container);

        if (mesh.vertexCount > 0)
            meshFilter.sharedMesh = mesh;
        else
            Debug.LogWarning("belt: SplineMesh.Extrude produced empty mesh");

        target.isStatic = true;
    }

    private static quaternion ComputeKnotRotation(float3 forward, float3 up)
    {
        if (math.lengthsq(forward) < 0.001f)
            return quaternion.identity;
        return quaternion.LookRotationSafe(math.normalize(forward), up);
    }

    /// <summary>
    /// Flat rectangular cross-section for belt extrusion.
    /// 0.6m wide x 0.08m thick. Radius=1 in Extrude call means these
    /// values are used directly as the cross-section dimensions.
    /// </summary>
    private sealed class BeltCrossSection : IExtrudeShape
    {
        private const float HalfWidth = BeltWidth * 0.5f;
        private const float HalfThickness = BeltThickness * 0.5f;

        public int SideCount => 4;

        private static readonly float2[] _verts =
        {
            new float2(-HalfWidth, -HalfThickness),
            new float2( HalfWidth, -HalfThickness),
            new float2( HalfWidth,  HalfThickness),
            new float2(-HalfWidth,  HalfThickness),
        };

        public float2 GetPosition(float t, int index) => _verts[index];
    }
}
