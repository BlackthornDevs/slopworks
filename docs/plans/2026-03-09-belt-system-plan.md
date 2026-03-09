# Belt System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the current Manhattan-grid belt system with Satisfactory-style curved conveyor belts using Unity Splines, spline-based mesh generation, and free-form two-click placement.

**Architecture:** Existing BeltSegment/BeltNetwork simulation is geometry-agnostic and stays unchanged. New `BeltSegment.FromArcLength()` factory method feeds arc-length-based subdivisions into the same gap-based transport. BeltPort MonoBehaviour on prefabs replaces grid-adjacency auto-wiring. Unity Splines bake-and-destroy for mesh generation. BeltSnapAnchor on supports for placement guides.

**Tech Stack:** Unity 2022.3+, Unity Splines 2.7+, FishNet 4.x, NUnit

**Design doc:** `docs/plans/2026-03-09-belt-system-design.md`
**Research:** `docs/research/belts/` (14 files)
**Design decisions:** `docs/research/belts/design-decisions.md` (D-BLT-001 through D-BLT-006)

---

## Task 1: Add Unity Splines Package

**Files:**
- Modify: `Packages/manifest.json`

**Step 1: Add com.unity.splines to the project**

Run: Open Unity Package Manager or add to manifest.json:
```json
"com.unity.splines": "2.7.2"
```

**Step 2: Verify import**

Open Unity, confirm no compilation errors. The package should appear in Package Manager under "Unity Registry".

**Step 3: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "Add Unity Splines 2.7 package for belt mesh generation"
```

---

## Task 2: BeltSegment.FromArcLength Factory Method

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Automation/BeltSegment.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/BeltSegmentTests.cs`

**Step 1: Write the failing test**

Add to `BeltSegmentTests.cs`:

```csharp
[Test]
public void FromArcLength_3Meters_Creates300Subdivisions()
{
    var belt = BeltSegment.FromArcLength(3.0f);

    Assert.AreEqual(300, belt.TotalLength);
}

[Test]
public void FromArcLength_1Point5Meters_Creates150Subdivisions()
{
    var belt = BeltSegment.FromArcLength(1.5f);

    Assert.AreEqual(150, belt.TotalLength);
}

[Test]
public void FromArcLength_ZeroLength_ThrowsArgument()
{
    Assert.Throws<System.ArgumentOutOfRangeException>(() => BeltSegment.FromArcLength(0f));
}

[Test]
public void FromArcLength_NegativeLength_ThrowsArgument()
{
    Assert.Throws<System.ArgumentOutOfRangeException>(() => BeltSegment.FromArcLength(-1f));
}

[Test]
public void FromArcLength_VeryShort_ClampsToMinimum1()
{
    // 0.005m = 0.5 subdivisions, rounds to 1
    var belt = BeltSegment.FromArcLength(0.005f);

    Assert.GreaterOrEqual(belt.TotalLength, 1);
}
```

**Step 2: Run tests to verify they fail**

Run tests in Unity Test Runner (EditMode). Expected: FAIL -- `FromArcLength` does not exist.

**Step 3: Implement FromArcLength**

Add to `BeltSegment.cs` after the existing constructor (line 39):

```csharp
/// <summary>
/// Create a belt segment from arc length in meters.
/// Uses SubdivisionsPerTile (100) subdivisions per meter of arc length.
/// This is the factory method for curved belts where length is computed
/// from spline arc length rather than Manhattan tile distance.
/// </summary>
public static BeltSegment FromArcLength(float arcLengthMeters)
{
    if (arcLengthMeters <= 0f)
        throw new ArgumentOutOfRangeException(nameof(arcLengthMeters),
            "Arc length must be positive.");

    int subdivisions = Math.Max(1,
        (int)Math.Round(arcLengthMeters * BeltItem.SubdivisionsPerTile));

    return new BeltSegment(subdivisions);
}

// Private constructor for subdivision-based creation (arc length path)
private BeltSegment(int totalSubdivisions, bool fromSubdivisions)
{
    if (totalSubdivisions <= 0)
        throw new ArgumentOutOfRangeException(nameof(totalSubdivisions),
            "Total subdivisions must be positive.");

    _totalLength = totalSubdivisions;
    _terminalGap = (ushort)Math.Min(_totalLength, ushort.MaxValue);
}
```

Update `FromArcLength` to use the private constructor:

```csharp
public static BeltSegment FromArcLength(float arcLengthMeters)
{
    if (arcLengthMeters <= 0f)
        throw new ArgumentOutOfRangeException(nameof(arcLengthMeters),
            "Arc length must be positive.");

    int subdivisions = Math.Max(1,
        (int)Math.Round(arcLengthMeters * BeltItem.SubdivisionsPerTile));

    return new BeltSegment(subdivisions, fromSubdivisions: true);
}
```

**Step 4: Run tests to verify they pass**

Run tests in Unity Test Runner. Expected: all 5 new tests PASS, all existing tests still PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltSegment.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltSegmentTests.cs
git commit -m "Add BeltSegment.FromArcLength factory method for curved belts"
```

---

## Task 3: BeltPort Component

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltPort.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/BeltPortTests.cs`

**Step 1: Write the failing test**

Create `BeltPortTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltPortTests
{
    [Test]
    public void BeltPort_DefaultDirection_IsInput()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(BeltPortDirection.Input, port.Direction);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_CanSetOutput()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();
        port.Direction = BeltPortDirection.Output;

        Assert.AreEqual(BeltPortDirection.Output, port.Direction);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_SlotIndex_DefaultsToZero()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(0, port.SlotIndex);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_WorldDirection_MatchesTransformForward()
    {
        var go = new GameObject("TestPort");
        go.transform.forward = Vector3.right;
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(Vector3.right, port.WorldDirection);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_WorldPosition_MatchesTransformPosition()
    {
        var parent = new GameObject("Parent");
        var child = new GameObject("Port");
        child.transform.SetParent(parent.transform);
        child.transform.localPosition = new Vector3(1f, 0f, 0f);
        var port = child.AddComponent<BeltPort>();

        Assert.AreEqual(new Vector3(1f, 0f, 0f), port.WorldPosition);

        Object.DestroyImmediate(parent);
    }

    [Test]
    public void FindPorts_ReturnsAllBeltPortsOnGameObject()
    {
        var parent = new GameObject("Machine");
        var input = new GameObject("InputPort");
        input.transform.SetParent(parent.transform);
        input.AddComponent<BeltPort>().Direction = BeltPortDirection.Input;

        var output = new GameObject("OutputPort");
        output.transform.SetParent(parent.transform);
        output.AddComponent<BeltPort>().Direction = BeltPortDirection.Output;

        var ports = parent.GetComponentsInChildren<BeltPort>();

        Assert.AreEqual(2, ports.Length);

        Object.DestroyImmediate(parent);
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL -- `BeltPort` and `BeltPortDirection` do not exist.

**Step 3: Implement BeltPort**

Create `Assets/_Slopworks/Scripts/Automation/BeltPort.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Direction a belt port faces for item flow.
/// </summary>
public enum BeltPortDirection
{
    Input,
    Output
}

/// <summary>
/// A connection point on a prefab where a belt can attach.
/// Placed as a child GameObject on machine, storage, and belt prefabs.
/// Position and forward direction come from the child transform.
/// </summary>
public class BeltPort : MonoBehaviour
{
    [SerializeField] private BeltPortDirection _direction = BeltPortDirection.Input;
    [SerializeField] private int _slotIndex;
    [SerializeField] private string _slotLabel;

    public BeltPortDirection Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public int SlotIndex
    {
        get => _slotIndex;
        set => _slotIndex = value;
    }

    public string SlotLabel
    {
        get => _slotLabel;
        set => _slotLabel = value;
    }

    /// <summary>
    /// World-space position of this port.
    /// </summary>
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// World-space direction this port faces (outward from the building).
    /// For Output ports, this is the direction items leave.
    /// For Input ports, this is the direction items arrive from.
    /// </summary>
    public Vector3 WorldDirection => transform.forward;
}
```

**Step 4: Run tests to verify they pass**

Expected: all 6 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltPort.cs \
       Assets/_Slopworks/Scripts/Automation/BeltPort.cs.meta \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltPortTests.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltPortTests.cs.meta
git commit -m "Add BeltPort component for belt connection points on prefabs"
```

---

## Task 4: BeltSnapAnchor Component

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltSnapAnchor.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/BeltSnapAnchorTests.cs`

**Step 1: Write the failing test**

Create `BeltSnapAnchorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltSnapAnchorTests
{
    [Test]
    public void SnapAnchor_Position_MatchesTransformPosition()
    {
        var go = new GameObject("Support");
        go.transform.position = new Vector3(5f, 1f, 3f);
        var anchor = go.AddComponent<BeltSnapAnchor>();

        Assert.AreEqual(new Vector3(5f, 1f, 3f), anchor.WorldPosition);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SnapAnchor_Direction_MatchesTransformForward()
    {
        var go = new GameObject("Support");
        go.transform.forward = Vector3.left;
        var anchor = go.AddComponent<BeltSnapAnchor>();

        // Compare with tolerance due to float rotation
        Assert.That(anchor.WorldDirection, Is.EqualTo(Vector3.left).Using(
            new Vector3EqualityComparer(0.001f)));

        Object.DestroyImmediate(go);
    }
}

// Helper for Vector3 comparison with tolerance
public class Vector3EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector3>
{
    private readonly float _tolerance;
    public Vector3EqualityComparer(float tolerance) { _tolerance = tolerance; }

    public bool Equals(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < _tolerance;
    }

    public int GetHashCode(Vector3 obj) => obj.GetHashCode();
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL -- `BeltSnapAnchor` does not exist.

**Step 3: Implement BeltSnapAnchor**

Create `Assets/_Slopworks/Scripts/Automation/BeltSnapAnchor.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Placement guide on a support prefab. Provides a snap position and direction
/// for belt placement. Not a network node -- purely a placement helper.
/// Belt reads position and direction at construction time; no ongoing relationship.
/// </summary>
public class BeltSnapAnchor : MonoBehaviour
{
    /// <summary>
    /// World-space position for belt endpoint placement.
    /// </summary>
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// World-space direction for belt tangent at this anchor.
    /// </summary>
    public Vector3 WorldDirection => transform.forward;
}
```

**Step 4: Run tests to verify they pass**

Expected: all 2 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltSnapAnchor.cs \
       Assets/_Slopworks/Scripts/Automation/BeltSnapAnchor.cs.meta \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltSnapAnchorTests.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltSnapAnchorTests.cs.meta
git commit -m "Add BeltSnapAnchor component for support placement guides"
```

---

## Task 5: BeltSplineBuilder (Hermite Spline Construction)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltSplineBuilder.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/BeltSplineBuilderTests.cs`

**Step 1: Write the failing tests**

Create `BeltSplineBuilderTests.cs`:

```csharp
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
        var startDir = Vector3.right;
        var endDir = Vector3.right;

        var data = BeltSplineBuilder.Build(start, startDir, end, endDir);

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
        var startDir = Vector3.right;
        var endDir = Vector3.forward;

        var data = BeltSplineBuilder.Build(start, startDir, end, endDir);

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
        // Very short belt -- tangent magnitude should be clamped to min 0.5
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.5f, 0, 0);

        var data = BeltSplineBuilder.Build(start, Vector3.right, end, Vector3.right);

        // Should still produce valid spline without NaN
        var mid = data.Evaluate(0.5f);
        Assert.IsFalse(float.IsNaN(mid.x));
    }

    [Test]
    public void BezierControlPoints_MatchHermiteToBezierConversion()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(9, 0, 0);
        var startDir = Vector3.right;
        var endDir = Vector3.right;

        var data = BeltSplineBuilder.Build(start, startDir, end, endDir);
        var bezier = data.GetBezierControlPoints();

        // Hermite-to-Bezier: P1 = P0 + T0/3, P2 = P3 - T1/3
        // With distance 9, tangent magnitude = 9/3 = 3
        // T0 = right * 3 = (3,0,0), T1 = right * 3 = (3,0,0)
        // P1 = (0,0,0) + (3,0,0)/3 = (1,0,0)
        // P2 = (9,0,0) - (3,0,0)/3 = (8,0,0)
        AssertVec3Near(new Vector3(0, 0, 0), bezier.p0);
        AssertVec3Near(new Vector3(1, 0, 0), bezier.p1);
        AssertVec3Near(new Vector3(8, 0, 0), bezier.p2);
        AssertVec3Near(new Vector3(9, 0, 0), bezier.p3);
    }

    private void AssertVec3Near(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, Tolerance, $"X mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.y, actual.y, Tolerance, $"Y mismatch: expected {expected}, got {actual}");
        Assert.AreEqual(expected.z, actual.z, Tolerance, $"Z mismatch: expected {expected}, got {actual}");
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL -- `BeltSplineBuilder` does not exist.

**Step 3: Implement BeltSplineBuilder**

Create `Assets/_Slopworks/Scripts/Automation/BeltSplineBuilder.cs`:

```csharp
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
    private const int ArcLengthSamples = 64;

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

        var data = new BeltSplineData(startPos, t0, endPos, t1);
        return data;
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
```

**Step 4: Run tests to verify they pass**

Expected: all 7 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltSplineBuilder.cs \
       Assets/_Slopworks/Scripts/Automation/BeltSplineBuilder.cs.meta \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltSplineBuilderTests.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltSplineBuilderTests.cs.meta
git commit -m "Add BeltSplineBuilder for Hermite spline construction and arc length"
```

---

## Task 6: BeltSplineMeshBaker (Unity Splines Mesh Generation)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltSplineMeshBaker.cs`

This task cannot be unit tested in EditMode because it depends on Unity Splines runtime components (SplineContainer, SplineExtrude). It will be verified in the playtest scene (Task 12).

**Step 1: Implement the bake-and-destroy mesh generator**

Create `Assets/_Slopworks/Scripts/Automation/BeltSplineMeshBaker.cs`:

```csharp
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Generates a belt mesh from spline data using Unity Splines' SplineExtrude,
/// then bakes the result and destroys the temporary components.
/// Call BakeMesh() once at belt placement time. No runtime overhead after baking.
/// </summary>
public static class BeltSplineMeshBaker
{
    private const float BeltWidth = 0.6f;
    private const float BeltThickness = 0.08f;
    private const int SegmentsPerMeter = 4;

    /// <summary>
    /// Generate a belt mesh from Hermite spline data and apply it to the target GameObject.
    /// Uses bake-and-destroy: creates temporary SplineContainer + SplineExtrude,
    /// generates mesh, copies to MeshFilter, destroys temp components.
    /// </summary>
    /// <param name="target">The belt GameObject to receive the mesh.</param>
    /// <param name="splineData">Hermite spline data from BeltSplineBuilder.</param>
    /// <param name="material">Material to apply to the belt mesh.</param>
    public static void BakeMesh(GameObject target, BeltSplineData splineData, Material material)
    {
        var bezier = splineData.GetBezierControlPoints();

        // Ensure MeshFilter and MeshRenderer exist
        var meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = target.AddComponent<MeshFilter>();

        var meshRenderer = target.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = target.AddComponent<MeshRenderer>();

        if (material != null)
            meshRenderer.sharedMaterial = material;

        // Create temporary spline container
        var splineContainer = target.AddComponent<SplineContainer>();
        var spline = splineContainer.Spline;
        spline.Clear();

        // Convert world-space control points to local space
        var worldToLocal = target.transform.worldToLocalMatrix;

        var localP0 = worldToLocal.MultiplyPoint3x4(bezier.p0);
        var localP3 = worldToLocal.MultiplyPoint3x4(bezier.p3);

        // Tangent in/out are relative directions, not positions
        // For Hermite-to-Bezier: tangentOut at knot0 = (P1_bezier - P0) in local space
        // tangentIn at knot1 = (P2_bezier - P3) in local space
        var localTangentOut0 = worldToLocal.MultiplyPoint3x4(bezier.p1)
            - worldToLocal.MultiplyPoint3x4(bezier.p0);
        var localTangentIn1 = worldToLocal.MultiplyPoint3x4(bezier.p2)
            - worldToLocal.MultiplyPoint3x4(bezier.p3);

        spline.Add(new BezierKnot(
            (float3)localP0,
            float3.zero,
            (float3)localTangentOut0
        ));

        spline.Add(new BezierKnot(
            (float3)localP3,
            (float3)localTangentIn1,
            float3.zero
        ));

        // Create SplineExtrude to generate mesh
        var extrude = target.AddComponent<SplineExtrude>();

        // Configure extrude -- Road profile gives a flat belt shape
        int segments = Mathf.Max(4,
            Mathf.RoundToInt(splineData.ArcLength * SegmentsPerMeter));
        extrude.RebuildOnSplineChange = false;

        // Force a rebuild
        extrude.Rebuild();

        // Copy the generated mesh
        var generatedMesh = meshFilter.sharedMesh;
        if (generatedMesh != null)
        {
            var bakedMesh = Object.Instantiate(generatedMesh);
            bakedMesh.name = "BakedBeltMesh";

            // Clean up temp components
            Object.DestroyImmediate(extrude);
            Object.DestroyImmediate(splineContainer);

            // Apply the baked mesh
            meshFilter.sharedMesh = bakedMesh;
        }
        else
        {
            // Cleanup even on failure
            Object.DestroyImmediate(extrude);
            Object.DestroyImmediate(splineContainer);
            Debug.LogWarning("belt: SplineExtrude failed to generate mesh");
        }

        // Mark as static for batching
        target.isStatic = true;
    }
}
```

Note: The exact SplineExtrude API (profile type, segment count, rebuild method) may need adjustment based on the Unity Splines version. This will be validated in the playtest scene (Task 12). The important contract is: `BakeMesh(target, splineData, material)` produces a mesh on the target.

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltSplineMeshBaker.cs \
       Assets/_Slopworks/Scripts/Automation/BeltSplineMeshBaker.cs.meta
git commit -m "Add BeltSplineMeshBaker for bake-and-destroy belt mesh generation"
```

---

## Task 7: Belt Placement Validation

**Files:**
- Create: `Assets/_Slopworks/Scripts/Automation/BeltPlacementValidator.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/BeltPlacementValidatorTests.cs`

**Step 1: Write the failing tests**

Create `BeltPlacementValidatorTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltPlacementValidatorTests
{
    [Test]
    public void Validate_ValidStraightBelt_ReturnsTrue()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_TooShort_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.3f, 0, 0); // < 0.5m
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooShort, result.Error);
    }

    [Test]
    public void Validate_TooLong_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(60, 0, 0); // > 56m
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooLong, result.Error);
    }

    [Test]
    public void Validate_TooSteep_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(3, 10, 0); // steep upward
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooSteep, result.Error);
    }

    [Test]
    public void Validate_ExactMinLength_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.5f, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_ExactMaxLength_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(56, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_45DegreeSlopeExactly_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 10, 0); // exactly 45 degrees
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL -- `BeltPlacementValidator` does not exist.

**Step 3: Implement BeltPlacementValidator**

Create `Assets/_Slopworks/Scripts/Automation/BeltPlacementValidator.cs`:

```csharp
using UnityEngine;

public enum BeltValidationError
{
    None,
    TooShort,
    TooLong,
    TooSteep
}

public struct BeltValidationResult
{
    public bool IsValid;
    public BeltValidationError Error;

    public static BeltValidationResult Valid()
    {
        return new BeltValidationResult { IsValid = true, Error = BeltValidationError.None };
    }

    public static BeltValidationResult Invalid(BeltValidationError error)
    {
        return new BeltValidationResult { IsValid = false, Error = error };
    }
}

/// <summary>
/// Validates belt placement parameters before sending to server.
/// Pure math -- no MonoBehaviour, no side effects.
/// </summary>
public static class BeltPlacementValidator
{
    public const float MinLength = 0.5f;
    public const float MaxLength = 56f;
    public const float MaxSlopeAngle = 45f;

    public static BeltValidationResult Validate(
        Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir)
    {
        float distance = Vector3.Distance(startPos, endPos);

        if (distance < MinLength)
            return BeltValidationResult.Invalid(BeltValidationError.TooShort);

        if (distance > MaxLength)
            return BeltValidationResult.Invalid(BeltValidationError.TooLong);

        // Check slope angle
        float horizontalDist = new Vector2(endPos.x - startPos.x, endPos.z - startPos.z).magnitude;
        float verticalDist = Mathf.Abs(endPos.y - startPos.y);

        if (horizontalDist > 0.001f)
        {
            float slopeAngle = Mathf.Atan2(verticalDist, horizontalDist) * Mathf.Rad2Deg;
            if (slopeAngle > MaxSlopeAngle)
                return BeltValidationResult.Invalid(BeltValidationError.TooSteep);
        }
        else if (verticalDist > 0.001f)
        {
            // Purely vertical = 90 degrees > max
            return BeltValidationResult.Invalid(BeltValidationError.TooSteep);
        }

        return BeltValidationResult.Valid();
    }
}
```

**Step 4: Run tests to verify they pass**

Expected: all 7 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Automation/BeltPlacementValidator.cs \
       Assets/_Slopworks/Scripts/Automation/BeltPlacementValidator.cs.meta \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltPlacementValidatorTests.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/BeltPlacementValidatorTests.cs.meta
git commit -m "Add BeltPlacementValidator with length and slope constraints"
```

---

## Task 8: Update NetworkBeltSegment for Spline Sync

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/NetworkBeltSegment.cs`

**Step 1: Rewrite NetworkBeltSegment with spline SyncVars**

Replace the contents of `NetworkBeltSegment.cs`:

```csharp
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

/// <summary>
/// FishNet wrapper for a belt segment. Syncs spline geometry via SyncVars
/// so clients can reconstruct the mesh deterministically. Syncs item state
/// via SyncList for visual rendering. Server ticks the simulation.
/// </summary>
public class NetworkBeltSegment : NetworkBehaviour
{
    private BeltSegment _segment;
    private BeltSplineData _splineData;

    // Spline geometry -- synced once at spawn (D-BLT-006)
    private readonly SyncVar<Vector3> _syncStartPos = new();
    private readonly SyncVar<Vector3> _syncStartTangent = new();
    private readonly SyncVar<Vector3> _syncEndPos = new();
    private readonly SyncVar<Vector3> _syncEndTangent = new();
    private readonly SyncVar<byte> _syncTier = new();

    // Item state -- synced every tick
    private readonly SyncList<BeltItem> _syncItems = new();
    private readonly SyncVar<ushort> _syncTerminalGap = new();

    public BeltSegment Segment => _segment;
    public BeltSplineData SplineData => _splineData;
    public int ItemCount => _syncItems.Count;
    public Vector3 StartPos => _syncStartPos.Value;
    public Vector3 EndPos => _syncEndPos.Value;
    public byte Tier => _syncTier.Value;

    /// <summary>
    /// Server-side initialization with spline data.
    /// Called by GridManager after spawning the belt.
    /// </summary>
    public void ServerInit(BeltSegment segment, BeltSplineData splineData, byte tier = 0)
    {
        _segment = segment;
        _splineData = splineData;

        _syncStartPos.Value = splineData.P0;
        _syncStartTangent.Value = splineData.T0;
        _syncEndPos.Value = splineData.P1;
        _syncEndTangent.Value = splineData.T1;
        _syncTier.Value = tier;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Reconstruct spline from synced data
        _splineData = new BeltSplineData(
            _syncStartPos.Value,
            _syncStartTangent.Value,
            _syncEndPos.Value,
            _syncEndTangent.Value);

        // Bake mesh on client
        var material = GetComponent<MeshRenderer>()?.sharedMaterial;
        BeltSplineMeshBaker.BakeMesh(gameObject, _splineData, material);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // If no external init happened (fallback), create a default segment
        if (_segment == null)
        {
            _splineData = new BeltSplineData(
                _syncStartPos.Value,
                _syncStartTangent.Value,
                _syncEndPos.Value,
                _syncEndTangent.Value);
            _segment = BeltSegment.FromArcLength(_splineData.ArcLength);
        }
    }

    /// <summary>
    /// Push simulation state to SyncList for client rendering.
    /// Called by NetworkFactorySimulation after each tick.
    /// </summary>
    public void ServerSyncState()
    {
        if (_segment == null) return;

        var items = _segment.GetItems();

        while (_syncItems.Count > items.Count)
            _syncItems.RemoveAt(_syncItems.Count - 1);

        for (int i = 0; i < items.Count; i++)
        {
            if (i < _syncItems.Count)
            {
                if (_syncItems[i].itemId != items[i].itemId ||
                    _syncItems[i].distanceToNext != items[i].distanceToNext)
                    _syncItems[i] = items[i];
            }
            else
            {
                _syncItems.Add(items[i]);
            }
        }

        _syncTerminalGap.Value = _segment.TerminalGap;
    }

    /// <summary>
    /// Get world-space positions for belt items using the spline.
    /// Clients call this for visual rendering.
    /// </summary>
    public void GetItemWorldPositions(List<Vector3> positions)
    {
        positions.Clear();
        if (_syncItems.Count == 0 || _splineData == null) return;

        int totalLength = _segment?.TotalLength ??
            (int)System.Math.Round(_splineData.ArcLength * BeltItem.SubdivisionsPerTile);
        if (totalLength == 0) return;

        float cumulative = 0f;
        for (int i = 0; i < _syncItems.Count; i++)
        {
            cumulative += _syncItems[i].distanceToNext;
            float t = cumulative / totalLength;
            positions.Add(_splineData.Evaluate(t));
        }
    }
}
```

**Step 2: Verify compilation**

Let the user open Unity and confirm no compilation errors.

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/NetworkBeltSegment.cs
git commit -m "Update NetworkBeltSegment with spline SyncVars and arc-length simulation"
```

---

## Task 9: Update GridManager.CmdPlaceBelt for Spline Belts

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`

**Step 1: Replace CmdPlaceBelt with spline-based placement**

Replace `CmdPlaceBelt` (lines 381-441 of `GridManager.cs`) with:

```csharp
[ServerRpc(RequireOwnership = false)]
public void CmdPlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir,
    byte tier = 0, int variant = 0, NetworkConnection sender = null)
{
    if (!IsServerInitialized) return;

    // Validate placement
    var validation = BeltPlacementValidator.Validate(startPos, startDir, endPos, endDir);
    if (!validation.IsValid)
    {
        Debug.Log($"grid: belt placement rejected: {validation.Error} by {sender?.ClientId}");
        return;
    }

    var prefab = GetPrefab(BuildingCategory.Belt, variant);
    if (prefab == null) return;

    // Build spline
    var splineData = BeltSplineBuilder.Build(startPos, startDir, endPos, endDir);

    // Create simulation segment from arc length
    var segment = BeltSegment.FromArcLength(splineData.ArcLength);

    // Spawn at midpoint
    var midpoint = splineData.Evaluate(0.5f);
    var go = Instantiate(prefab, midpoint, Quaternion.identity);

    // Reset scale -- mesh comes from SplineExtrude, not prefab scale
    go.transform.localScale = Vector3.one;

    var info = go.AddComponent<PlacementInfo>();
    info.Category = BuildingCategory.Belt;
    info.SurfaceY = startPos.y;

    var netBelt = go.GetComponent<NetworkBeltSegment>();
    if (netBelt != null)
        netBelt.ServerInit(segment, splineData, tier);

    ServerManager.Spawn(go);

    // Server also bakes mesh (host needs to see it too)
    var material = go.GetComponent<MeshRenderer>()?.sharedMaterial;
    BeltSplineMeshBaker.BakeMesh(go, splineData, material);

    // Add belt collider for raycast interaction (D-BLT-005)
    var meshCollider = go.AddComponent<MeshCollider>();
    meshCollider.sharedMesh = go.GetComponent<MeshFilter>()?.sharedMesh;

    var visualizer = go.AddComponent<BeltItemVisualizer>();
    visualizer.Init(netBelt);

    if (netBelt != null && _factorySimulation != null)
        _factorySimulation.RegisterBelt(netBelt);

    Debug.Log($"grid: belt placed from {startPos} to {endPos} arc={splineData.ArcLength:F1}m by {sender?.ClientId}");
}
```

Also update `CmdDelete` to handle belt cleanup (disconnect ports from simulation when implemented).

**Step 2: Verify compilation**

Let the user open Unity and confirm no errors.

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs
git commit -m "Update CmdPlaceBelt for spline-based curved belt placement"
```

---

## Task 10: Update NetworkBuildController Belt Input

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs`

**Step 1: Replace HandleBeltInput with two-click spline placement**

Replace the existing `HandleBeltInput` method with:

```csharp
// Belt placement state
private enum BeltPlacementState { Idle, PickingStart, Dragging }
private BeltPlacementState _beltState = BeltPlacementState.Idle;
private Vector3 _beltStartPos;
private Vector3 _beltStartDir;
private GameObject _beltPreviewLine;
private LineRenderer _beltLineRenderer;

private void HandleBeltInput(Mouse mouse)
{
    var cam = Camera.main;
    if (cam == null) return;

    var ray = cam.ScreenPointToRay(mouse.position.ReadValue());

    switch (_beltState)
    {
        case BeltPlacementState.Idle:
        case BeltPlacementState.PickingStart:
            HandleBeltPickStart(mouse, ray);
            break;
        case BeltPlacementState.Dragging:
            HandleBeltDragging(mouse, ray);
            break;
    }
}

private void HandleBeltPickStart(Mouse mouse, Ray ray)
{
    if (!mouse.leftButton.wasPressedThisFrame) return;

    // Try to hit a BeltPort or BeltSnapAnchor first, then fall back to ground
    if (TryResolveBeltEndpoint(ray, out var pos, out var dir))
    {
        _beltStartPos = pos;
        _beltStartDir = dir;
        _beltState = BeltPlacementState.Dragging;

        // Create preview LineRenderer
        if (_beltPreviewLine == null)
        {
            _beltPreviewLine = new GameObject("BeltPreview");
            _beltLineRenderer = _beltPreviewLine.AddComponent<LineRenderer>();
            _beltLineRenderer.startWidth = 0.3f;
            _beltLineRenderer.endWidth = 0.3f;
            _beltLineRenderer.positionCount = 30;
            _beltLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        _beltPreviewLine.SetActive(true);
    }
}

private void HandleBeltDragging(Mouse mouse, Ray ray)
{
    // Update preview line every frame
    if (TryResolveBeltEndpoint(ray, out var endPos, out var endDir))
    {
        var splineData = BeltSplineBuilder.Build(_beltStartPos, _beltStartDir, endPos, endDir);
        var validation = BeltPlacementValidator.Validate(
            _beltStartPos, _beltStartDir, endPos, endDir);

        // Update LineRenderer with spline samples
        var color = validation.IsValid ? Color.green : Color.red;
        _beltLineRenderer.startColor = color;
        _beltLineRenderer.endColor = color;

        for (int i = 0; i < 30; i++)
        {
            float t = (float)i / 29;
            _beltLineRenderer.SetPosition(i, splineData.Evaluate(t));
        }

        // Second click to confirm
        if (mouse.leftButton.wasPressedThisFrame && validation.IsValid)
        {
            GridManager.Instance.CmdPlaceBelt(
                _beltStartPos, _beltStartDir,
                endPos, endDir);

            _beltState = BeltPlacementState.Idle;
            _beltPreviewLine.SetActive(false);
        }
    }

    // Right-click to cancel
    if (mouse.rightButton.wasPressedThisFrame)
    {
        _beltState = BeltPlacementState.Idle;
        if (_beltPreviewLine != null)
            _beltPreviewLine.SetActive(false);
    }
}

/// <summary>
/// Resolve a raycast hit to a belt endpoint position and direction.
/// Priority: BeltPort > BeltSnapAnchor > ground hit.
/// </summary>
private bool TryResolveBeltEndpoint(Ray ray, out Vector3 pos, out Vector3 dir)
{
    pos = Vector3.zero;
    dir = Vector3.forward;

    // First try BeltPort / BeltSnapAnchor layers
    if (Physics.Raycast(ray, out var hit, 200f,
        PhysicsLayers.StructuralPlacementMask | PhysicsLayers.InteractMask |
        (1 << PhysicsLayers.SnapPoints)))
    {
        // Check for BeltPort on hit object or parents
        var beltPort = hit.collider.GetComponentInParent<BeltPort>();
        if (beltPort != null)
        {
            pos = beltPort.WorldPosition;
            dir = beltPort.Direction == BeltPortDirection.Output
                ? beltPort.WorldDirection
                : -beltPort.WorldDirection;
            return true;
        }

        // Check for BeltSnapAnchor
        var snapAnchor = hit.collider.GetComponentInParent<BeltSnapAnchor>();
        if (snapAnchor != null)
        {
            pos = snapAnchor.WorldPosition;
            dir = snapAnchor.WorldDirection;
            return true;
        }

        // Ground/structure hit -- derive direction from camera
        pos = hit.point;
        var camForward = Camera.main.transform.forward;
        camForward.y = 0;
        dir = camForward.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        return true;
    }

    return false;
}
```

Also add cleanup in the tool-switch method to reset belt placement state.

**Step 2: Verify compilation**

Let the user open Unity and confirm no errors.

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Player/NetworkBuildController.cs
git commit -m "Update belt input handler for two-click spline placement with preview"
```

---

## Task 11: Add BeltPort Editor Tooling

**Files:**
- Create: `Assets/_Slopworks/Scripts/Editor/BeltPortEditor.cs`

**Step 1: Create editor menu item for adding BeltPorts to prefabs**

Create `Assets/_Slopworks/Scripts/Editor/BeltPortEditor.cs`:

```csharp
using UnityEditor;
using UnityEngine;

public static class BeltPortEditor
{
    [MenuItem("Slopworks/Add Belt Port/Input")]
    private static void AddInputPort()
    {
        AddBeltPort(BeltPortDirection.Input);
    }

    [MenuItem("Slopworks/Add Belt Port/Output")]
    private static void AddOutputPort()
    {
        AddBeltPort(BeltPortDirection.Output);
    }

    private static void AddBeltPort(BeltPortDirection direction)
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("belt editor: select a GameObject first");
            return;
        }

        var portName = direction == BeltPortDirection.Input ? "BeltPort_Input" : "BeltPort_Output";

        // Count existing ports of this direction to auto-increment slot index
        var existing = selected.GetComponentsInChildren<BeltPort>();
        int slotIndex = 0;
        foreach (var p in existing)
        {
            if (p.Direction == direction)
                slotIndex++;
        }

        var child = new GameObject($"{portName}_{slotIndex}");
        child.transform.SetParent(selected.transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.layer = PhysicsLayers.SnapPoints;

        var port = child.AddComponent<BeltPort>();
        port.Direction = direction;
        port.SlotIndex = slotIndex;

        // Add a small sphere collider for raycast targeting
        var collider = child.AddComponent<SphereCollider>();
        collider.radius = 0.15f;
        collider.isTrigger = true;

        Selection.activeGameObject = child;
        Undo.RegisterCreatedObjectUndo(child, $"Add Belt Port ({direction})");

        Debug.Log($"belt editor: added {direction} port (slot {slotIndex}) to {selected.name}");
    }
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Editor/BeltPortEditor.cs \
       Assets/_Slopworks/Scripts/Editor/BeltPortEditor.cs.meta
git commit -m "Add editor tooling for placing BeltPort components on prefabs"
```

---

## Task 12: Belt Playtest Scene

**Files:**
- Create: `Assets/_Slopworks/Scripts/Debug/BeltPlaytestSetup.cs`

This is the integration verification. A bootstrapper that lets you place curved belts, see them render, and verify items flow along the spline path.

**Step 1: Create the playtest bootstrapper**

Create `Assets/_Slopworks/Scripts/Debug/BeltPlaytestSetup.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Playtest bootstrapper for curved belt system. Drop on empty GameObject, hit Play.
/// Tests: spline construction, mesh baking, item flow, visual positioning.
///
/// Controls:
/// - Left click: set belt start / confirm belt end
/// - Right click: cancel belt placement
/// - P: pre-seed a straight belt with flowing items
/// - I: insert item onto the selected belt
/// - Space: tick simulation manually
/// </summary>
public class BeltPlaytestSetup : MonoBehaviour
{
    [SerializeField] private bool _preSeedBelt = true;
    [SerializeField] private Material _beltMaterial;

    private BeltNetwork _beltNetwork;
    private List<BeltSegment> _segments = new();
    private List<BeltSplineData> _splines = new();
    private List<GameObject> _beltObjects = new();

    // Placement state
    private bool _pickingStart = true;
    private Vector3 _startPos;
    private Vector3 _startDir;
    private LineRenderer _previewLine;

    // Item visuals
    private List<GameObject> _itemVisuals = new();
    private List<float> _positionBuffer = new();

    private void Awake()
    {
        _beltNetwork = new BeltNetwork();

        if (_beltMaterial == null)
        {
            _beltMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _beltMaterial.color = new Color(0.3f, 0.3f, 0.35f);
        }

        // Create ground plane
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f);

        // Preview line
        var lineObj = new GameObject("BeltPreview");
        _previewLine = lineObj.AddComponent<LineRenderer>();
        _previewLine.startWidth = 0.2f;
        _previewLine.endWidth = 0.2f;
        _previewLine.positionCount = 30;
        _previewLine.material = new Material(Shader.Find("Sprites/Default"));
        _previewLine.startColor = Color.green;
        _previewLine.endColor = Color.green;
        lineObj.SetActive(false);

        if (_preSeedBelt)
            PreSeed();

        Debug.Log("belt playtest: ready. left-click to place belts, P to pre-seed, I to insert item, space to tick");
    }

    private void PreSeed()
    {
        // Straight belt from (0,0.5,0) to (10,0.5,0)
        var start = new Vector3(0, 0.5f, 0);
        var end = new Vector3(10, 0.5f, 0);
        PlaceBelt(start, Vector3.right, end, Vector3.right);

        // Insert some items
        if (_segments.Count > 0)
        {
            _segments[0].TryInsertAtStart("iron_ore", 50);
            _segments[0].TryInsertAtStart("copper_ore", 50);
            Debug.Log("belt playtest: pre-seeded straight belt with 2 items");
        }
    }

    private void PlaceBelt(Vector3 startPos, Vector3 startDir, Vector3 endPos, Vector3 endDir)
    {
        var validation = BeltPlacementValidator.Validate(startPos, startDir, endPos, endDir);
        if (!validation.IsValid)
        {
            Debug.Log($"belt playtest: placement rejected: {validation.Error}");
            return;
        }

        var splineData = BeltSplineBuilder.Build(startPos, startDir, endPos, endDir);
        var segment = BeltSegment.FromArcLength(splineData.ArcLength);

        var go = new GameObject($"Belt_{_segments.Count}");
        go.transform.position = splineData.Evaluate(0.5f);

        BeltSplineMeshBaker.BakeMesh(go, splineData, _beltMaterial);

        _segments.Add(segment);
        _splines.Add(splineData);
        _beltObjects.Add(go);

        // Wire to previous belt
        if (_segments.Count > 1)
        {
            _beltNetwork.Connect(_segments[_segments.Count - 2], segment);
            Debug.Log($"belt playtest: wired belt {_segments.Count - 2} -> {_segments.Count - 1}");
        }

        Debug.Log($"belt playtest: placed belt {_segments.Count - 1}, arc={splineData.ArcLength:F1}m, subs={segment.TotalLength}");
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // Space: tick simulation
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            for (int i = 0; i < _segments.Count; i++)
                _segments[i].Tick(10);
            _beltNetwork.Tick();
            Debug.Log("belt playtest: ticked simulation");
        }

        // I: insert item on first belt
        if (keyboard.iKey.wasPressedThisFrame && _segments.Count > 0)
        {
            bool inserted = _segments[0].TryInsertAtStart("iron_ore", 50);
            Debug.Log($"belt playtest: insert item: {(inserted ? "success" : "failed")}");
        }

        // P: pre-seed
        if (keyboard.pKey.wasPressedThisFrame)
            PreSeed();

        // Belt placement with mouse
        HandleMousePlacement(mouse);

        // Update item visuals
        UpdateItemVisuals();
    }

    private void HandleMousePlacement(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit, 200f)) return;

        if (_pickingStart)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _startPos = hit.point;
                var camFwd = cam.transform.forward;
                camFwd.y = 0;
                _startDir = camFwd.normalized;
                _pickingStart = false;
                _previewLine.gameObject.SetActive(true);
                Debug.Log($"belt playtest: start set at {hit.point}");
            }
        }
        else
        {
            // Update preview
            var endDir = (hit.point - _startPos).normalized;
            endDir.y = 0;
            if (endDir.sqrMagnitude < 0.001f) endDir = _startDir;

            var splineData = BeltSplineBuilder.Build(_startPos, _startDir, hit.point, endDir);
            var validation = BeltPlacementValidator.Validate(_startPos, _startDir, hit.point, endDir);

            var color = validation.IsValid ? Color.green : Color.red;
            _previewLine.startColor = color;
            _previewLine.endColor = color;

            for (int i = 0; i < 30; i++)
            {
                float t = (float)i / 29;
                _previewLine.SetPosition(i, splineData.Evaluate(t));
            }

            if (mouse.leftButton.wasPressedThisFrame && validation.IsValid)
            {
                PlaceBelt(_startPos, _startDir, hit.point, endDir);
                _pickingStart = true;
                _previewLine.gameObject.SetActive(false);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _pickingStart = true;
                _previewLine.gameObject.SetActive(false);
                Debug.Log("belt playtest: placement cancelled");
            }
        }
    }

    private void UpdateItemVisuals()
    {
        // Clean up old visuals
        foreach (var v in _itemVisuals)
            if (v != null) Destroy(v);
        _itemVisuals.Clear();

        for (int s = 0; s < _segments.Count; s++)
        {
            _positionBuffer.Clear();
            _segments[s].GetItemPositions(_positionBuffer);

            for (int i = 0; i < _positionBuffer.Count; i++)
            {
                float t = _positionBuffer[i];
                var worldPos = _splines[s].Evaluate(t);

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = worldPos;
                sphere.transform.localScale = Vector3.one * 0.15f;
                DestroyImmediate(sphere.GetComponent<Collider>());

                var items = _segments[s].GetItems();
                var color = items[i].itemId == "iron_ore"
                    ? new Color(0.6f, 0.3f, 0.2f)
                    : new Color(0.8f, 0.5f, 0.2f);
                sphere.GetComponent<Renderer>().material.color = color;

                _itemVisuals.Add(sphere);
            }
        }
    }

    private void OnGUI()
    {
        int y = 10;
        GUI.Label(new Rect(10, y, 400, 25), $"belts: {_segments.Count}");
        y += 20;

        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            GUI.Label(new Rect(10, y, 500, 25),
                $"  belt {i}: items={seg.ItemCount} gap={seg.TerminalGap} len={seg.TotalLength}");
            y += 18;
        }

        y += 10;
        GUI.Label(new Rect(10, y, 400, 25), "controls: click=place, space=tick, I=insert, P=preseed");
    }
}
```

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/BeltPlaytestSetup.cs \
       Assets/_Slopworks/Scripts/Debug/BeltPlaytestSetup.cs.meta
git commit -m "Add BeltPlaytestSetup bootstrapper for curved belt integration testing"
```

---

## Task 13: Support Prefab and CmdPlaceSupport

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Network/GridManager.cs`

**Step 1: Add CmdPlaceSupport ServerRpc**

Add to `GridManager.cs` after `CmdPlaceBelt`:

```csharp
[ServerRpc(RequireOwnership = false)]
public void CmdPlaceSupport(Vector3 position, Quaternion rotation,
    int variant = 0, NetworkConnection sender = null)
{
    if (!IsServerInitialized) return;

    var prefab = GetPrefab(BuildingCategory.Support, variant);
    if (prefab == null)
    {
        // Runtime fallback: create a simple pole
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
        go.GetComponent<Renderer>().material.color = new Color(0.4f, 0.4f, 0.45f);

        // Add snap anchor
        var anchorChild = new GameObject("SnapAnchor");
        anchorChild.transform.SetParent(go.transform);
        anchorChild.transform.localPosition = new Vector3(0, 1f, 0); // top of pole
        anchorChild.transform.localRotation = Quaternion.identity;
        anchorChild.AddComponent<BeltSnapAnchor>();
        anchorChild.layer = PhysicsLayers.SnapPoints;
        var col = anchorChild.AddComponent<SphereCollider>();
        col.radius = 0.2f;
        col.isTrigger = true;

        var nob = go.AddComponent<FishNet.Object.NetworkObject>();
        ServerManager.Spawn(go);

        Debug.Log($"grid: support placed at {position} by {sender?.ClientId}");
        return;
    }

    var instance = Instantiate(prefab, position, rotation);
    ServerManager.Spawn(instance);
    Debug.Log($"grid: support placed at {position} by {sender?.ClientId}");
}
```

Also add `Support` to `BuildingCategory.cs`:

```csharp
public enum BuildingCategory
{
    Foundation,
    Wall,
    Ramp,
    Machine,
    Storage,
    Belt,
    Support
}
```

And add the Support folder to `LoadPrefabVariants()`:

```csharp
{ BuildingCategory.Support, "Prefabs/Buildings/Supports" }
```

**Step 2: Create the Resources folder for support prefabs**

```bash
mkdir -p "Assets/_Slopworks/Resources/Prefabs/Buildings/Supports"
```

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Network/GridManager.cs \
       Assets/_Slopworks/Scripts/Network/BuildingCategory.cs \
       "Assets/_Slopworks/Resources/Prefabs/Buildings/Supports"
git commit -m "Add CmdPlaceSupport, Support building category, and support folder"
```

---

## Task 14: Run All Tests and Manual Playtest

**Step 1: Run all EditMode tests**

Open Unity Test Runner, run all EditMode tests. Every existing test must still pass. New tests from Tasks 2-7 must pass. Report exact counts.

**Step 2: Create a test scene**

1. Create new scene `Assets/_Slopworks/Scenes/BeltPlaytest.unity`
2. Add empty GameObject, attach `BeltPlaytestSetup`
3. Add a Camera + basic lighting
4. Enter Play mode

**Step 3: Manual verification checklist**

- [ ] Pre-seeded straight belt renders with correct mesh shape
- [ ] Items appear as spheres on the belt and move when Space is pressed
- [ ] Items flow from belt input to output along the spline curve
- [ ] Two-click placement works: first click sets start, second click places belt
- [ ] LineRenderer preview updates while dragging and shows green/red validation
- [ ] Curved belt (click start, move mouse to side, click end) renders a smooth curve
- [ ] Items on curved belt follow the spline path, not a straight line
- [ ] Cancel with right-click resets placement state
- [ ] Too-short belts show red preview and reject placement
- [ ] Belt-to-belt item transfer works (place two belts end-to-end, insert items)

**Step 4: Fix any issues found during playtest**

If SplineExtrude API doesn't match the code in Task 6, adjust `BeltSplineMeshBaker.cs` based on the actual Unity Splines 2.7 API.

**Step 5: Commit fixes**

```bash
git add -A
git commit -m "Fix integration issues found during belt playtest"
```

---

## Execution Summary

| Task | Component | Test Type | Files |
|------|-----------|-----------|-------|
| 1 | Unity Splines package | Package import | manifest.json |
| 2 | BeltSegment.FromArcLength | EditMode unit | BeltSegment.cs |
| 3 | BeltPort component | EditMode unit | BeltPort.cs |
| 4 | BeltSnapAnchor component | EditMode unit | BeltSnapAnchor.cs |
| 5 | BeltSplineBuilder | EditMode unit | BeltSplineBuilder.cs |
| 6 | BeltSplineMeshBaker | Playtest scene | BeltSplineMeshBaker.cs |
| 7 | BeltPlacementValidator | EditMode unit | BeltPlacementValidator.cs |
| 8 | NetworkBeltSegment update | Compilation check | NetworkBeltSegment.cs |
| 9 | GridManager.CmdPlaceBelt | Compilation check | GridManager.cs |
| 10 | NetworkBuildController input | Compilation check | NetworkBuildController.cs |
| 11 | BeltPort editor tooling | Manual verify | BeltPortEditor.cs |
| 12 | Belt playtest bootstrapper | Integration playtest | BeltPlaytestSetup.cs |
| 13 | Support prefab + CmdPlaceSupport | Compilation check | GridManager.cs |
| 14 | Full test run + manual playtest | All | All |

Tasks 1-7 are independent and can be parallelized. Tasks 8-10 depend on earlier tasks. Tasks 12-14 are integration verification.
