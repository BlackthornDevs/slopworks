using NUnit.Framework;

[TestFixture]
public class SupplyChainIntegrationTests
{
    private const string WarehouseId = "warehouse_01";
    private const string FactoryId = "factory_01";
    private const string IronIngot = "iron_ingot";

    private BuildingState CreateBuilding(string id, int requiredMEP = 1, float productionInterval = 10f)
    {
        return new BuildingState(
            id, id, requiredMEP,
            new[] { IronIngot }, new[] { 1 }, productionInterval);
    }

    private void ClaimBuilding(BuildingState building)
    {
        for (int i = 0; i < building.RequiredMEPCount; i++)
        {
            string pointId = $"mep_{i}";
            building.AddRestorePoint(new MEPRestorePoint(pointId, MEPSystemType.Electrical));
            building.RestorePoint(pointId);
        }
    }

    // -- Full pipeline --

    [Test]
    public void FullPipeline_ClaimedBuilding_DeliversToStorage()
    {
        var building = CreateBuilding(WarehouseId, requiredMEP: 1, productionInterval: 10f);
        ClaimBuilding(building);

        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 5f);
        var manager = new SupplyLineManager();
        manager.RegisterLine(line);

        // produce (tick building past production interval)
        building.Tick(10f);

        Assert.AreEqual(1, manager.TotalInFlight);
        Assert.AreEqual(0, dest.GetCount(IronIngot));

        // transport (tick supply lines past transport delay)
        manager.TickAll(5f);

        Assert.AreEqual(0, manager.TotalInFlight);
        Assert.AreEqual(1, dest.GetCount(IronIngot));
        Assert.AreEqual(1, manager.TotalDelivered);

        line.Dispose();
    }

    // -- Unclaimed building --

    [Test]
    public void UnclaimedBuilding_ProducesNothing()
    {
        var building = CreateBuilding(WarehouseId, requiredMEP: 4, productionInterval: 10f);
        // do not claim
        building.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));

        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 5f);
        var manager = new SupplyLineManager();
        manager.RegisterLine(line);

        // tick past production interval
        building.Tick(10f);
        manager.TickAll(5f);

        Assert.AreEqual(0, dest.GetCount(IronIngot));
        Assert.AreEqual(0, manager.TotalDelivered);

        line.Dispose();
    }

    // -- Multiple production cycles --

    [Test]
    public void MultipleProductionCycles_AccumulateCorrectly()
    {
        var building = CreateBuilding(WarehouseId, requiredMEP: 1, productionInterval: 10f);
        ClaimBuilding(building);

        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 2f);
        var manager = new SupplyLineManager();
        manager.RegisterLine(line);

        // 3 production cycles
        building.Tick(10f);
        manager.TickAll(2f); // deliver first
        building.Tick(10f);
        manager.TickAll(2f); // deliver second
        building.Tick(10f);
        manager.TickAll(2f); // deliver third

        Assert.AreEqual(3, dest.GetCount(IronIngot));
        Assert.AreEqual(3, manager.TotalDelivered);

        line.Dispose();
    }

    // -- Dock full --

    [Test]
    public void DockFull_ItemsLostViaEvent()
    {
        var building = CreateBuilding(WarehouseId, requiredMEP: 1, productionInterval: 1f);
        ClaimBuilding(building);

        var dest = new StorageContainer(1, 1); // tiny: 1 slot, 1 stack
        dest.TryInsert(IronIngot); // fill it

        var line = new SupplyLine(building, dest, 1f);
        var manager = new SupplyLineManager();
        manager.RegisterLine(line);

        int lostCount = 0;
        line.OnItemLost += (id, amt) => lostCount += amt;

        building.Tick(1f);
        manager.TickAll(1f);

        Assert.AreEqual(1, lostCount);
        Assert.AreEqual(0, manager.TotalDelivered);

        line.Dispose();
    }

    // -- Dispose unsubscribes --

    [Test]
    public void Dispose_UnsubscribesFromBuildingEvents()
    {
        var building = CreateBuilding(WarehouseId, requiredMEP: 1, productionInterval: 1f);
        ClaimBuilding(building);

        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        line.Dispose();

        // produce after dispose
        building.Tick(1f);

        Assert.AreEqual(0, line.InFlightCount);
        Assert.AreEqual(0, dest.GetCount(IronIngot));
    }

    // -- Multi-building independence --

    [Test]
    public void MultiBuilding_IndependentDelivery()
    {
        var building1 = CreateBuilding(WarehouseId, requiredMEP: 1, productionInterval: 5f);
        ClaimBuilding(building1);
        var building2 = CreateBuilding(FactoryId, requiredMEP: 1, productionInterval: 10f);
        ClaimBuilding(building2);

        var dest = new StorageContainer(8, 64);
        var line1 = new SupplyLine(building1, dest, 2f);
        var line2 = new SupplyLine(building2, dest, 3f);
        var manager = new SupplyLineManager();
        manager.RegisterLine(line1);
        manager.RegisterLine(line2);

        // tick both buildings at different rates
        building1.Tick(5f);  // building1 produces
        building2.Tick(10f); // building2 produces

        Assert.AreEqual(2, manager.TotalInFlight);

        // tick supply lines -- line1 (2s delay) delivers, line2 (3s delay) still in flight
        manager.TickAll(2f);

        Assert.AreEqual(1, dest.GetCount(IronIngot)); // only line1 delivered
        Assert.AreEqual(1, manager.TotalInFlight);    // line2 still pending

        // tick remaining delay
        manager.TickAll(1f);

        Assert.AreEqual(2, dest.GetCount(IronIngot)); // both delivered
        Assert.AreEqual(0, manager.TotalInFlight);

        line1.Dispose();
        line2.Dispose();
    }
}
