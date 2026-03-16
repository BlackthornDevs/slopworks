using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Generates a belt mesh from waypoints using SplineMesh.Extrude with a flat
/// rectangular cross-section. Knot rotations are projected to horizontal so the
/// belt never twists on curves or ramps. Bake-and-destroy: temporary
/// SplineContainer is removed after mesh generation. No runtime overhead.
/// </summary>
public static class BeltSplineMeshBaker
{
    private const float BeltWidth = 0.6f;
    private const float BeltThickness = 0.08f;
    private const int SegmentsPerMeter = 4;

    /// <summary>
    /// Generate a belt mesh from route waypoints. Works for all routing modes.
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
            // Use actual 3D tangent so cross-section stays perpendicular to the
            // belt path on ramps and curves with elevation. World-up prevents roll.
            // Connector knots (first/last 2) are projected to horizontal to prevent
            // cross-section twist at the straight-to-curve junction.
            float3 forward;
            if (math.lengthsq(localTanOut) > 0.001f)
                forward = localTanOut;
            else if (math.lengthsq(localTanIn) > 0.001f)
                forward = -localTanIn;
            else
                forward = new float3(0, 0, 1);

            // Project to horizontal at connector endpoints only (first and last knot).
            // Using first/last 2 would cover arc knots on short routes (4 waypoints),
            // forcing all cross-sections horizontal on a descending path = stairstepping.
            bool isConnectorKnot = i == 0 || i == waypoints.Count - 1;
            if (isConnectorKnot)
                forward = new float3(forward.x, 0, forward.z);

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

        // Keep the SplineContainer for item position evaluation (NetworkBeltSegment uses it).
        // Only destroy if this is a ghost preview (Decal layer), not a placed belt.
        if (target.layer == PhysicsLayers.Decal)
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
