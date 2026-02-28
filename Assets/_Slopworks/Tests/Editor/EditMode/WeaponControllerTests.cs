using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WeaponControllerTests
{
    private WeaponDefinitionSO _def;
    private WeaponController _weapon;

    [SetUp]
    public void SetUp()
    {
        _def = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
        _def.weaponId = "test_rifle";
        _def.damage = 25f;
        _def.fireRate = 2f;
        _def.range = 50f;
        _def.damageType = DamageType.Kinetic;
        _def.magazineSize = 3;
        _def.reloadTime = 1f;

        _weapon = new WeaponController(_def);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_def);
    }

    [Test]
    public void Constructor_AmmoEqualsMAgazineSize()
    {
        Assert.AreEqual(3, _weapon.CurrentAmmo);
    }

    [Test]
    public void TryFire_SucceedsWithAmmoAndNoCooldown()
    {
        Assert.IsTrue(_weapon.TryFire());
    }

    [Test]
    public void TryFire_DecrementsAmmo()
    {
        _weapon.TryFire();

        Assert.AreEqual(2, _weapon.CurrentAmmo);
    }

    [Test]
    public void TryFire_FailsOnCooldown()
    {
        _weapon.TryFire();
        bool secondShot = _weapon.TryFire();

        Assert.IsFalse(secondShot);
    }

    [Test]
    public void TryFire_SucceedsAfterCooldownExpires()
    {
        _weapon.TryFire();
        _weapon.Tick(1f); // cooldown = 1/fireRate = 0.5s, so 1s is enough

        Assert.IsTrue(_weapon.TryFire());
    }

    [Test]
    public void TryFire_FailsWithZeroAmmo()
    {
        _weapon.TryFire();
        _weapon.Tick(1f);
        _weapon.TryFire();
        _weapon.Tick(1f);
        _weapon.TryFire();
        _weapon.Tick(1f);

        Assert.AreEqual(0, _weapon.CurrentAmmo);
        Assert.IsFalse(_weapon.TryFire());
    }

    [Test]
    public void Reload_SetsIsReloading()
    {
        _weapon.Reload();

        Assert.IsTrue(_weapon.IsReloading);
    }

    [Test]
    public void Reload_RestoresAmmoAfterDelay()
    {
        _weapon.TryFire();
        _weapon.TryFire(); // need to clear cooldown first
        // Actually, let's just drain and reload
        _weapon.Reload();
        _weapon.Tick(1.1f); // reloadTime = 1f

        Assert.AreEqual(3, _weapon.CurrentAmmo);
        Assert.IsFalse(_weapon.IsReloading);
    }

    [Test]
    public void TryFire_FailsWhileReloading()
    {
        _weapon.Reload();

        Assert.IsFalse(_weapon.TryFire());
    }

    [Test]
    public void FireRate_CooldownMatchesDefinition()
    {
        // fireRate = 2 shots/sec, so cooldown = 0.5s
        _weapon.TryFire();
        _weapon.Tick(0.4f); // not enough

        Assert.IsFalse(_weapon.CanFire);

        _weapon.Tick(0.2f); // total 0.6s, past 0.5s cooldown

        Assert.IsTrue(_weapon.CanFire);
    }

    [Test]
    public void TryFire_ZeroFireRate_NoCooldown()
    {
        _def.fireRate = 0f;
        var weapon = new WeaponController(_def);

        weapon.TryFire();

        Assert.IsTrue(weapon.CanFire);
    }

    [Test]
    public void Range_ReturnsDefinitionRange()
    {
        Assert.AreEqual(50f, _weapon.Range);
    }

    [Test]
    public void BuildDamageData_ReturnsCorrectValues()
    {
        var data = _weapon.BuildDamageData("player_1");

        Assert.AreEqual(25f, data.amount);
        Assert.AreEqual("player_1", data.sourceId);
        Assert.AreEqual(DamageType.Kinetic, data.type);
    }

    [Test]
    public void Reload_IgnoredWhileAlreadyReloading()
    {
        _weapon.Reload();
        _weapon.Tick(0.5f); // halfway through reload
        _weapon.Reload(); // should be ignored
        _weapon.Tick(0.6f); // total 1.1s from first reload

        Assert.AreEqual(3, _weapon.CurrentAmmo);
        Assert.IsFalse(_weapon.IsReloading);
    }

    [Test]
    public void Reload_WhenAlreadyFull_StillReloads()
    {
        _weapon.Reload();
        _weapon.Tick(1.1f);

        Assert.AreEqual(3, _weapon.CurrentAmmo);
    }
}
