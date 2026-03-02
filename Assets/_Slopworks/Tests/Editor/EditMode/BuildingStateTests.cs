using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class BuildingStateTests
{
    private const string WarehouseId = "warehouse_01";
    private const string WarehouseName = "Abandoned Warehouse";
    private const string IronIngot = "iron_ingot";

    private BuildingState CreateBuilding(int requiredMEP = 4, float productionInterval = 30f)
    {
        return new BuildingState(
            WarehouseId, WarehouseName, requiredMEP,
            new[] { IronIngot }, new[] { 1 }, productionInterval);
    }

    private void AddDefaultRestorePoints(BuildingState state, int count = 4)
    {
        var types = new[] { MEPSystemType.Electrical, MEPSystemType.Plumbing,
                            MEPSystemType.Mechanical, MEPSystemType.HVAC };
        for (int i = 0; i < count; i++)
            state.AddRestorePoint(new MEPRestorePoint($"mep_{i}", types[i % types.Length]));
    }

    // -- Restore point basics --

    [Test]
    public void RestorePoint_NewlyCreated_IsNotRestored()
    {
        var point = new MEPRestorePoint("test", MEPSystemType.Electrical);
        Assert.IsFalse(point.IsRestored);
    }

    [Test]
    public void RestorePoint_Restore_SetsRestoredTrue()
    {
        var point = new MEPRestorePoint("test", MEPSystemType.Electrical);
        bool result = point.Restore();
        Assert.IsTrue(result);
        Assert.IsTrue(point.IsRestored);
    }

    [Test]
    public void RestorePoint_DoubleRestore_ReturnsFalse()
    {
        var point = new MEPRestorePoint("test", MEPSystemType.Electrical);
        point.Restore();
        bool result = point.Restore();
        Assert.IsFalse(result);
    }

    // -- Building state restore --

    [Test]
    public void BuildingState_NewBuilding_IsNotClaimed()
    {
        var state = CreateBuilding();
        AddDefaultRestorePoints(state);
        Assert.IsFalse(state.IsClaimed);
        Assert.AreEqual(0, state.RestoredCount);
    }

    [Test]
    public void BuildingState_RestorePoint_IncrementsCount()
    {
        var state = CreateBuilding();
        AddDefaultRestorePoints(state);

        state.RestorePoint("mep_0");

        Assert.AreEqual(1, state.RestoredCount);
        Assert.IsFalse(state.IsClaimed);
    }

    [Test]
    public void BuildingState_RestorePoint_FiresEvent()
    {
        var state = CreateBuilding();
        AddDefaultRestorePoints(state);
        int eventCount = 0;
        state.OnPointRestored += () => eventCount++;

        state.RestorePoint("mep_0");

        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void BuildingState_DoubleRestore_DoesNotIncrementCount()
    {
        var state = CreateBuilding();
        AddDefaultRestorePoints(state);

        state.RestorePoint("mep_0");
        bool second = state.RestorePoint("mep_0");

        Assert.IsFalse(second);
        Assert.AreEqual(1, state.RestoredCount);
    }

    [Test]
    public void BuildingState_InvalidPointId_ReturnsFalse()
    {
        var state = CreateBuilding();
        AddDefaultRestorePoints(state);

        bool result = state.RestorePoint("nonexistent");

        Assert.IsFalse(result);
        Assert.AreEqual(0, state.RestoredCount);
    }

    // -- Claim threshold --

    [Test]
    public void BuildingState_RestoreAllPoints_ClaimsBuilding()
    {
        var state = CreateBuilding(requiredMEP: 4);
        AddDefaultRestorePoints(state, 4);

        for (int i = 0; i < 4; i++)
            state.RestorePoint($"mep_{i}");

        Assert.IsTrue(state.IsClaimed);
    }

    [Test]
    public void BuildingState_ClaimFiresEvent()
    {
        var state = CreateBuilding(requiredMEP: 2);
        AddDefaultRestorePoints(state, 2);
        bool claimed = false;
        state.OnBuildingClaimed += () => claimed = true;

        state.RestorePoint("mep_0");
        Assert.IsFalse(claimed);

        state.RestorePoint("mep_1");
        Assert.IsTrue(claimed);
    }

    [Test]
    public void BuildingState_PartialRestore_DoesNotClaim()
    {
        var state = CreateBuilding(requiredMEP: 4);
        AddDefaultRestorePoints(state, 4);

        state.RestorePoint("mep_0");
        state.RestorePoint("mep_1");
        state.RestorePoint("mep_2");

        Assert.IsFalse(state.IsClaimed);
        Assert.AreEqual(3, state.RestoredCount);
    }

    // -- Production ticking --

    [Test]
    public void BuildingState_Tick_UnclaimedProducesNothing()
    {
        var state = CreateBuilding(requiredMEP: 4, productionInterval: 10f);
        AddDefaultRestorePoints(state, 4);
        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(100f);

        Assert.AreEqual(0, produced.Count);
    }

    [Test]
    public void BuildingState_Tick_ClaimedProducesAfterInterval()
    {
        var state = CreateBuilding(requiredMEP: 1, productionInterval: 10f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(10f);

        Assert.AreEqual(1, produced.Count);
        Assert.AreEqual(IronIngot, produced[0].id);
        Assert.AreEqual(1, produced[0].amount);
    }

    [Test]
    public void BuildingState_Tick_AccumulatesOverMultipleTicks()
    {
        var state = CreateBuilding(requiredMEP: 1, productionInterval: 10f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(5f);
        Assert.AreEqual(0, produced.Count);

        state.Tick(5f);
        Assert.AreEqual(1, produced.Count);
    }

    [Test]
    public void BuildingState_Tick_MultipleProductionsInOneTick()
    {
        var state = CreateBuilding(requiredMEP: 1, productionInterval: 10f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(25f);

        Assert.AreEqual(2, produced.Count);
    }

    [Test]
    public void BuildingState_Tick_ZeroInterval_ProducesNothing()
    {
        var state = CreateBuilding(requiredMEP: 1, productionInterval: 0f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(10f);

        Assert.AreEqual(0, produced.Count);
    }

    // -- BuildingManager --

    [Test]
    public void BuildingManager_Register_IncreasesCount()
    {
        var manager = new BuildingManager();
        var state = CreateBuilding();

        manager.Register(state);

        Assert.AreEqual(1, manager.BuildingCount);
    }

    [Test]
    public void BuildingManager_Get_ReturnsRegisteredBuilding()
    {
        var manager = new BuildingManager();
        var state = CreateBuilding();
        manager.Register(state);

        var found = manager.Get(WarehouseId);

        Assert.AreSame(state, found);
    }

    [Test]
    public void BuildingManager_Get_UnknownId_ReturnsNull()
    {
        var manager = new BuildingManager();
        Assert.IsNull(manager.Get("nonexistent"));
    }

    [Test]
    public void BuildingManager_ClaimedCount_ReflectsState()
    {
        var manager = new BuildingManager();
        var state = CreateBuilding(requiredMEP: 1);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        manager.Register(state);

        Assert.AreEqual(0, manager.ClaimedCount);

        state.RestorePoint("mep_0");

        Assert.AreEqual(1, manager.ClaimedCount);
    }

    [Test]
    public void BuildingManager_TickAll_TicksAllBuildings()
    {
        var manager = new BuildingManager();
        var state = CreateBuilding(requiredMEP: 1, productionInterval: 10f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");
        manager.Register(state);

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        manager.TickAll(10f);

        Assert.AreEqual(1, produced.Count);
    }
}
