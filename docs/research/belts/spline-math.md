# Spline Math for Belts

Research compiled 2026-03-09. Implementation-ready formulas and algorithms.

## 1. Cubic Hermite Spline -- Core Evaluation

Given endpoints P0, P1 and tangent vectors T0, T1, for t in [0, 1]:

### Position

```
position(t) = (2t^3 - 3t^2 + 1)*P0
            + (t^3 - 2t^2 + t)*T0
            + (-2t^3 + 3t^2)*P1
            + (t^3 - t^2)*T1
```

Hermite basis functions:
- h00(t) = 2t^3 - 3t^2 + 1  (weights P0)
- h10(t) = t^3 - 2t^2 + t    (weights T0)
- h01(t) = -2t^3 + 3t^2      (weights P1)
- h11(t) = t^3 - t^2          (weights T1)

### First Derivative (Tangent)

```
tangent(t) = (6t^2 - 6t)*P0
           + (3t^2 - 4t + 1)*T0
           + (-6t^2 + 6t)*P1
           + (3t^2 - 2t)*T1
```

Verify: tangent(0) = T0, tangent(1) = T1

### Second Derivative (Acceleration)

```
accel(t) = (12t - 6)*P0
         + (6t - 4)*T0
         + (-12t + 6)*P1
         + (6t - 2)*T1
```

### Normal and Binormal (Frenet Frame)

```
T_hat = normalize(tangent(t))
B_hat = normalize(tangent(t) x accel(t))
N_hat = B_hat x T_hat
```

WARNING: Frenet frame is undefined at zero curvature and flips at inflection points. Use rotation-minimizing frames (section 4) for mesh extrusion instead.

### Hermite-to-Bezier Conversion

Hermite {P0, T0, P1, T1} to Bezier {B0, B1, B2, B3}:
```
B0 = P0
B1 = P0 + T0/3
B2 = P1 - T1/3
B3 = P1
```

Reverse:
```
P0 = B0,  P1 = B3
T0 = 3*(B1 - B0)
T1 = 3*(B3 - B2)
```

Hermite is natural for belt placement (position + direction at each end). Convert to Bezier for evaluation/rendering if needed.

## 2. Tangent Calculation from Port Geometry

### Direction

- Start tangent direction = output port's forward vector (direction items leave)
- End tangent direction = input port's forward vector (direction items arrive)
- T1 = destinationPort.forward (not negated -- Hermite handles arrival naturally)

### Tangent Magnitude (Critical Design Decision)

Controls how far the curve "pulls" in the tangent direction before bending.

**Approach 1: Fixed fraction of chord distance** (simplest)
```csharp
float chord = Vector3.Distance(P0, P1);
float mag = chord * 0.5f;  // 0.33 to 0.5 is the sweet spot
T0 = portOut.forward * mag;
T1 = portIn.forward * mag;
```
Factor of 1/3 matches circular arc closely for 90-degree turns. Factor of 0.5 gives rounder curves.

**Approach 2: Clamped proportional** (handles edge cases)
```csharp
float chord = Vector3.Distance(P0, P1);
float mag = Mathf.Clamp(chord * 0.5f, minTangent, maxTangent);
T0 = portOut.forward * mag;
T1 = portIn.forward * mag;
```
minTangent (~0.5) prevents degenerate curves when ports very close.
maxTangent (~10) prevents extreme overshoot when ports far apart.

**Approach 3: Angle-aware** (best quality)
```csharp
float chord = Vector3.Distance(P0, P1);
float dot = Vector3.Dot(portOut.forward, -portIn.forward); // 1 = same dir, -1 = U-turn
float angleFactor = Mathf.Lerp(0.33f, 0.75f, (1f - dot) * 0.5f);
float mag = chord * angleFactor;
```
Scales from 0.33*chord for straight paths to 0.75*chord for U-turns.

## 3. Arc-Length Parameterization

Splines are parameterized by t (0 to 1), but items move by distance. Need to convert between distance and t.

### Step 1: Compute arc length using Gauss-Legendre quadrature

Arc length = integral from 0 to 1 of ||tangent(t)|| dt

5-point Gauss-Legendre (exact for polynomials up to degree 9):

| Node (xi) | Weight (wi) |
|---|---|
| 0.0 | 0.5688888889 |
| +/- 0.5384693101 | 0.4786286705 |
| +/- 0.9061798459 | 0.2369268851 |

```csharp
static readonly float[] GaussNodes = { 0f, -0.5384693101f, 0.5384693101f, -0.9061798459f, 0.9061798459f };
static readonly float[] GaussWeights = { 0.5688888889f, 0.4786286705f, 0.4786286705f, 0.2369268851f, 0.2369268851f };

float ComputeArcLength(float tStart, float tEnd)
{
    float halfRange = (tEnd - tStart) * 0.5f;
    float midpoint = (tEnd + tStart) * 0.5f;
    float sum = 0f;
    for (int i = 0; i < 5; i++)
    {
        float t = halfRange * GaussNodes[i] + midpoint;
        sum += GaussWeights[i] * EvalTangent(t).magnitude;
    }
    return halfRange * sum;
}
```

### Step 2: Build lookup table

```csharp
int N = 64; // 32-64 typical; 64 overkill for belts under 20m
float[] tValues = new float[N + 1];
float[] arcLengths = new float[N + 1];

tValues[0] = 0f;
arcLengths[0] = 0f;

for (int i = 1; i <= N; i++)
{
    tValues[i] = (float)i / N;
    arcLengths[i] = arcLengths[i - 1] + ComputeArcLength(tValues[i - 1], tValues[i]);
}

float totalLength = arcLengths[N];
```

32 samples = sub-millimeter error for belts under 10m.

### Step 3: Map distance to parameter (binary search + lerp)

```csharp
float DistanceToParam(float distance)
{
    distance = Mathf.Clamp(distance, 0f, totalLength);
    int lo = 0, hi = N;
    while (hi - lo > 1)
    {
        int mid = (lo + hi) / 2;
        if (arcLengths[mid] < distance) lo = mid;
        else hi = mid;
    }
    float segLen = arcLengths[hi] - arcLengths[lo];
    float frac = (segLen < 1e-6f) ? 0f : (distance - arcLengths[lo]) / segLen;
    return Mathf.Lerp(tValues[lo], tValues[hi], frac);
}
```

### Incremental advancement (per-tick item movement)

For items moving each tick, advance incrementally instead of full binary search:

```csharp
float speed = EvalTangent(item.t).magnitude;
if (speed > 1e-6f)
    item.t += deltaDistance / speed;
```

Euler step, accurate for small deltaDistance. Only use full lookup for initial placement or arbitrary jumps.

## 4. Rotation-Minimizing Frames (RMF)

### Why Not Frenet

Frenet frame {T, N, B} from derivatives has two fatal problems for mesh extrusion:
1. Undefined on straight segments (curvature = 0, normal undefined)
2. Flips 180 degrees at inflection points (curvature changes sign)

Both common on conveyor belts.

### Double Reflection Method (Wang et al. 2008)

Standard algorithm. Propagates initial frame along curve via two reflections per step. 4th-order accuracy (error ~ h^4).

```csharp
// Input: positions x[0..N], tangents t[0..N] (unit vectors)
// Input: initial frame vectors r[0], s[0] (perpendicular to t[0])
// Output: r[0..N], s[0..N]

for (int i = 0; i < N; i++)
{
    Vector3 v1 = x[i + 1] - x[i];
    float c1 = Vector3.Dot(v1, v1);

    // First reflection
    Vector3 rL = r[i] - (2f / c1) * Vector3.Dot(v1, r[i]) * v1;
    Vector3 tL = t[i] - (2f / c1) * Vector3.Dot(v1, t[i]) * v1;

    // Second reflection
    Vector3 v2 = t[i + 1] - tL;
    float c2 = Vector3.Dot(v2, v2);

    r[i + 1] = rL - (2f / c2) * Vector3.Dot(v2, rL) * v2;
    s[i + 1] = Vector3.Cross(t[i + 1], r[i + 1]);
}
```

### Initial frame

For belts, align "up" with world-up when horizontal:

```csharp
Vector3 r0 = Vector3.Cross(t[0], Vector3.up).normalized;
if (r0.sqrMagnitude < 0.001f) // tangent nearly vertical
    r0 = Vector3.Cross(t[0], Vector3.forward).normalized;
Vector3 s0 = Vector3.Cross(t[0], r0); // approximately world-up on horizontal belts
```

Sample spacing: 0.5m arc length. 10m belt = 20 frames. Runs in microseconds.

## 5. 3D Curve Constraints

### Maximum Curvature (Minimum Turn Radius)

```csharp
float Curvature(float t)
{
    Vector3 d1 = EvalTangent(t);
    Vector3 d2 = EvalAcceleration(t);
    float crossMag = Vector3.Cross(d1, d2).magnitude;
    float speedCubed = Mathf.Pow(d1.magnitude, 3f);
    return (speedCubed < 1e-10f) ? 0f : crossMag / speedCubed;
}
```

Minimum turn radius R_min = 1 / kappa_max. For 2m min radius: maxKappa = 0.5. Sample at 20+ points.

### Maximum Slope

```csharp
float SlopeAngle(float t)
{
    Vector3 tan = EvalTangent(t).normalized;
    return Mathf.Asin(Mathf.Abs(tan.y)) * Mathf.Rad2Deg;
}
```

### Self-Intersection Detection

Sample N points, check if any non-adjacent pair is closer than belt width:

```csharp
bool SelfIntersects(float beltWidth, int samples = 32)
{
    Vector3[] pts = new Vector3[samples];
    for (int i = 0; i < samples; i++)
        pts[i] = EvalPosition((float)i / (samples - 1));

    for (int i = 0; i < samples; i++)
        for (int j = i + 3; j < samples; j++)
            if (Vector3.Distance(pts[i], pts[j]) < beltWidth)
                return true;
    return false;
}
```

### Collision with Existing Geometry

Sphere sweep along curve:

```csharp
bool CollidesWithWorld(float radius, int samples, LayerMask mask)
{
    for (int i = 0; i < samples - 1; i++)
    {
        float t0 = (float)i / (samples - 1);
        float t1 = (float)(i + 1) / (samples - 1);
        Vector3 a = EvalPosition(t0);
        Vector3 b = EvalPosition(t1);
        Vector3 dir = b - a;
        if (Physics.SphereCast(a, radius, dir.normalized, out _, dir.magnitude, mask))
            return true;
    }
    return false;
}
```

## 6. Curve Fitting for Belt Placement

### Single Cubic (Common Case)

Hermite form gives the curve directly from start pos+dir and end pos+dir. Only free parameter is tangent magnitude (section 2).

### Detecting Degenerate Cases

U-turn with short chord:
```csharp
float dot = Vector3.Dot(startDir, -endDir);
float chord = Vector3.Distance(P0, P1);
bool isProblematic = dot < -0.5f && chord < minTurnRadius * 2f;
```

### Two-Segment Solution

When single cubic fails validation, split at midpoint:
```csharp
Vector3 mid = (P0 + P1) * 0.5f + Vector3.up * heightBias;
Vector3 midTangent = (P1 - P0).normalized;
// Segment 1: P0 -> mid, Segment 2: mid -> P1
// Share position and tangent at mid (C1 continuity)
```

For midpoint tangent direction: chord direction (P1 - P0).normalized.
For magnitude: half the chord of each sub-segment.

## 7. Mesh Generation Along a Curve

### Cross-Section Profile

```csharp
// Belt: flat top with sides. Width = 1m, height = 0.1m
// Profile in local XY, centered at origin
Vector2[] profile = new Vector2[]
{
    new(-0.5f, 0f),      // bottom-left
    new(-0.5f, 0.1f),    // top-left
    new(0.5f, 0.1f),     // top-right
    new(0.5f, 0f),       // bottom-right
};
float[] profileU = new float[] { 0f, 0.25f, 0.75f, 1f };
```

### Vertex Generation

For each sample point, transform cross-section by RMF frame:

```csharp
for (int i = 0; i < rings; i++)
{
    Frame f = frames[i];
    float v = arcLengthAtFrame[i] / totalArcLength;

    for (int j = 0; j < vertsPerRing; j++)
    {
        Vector3 localPos = f.right * profile[j].x + f.up * profile[j].y;
        vertices[i * vertsPerRing + j] = f.position + localPos;
        normals[i * vertsPerRing + j] = localPos.normalized;
        uvs[i * vertsPerRing + j] = new Vector2(profileU[j], v);
    }
}
```

### Triangle Generation

Connect adjacent rings with quads (2 triangles each):

```csharp
int idx = 0;
for (int i = 0; i < rings - 1; i++)
{
    for (int j = 0; j < vertsPerRing - 1; j++)
    {
        int curr = i * vertsPerRing + j;
        int next = curr + vertsPerRing;

        triangles[idx++] = curr;
        triangles[idx++] = next;
        triangles[idx++] = curr + 1;

        triangles[idx++] = curr + 1;
        triangles[idx++] = next;
        triangles[idx++] = next + 1;
    }
}
```

Unity uses clockwise winding for front-faces.

### UV Generation

- U = position around cross-section profile (0 at one edge, 1 at other)
- V = arcLengthAtSample / textureRepeatLength for tiling
- Scrolling belt texture: offset V each frame by `Time.time * beltSpeed / textureRepeatLength`

### Cost

10m belt at 0.5m ring spacing with 8-vertex cross-section:
- 20 rings x 8 verts = 160 vertices
- 19 gaps x 7 quads x 2 tris = 266 triangles
- 100 belts = 16,000 vertices (negligible for modern GPU)

## Recommended Parameters

| Parameter | Value | Notes |
|---|---|---|
| Tangent magnitude | chord * 0.5, clamped [0.5, 10] | Angle-aware for polish |
| Arc-length table samples | 32-64 per segment | 32 fine for belts under 10m |
| Gauss-Legendre order | 5-point | Exact for degree-9 integrands |
| RMF sample spacing | 0.5m arc length | ~20 frames for 10m belt |
| Mesh rings | Same as RMF samples | One ring per frame |
| Cross-section vertices | 4-8 | 4 for flat belt, 8 for rounded |
| Curvature check samples | 20 | Catches worst-case kappa |
| Min turn radius | 2m (design choice) | kappa_max = 0.5 |
| Max slope | 45 degrees (design choice) | Check at 20 samples |

## Sources

- [Cubic Hermite spline - Wikipedia](https://en.wikipedia.org/wiki/Cubic_Hermite_spline)
- [Hermite Curve Interpolation](https://www.cubic.org/docs/hermite.htm)
- [Arc-Length Parameterized Spline Curves (PDF)](https://homepage.divms.uiowa.edu/~kearney/pubs/CurvesAndSurfacesArcLength.pdf)
- [Making Splines Useful: Arc Length - Handmade Network](https://seabird.handmade.network/blogs/p/2874-making_splines_useful__arc_length)
- [Computation of Rotation Minimizing Frames (Wang et al. 2008)](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/Computation-of-rotation-minimizing-frames.pdf)
- [A Primer on Bezier Curves](https://pomax.github.io/bezierinfo/)
- [Converting Between Bezier and Hermite - CMU 15-462](http://15462.courses.cs.cmu.edu/fall2020/article/10)
- [Curvature formula - Calculus III](https://tutorial.math.lamar.edu/classes/calciii/curvature.aspx)
