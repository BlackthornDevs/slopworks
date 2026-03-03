using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class TurretControllerTests
{
    private TurretDefinitionSO _def;
    private TurretController _turret;

    [SetUp]
    public void SetUp()
    {
        _def = ScriptableObject.CreateInstance<TurretDefinitionSO>();
        _def.turretId = "test_turret";
        _def.range = 20f;
        _def.fireInterval = 0.5f;
        _def.damagePerShot = 10f;
        _def.damageType = DamageType.Kinetic;
        _def.ammoItemId = "bullet";
        _def.powerConsumption = 50f;
        _def.powerThreshold = 0.5f;
        _def.ammoSlotCount = 1;
        _def.ammoMaxStackSize = 64;

        _turret = new TurretController(_def);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_def);
    }

    // -- target selection --

    [Test]
    public void SelectsNearestEnemyInRange()
    {
        LoadAmmo(5);
        var candidates = new List<Vector3>
        {
            new Vector3(15f, 0f, 0f), // 15 units
            new Vector3(5f, 0f, 0f),  // 5 units -- closest
            new Vector3(10f, 0f, 0f)  // 10 units
        };

        var result = _turret.Tick(1f, candidates);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Value.targetIndex);
    }

    [Test]
    public void IgnoresEnemiesOutOfRange()
    {
        LoadAmmo(5);
        var candidates = new List<Vector3>
        {
            new Vector3(25f, 0f, 0f), // 25 units, out of 20 range
            new Vector3(30f, 0f, 0f)  // 30 units
        };

        var result = _turret.Tick(1f, candidates);

        Assert.IsNull(result);
        Assert.IsFalse(_turret.HasTarget);
    }

    [Test]
    public void NoTargetWithEmptyCandidateList()
    {
        LoadAmmo(5);
        var candidates = new List<Vector3>();

        var result = _turret.Tick(1f, candidates);

        Assert.IsNull(result);
        Assert.IsFalse(_turret.HasTarget);
    }

    [Test]
    public void NoTargetWithNullCandidateList()
    {
        LoadAmmo(5);

        var result = _turret.Tick(1f, null);

        Assert.IsNull(result);
    }

    // -- fire rate --

    [Test]
    public void FiresImmediatelyOnFirstTick()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(0.1f, candidates);

        Assert.IsNotNull(result);
    }

    [Test]
    public void RespectsCooldownBetweenShots()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        _turret.Tick(1f, candidates); // fires, sets 0.5s cooldown
        var result = _turret.Tick(0.3f, candidates); // 0.3s < 0.5s cooldown

        Assert.IsNull(result);
    }

    [Test]
    public void FiresAfterCooldownExpires()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        _turret.Tick(1f, candidates); // fires
        _turret.Tick(0.3f, candidates); // still on cooldown
        var result = _turret.Tick(0.3f, candidates); // 0.6s total, past 0.5s cooldown

        Assert.IsNotNull(result);
    }

    // -- ammo consumption --

    [Test]
    public void ConsumesOneAmmoPerShot()
    {
        LoadAmmo(3);
        var candidates = EnemyAt(10f);

        _turret.Tick(1f, candidates);

        Assert.AreEqual(2, _turret.AmmoStorage.GetCount("bullet"));
    }

    [Test]
    public void StopsFiringWhenOutOfAmmo()
    {
        LoadAmmo(1);
        var candidates = EnemyAt(10f);

        _turret.Tick(1f, candidates); // fires last bullet
        var result = _turret.Tick(1f, candidates); // no ammo

        Assert.IsNull(result);
    }

    [Test]
    public void ResumesAfterAmmoLoaded()
    {
        LoadAmmo(1);
        var candidates = EnemyAt(10f);

        _turret.Tick(1f, candidates); // fires last bullet
        _turret.Tick(1f, candidates); // no ammo, null
        LoadAmmo(1);
        var result = _turret.Tick(1f, candidates); // should fire again

        Assert.IsNotNull(result);
    }

    // -- power --

    [Test]
    public void DoesNotFireWithLowPower()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates, 0.3f); // below 0.5 threshold

        Assert.IsNull(result);
    }

    [Test]
    public void FiresAtExactPowerThreshold()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates, 0.5f); // exactly at threshold

        Assert.IsNotNull(result);
    }

    [Test]
    public void FiresWithFullPower()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates, 1f);

        Assert.IsNotNull(result);
    }

    // -- fire event data --

    [Test]
    public void FireEventContainsCorrectDamage()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates);

        Assert.AreEqual(10f, result.Value.damage);
    }

    [Test]
    public void FireEventContainsCorrectDamageType()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates);

        Assert.AreEqual(DamageType.Kinetic, result.Value.damageType);
    }

    [Test]
    public void FireEventContainsTurretId()
    {
        LoadAmmo(5);
        var candidates = EnemyAt(10f);

        var result = _turret.Tick(1f, candidates);

        Assert.AreEqual("test_turret", result.Value.sourceId);
    }

    // -- no ammo requirement --

    [Test]
    public void FiresWithoutAmmoWhenAmmoIdEmpty()
    {
        _def.ammoItemId = "";
        var turret = new TurretController(_def);
        var candidates = EnemyAt(10f);

        var result = turret.Tick(1f, candidates);

        Assert.IsNotNull(result);
    }

    [Test]
    public void FiresWithoutAmmoWhenAmmoIdNull()
    {
        _def.ammoItemId = null;
        var turret = new TurretController(_def);
        var candidates = EnemyAt(10f);

        var result = turret.Tick(1f, candidates);

        Assert.IsNotNull(result);
    }

    // -- edge cases --

    [Test]
    public void EnemyExactlyAtRangeLimit()
    {
        LoadAmmo(5);
        var candidates = new List<Vector3> { new Vector3(20f, 0f, 0f) }; // exactly 20 units

        var result = _turret.Tick(1f, candidates);

        Assert.IsNotNull(result);
    }

    [Test]
    public void EnemyJustBeyondRange()
    {
        LoadAmmo(5);
        var candidates = new List<Vector3> { new Vector3(20.1f, 0f, 0f) };

        var result = _turret.Tick(1f, candidates);

        Assert.IsNull(result);
    }

    [Test]
    public void RangeExposedFromDefinition()
    {
        Assert.AreEqual(20f, _turret.Range);
    }

    [Test]
    public void AmmoItemIdExposedFromDefinition()
    {
        Assert.AreEqual("bullet", _turret.AmmoItemId);
    }

    // -- helpers --

    private void LoadAmmo(int count)
    {
        _turret.AmmoStorage.TryInsertStack("bullet", count);
    }

    private List<Vector3> EnemyAt(float distance)
    {
        return new List<Vector3> { new Vector3(distance, 0f, 0f) };
    }
}
