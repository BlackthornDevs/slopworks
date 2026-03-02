using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class SupplyLineTests
{
    private const string WarehouseId = "warehouse_01";
    private const string WarehouseName = "Abandoned Warehouse";
    private const string IronIngot = "iron_ingot";

    private BuildingState CreateClaimedBuilding(float productionInterval = 30f)
    {
        var state = new BuildingState(
            WarehouseId, WarehouseName, 1,
            new[] { IronIngot }, new[] { 1 }, productionInterval);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0"); // claim it
        return state;
    }

    private BuildingState CreateUnclaimedBuilding(float productionInterval = 30f)
    {
        var state = new BuildingState(
            WarehouseId, WarehouseName, 2,
            new[] { IronIngot }, new[] { 1 }, productionInterval);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.AddRestorePoint(new MEPRestorePoint("mep_1", MEPSystemType.Plumbing));
        return state;
    }

    // -- Construction --

    [Test]
    public void Constructor_SetsProperties()
    {
        var building = CreateClaimedBuilding();
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        Assert.AreEqual(building, line.Source);
        Assert.AreEqual(dest, line.Destination);
        Assert.AreEqual(10f, line.TransportDelay);
        Assert.AreEqual(0, line.InFlightCount);
        Assert.AreEqual(0, line.TotalDelivered);

        line.Dispose();
    }

    [Test]
    public void Constructor_NullSource_Throws()
    {
        var dest = new StorageContainer(8, 64);
        Assert.Throws<System.ArgumentNullException>(() => new SupplyLine(null, dest, 10f));
    }

    [Test]
    public void Constructor_NullDestination_Throws()
    {
        var building = CreateClaimedBuilding();
        Assert.Throws<System.ArgumentNullException>(() => new SupplyLine(building, null, 10f));
    }

    // -- Item production enters in-flight --

    [Test]
    public void Production_AddsInFlightItem()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        building.Tick(1f); // triggers OnItemProduced

        Assert.AreEqual(1, line.InFlightCount);

        line.Dispose();
    }

    // -- Transport delay --

    [Test]
    public void Tick_BeforeDelay_ItemStaysInFlight()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        building.Tick(1f);
        line.Tick(5f); // half the delay

        Assert.AreEqual(1, line.InFlightCount);
        Assert.AreEqual(0, line.TotalDelivered);

        line.Dispose();
    }

    [Test]
    public void Tick_AfterDelay_DeliversItem()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        building.Tick(1f);
        line.Tick(10f);

        Assert.AreEqual(0, line.InFlightCount);
        Assert.AreEqual(1, line.TotalDelivered);
        Assert.AreEqual(1, dest.GetCount(IronIngot));

        line.Dispose();
    }

    [Test]
    public void Tick_ExactDelay_DeliversItem()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 5f);

        building.Tick(1f);
        line.Tick(5f);

        Assert.AreEqual(0, line.InFlightCount);
        Assert.AreEqual(1, line.TotalDelivered);

        line.Dispose();
    }

    // -- Delivery events --

    [Test]
    public void OnItemDelivered_FiresOnDelivery()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        string deliveredId = null;
        int deliveredAmount = 0;
        line.OnItemDelivered += (id, amt) => { deliveredId = id; deliveredAmount = amt; };

        building.Tick(1f);
        line.Tick(1f);

        Assert.AreEqual(IronIngot, deliveredId);
        Assert.AreEqual(1, deliveredAmount);

        line.Dispose();
    }

    [Test]
    public void OnItemLost_FiresWhenDestinationFull()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(1, 1); // tiny: 1 slot, stack size 1
        // fill the destination
        dest.TryInsert(IronIngot);

        var line = new SupplyLine(building, dest, 1f);

        string lostId = null;
        int lostAmount = 0;
        line.OnItemLost += (id, amt) => { lostId = id; lostAmount = amt; };

        building.Tick(1f);
        line.Tick(1f);

        Assert.AreEqual(IronIngot, lostId);
        Assert.AreEqual(1, lostAmount);
        Assert.AreEqual(0, line.TotalDelivered);

        line.Dispose();
    }

    // -- Multiple production cycles --

    [Test]
    public void MultipleProductions_AccumulateInFlight()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        building.Tick(1f);
        building.Tick(1f);
        building.Tick(1f);

        Assert.AreEqual(3, line.InFlightCount);

        line.Dispose();
    }

    [Test]
    public void MultipleDeliveries_AccumulateTotalDelivered()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        building.Tick(1f);
        line.Tick(1f);
        building.Tick(1f);
        line.Tick(1f);

        Assert.AreEqual(2, line.TotalDelivered);
        Assert.AreEqual(2, dest.GetCount(IronIngot));

        line.Dispose();
    }

    // -- Dispose --

    [Test]
    public void Dispose_UnsubscribesFromBuildingEvents()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        line.Dispose();

        // produce after dispose -- should not add in-flight items
        building.Tick(1f);

        Assert.AreEqual(0, line.InFlightCount);
    }

    [Test]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        var building = CreateClaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        line.Dispose();
        Assert.DoesNotThrow(() => line.Dispose());
    }

    // -- Unclaimed building --

    [Test]
    public void UnclaimedBuilding_ProducesNothing()
    {
        var building = CreateUnclaimedBuilding(productionInterval: 1f);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 1f);

        building.Tick(1f);

        Assert.AreEqual(0, line.InFlightCount);

        line.Dispose();
    }
}
