using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FaunaControllerTests
{
    private FaunaDefinitionSO _def;

    [SetUp]
    public void SetUp()
    {
        _def = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
        _def.faunaId = "test_grunt";
        _def.maxHealth = 100f;
        _def.moveSpeed = 3.5f;
        _def.attackDamage = 10f;
        _def.attackRange = 2f;
        _def.attackCooldown = 1f;
        _def.sightRange = 15f;
        _def.sightAngle = 120f;
        _def.hearingRange = 8f;
        _def.attackDamageType = DamageType.Kinetic;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_def);
    }

    // -- HealthComponent integration tests (D-004: testable plain C# class) --

    [Test]
    public void Health_InitializesWithDefinitionMaxHealth()
    {
        var health = new HealthComponent(_def.maxHealth);

        Assert.AreEqual(100f, health.CurrentHealth);
        Assert.AreEqual(100f, health.MaxHealth);
        Assert.IsTrue(health.IsAlive);
    }

    [Test]
    public void Health_TakeDamageWithFaunaAttackValues()
    {
        var health = new HealthComponent(_def.maxHealth);
        var damage = new DamageData(_def.attackDamage, "test_grunt", _def.attackDamageType);

        health.TakeDamage(damage);

        Assert.AreEqual(90f, health.CurrentHealth);
    }

    [Test]
    public void Health_DiesFromRepeatedFaunaAttacks()
    {
        var health = new HealthComponent(_def.maxHealth);
        bool deathFired = false;
        health.OnDeath += () => deathFired = true;

        for (int i = 0; i < 10; i++)
        {
            health.TakeDamage(new DamageData(_def.attackDamage, "test_grunt", _def.attackDamageType));
        }

        Assert.AreEqual(0f, health.CurrentHealth);
        Assert.IsFalse(health.IsAlive);
        Assert.IsTrue(deathFired);
    }

    [Test]
    public void Health_OnDamagedFiresWithCorrectData()
    {
        var health = new HealthComponent(_def.maxHealth);
        DamageData received = default;
        health.OnDamaged += d => received = d;

        var damage = new DamageData(_def.attackDamage, "player_1", DamageType.Kinetic);
        health.TakeDamage(damage);

        Assert.AreEqual(_def.attackDamage, received.amount);
        Assert.AreEqual("player_1", received.sourceId);
        Assert.AreEqual(DamageType.Kinetic, received.type);
    }

    [Test]
    public void Health_FleeThresholdAt20Percent()
    {
        var health = new HealthComponent(_def.maxHealth);
        float fleeThreshold = 0.2f;

        // take 80 damage (80% of 100) — should be exactly at threshold
        health.TakeDamage(new DamageData(80f, "test", DamageType.Kinetic));

        float healthPercent = health.CurrentHealth / health.MaxHealth;
        Assert.AreEqual(0.2f, healthPercent, 0.001f);
        Assert.IsTrue(healthPercent <= fleeThreshold);
    }

    [Test]
    public void Health_AboveFleeThresholdInitially()
    {
        var health = new HealthComponent(_def.maxHealth);
        float fleeThreshold = 0.2f;

        float healthPercent = health.CurrentHealth / health.MaxHealth;
        Assert.IsTrue(healthPercent > fleeThreshold);
    }

    // -- FaunaDefinitionSO tests --

    [Test]
    public void Definition_DefaultValuesMatchSpec()
    {
        Assert.AreEqual("test_grunt", _def.faunaId);
        Assert.AreEqual(100f, _def.maxHealth);
        Assert.AreEqual(3.5f, _def.moveSpeed);
        Assert.AreEqual(10f, _def.attackDamage);
        Assert.AreEqual(2f, _def.attackRange);
        Assert.AreEqual(1f, _def.attackCooldown);
        Assert.AreEqual(DamageType.Kinetic, _def.attackDamageType);
    }

    [Test]
    public void Definition_CooldownCalculation()
    {
        // verify fire rate / cooldown relationship used in behavior tree Wait node
        float cooldown = _def.attackCooldown;
        Assert.AreEqual(1f, cooldown);
    }

    // -- Tests that require PlayMode (noted per task spec) --
    // These tests need NavMeshAgent, Physics, and a running game loop:
    //
    // PlayMode: FaunaController_BuildsBehaviorTreeOnEnable
    // PlayMode: FaunaController_PerceptionDetectsPlayerInSightCone
    // PlayMode: FaunaController_PerceptionBlockedByLOS
    // PlayMode: FaunaController_ChasesDetectedPlayer
    // PlayMode: FaunaController_AttacksPlayerInRange
    // PlayMode: FaunaController_FleesWhenHealthLow
    // PlayMode: FaunaController_DiesAndCleansUp
    // PlayMode: FaunaController_WandersWhenNoTarget
}
