using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Computes waypoints for orthogonal (straight) belt routing.
/// Routes from A to B using axis-aligned segments with 90-degree turns.
/// Each 90-degree turn is expanded into an arc-start + arc-end pair that
/// produces a smooth quarter-circle Bezier arc with a consistent radius.
/// Elevation changes use a straight exit from the port, then a ramp, keeping
/// all turns flat and horizontal.
/// Pure math -- no MonoBehaviour, no side effects.
/// </summary>
public static class BeltRouteBuilder
{
    public const float TurnRadius = 1.0f;
    public const float MaxRampAngle = 30f;
    private const float BezierK = 0.5523f; // 4/3 * tan(pi/8), quarter-circle approximation
    public const float MinSegLength = 0.2f;
    public const float MinExitLength = 0.5f; // minimum straight out of port before ramp
    public const float MinPostRampLength = 0.5f; // minimum flat after ramp before first turn

    public struct Waypoint
    {
        public Vector3 Position;
        /// <summary>Bezier tangent arriving at this point (world space, points backward toward previous).</summary>
        public Vector3 TangentIn;
        /// <summary>Bezier tangent leaving this point (world space, points forward toward next).</summary>
        public Vector3 TangentOut;
    }

    // Internal corner representation before arc expansion
    private struct Corner
    {
        public Vector3 Position; // geometric corner (flat, y=0)
        public Vector3 InDir;    // normalized horizontal direction arriving
        public Vector3 OutDir;   // normalized horizontal direction leaving
    }

    /// <summary>
    /// Build an orthogonal route from start to end using 90-degree arc turns.
    /// Elevation changes are ramped on the first straight segment.
    /// Returns waypoints with proper Bezier tangents for mesh baking.
    /// </summary>
    public static List<Waypoint> Build(Vector3 startPos, Vector3 startDir,
        Vector3 endPos, Vector3 endDir)
    {
        var startAxis = SnapToCardinal(startDir);
        var endAxis = SnapToCardinal(endDir);

        // If endpoints are aligned along the start axis, skip corners
        var flatDelta = Flat(endPos) - Flat(startPos);
        float crossDist = (flatDelta - Vector3.Dot(flatDelta, startAxis) * startAxis).magnitude;
        if (crossDist < 0.1f)
        {
            // Flat aligned: simple straight line. Elevation change: S-curve via AssembleRoute.
            if (Mathf.Abs(endPos.y - startPos.y) > 0.01f)
                return AssembleRoute(startPos, startAxis, endPos, new List<Corner>());
            return BuildStraightLine(startPos, endPos);
        }

        float dot = Vector3.Dot(startAxis, endAxis);

        List<Corner> corners;
        if (Mathf.Abs(dot) < 0.01f)
            corners = ComputeLCorners(startPos, startAxis, endPos);
        else if (dot > 0.5f)
            corners = ComputeZCorners(startPos, startAxis, endPos, endAxis);
        else
            corners = ComputeUCorners(startPos, startAxis, endPos, endAxis);

        return AssembleRoute(startPos, startAxis, endPos, corners);
    }

    /// <summary>
    /// Compute total path length from waypoints (piecewise straight segments).
    /// </summary>
    public static float ComputeRouteLength(List<Waypoint> waypoints)
    {
        float length = 0f;
        for (int i = 1; i < waypoints.Count; i++)
            length += Vector3.Distance(waypoints[i - 1].Position, waypoints[i].Position);
        return length;
    }

    /// <summary>
    /// Evaluate position along route at parameter t in [0, 1].
    /// Piecewise linear interpolation between waypoints.
    /// </summary>
    public static Vector3 EvaluateRoute(List<Waypoint> waypoints, float totalLength, float t)
    {
        t = Mathf.Clamp01(t);
        float targetDist = t * totalLength;

        float cumulative = 0f;
        for (int i = 1; i < waypoints.Count; i++)
        {
            float segLen = Vector3.Distance(waypoints[i - 1].Position, waypoints[i].Position);
            if (cumulative + segLen >= targetDist || i == waypoints.Count - 1)
            {
                float localT = segLen > 0.001f ? (targetDist - cumulative) / segLen : 0f;
                return Vector3.Lerp(waypoints[i - 1].Position, waypoints[i].Position, localT);
            }
            cumulative += segLen;
        }
        return waypoints[waypoints.Count - 1].Position;
    }

    // -- Corner computation (geometric corners, flat XZ positions) --

    private static List<Corner> ComputeLCorners(Vector3 startPos, Vector3 startAxis, Vector3 endPos)
    {
        bool startAlongZ = Mathf.Abs(startAxis.z) > 0.5f;
        var cPos = startAlongZ
            ? new Vector3(startPos.x, 0, endPos.z)
            : new Vector3(endPos.x, 0, startPos.z);

        var inDir = DirectionBetween(Flat(startPos), cPos, startAxis);
        var outDir = DirectionBetween(cPos, Flat(endPos), Vector3.forward);

        return new List<Corner>
        {
            new Corner { Position = cPos, InDir = inDir, OutDir = outDir }
        };
    }

    private static List<Corner> ComputeZCorners(Vector3 startPos, Vector3 startAxis,
        Vector3 endPos, Vector3 endAxis)
    {
        bool alongZ = Mathf.Abs(startAxis.z) > 0.5f;
        float mid = alongZ
            ? (startPos.z + endPos.z) * 0.5f
            : (startPos.x + endPos.x) * 0.5f;

        Vector3 c1, c2, crossAxis;
        if (alongZ)
        {
            c1 = new Vector3(startPos.x, 0, mid);
            c2 = new Vector3(endPos.x, 0, mid);
            crossAxis = (endPos.x > startPos.x) ? Vector3.right : Vector3.left;
            // Handle zero cross distance
            if (Mathf.Abs(endPos.x - startPos.x) < 0.01f)
                crossAxis = Vector3.right;
        }
        else
        {
            c1 = new Vector3(mid, 0, startPos.z);
            c2 = new Vector3(mid, 0, endPos.z);
            crossAxis = (endPos.z > startPos.z) ? Vector3.forward : Vector3.back;
            if (Mathf.Abs(endPos.z - startPos.z) < 0.01f)
                crossAxis = Vector3.forward;
        }

        // Use actual leg directions -- startAxis/endAxis may point wrong way
        // when the midpoint is behind the start or end position
        var inDir = DirectionBetween(Flat(startPos), c1, startAxis);
        var outDir = DirectionBetween(c2, Flat(endPos), endAxis);

        return new List<Corner>
        {
            new Corner { Position = c1, InDir = inDir, OutDir = crossAxis },
            new Corner { Position = c2, InDir = crossAxis, OutDir = outDir }
        };
    }

    private static List<Corner> ComputeUCorners(Vector3 startPos, Vector3 startAxis,
        Vector3 endPos, Vector3 endAxis)
    {
        bool alongZ = Mathf.Abs(startAxis.z) > 0.5f;
        float overshoot = TurnRadius * 2f + 1f;

        Vector3 c1, c2, crossAxis;
        if (alongZ)
        {
            float targetZ = startAxis.z > 0
                ? Mathf.Max(startPos.z, endPos.z) + overshoot
                : Mathf.Min(startPos.z, endPos.z) - overshoot;
            c1 = new Vector3(startPos.x, 0, targetZ);
            c2 = new Vector3(endPos.x, 0, targetZ);
            crossAxis = (endPos.x > startPos.x) ? Vector3.right : Vector3.left;
            if (Mathf.Abs(endPos.x - startPos.x) < 0.01f)
                crossAxis = Vector3.right;
        }
        else
        {
            float targetX = startAxis.x > 0
                ? Mathf.Max(startPos.x, endPos.x) + overshoot
                : Mathf.Min(startPos.x, endPos.x) - overshoot;
            c1 = new Vector3(targetX, 0, startPos.z);
            c2 = new Vector3(targetX, 0, endPos.z);
            crossAxis = (endPos.z > startPos.z) ? Vector3.forward : Vector3.back;
            if (Mathf.Abs(endPos.z - startPos.z) < 0.01f)
                crossAxis = Vector3.forward;
        }

        var inDir = DirectionBetween(Flat(startPos), c1, startAxis);
        var outDir = DirectionBetween(c2, Flat(endPos), -endAxis);

        return new List<Corner>
        {
            new Corner { Position = c1, InDir = inDir, OutDir = crossAxis },
            new Corner { Position = c2, InDir = crossAxis, OutDir = outDir }
        };
    }

    // -- Route assembly: expand corners into arc pairs, apply elevation, compute tangents --

    private static List<Waypoint> AssembleRoute(Vector3 startPos, Vector3 startAxis,
        Vector3 endPos, List<Corner> corners)
    {
        float deltaY = endPos.y - startPos.y;
        bool hasElevation = Mathf.Abs(deltaY) > 0.01f;

        // No corners and no elevation: simple straight line
        if (corners.Count == 0 && !hasElevation)
            return BuildStraightLine(startPos, endPos);

        // Compute leg distances (between consecutive geometric points: start, corners..., end)
        var legPoints = new List<Vector3>();
        legPoints.Add(Flat(startPos));
        for (int i = 0; i < corners.Count; i++)
            legPoints.Add(corners[i].Position);
        legPoints.Add(Flat(endPos));

        var legDists = new List<float>();
        for (int i = 0; i < legPoints.Count - 1; i++)
            legDists.Add(HorizontalDist(legPoints[i], legPoints[i + 1]));

        // Clamp turn radius per corner based on adjacent leg lengths.
        // Two-pass: first clamp each corner to its own legs, then resolve
        // shared legs fairly so adjacent corners get equal treatment.
        var radii = new float[corners.Count];
        for (int i = 0; i < corners.Count; i++)
        {
            float before = legDists[i];
            float after = legDists[i + 1];
            radii[i] = Mathf.Min(TurnRadius, before - MinSegLength, after - MinSegLength);
            radii[i] = Mathf.Max(radii[i], 0.05f);
        }

        // Second pass: adjacent corners share a leg, split fairly
        for (int i = 0; i < corners.Count - 1; i++)
        {
            float shared = legDists[i + 1];
            float combined = radii[i] + radii[i + 1];
            if (combined > shared - MinSegLength)
            {
                float half = Mathf.Max((shared - MinSegLength) * 0.5f, 0.05f);
                radii[i] = Mathf.Min(radii[i], half);
                radii[i + 1] = Mathf.Min(radii[i + 1], half);
            }
        }

        // Build the route point sequence
        // Types: 0=start, 1=end, 2=straight interior, 3=arcStart, 4=arcEnd, 5=rampEnd
        var points = new List<(Vector3 pos, int type, int cornerIdx)>();
        points.Add((startPos, 0, -1));

        // Elevation change: smooth S-curve ramp using horizontal tangents.
        // Single rampEnd waypoint -- Bezier naturally curves from startY to endY
        // without any abrupt direction changes that cause mesh twist.
        if (hasElevation)
        {
            // Available horizontal distance: total first leg minus turn radius (if corners exist)
            float firstLegAvail = corners.Count > 0
                ? legDists[0] - radii[0]
                : legDists[0];

            // Ramp distance for MaxRampAngle, scaled by 1.5 to compensate for S-curve:
            // cubic Bezier with horizontal tangents concentrates height change in the
            // middle, making peak angle ~1.5x steeper than the average.
            float rampDist = 1.5f * Mathf.Abs(deltaY) / Mathf.Tan(MaxRampAngle * Mathf.Deg2Rad);
            rampDist = Mathf.Max(rampDist, MinSegLength);

            // If not enough room, clamp ramp to available space
            // No corners: ramp can use full distance. With corners: reserve post-ramp flat.
            float reserve = corners.Count > 0 ? MinPostRampLength : 0f;
            float maxRampDist = Mathf.Max(firstLegAvail - reserve, MinSegLength);
            rampDist = Mathf.Min(rampDist, maxRampDist);

            var rampEndPos = startPos + startAxis * rampDist;
            rampEndPos.y = endPos.y;
            points.Add((rampEndPos, 5, -1));
        }

        // Expand each corner into arc-start + arc-end
        for (int i = 0; i < corners.Count; i++)
        {
            float r = radii[i];
            var c = corners[i];

            var arcStart = c.Position - c.InDir * r;
            arcStart.y = endPos.y;
            var arcEnd = c.Position + c.OutDir * r;
            arcEnd.y = endPos.y;

            points.Add((arcStart, 3, i));
            points.Add((arcEnd, 4, i));
        }

        points.Add((endPos, 1, -1));

        // Compute tangents for each waypoint
        var waypoints = new List<Waypoint>();
        for (int p = 0; p < points.Count; p++)
        {
            var (pos, type, cIdx) = points[p];

            // Vectors to neighbors
            Vector3 toNext = p < points.Count - 1 ? points[p + 1].pos - pos : Vector3.zero;
            Vector3 toPrev = p > 0 ? points[p - 1].pos - pos : Vector3.zero;
            float distNext = toNext.magnitude;
            float distPrev = toPrev.magnitude;
            Vector3 dirNext = distNext > 0.001f ? toNext / distNext : Vector3.zero;
            Vector3 dirPrev = distPrev > 0.001f ? toPrev / distPrev : Vector3.zero;

            Vector3 tanIn = Vector3.zero;
            Vector3 tanOut = Vector3.zero;

            switch (type)
            {
                case 0: // Start -- use horizontal tangent if next point is rampEnd
                {
                    Vector3 flatNext = new Vector3(toNext.x, 0, toNext.z);
                    float flatDistNext = flatNext.magnitude;
                    if (flatDistNext > 0.001f)
                        tanOut = (flatNext / flatDistNext) * (distNext / 3f);
                    else
                        tanOut = dirNext * (distNext / 3f);
                    break;
                }

                case 1: // End
                    tanIn = dirPrev * (distPrev / 3f);
                    break;

                case 2: // Ramp transition -- dist/3 for perfect straight Bezier lines
                    tanIn = dirPrev * (distPrev / 3f);
                    tanOut = dirNext * (distNext / 3f);
                    break;

                case 3: // ArcStart -- straight segment ends, arc begins
                {
                    float r = radii[cIdx];
                    // Straight segment tangent (capped to prevent overshoot)
                    tanIn = dirPrev * Mathf.Min(distPrev / 3f, r * 2f);
                    // Arc tangent (forward, continuing in incoming direction)
                    tanOut = corners[cIdx].InDir * (r * BezierK);
                    break;
                }

                case 4: // ArcEnd -- arc ends, straight segment begins
                {
                    float r = radii[cIdx];
                    // Arc tangent (backward, opposite to outgoing direction)
                    tanIn = -corners[cIdx].OutDir * (r * BezierK);
                    // Straight segment tangent (capped to prevent overshoot)
                    tanOut = dirNext * Mathf.Min(distNext / 3f, r * 2f);
                    break;
                }

                case 5: // RampEnd -- horizontal tangents for smooth S-curve elevation
                {
                    Vector3 flatPrev = new Vector3(toPrev.x, 0, toPrev.z);
                    Vector3 flatNext = new Vector3(toNext.x, 0, toNext.z);
                    float flatDistPrev = flatPrev.magnitude;
                    float flatDistNext = flatNext.magnitude;
                    tanIn = flatDistPrev > 0.001f ? (flatPrev / flatDistPrev) * (distPrev / 3f) : Vector3.zero;
                    tanOut = flatDistNext > 0.001f ? (flatNext / flatDistNext) * (distNext / 3f) : Vector3.zero;
                    break;
                }
            }

            waypoints.Add(new Waypoint { Position = pos, TangentIn = tanIn, TangentOut = tanOut });
        }

        return waypoints;
    }

    private static List<Waypoint> BuildStraightLine(Vector3 startPos, Vector3 endPos)
    {
        var dir = endPos - startPos;
        float dist = dir.magnitude;
        var dirNorm = dist > 0.001f ? dir / dist : Vector3.forward;
        float tanMag = dist / 3f;

        return new List<Waypoint>
        {
            new Waypoint
            {
                Position = startPos,
                TangentIn = Vector3.zero,
                TangentOut = dirNorm * tanMag
            },
            new Waypoint
            {
                Position = endPos,
                TangentIn = -dirNorm * tanMag,
                TangentOut = Vector3.zero
            }
        };
    }

    // -- Helpers --

    private static Vector3 DirectionBetween(Vector3 from, Vector3 to, Vector3 fallback)
    {
        var delta = to - from;
        return delta.sqrMagnitude > 0.001f ? delta.normalized : fallback;
    }

    public static Vector3 SnapToCardinal(Vector3 dir)
    {
        var flat = new Vector3(dir.x, 0, dir.z);
        if (flat.sqrMagnitude < 0.001f) return Vector3.forward;

        if (Mathf.Abs(flat.x) >= Mathf.Abs(flat.z))
            return flat.x >= 0 ? Vector3.right : Vector3.left;
        return flat.z >= 0 ? Vector3.forward : Vector3.back;
    }

    private static float HorizontalDist(Vector3 a, Vector3 b)
    {
        return new Vector2(b.x - a.x, b.z - a.z).magnitude;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0, v.z);
    }
}
