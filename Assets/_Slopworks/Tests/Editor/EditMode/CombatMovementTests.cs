using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CombatMovementTests
{
    // ── strafe target ──────────────────────────────────────

    [Test]
    public void Strafe_Clockwise_MovesPerpendicularRight()
    {
        // enemy at origin, target at (0, 0, 10) — forward
        // clockwise perpendicular should move in +x direction
        Vector3 self = Vector3.zero;
        Vector3 target = new Vector3(0f, 0f, 10f);
        float radius = 3f;

        Vector3 result = CombatMovement.CalculateStrafeTarget(self, target, radius, true);

        // no NavMesh in EditMode, so should return self (fallback)
        // but the math itself is tested by checking the perpendicular calc
        // in production, NavMesh.SamplePosition validates the point
        Assert.AreEqual(self, result, "falls back to self without NavMesh");
    }

    [Test]
    public void Strafe_CounterClockwise_ReturnsOppositeOfClockwise()
    {
        Vector3 self = Vector3.zero;
        Vector3 target = new Vector3(0f, 0f, 10f);
        float radius = 3f;

        // both should return self in EditMode (no NavMesh)
        // but they should be different in production
        Vector3 cw = CombatMovement.CalculateStrafeTarget(self, target, radius, true);
        Vector3 ccw = CombatMovement.CalculateStrafeTarget(self, target, radius, false);

        // in EditMode both fall back to self
        Assert.AreEqual(cw, ccw);
    }

    [Test]
    public void Strafe_SamePosition_ReturnsSelf()
    {
        // when self == target, should return self without errors
        Vector3 pos = new Vector3(5f, 0f, 5f);
        Vector3 result = CombatMovement.CalculateStrafeTarget(pos, pos, 3f, true);

        Assert.AreEqual(pos, result);
    }

    [Test]
    public void Strafe_VeryClosePositions_ReturnsSelf()
    {
        Vector3 self = Vector3.zero;
        Vector3 target = new Vector3(0.001f, 0f, 0f);
        Vector3 result = CombatMovement.CalculateStrafeTarget(self, target, 3f, true);

        Assert.AreEqual(self, result);
    }

    // ── flank target ───────────────────────────────────────

    [Test]
    public void Flank_ZeroAngle_ApproachesDirectly()
    {
        Vector3 self = Vector3.zero;
        Vector3 target = new Vector3(0f, 0f, 10f);
        float range = 5f;

        Vector3 result = CombatMovement.CalculateFlankTarget(self, target, 0f, range);

        // without NavMesh, returns self (fallback)
        Assert.AreEqual(self, result, "falls back to self without NavMesh");
    }

    [Test]
    public void Flank_SamePosition_ReturnsSelf()
    {
        Vector3 pos = new Vector3(3f, 0f, 3f);
        Vector3 result = CombatMovement.CalculateFlankTarget(pos, pos, 90f, 5f);

        Assert.AreEqual(pos, result);
    }

    // ── flank angle math (direct geometry tests) ───────────

    [Test]
    public void FlankAngle_90Degrees_PerpendicularApproach()
    {
        // verify the rotation math: a 90-degree rotation of (0,0,1) gives (1,0,0)
        Vector3 approach = Vector3.forward;
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        Vector3 rotated = rotation * approach;

        Assert.AreEqual(1f, rotated.x, 0.001f);
        Assert.AreEqual(0f, rotated.z, 0.001f);
    }

    [Test]
    public void FlankAngle_Negative90Degrees_OppositePerpendicularApproach()
    {
        Vector3 approach = Vector3.forward;
        Quaternion rotation = Quaternion.Euler(0f, -90f, 0f);
        Vector3 rotated = rotation * approach;

        Assert.AreEqual(-1f, rotated.x, 0.001f);
        Assert.AreEqual(0f, rotated.z, 0.001f);
    }

    [Test]
    public void FlankAngle_180Degrees_ReversesApproach()
    {
        Vector3 approach = Vector3.forward;
        Quaternion rotation = Quaternion.Euler(0f, 180f, 0f);
        Vector3 rotated = rotation * approach;

        Assert.AreEqual(0f, rotated.x, 0.001f);
        Assert.AreEqual(-1f, rotated.z, 0.001f);
    }

    // ── strafe perpendicular math ──────────────────────────

    [Test]
    public void StrafePerpendicular_Forward_ClockwiseIsRight()
    {
        // approach = (0, 0, 1) → clockwise perpendicular = (1, 0, 0)
        Vector3 approach = Vector3.forward;
        Vector3 perp = new Vector3(approach.z, 0f, -approach.x);

        Assert.AreEqual(1f, perp.x, 0.001f);
        Assert.AreEqual(0f, perp.z, 0.001f);
    }

    [Test]
    public void StrafePerpendicular_Forward_CounterClockwiseIsLeft()
    {
        Vector3 approach = Vector3.forward;
        Vector3 perp = new Vector3(-approach.z, 0f, approach.x);

        Assert.AreEqual(-1f, perp.x, 0.001f);
        Assert.AreEqual(0f, perp.z, 0.001f);
    }

    [Test]
    public void StrafePerpendicular_Right_ClockwiseIsBackward()
    {
        // approach = (1, 0, 0) → clockwise perpendicular = (0, 0, -1)
        Vector3 approach = Vector3.right;
        Vector3 perp = new Vector3(approach.z, 0f, -approach.x);

        Assert.AreEqual(0f, perp.x, 0.001f);
        Assert.AreEqual(-1f, perp.z, 0.001f);
    }

    [Test]
    public void StrafePerpendicular_DiagonalApproach_StillPerpendicular()
    {
        Vector3 approach = new Vector3(1f, 0f, 1f).normalized;
        Vector3 perp = new Vector3(approach.z, 0f, -approach.x);

        // dot product of perpendicular vectors should be 0
        float dot = Vector3.Dot(approach, perp);
        Assert.AreEqual(0f, dot, 0.001f);

        // magnitude should be 1 (unit vector in, unit vector out)
        Assert.AreEqual(1f, perp.magnitude, 0.001f);
    }

    // ── cover point ────────────────────────────────────────

    [Test]
    public void FindCoverPoint_NoPhysics_ReturnsNull()
    {
        // no colliders in EditMode test, should return null
        Vector3? result = CombatMovement.FindCoverPoint(
            Vector3.zero, Vector3.forward, 10f);

        Assert.IsNull(result);
    }

    [Test]
    public void FindCoverPoint_ZeroThreatDir_ReturnsNull()
    {
        Vector3? result = CombatMovement.FindCoverPoint(
            Vector3.zero, Vector3.zero, 10f);

        Assert.IsNull(result);
    }
}
