using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SettlementBuildingTests
{
    private SettlementBuildingDefinitionSO _definition;
    private SettlementBuilding _building;

    [SetUp]
    public void SetUp()
    {
        _definition = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _definition.buildingId = "test_farm";
        _definition.displayName = "Test Farm";
        _definition.buildingType = SettlementBuildingType.Farmstead;

        // 3 repair stages with different required items
        _definition.repairStages = new RepairStageDefinition[]
        {
            new RepairStageDefinition
            {
                requiredItemIds = new[] { "wood" },
                requiredAmounts = new[] { 5 },
                unlockedCapability = SettlementCapability.None
            },
            new RepairStageDefinition
            {
                requiredItemIds = new[] { "stone", "nails" },
                requiredAmounts = new[] { 10, 4 },
                unlockedCapability = SettlementCapability.None
            },
            new RepairStageDefinition
            {
                requiredItemIds = new[] { "steel" },
                requiredAmounts = new[] { 3 },
                unlockedCapability = SettlementCapability.CraftingStation
            }
        };

        // Production: raw_food, amount 1, interval 10s
        _definition.production = new ProductionDefinition
        {
            producedItemId = "raw_food",
            producedAmount = 1,
            productionInterval = 10f,
            requiresSupplyLine = false
        };

        _definition.territoryRadius = 20f;
        _definition.connectionRange = 100f;
        _definition.workerSlots = 3;
        _definition.workerBonusPerSlot = 0.25f;

        // 1 upgrade tier: greenhouse
        _definition.upgradeTiers = new UpgradeTierDefinition[]
        {
            new UpgradeTierDefinition
            {
                tierName = "greenhouse",
                requiredItemIds = new[] { "glass" },
                requiredAmounts = new[] { 10 },
                productionOverride = new ProductionDefinition
                {
                    producedItemId = "medicine",
                    producedAmount = 1,
                    productionInterval = 20f,
                    requiresSupplyLine = false
                },
                territoryBonus = 5f,
                workerSlotsBonus = 1,
                unlockedCapability = SettlementCapability.None
            }
        };

        _building = new SettlementBuilding("test_farm_01", _definition, new Vector3(10, 0, 20));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_definition);
    }

    // -- repair (8 tests) --

    [Test]
    public void StartsAtRepairLevelZero()
    {
        Assert.AreEqual(0, _building.RepairLevel);
    }

    [Test]
    public void AdvanceRepairIncrementsLevel()
    {
        _building.AdvanceRepair();

        Assert.AreEqual(1, _building.RepairLevel);
    }

    [Test]
    public void CannotRepairPastMaxLevel()
    {
        // Max is 3 (3 repair stages)
        _building.AdvanceRepair();
        _building.AdvanceRepair();
        _building.AdvanceRepair();
        _building.AdvanceRepair(); // should be no-op

        Assert.AreEqual(3, _building.RepairLevel);
    }

    [Test]
    public void IsClaimedWhenFullyRepaired()
    {
        Assert.IsFalse(_building.IsClaimed);

        _building.AdvanceRepair();
        _building.AdvanceRepair();
        _building.AdvanceRepair();

        Assert.IsTrue(_building.IsClaimed);
    }

    [Test]
    public void GetRepairRequirementsReturnsCurrentStage()
    {
        // At level 0, should return stage 0 requirements: wood x5
        var reqs = _building.GetRepairRequirements();

        Assert.AreEqual(1, reqs.Length);
        Assert.AreEqual("wood", reqs[0].itemId);
        Assert.AreEqual(5, reqs[0].amount);
    }

    [Test]
    public void GetRepairRequirementsReturnsEmptyWhenFullyRepaired()
    {
        _building.AdvanceRepair();
        _building.AdvanceRepair();
        _building.AdvanceRepair();

        var reqs = _building.GetRepairRequirements();

        Assert.AreEqual(0, reqs.Length);
    }

    [Test]
    public void RepairFiresEvent()
    {
        string firedId = null;
        int firedLevel = -1;
        _building.OnRepaired += (id, level) => { firedId = id; firedLevel = level; };

        _building.AdvanceRepair();

        Assert.AreEqual("test_farm_01", firedId);
        Assert.AreEqual(1, firedLevel);
    }

    [Test]
    public void ClaimFiresEvent()
    {
        string firedId = null;
        _building.OnClaimed += (id) => { firedId = id; };

        _building.AdvanceRepair();
        _building.AdvanceRepair();

        Assert.IsNull(firedId); // not yet claimed

        _building.AdvanceRepair(); // fully repaired -> claimed

        Assert.AreEqual("test_farm_01", firedId);
    }

    // -- production (4 tests) --

    [Test]
    public void DoesNotProduceWhenUnclaimed()
    {
        bool produced = false;
        _building.OnProduced += (id, itemId, amount) => { produced = true; };

        _building.Tick(100f); // way past interval, but unclaimed

        Assert.IsFalse(produced);
    }

    [Test]
    public void ProducesAfterClaimAndInterval()
    {
        FullyRepair();

        string producedItem = null;
        int producedAmount = 0;
        _building.OnProduced += (id, itemId, amount) => { producedItem = itemId; producedAmount = amount; };

        _building.Tick(10f); // exactly one interval

        Assert.AreEqual("raw_food", producedItem);
        Assert.AreEqual(1, producedAmount);
    }

    [Test]
    public void DoesNotProduceBeforeIntervalElapsed()
    {
        FullyRepair();

        bool produced = false;
        _building.OnProduced += (id, itemId, amount) => { produced = true; };

        _building.Tick(9.99f); // just under 10s interval

        Assert.IsFalse(produced);
    }

    [Test]
    public void MultipleProductionCyclesInOneTick()
    {
        FullyRepair();

        int produceCount = 0;
        _building.OnProduced += (id, itemId, amount) => { produceCount++; };

        _building.Tick(25f); // 2 full cycles (10s each), 5s leftover

        Assert.AreEqual(2, produceCount);
    }

    // -- workers (5 tests) --

    [Test]
    public void AssignWorkerSucceeds()
    {
        FullyRepair();

        bool result = _building.AssignWorker("worker_1");

        Assert.IsTrue(result);
        Assert.AreEqual(1, _building.WorkerCount);
        Assert.IsTrue(_building.AssignedWorkerIds.Contains("worker_1"));
    }

    [Test]
    public void CannotAssignMoreThanMaxSlots()
    {
        FullyRepair();

        _building.AssignWorker("w1");
        _building.AssignWorker("w2");
        _building.AssignWorker("w3");
        bool result = _building.AssignWorker("w4"); // base slots = 3, should fail

        Assert.IsFalse(result);
        Assert.AreEqual(3, _building.WorkerCount);
    }

    [Test]
    public void CannotAssignSameWorkerTwice()
    {
        FullyRepair();

        _building.AssignWorker("worker_1");
        bool result = _building.AssignWorker("worker_1");

        Assert.IsFalse(result);
        Assert.AreEqual(1, _building.WorkerCount);
    }

    [Test]
    public void UnassignWorkerSucceeds()
    {
        FullyRepair();

        _building.AssignWorker("worker_1");
        bool result = _building.UnassignWorker("worker_1");

        Assert.IsTrue(result);
        Assert.AreEqual(0, _building.WorkerCount);
    }

    [Test]
    public void WorkersBoostProductionRate()
    {
        FullyRepair();

        // With 2 workers at 0.25 bonus each: multiplier = 1 + 0.25*2 = 1.5
        // Effective interval = 10 / 1.5 = 6.667s
        _building.AssignWorker("w1");
        _building.AssignWorker("w2");

        Assert.AreEqual(1.5f, _building.EffectiveProductionMultiplier, 0.001f);
    }

    // -- worker production effect (1 test) --

    [Test]
    public void WorkerBonusAffectsProductionTiming()
    {
        FullyRepair();

        _building.AssignWorker("w1");
        _building.AssignWorker("w2");
        // multiplier = 1.5, effective interval = 10/1.5 = 6.667s

        int produceCount = 0;
        _building.OnProduced += (id, itemId, amount) => { produceCount++; };

        _building.Tick(7f); // should produce once (6.667 < 7 < 13.333)

        Assert.AreEqual(1, produceCount);
    }

    // -- upgrades (6 tests) --

    [Test]
    public void StartsAtUpgradeTierZero()
    {
        Assert.AreEqual(0, _building.UpgradeTier);
    }

    [Test]
    public void CannotUpgradeWhenUnclaimed()
    {
        bool result = _building.AdvanceUpgrade();

        Assert.IsFalse(result);
        Assert.AreEqual(0, _building.UpgradeTier);
    }

    [Test]
    public void UpgradeAdvancesTier()
    {
        FullyRepair();

        bool result = _building.AdvanceUpgrade();

        Assert.IsTrue(result);
        Assert.AreEqual(1, _building.UpgradeTier);
    }

    [Test]
    public void CannotUpgradePastMaxTier()
    {
        FullyRepair();

        _building.AdvanceUpgrade(); // tier 1 (max for this definition)
        bool result = _building.AdvanceUpgrade(); // should fail

        Assert.IsFalse(result);
        Assert.AreEqual(1, _building.UpgradeTier);
    }

    [Test]
    public void UpgradeOverridesProduction()
    {
        FullyRepair();
        _building.AdvanceUpgrade();

        // Upgrade tier 1 overrides production to medicine, interval 20s
        string producedItem = null;
        _building.OnProduced += (id, itemId, amount) => { producedItem = itemId; };

        _building.Tick(20f);

        Assert.AreEqual("medicine", producedItem);
    }

    [Test]
    public void UpgradeIncreasesTerritory()
    {
        FullyRepair();

        float baseTerritoryRadius = _building.EffectiveTerritoryRadius;
        Assert.AreEqual(20f, baseTerritoryRadius, 0.001f);

        _building.AdvanceUpgrade(); // +5f territory bonus

        Assert.AreEqual(25f, _building.EffectiveTerritoryRadius, 0.001f);
    }

    // -- upgrade extras (2 tests) --

    [Test]
    public void UpgradeIncreasesWorkerSlots()
    {
        FullyRepair();

        Assert.AreEqual(3, _building.MaxWorkerSlots); // base

        _building.AdvanceUpgrade(); // +1 worker slot bonus

        Assert.AreEqual(4, _building.MaxWorkerSlots);
    }

    [Test]
    public void UpgradeFiresEvent()
    {
        FullyRepair();

        string firedId = null;
        int firedTier = -1;
        _building.OnUpgraded += (id, tier) => { firedId = id; firedTier = tier; };

        _building.AdvanceUpgrade();

        Assert.AreEqual("test_farm_01", firedId);
        Assert.AreEqual(1, firedTier);
    }

    // -- territory (2 tests) --

    [Test]
    public void TerritoryRadiusMatchesDefinitionWhenClaimed()
    {
        FullyRepair();

        Assert.AreEqual(20f, _building.EffectiveTerritoryRadius, 0.001f);
    }

    [Test]
    public void TerritoryRadiusIsZeroWhenUnclaimed()
    {
        Assert.AreEqual(0f, _building.EffectiveTerritoryRadius, 0.001f);
    }

    // -- upgrade requirements (2 tests) --

    [Test]
    public void GetUpgradeRequirementsReturnsCurrentTier()
    {
        FullyRepair();

        // At tier 0, should return tier 0 upgrade requirements: glass x10
        var reqs = _building.GetUpgradeRequirements();

        Assert.AreEqual(1, reqs.Length);
        Assert.AreEqual("glass", reqs[0].itemId);
        Assert.AreEqual(10, reqs[0].amount);
    }

    [Test]
    public void GetUpgradeRequirementsReturnsEmptyAtMaxTier()
    {
        FullyRepair();
        _building.AdvanceUpgrade(); // now at max tier

        var reqs = _building.GetUpgradeRequirements();

        Assert.AreEqual(0, reqs.Length);
    }

    // -- network sync setters (implicit tests via other behaviors) --

    [Test]
    public void SetRepairLevelDoesNotFireEvents()
    {
        bool eventFired = false;
        _building.OnRepaired += (id, level) => { eventFired = true; };
        _building.OnClaimed += (id) => { eventFired = true; };

        _building.SetRepairLevel(3); // set to max without events

        Assert.IsFalse(eventFired);
        Assert.AreEqual(3, _building.RepairLevel);
        Assert.IsTrue(_building.IsClaimed);
    }

    [Test]
    public void SetUpgradeTierDoesNotFireEvents()
    {
        FullyRepair();

        bool eventFired = false;
        _building.OnUpgraded += (id, tier) => { eventFired = true; };

        _building.SetUpgradeTier(1);

        Assert.IsFalse(eventFired);
        Assert.AreEqual(1, _building.UpgradeTier);
    }

    // -- helpers --

    private void FullyRepair()
    {
        for (int i = 0; i < _definition.MaxRepairLevel; i++)
        {
            _building.AdvanceRepair();
        }
    }
}
