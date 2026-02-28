using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class HealthBehaviourPlayTests
{
    private GameObject _go;

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
            Object.Destroy(_go);
    }

    // ── basic wiring ────────────────────────────────────

    [UnityTest]
    public IEnumerator Awake_CreatesHealthComponent()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();

        // wait one frame for Awake to run
        yield return null;

        Assert.IsNotNull(hb.Health);
        Assert.IsTrue(hb.Health.IsAlive);
    }

    [UnityTest]
    public IEnumerator HealthComponent_DefaultMaxHealth_Is100()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();
        yield return null;

        Assert.AreEqual(100f, hb.Health.MaxHealth);
        Assert.AreEqual(100f, hb.Health.CurrentHealth);
    }

    // ── damage through behaviour ────────────────────────

    [UnityTest]
    public IEnumerator TakeDamage_ReducesHealth()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();
        yield return null;

        var damage = new DamageData(25f, "test_source", DamageType.Kinetic);
        hb.Health.TakeDamage(damage);

        Assert.AreEqual(75f, hb.Health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator TakeDamage_FiresOnDamagedEvent()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();
        yield return null;

        bool eventFired = false;
        hb.Health.OnDamaged += _ => eventFired = true;

        var damage = new DamageData(10f, "test_source", DamageType.Kinetic);
        hb.Health.TakeDamage(damage);

        Assert.IsTrue(eventFired);
    }

    [UnityTest]
    public IEnumerator LethalDamage_FiresOnDeathEvent()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();
        yield return null;

        bool deathFired = false;
        hb.Health.OnDeath += () => deathFired = true;

        var damage = new DamageData(200f, "test_source", DamageType.Kinetic);
        hb.Health.TakeDamage(damage);

        Assert.IsTrue(deathFired);
        Assert.IsFalse(hb.Health.IsAlive);
    }

    // ── two entities interacting ────────────────────────

    [UnityTest]
    public IEnumerator TwoEntities_DamageOneDoesNotAffectOther()
    {
        _go = new GameObject("EntityA");
        var hbA = _go.AddComponent<HealthBehaviour>();

        var goB = new GameObject("EntityB");
        var hbB = goB.AddComponent<HealthBehaviour>();

        yield return null;

        var damage = new DamageData(50f, "test_source", DamageType.Kinetic);
        hbA.Health.TakeDamage(damage);

        Assert.AreEqual(50f, hbA.Health.CurrentHealth);
        Assert.AreEqual(100f, hbB.Health.CurrentHealth);

        Object.Destroy(goB);
    }

    // ── sourcePosition wiring ───────────────────────────

    [UnityTest]
    public IEnumerator DamageData_SourcePosition_PassedThroughEvent()
    {
        _go = new GameObject("TestEntity");
        var hb = _go.AddComponent<HealthBehaviour>();
        yield return null;

        Vector3 receivedPosition = Vector3.zero;
        hb.Health.OnDamaged += d => receivedPosition = d.sourcePosition;

        var sourcePos = new Vector3(10f, 0f, 5f);
        var damage = new DamageData(10f, "test_source", DamageType.Kinetic, sourcePos);
        hb.Health.TakeDamage(damage);

        Assert.AreEqual(sourcePos, receivedPosition);
    }
}
