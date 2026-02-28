using NUnit.Framework;

[TestFixture]
public class FaunaAITests
{
    private FaunaAI MakeAI(float attackCooldown = 1f, float fleeConfidenceThreshold = 0.3f,
                           float alertRange = 20f, float alertStaleTime = 15f,
                           float forceFleeDuration = 5f, float aggressionBoostDuration = 3f,
                           float aggressionBoostMultiplier = 1.3f)
    {
        return new FaunaAI(attackCooldown, fleeConfidenceThreshold, alertRange,
                           alertStaleTime, forceFleeDuration, aggressionBoostDuration,
                           aggressionBoostMultiplier);
    }

    // ── attack timing ─────────────────────────────────────

    [Test]
    public void CanAttack_TrueInitially()
    {
        var ai = MakeAI(attackCooldown: 1f);
        Assert.IsTrue(ai.CanAttack(0f));
    }

    [Test]
    public void CanAttack_FalseOnCooldown()
    {
        var ai = MakeAI(attackCooldown: 1f);
        ai.RecordAttack(0f);
        Assert.IsFalse(ai.CanAttack(0.5f));
    }

    [Test]
    public void CanAttack_TrueAfterCooldown()
    {
        var ai = MakeAI(attackCooldown: 1f);
        ai.RecordAttack(0f);
        Assert.IsTrue(ai.CanAttack(1.1f));
    }

    [Test]
    public void CanAttack_ExactCooldownBoundary()
    {
        var ai = MakeAI(attackCooldown: 1f);
        ai.RecordAttack(0f);
        Assert.IsTrue(ai.CanAttack(1f));
    }

    // ── threat evaluation ─────────────────────────────────

    [Test]
    public void ShouldFlee_TrueWhenHealthLow()
    {
        var ai = MakeAI();
        Assert.IsTrue(ai.ShouldFlee(0.15f, 0f));
    }

    [Test]
    public void ShouldFlee_TrueAtExactly20Percent()
    {
        var ai = MakeAI();
        Assert.IsTrue(ai.ShouldFlee(0.2f, 0f));
    }

    [Test]
    public void ShouldFlee_FalseWhenHealthy()
    {
        var ai = MakeAI();
        Assert.IsFalse(ai.ShouldFlee(0.5f, 0f));
    }

    [Test]
    public void ShouldFlee_TrueWhenMoraleForced()
    {
        var ai = MakeAI(forceFleeDuration: 5f);
        ai.ReactToAllyDeath(1f, 0.1f, 1f); // low confidence -> flee
        Assert.IsTrue(ai.ShouldFlee(0.8f, 3f)); // healthy but forced
    }

    [Test]
    public void ShouldFlee_FalseAfterMoraleFleeExpires()
    {
        var ai = MakeAI(forceFleeDuration: 5f);
        ai.ReactToAllyDeath(1f, 0.1f, 1f);
        Assert.IsFalse(ai.ShouldFlee(0.8f, 7f)); // past flee duration
    }

    // ── pack coordination ─────────────────────────────────

    [Test]
    public void HasUnreactedAllyDeath_FalseInitially()
    {
        var ai = MakeAI();
        Assert.IsFalse(ai.HasUnreactedAllyDeath(0f));
    }

    [Test]
    public void HasUnreactedAllyDeath_TrueOnNewDeath()
    {
        var ai = MakeAI();
        Assert.IsTrue(ai.HasUnreactedAllyDeath(1f));
    }

    [Test]
    public void HasUnreactedAllyDeath_FalseAfterReacted()
    {
        var ai = MakeAI();
        ai.ReactToAllyDeath(1f, 0.5f, 1f);
        Assert.IsFalse(ai.HasUnreactedAllyDeath(1f));
    }

    [Test]
    public void HasUnreactedAllyDeath_TrueOnSecondDeath()
    {
        var ai = MakeAI();
        ai.ReactToAllyDeath(1f, 0.5f, 1f);
        Assert.IsTrue(ai.HasUnreactedAllyDeath(2f));
    }

    [Test]
    public void ReactToAllyDeath_FleeWhenConfidenceBelowThreshold()
    {
        var ai = MakeAI(fleeConfidenceThreshold: 0.3f);
        var reaction = ai.ReactToAllyDeath(1f, 0.1f, 1f);
        Assert.AreEqual(FaunaAI.AllyDeathReaction.Flee, reaction);
    }

    [Test]
    public void ReactToAllyDeath_BoostWhenConfidenceAboveThreshold()
    {
        var ai = MakeAI(fleeConfidenceThreshold: 0.3f);
        var reaction = ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.AreEqual(FaunaAI.AllyDeathReaction.Boost, reaction);
    }

    // ── alert evaluation ──────────────────────────────────

    [Test]
    public void IsAlertRelevant_FalseWhenNotValid()
    {
        var ai = MakeAI(alertRange: 20f);
        Assert.IsFalse(ai.IsAlertRelevant(false, 0f, 0f, 5f));
    }

    [Test]
    public void IsAlertRelevant_FalseWhenStale()
    {
        var ai = MakeAI(alertRange: 20f, alertStaleTime: 15f);
        Assert.IsFalse(ai.IsAlertRelevant(true, 0f, 20f, 5f));
    }

    [Test]
    public void IsAlertRelevant_FalseWhenTooFar()
    {
        var ai = MakeAI(alertRange: 20f);
        Assert.IsFalse(ai.IsAlertRelevant(true, 0f, 1f, 25f));
    }

    [Test]
    public void IsAlertRelevant_TrueWhenCloseAndFresh()
    {
        var ai = MakeAI(alertRange: 20f, alertStaleTime: 15f);
        Assert.IsTrue(ai.IsAlertRelevant(true, 0f, 5f, 10f));
    }

    // ── aggression ────────────────────────────────────────

    [Test]
    public void IsAggressionActive_FalseInitially()
    {
        var ai = MakeAI();
        Assert.IsFalse(ai.IsAggressionActive(0f));
    }

    [Test]
    public void IsAggressionActive_TrueAfterBoostReaction()
    {
        var ai = MakeAI(aggressionBoostDuration: 3f);
        ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.IsTrue(ai.IsAggressionActive(2f));
    }

    [Test]
    public void IsAggressionActive_FalseAfterDuration()
    {
        var ai = MakeAI(aggressionBoostDuration: 3f);
        ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.IsFalse(ai.IsAggressionActive(5f));
    }

    [Test]
    public void GetSpeedMultiplier_ReturnsMultiplierDuringBoost()
    {
        var ai = MakeAI(aggressionBoostMultiplier: 1.3f);
        ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.AreEqual(1.3f, ai.GetSpeedMultiplier(2f), 0.001f);
    }

    [Test]
    public void GetSpeedMultiplier_Returns1WhenNotBoosted()
    {
        var ai = MakeAI();
        Assert.AreEqual(1f, ai.GetSpeedMultiplier(0f));
    }

    [Test]
    public void TryRevertAggression_TrueWhenExpired()
    {
        var ai = MakeAI(aggressionBoostDuration: 3f);
        ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.IsTrue(ai.TryRevertAggression(5f));
        Assert.IsFalse(ai.IsAggressionActive(5f));
    }

    [Test]
    public void TryRevertAggression_FalseWhenStillActive()
    {
        var ai = MakeAI(aggressionBoostDuration: 3f);
        ai.ReactToAllyDeath(1f, 0.8f, 1f);
        Assert.IsFalse(ai.TryRevertAggression(2f));
    }

    // ── strafe ────────────────────────────────────────────

    [Test]
    public void ToggleStrafeDirection_Alternates()
    {
        var ai = MakeAI();
        bool first = ai.ToggleStrafeDirection();
        bool second = ai.ToggleStrafeDirection();
        bool third = ai.ToggleStrafeDirection();

        Assert.IsTrue(first);
        Assert.IsFalse(second);
        Assert.IsTrue(third);
    }
}
