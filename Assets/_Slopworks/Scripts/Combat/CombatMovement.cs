using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Static utility methods for combat movement: strafing, flanking, and cover-seeking.
/// All methods return NavMesh-validated positions where possible.
/// </summary>
public static class CombatMovement
{
    /// <summary>
    /// Returns a point on a perpendicular offset from the current position, keeping
    /// roughly the same distance to the target. Alternates CW/CCW for unpredictable movement.
    /// </summary>
    public static Vector3 CalculateStrafeTarget(Vector3 self, Vector3 target, float radius, bool clockwise)
    {
        Vector3 toTarget = target - self;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
            return self;

        Vector3 approach = toTarget.normalized;
        Vector3 perpendicular = clockwise
            ? new Vector3(approach.z, 0f, -approach.x)
            : new Vector3(-approach.z, 0f, approach.x);

        Vector3 strafePoint = self + perpendicular * radius;

        if (NavMesh.SamplePosition(strafePoint, out NavMeshHit hit, radius, NavMesh.AllAreas))
            return hit.position;

        return self;
    }

    /// <summary>
    /// Returns a position that approaches the target from an offset angle.
    /// Used by pack members to avoid stacking on the same approach vector.
    /// </summary>
    public static Vector3 CalculateFlankTarget(Vector3 self, Vector3 target, float angle, float range)
    {
        Vector3 toTarget = target - self;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.01f)
            return self;

        Vector3 approach = toTarget.normalized;
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 flankDir = rotation * approach;

        // position is offset from target — enemy approaches from the flanked angle
        Vector3 flankPoint = target - flankDir * range;

        if (NavMesh.SamplePosition(flankPoint, out NavMeshHit hit, range, NavMesh.AllAreas))
            return hit.position;

        return self;
    }

    /// <summary>
    /// Casts 8 rays in a semicircle toward the threat to find obstacles,
    /// then returns a NavMesh-valid point on the far side of the closest one.
    /// Returns null if no cover is found.
    /// </summary>
    public static Vector3? FindCoverPoint(Vector3 from, Vector3 threatDir, float searchRadius)
    {
        Vector3 toThreat = threatDir.normalized;
        toThreat.y = 0f;

        if (toThreat.sqrMagnitude < 0.01f)
            return null;

        Vector3? bestPoint = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < 8; i++)
        {
            // spread rays across 180 degrees centered on the threat direction
            float angle = -90f + (180f / 7f) * i;
            Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
            Vector3 rayDir = rotation * toThreat;

            if (!Physics.Raycast(from, rayDir, out RaycastHit hit, searchRadius, PhysicsLayers.FaunaLOSMask))
                continue;

            // cover point is on the far side of the obstacle from the threat
            Vector3 coverPoint = hit.point + rayDir * 2f;

            if (!NavMesh.SamplePosition(coverPoint, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                continue;

            float dist = Vector3.Distance(from, navHit.position);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestPoint = navHit.position;
            }
        }

        return bestPoint;
    }
}
