using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SettlementGraphTests
{
    private SettlementBuildingDefinitionSO _farmDef;
    private SettlementBuildingDefinitionSO _workshopDef;
    private SettlementBuildingDefinitionSO _factoryDef;
    private SettlementGraph _graph;

    [SetUp]
    public void SetUp()
    {
        // farm: connectionRange=100, territoryRadius=20, 1 repair stage, produces raw_food every 10s
        _farmDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _farmDef.buildingId = "farm";
        _farmDef.displayName = "Farm";
        _farmDef.buildingType = SettlementBuildingType.Farmstead;
        _farmDef.connectionRange = 100f;
        _farmDef.territoryRadius = 20f;
        _farmDef.workerSlots = 2;
        _farmDef.workerBonusPerSlot = 0.25f;
        _farmDef.repairStages = new RepairStageDefinition[]
        {
            new RepairStageDefinition
            {
                requiredItemIds = new[] { "wood" },
                requiredAmounts = new[] { 5 }
            }
        };
        _farmDef.production = new ProductionDefinition
        {
            producedItemId = "raw_food",
            producedAmount = 1,
            productionInterval = 10f,
            requiresSupplyLine = false
        };

        // workshop: connectionRange=100, territoryRadius=25, 1 repair stage, no production
        _workshopDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _workshopDef.buildingId = "workshop";
        _workshopDef.displayName = "Workshop";
        _workshopDef.buildingType = SettlementBuildingType.Workshop;
        _workshopDef.connectionRange = 100f;
        _workshopDef.territoryRadius = 25f;
        _workshopDef.workerSlots = 2;
        _workshopDef.workerBonusPerSlot = 0.25f;
        _workshopDef.repairStages = new RepairStageDefinition[]
        {
            new RepairStageDefinition
            {
                requiredItemIds = new[] { "stone" },
                requiredAmounts = new[] { 10 }
            }
        };

        // factory_yard: connectionRange=150, territoryRadius=30, 0 repair stages (pre-claimed)
        _factoryDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _factoryDef.buildingId = "factory_yard";
        _factoryDef.displayName = "Factory Yard";
        _factoryDef.buildingType = SettlementBuildingType.Depot;
        _factoryDef.connectionRange = 150f;
        _factoryDef.territoryRadius = 30f;
        _factoryDef.workerSlots = 0;
        _factoryDef.workerBonusPerSlot = 0f;
        _factoryDef.repairStages = new RepairStageDefinition[0]; // 0 stages = pre-claimed

        _graph = new SettlementGraph("factory_yard");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_farmDef);
        Object.DestroyImmediate(_workshopDef);
        Object.DestroyImmediate(_factoryDef);
    }

    // -- registration (4 tests) --

    [Test]
    public void RegisterBuildingAddsToGraph()
    {
        bool result = _graph.Register(_farmDef, new Vector3(50, 0, 50));

        Assert.IsTrue(result);
        Assert.IsNotNull(_graph.Get("farm"));
        Assert.AreEqual("farm", _graph.Get("farm").BuildingId);
    }

    [Test]
    public void GetReturnsNullForUnknownId()
    {
        Assert.IsNull(_graph.Get("nonexistent"));
    }

    [Test]
    public void CannotRegisterDuplicateId()
    {
        _graph.Register(_farmDef, new Vector3(50, 0, 50));

        bool result = _graph.Register(_farmDef, new Vector3(100, 0, 100));

        Assert.IsFalse(result);
    }

    [Test]
    public void AllBuildingsReturnsAllRegistered()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 50));
        _graph.Register(_workshopDef, new Vector3(80, 0, 80));

        var all = _graph.AllBuildings;

        Assert.AreEqual(3, all.Count);
        Assert.IsTrue(all.ContainsKey("factory_yard"));
        Assert.IsTrue(all.ContainsKey("farm"));
        Assert.IsTrue(all.ContainsKey("workshop"));
    }

    // -- roads (4 tests) --

    [Test]
    public void BuildRoadConnectsTwoBuildings()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        bool result = _graph.BuildRoad("factory_yard", "farm");

        Assert.IsTrue(result);
        Assert.IsTrue(_graph.AreConnected("factory_yard", "farm"));
    }

    [Test]
    public void CannotBuildRoadToSelf()
    {
        _graph.Register(_factoryDef, Vector3.zero);

        bool result = _graph.BuildRoad("factory_yard", "factory_yard");

        Assert.IsFalse(result);
    }

    [Test]
    public void CannotBuildRoadBeyondRange()
    {
        // farm connectionRange=100, factory connectionRange=150
        // min is 100, place farm at distance 150 (beyond min range)
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(150, 0, 0));

        bool result = _graph.BuildRoad("factory_yard", "farm");

        Assert.IsFalse(result);
    }

    [Test]
    public void CannotBuildDuplicateRoad()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");
        bool result = _graph.BuildRoad("factory_yard", "farm");

        Assert.IsFalse(result);
    }

    // -- road bidirectionality (1 test) --

    [Test]
    public void RoadIsBidirectional()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");

        // road from A->B also means B->A, and reverse duplicate should also fail
        Assert.IsTrue(_graph.AreConnected("farm", "factory_yard"));

        bool reverseDuplicate = _graph.BuildRoad("farm", "factory_yard");
        Assert.IsFalse(reverseDuplicate);
    }

    // -- connectivity (6 tests) --

    [Test]
    public void AreConnectedReturnsFalseWithoutRoad()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        Assert.IsFalse(_graph.AreConnected("factory_yard", "farm"));
    }

    [Test]
    public void TransitiveConnectionWorks()
    {
        // factory -- farm -- workshop (transitive: factory connected to workshop)
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");

        Assert.IsTrue(_graph.AreConnected("factory_yard", "workshop"));
    }

    [Test]
    public void HasFactoryConnectionReturnsTrueWhenDirectlyConnected()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");

        Assert.IsTrue(_graph.HasFactoryConnection("farm"));
    }

    [Test]
    public void HasFactoryConnectionReturnsTrueTransitively()
    {
        // factory -- farm -- workshop
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");

        Assert.IsTrue(_graph.HasFactoryConnection("workshop"));
    }

    [Test]
    public void HasFactoryConnectionReturnsFalseWhenDisconnected()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        // no road built
        Assert.IsFalse(_graph.HasFactoryConnection("farm"));
    }

    [Test]
    public void FactoryYardAlwaysHasFactoryConnection()
    {
        _graph.Register(_factoryDef, Vector3.zero);

        Assert.IsTrue(_graph.HasFactoryConnection("factory_yard"));
    }

    // -- production tick (2 tests) --

    [Test]
    public void TickProducesForClaimedBuildings()
    {
        // factory_yard has 0 repair stages, so it's pre-claimed.
        // But it has no production, so use the farm instead.
        // Farm has 1 repair stage, so we need to claim it.
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        var farm = _graph.Get("farm");
        farm.AdvanceRepair(); // claimed (1 repair stage)

        string producedItem = null;
        int producedAmount = 0;
        farm.OnProduced += (id, itemId, amount) => { producedItem = itemId; producedAmount = amount; };

        _graph.Tick(10f);

        Assert.AreEqual("raw_food", producedItem);
        Assert.AreEqual(1, producedAmount);
    }

    [Test]
    public void TickSkipsUnclaimedBuildings()
    {
        _graph.Register(_farmDef, new Vector3(50, 0, 0));

        var farm = _graph.Get("farm");
        // don't repair -- farm is unclaimed

        bool produced = false;
        farm.OnProduced += (id, itemId, amount) => { produced = true; };

        _graph.Tick(100f);

        Assert.IsFalse(produced);
    }

    // -- roads list (1 test) --

    [Test]
    public void RoadsListReturnsAllRoads()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");

        Assert.AreEqual(2, _graph.Roads.Count);
    }

    // -- territory (3 tests) --

    [Test]
    public void IsInTerritoryReturnsTrueInsideRadius()
    {
        // factory_yard at origin, territoryRadius=30, 0 repair stages = pre-claimed
        _graph.Register(_factoryDef, Vector3.zero);

        // point 15 units away, well within radius 30
        Assert.IsTrue(_graph.IsInTerritory(new Vector3(15, 0, 0)));
    }

    [Test]
    public void IsInTerritoryReturnsFalseOutsideRadius()
    {
        // factory_yard at origin, territoryRadius=30
        _graph.Register(_factoryDef, Vector3.zero);

        // point 50 units away, outside radius 30
        Assert.IsFalse(_graph.IsInTerritory(new Vector3(50, 0, 0)));
    }

    [Test]
    public void IsInTerritoryReturnsFalseForUnclaimedBuilding()
    {
        // farm at origin, territoryRadius=20, but has 1 repair stage so starts unclaimed
        _graph.Register(_farmDef, Vector3.zero);

        // point right on top of the farm, but unclaimed -> EffectiveTerritoryRadius is 0
        Assert.IsFalse(_graph.IsInTerritory(new Vector3(1, 0, 0)));
    }
}
