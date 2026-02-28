using NUnit.Framework;

[TestFixture]
public class HealthComponentTests
{
    private HealthComponent _health;

    [SetUp]
    public void SetUp()
    {
        _health = new HealthComponent(100f);
    }

    [Test]
    public void Constructor_SetsMaxHealth()
    {
        Assert.AreEqual(100f, _health.MaxHealth);
    }

    [Test]
    public void Constructor_SetsCurrentHealthToMax()
    {
        Assert.AreEqual(100f, _health.CurrentHealth);
    }

    [Test]
    public void Constructor_IsAliveReturnsTrue()
    {
        Assert.IsTrue(_health.IsAlive);
    }

    [Test]
    public void TakeDamage_ReducesHealth()
    {
        _health.TakeDamage(MakeDamage(30f));

        Assert.AreEqual(70f, _health.CurrentHealth);
    }

    [Test]
    public void TakeDamage_HealthDoesNotGoBelowZero()
    {
        _health.TakeDamage(MakeDamage(150f));

        Assert.AreEqual(0f, _health.CurrentHealth);
    }

    [Test]
    public void TakeDamage_IsAliveReturnsFalseAtZero()
    {
        _health.TakeDamage(MakeDamage(100f));

        Assert.IsFalse(_health.IsAlive);
    }

    [Test]
    public void TakeDamage_OnDamagedFiresWithCorrectData()
    {
        DamageData received = default;
        _health.OnDamaged += d => received = d;

        var damage = new DamageData(25f, "player_1", DamageType.Kinetic);
        _health.TakeDamage(damage);

        Assert.AreEqual(25f, received.amount);
        Assert.AreEqual("player_1", received.sourceId);
        Assert.AreEqual(DamageType.Kinetic, received.type);
    }

    [Test]
    public void TakeDamage_OnDeathFiresAtZeroHealth()
    {
        bool deathFired = false;
        _health.OnDeath += () => deathFired = true;

        _health.TakeDamage(MakeDamage(100f));

        Assert.IsTrue(deathFired);
    }

    [Test]
    public void TakeDamage_OnDeathFiresOnlyOnce()
    {
        int deathCount = 0;
        _health.OnDeath += () => deathCount++;

        _health.TakeDamage(MakeDamage(100f));
        _health.TakeDamage(MakeDamage(50f));

        Assert.AreEqual(1, deathCount);
    }

    [Test]
    public void TakeDamage_OverkillClampToZero()
    {
        _health.TakeDamage(MakeDamage(9999f));

        Assert.AreEqual(0f, _health.CurrentHealth);
    }

    [Test]
    public void Heal_RestoresHealth()
    {
        _health.TakeDamage(MakeDamage(60f));
        _health.Heal(25f);

        Assert.AreEqual(65f, _health.CurrentHealth);
    }

    [Test]
    public void Heal_DoesNotExceedMax()
    {
        _health.TakeDamage(MakeDamage(10f));
        _health.Heal(50f);

        Assert.AreEqual(100f, _health.CurrentHealth);
    }

    [Test]
    public void Heal_DoesNothingWhenDead()
    {
        _health.TakeDamage(MakeDamage(100f));
        _health.Heal(50f);

        Assert.AreEqual(0f, _health.CurrentHealth);
    }

    [Test]
    public void Heal_DoesNothingWithNegativeAmount()
    {
        _health.TakeDamage(MakeDamage(50f));
        _health.Heal(-10f);

        Assert.AreEqual(50f, _health.CurrentHealth);
    }

    [Test]
    public void Heal_DoesNothingWithZeroAmount()
    {
        _health.TakeDamage(MakeDamage(50f));
        _health.Heal(0f);

        Assert.AreEqual(50f, _health.CurrentHealth);
    }

    [Test]
    public void TakeDamage_ZeroDamageStillFiresOnDamaged()
    {
        bool fired = false;
        _health.OnDamaged += _ => fired = true;

        _health.TakeDamage(MakeDamage(0f));

        Assert.IsTrue(fired);
        Assert.AreEqual(100f, _health.CurrentHealth);
    }

    [Test]
    public void TakeDamage_IgnoredAfterDeath()
    {
        DamageData last = default;
        int callCount = 0;
        _health.OnDamaged += d => { last = d; callCount++; };

        _health.TakeDamage(MakeDamage(100f));
        int countAfterDeath = callCount;
        _health.TakeDamage(MakeDamage(50f));

        Assert.AreEqual(countAfterDeath, callCount);
    }

    private static DamageData MakeDamage(float amount)
    {
        return new DamageData(amount, "test", DamageType.Kinetic);
    }
}
