using NUnit.Framework;

[TestFixture]
public class SupplyLineManagerTests
{
    private const string WarehouseId = "warehouse_01";
    private const string FactoryId = "factory_01";
    private const string IronIngot = "iron_ingot";

    private BuildingState CreateClaimedBuilding(string id, float productionInterval = 1f)
    {
        var state = new BuildingState(
            id, id, 1,
            new[] { IronIngot }, new[] { 1 }, productionInterval);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");
        return state;
    }

    // -- Registration --

    [Test]
    public void RegisterLine_IncreasesLineCount()
    {
        var manager = new SupplyLineManager();
        var building = CreateClaimedBuilding(WarehouseId);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        manager.RegisterLine(line);

        Assert.AreEqual(1, manager.LineCount);

        line.Dispose();
    }

    [Test]
    public void UnregisterLine_DecreasesLineCount()
    {
        var manager = new SupplyLineManager();
        var building = CreateClaimedBuilding(WarehouseId);
        var dest = new StorageContainer(8, 64);
        var line = new SupplyLine(building, dest, 10f);

        manager.RegisterLine(line);
        manager.UnregisterLine(line);

        Assert.AreEqual(0, manager.LineCount);

        line.Dispose();
    }

    // -- Query --

    [Test]
    public void GetLinesForSource_ReturnsMatchingLines()
    {
        var manager = new SupplyLineManager();
        var building1 = CreateClaimedBuilding(WarehouseId);
        var building2 = CreateClaimedBuilding(FactoryId);
        var dest = new StorageContainer(8, 64);

        var line1 = new SupplyLine(building1, dest, 10f);
        var line2 = new SupplyLine(building2, dest, 10f);

        manager.RegisterLine(line1);
        manager.RegisterLine(line2);

        var result = manager.GetLinesForSource(WarehouseId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(line1, result[0]);

        line1.Dispose();
        line2.Dispose();
    }

    // -- TickAll --

    [Test]
    public void TickAll_TicksAllLines()
    {
        var manager = new SupplyLineManager();
        var building1 = CreateClaimedBuilding(WarehouseId);
        var building2 = CreateClaimedBuilding(FactoryId);
        var dest = new StorageContainer(8, 64);

        var line1 = new SupplyLine(building1, dest, 1f);
        var line2 = new SupplyLine(building2, dest, 1f);

        manager.RegisterLine(line1);
        manager.RegisterLine(line2);

        // produce items
        building1.Tick(1f);
        building2.Tick(1f);

        Assert.AreEqual(2, manager.TotalInFlight);

        // deliver
        manager.TickAll(1f);

        Assert.AreEqual(0, manager.TotalInFlight);
        Assert.AreEqual(2, manager.TotalDelivered);

        line1.Dispose();
        line2.Dispose();
    }

    // -- TotalInFlight --

    [Test]
    public void TotalInFlight_SumsAcrossAllLines()
    {
        var manager = new SupplyLineManager();
        var building1 = CreateClaimedBuilding(WarehouseId);
        var building2 = CreateClaimedBuilding(FactoryId);
        var dest = new StorageContainer(8, 64);

        var line1 = new SupplyLine(building1, dest, 10f);
        var line2 = new SupplyLine(building2, dest, 10f);

        manager.RegisterLine(line1);
        manager.RegisterLine(line2);

        building1.Tick(1f);
        building2.Tick(1f);
        building2.Tick(1f);

        Assert.AreEqual(3, manager.TotalInFlight);

        line1.Dispose();
        line2.Dispose();
    }

    // -- TotalDelivered --

    [Test]
    public void TotalDelivered_SumsAcrossAllLines()
    {
        var manager = new SupplyLineManager();
        var building1 = CreateClaimedBuilding(WarehouseId);
        var building2 = CreateClaimedBuilding(FactoryId);
        var dest = new StorageContainer(8, 64);

        var line1 = new SupplyLine(building1, dest, 1f);
        var line2 = new SupplyLine(building2, dest, 1f);

        manager.RegisterLine(line1);
        manager.RegisterLine(line2);

        building1.Tick(1f);
        building2.Tick(1f);
        manager.TickAll(1f);

        Assert.AreEqual(2, manager.TotalDelivered);

        line1.Dispose();
        line2.Dispose();
    }
}
