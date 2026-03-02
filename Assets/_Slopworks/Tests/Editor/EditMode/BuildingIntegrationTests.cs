using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// End-to-end integration tests for the building exploration pipeline.
/// Tests the full flow a player experiences: enter building, restore MEP points,
/// claim building, production ticks, items land in supply dock.
/// These catch integration seam bugs that unit tests on individual classes miss.
/// </summary>
[TestFixture]
public class BuildingIntegrationTests
{
    private const string IronIngot = "iron_ingot";

    // -- Full claim-to-production pipeline --

    [Test]
    public void FullPipeline_RestoreAllMEP_ClaimBuilding_ProduceItems()
    {
        // Set up the same pipeline as the bootstrapper
        var state = new BuildingState("warehouse", "Warehouse", 4,
            new[] { IronIngot }, new[] { 1 }, 10f);
        var manager = new BuildingManager();
        manager.Register(state);

        var supplyDock = new StorageContainer(8, 64);

        // Wire production to supply dock (same as bootstrapper)
        state.OnItemProduced += (itemId, amount) =>
        {
            for (int i = 0; i < amount; i++)
                supplyDock.TryInsert(itemId);
        };

        // Create and restore 4 MEP points
        var types = new[] { MEPSystemType.Electrical, MEPSystemType.Plumbing,
                            MEPSystemType.Mechanical, MEPSystemType.HVAC };
        for (int i = 0; i < 4; i++)
            state.AddRestorePoint(new MEPRestorePoint($"mep_{i}", types[i]));

        // Restore all points
        for (int i = 0; i < 4; i++)
            state.RestorePoint($"mep_{i}");

        Assert.IsTrue(state.IsClaimed, "Building should be claimed after restoring all 4 points");

        // Tick production -- 10s interval, tick 30s = 3 items
        manager.TickAll(30f);

        Assert.AreEqual(3, supplyDock.GetTotalItemCount(),
            "Supply dock should have 3 items after 30s at 10s interval");
        Assert.AreEqual(3, supplyDock.GetCount(IronIngot),
            "All items should be iron_ingot");
    }

    [Test]
    public void FullPipeline_PartialRestore_NoProduction()
    {
        var state = new BuildingState("warehouse", "Warehouse", 4,
            new[] { IronIngot }, new[] { 1 }, 10f);
        var manager = new BuildingManager();
        manager.Register(state);

        var supplyDock = new StorageContainer(8, 64);
        state.OnItemProduced += (itemId, amount) =>
        {
            for (int i = 0; i < amount; i++)
                supplyDock.TryInsert(itemId);
        };

        for (int i = 0; i < 4; i++)
            state.AddRestorePoint(new MEPRestorePoint($"mep_{i}", MEPSystemType.Electrical));

        // Only restore 3 of 4
        state.RestorePoint("mep_0");
        state.RestorePoint("mep_1");
        state.RestorePoint("mep_2");

        Assert.IsFalse(state.IsClaimed);

        manager.TickAll(100f);

        Assert.AreEqual(0, supplyDock.GetTotalItemCount(),
            "Unclaimed building should produce nothing");
    }

    [Test]
    public void FullPipeline_SupplyDockFull_DoesNotCrash()
    {
        var state = new BuildingState("warehouse", "Warehouse", 1,
            new[] { IronIngot }, new[] { 1 }, 1f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        // Tiny supply dock that fills immediately
        var supplyDock = new StorageContainer(1, 2);
        int lostCount = 0;
        state.OnItemProduced += (itemId, amount) =>
        {
            for (int i = 0; i < amount; i++)
            {
                if (!supplyDock.TryInsert(itemId))
                    lostCount++;
            }
        };

        var manager = new BuildingManager();
        manager.Register(state);

        // Tick enough to overflow the dock (2 slots, 1 stack size = 2 max, but stack 2)
        manager.TickAll(10f);

        Assert.IsTrue(supplyDock.IsFull, "Supply dock should be full");
        Assert.Greater(lostCount, 0, "Some items should have been lost to overflow");
    }

    // -- Behaviour-driven pipeline (full MonoBehaviour integration) --

    [Test]
    public void BehaviourPipeline_InteractAllMEP_ClaimsAndProduces()
    {
        var state = new BuildingState("warehouse", "Warehouse", 3,
            new[] { IronIngot }, new[] { 1 }, 5f);
        var manager = new BuildingManager();
        manager.Register(state);

        var supplyDock = new StorageContainer(8, 64);
        state.OnItemProduced += (itemId, amount) =>
        {
            for (int i = 0; i < amount; i++)
                supplyDock.TryInsert(itemId);
        };

        // Create 3 MEP behaviours on GameObjects (simulating what bootstrapper does)
        var types = new[] { MEPSystemType.Electrical, MEPSystemType.Plumbing, MEPSystemType.Mechanical };
        var mepObjects = new List<GameObject>();

        for (int i = 0; i < 3; i++)
        {
            var point = new MEPRestorePoint($"mep_{i}", types[i]);
            state.AddRestorePoint(point);

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.layer = PhysicsLayers.Interactable;
            var beh = obj.AddComponent<MEPRestorePointBehaviour>();
            beh.Initialize(point, state);
            mepObjects.Add(obj);
        }

        var fakePlayer = new GameObject("Player");

        // Player interacts with each MEP point
        for (int i = 0; i < 3; i++)
        {
            var beh = mepObjects[i].GetComponent<MEPRestorePointBehaviour>();
            beh.Interact(fakePlayer);
        }

        Assert.IsTrue(state.IsClaimed, "Building should be claimed after all interactions");

        // Tick production
        manager.TickAll(10f);

        Assert.AreEqual(2, supplyDock.GetTotalItemCount(),
            "Supply dock should have 2 items after 10s at 5s interval");

        // Cleanup
        Object.DestroyImmediate(fakePlayer);
        foreach (var obj in mepObjects)
            Object.DestroyImmediate(obj);
    }

    // -- BuildingManager with multiple buildings --

    [Test]
    public void MultipleBuildingsManager_IndependentClaim()
    {
        var manager = new BuildingManager();

        var b1 = new BuildingState("b1", "Building 1", 1,
            new[] { IronIngot }, new[] { 1 }, 10f);
        b1.AddRestorePoint(new MEPRestorePoint("b1_mep", MEPSystemType.Electrical));
        manager.Register(b1);

        var b2 = new BuildingState("b2", "Building 2", 1,
            new[] { "copper_ingot" }, new[] { 2 }, 10f);
        b2.AddRestorePoint(new MEPRestorePoint("b2_mep", MEPSystemType.Plumbing));
        manager.Register(b2);

        Assert.AreEqual(0, manager.ClaimedCount);

        b1.RestorePoint("b1_mep");
        Assert.AreEqual(1, manager.ClaimedCount);
        Assert.IsTrue(b1.IsClaimed);
        Assert.IsFalse(b2.IsClaimed);

        b2.RestorePoint("b2_mep");
        Assert.AreEqual(2, manager.ClaimedCount);
    }

    [Test]
    public void MultipleBuildingsManager_TickAll_ProducesDifferentItems()
    {
        var manager = new BuildingManager();

        var b1 = new BuildingState("b1", "B1", 1,
            new[] { "iron_ingot" }, new[] { 1 }, 10f);
        b1.AddRestorePoint(new MEPRestorePoint("b1_m", MEPSystemType.Electrical));
        b1.RestorePoint("b1_m");
        manager.Register(b1);

        var b2 = new BuildingState("b2", "B2", 1,
            new[] { "copper_ingot" }, new[] { 2 }, 10f);
        b2.AddRestorePoint(new MEPRestorePoint("b2_m", MEPSystemType.Plumbing));
        b2.RestorePoint("b2_m");
        manager.Register(b2);

        var allProduced = new List<(string id, int amount)>();
        b1.OnItemProduced += (id, amt) => allProduced.Add((id, amt));
        b2.OnItemProduced += (id, amt) => allProduced.Add((id, amt));

        manager.TickAll(10f);

        Assert.AreEqual(2, allProduced.Count, "Both buildings should produce");
        Assert.AreEqual("iron_ingot", allProduced[0].id);
        Assert.AreEqual(1, allProduced[0].amount);
        Assert.AreEqual("copper_ingot", allProduced[1].id);
        Assert.AreEqual(2, allProduced[1].amount);
    }

    // -- BuildingDefinitionSO constructor --

    [Test]
    public void BuildingState_FromSO_ExtractsFieldsCorrectly()
    {
        var def = ScriptableObject.CreateInstance<BuildingDefinitionSO>();
        def.buildingId = "test_so";
        def.displayName = "SO Building";
        def.requiredMEPCount = 3;
        def.producedItemIds = new[] { "scrap" };
        def.producedAmounts = new[] { 5 };
        def.productionInterval = 20f;

        var state = new BuildingState(def);

        Assert.AreEqual("test_so", state.BuildingId);
        Assert.AreEqual("SO Building", state.DisplayName);
        Assert.AreEqual(3, state.RequiredMEPCount);

        // Verify production works with SO-derived values
        state.AddRestorePoint(new MEPRestorePoint("m0", MEPSystemType.Electrical));
        state.AddRestorePoint(new MEPRestorePoint("m1", MEPSystemType.Plumbing));
        state.AddRestorePoint(new MEPRestorePoint("m2", MEPSystemType.Mechanical));
        state.RestorePoint("m0");
        state.RestorePoint("m1");
        state.RestorePoint("m2");
        Assert.IsTrue(state.IsClaimed);

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));
        state.Tick(20f);
        Assert.AreEqual(1, produced.Count);
        Assert.AreEqual("scrap", produced[0].id);
        Assert.AreEqual(5, produced[0].amount);

        Object.DestroyImmediate(def);
    }

    // -- Production timer precision --

    [Test]
    public void ProductionTimer_SmallDeltaTicks_AccumulateCorrectly()
    {
        var state = new BuildingState("b", "B", 1,
            new[] { IronIngot }, new[] { 1 }, 1f);
        state.AddRestorePoint(new MEPRestorePoint("m", MEPSystemType.Electrical));
        state.RestorePoint("m");

        int produced = 0;
        state.OnItemProduced += (_, __) => produced++;

        // Simulate 110 FixedUpdate ticks at 0.02s each = 2.2 seconds total
        // Using 110 instead of 100 to avoid float accumulation drift at boundary
        for (int i = 0; i < 110; i++)
            state.Tick(0.02f);

        Assert.AreEqual(2, produced, "110 ticks at 0.02s = 2.2s, should produce 2 items at 1s interval");
    }

    [Test]
    public void ProductionTimer_LargeDeltaTick_ProducesMultiple()
    {
        var state = new BuildingState("b", "B", 1,
            new[] { IronIngot }, new[] { 1 }, 30f);
        state.AddRestorePoint(new MEPRestorePoint("m", MEPSystemType.Electrical));
        state.RestorePoint("m");

        int produced = 0;
        state.OnItemProduced += (_, __) => produced++;

        // Simulate 5 minutes in one tick
        state.Tick(300f);

        Assert.AreEqual(10, produced, "300s / 30s interval = 10 items");
    }

    // -- Multi-output production --

    [Test]
    public void BuildingState_MultipleOutputItems_ProducesAll()
    {
        var state = new BuildingState("factory", "Factory", 1,
            new[] { "iron_ingot", "copper_wire" }, new[] { 2, 3 }, 10f);
        state.AddRestorePoint(new MEPRestorePoint("m", MEPSystemType.Electrical));
        state.RestorePoint("m");

        var produced = new List<(string id, int amount)>();
        state.OnItemProduced += (id, amt) => produced.Add((id, amt));

        state.Tick(10f);

        Assert.AreEqual(2, produced.Count, "Should produce both item types");
        Assert.AreEqual("iron_ingot", produced[0].id);
        Assert.AreEqual(2, produced[0].amount);
        Assert.AreEqual("copper_wire", produced[1].id);
        Assert.AreEqual(3, produced[1].amount);
    }

    // -- Event ordering --

    [Test]
    public void EventOrdering_PointRestoredFiresBeforeBuildingClaimed()
    {
        var state = new BuildingState("b", "B", 1,
            new[] { IronIngot }, new[] { 1 }, 10f);
        state.AddRestorePoint(new MEPRestorePoint("m", MEPSystemType.Electrical));

        var eventOrder = new List<string>();
        state.OnPointRestored += () => eventOrder.Add("restored");
        state.OnBuildingClaimed += () => eventOrder.Add("claimed");

        state.RestorePoint("m");

        Assert.AreEqual(2, eventOrder.Count);
        Assert.AreEqual("restored", eventOrder[0], "OnPointRestored should fire first");
        Assert.AreEqual("claimed", eventOrder[1], "OnBuildingClaimed should fire second");
    }

    [Test]
    public void ClaimEvent_FiresOnlyOnce()
    {
        var state = new BuildingState("b", "B", 2,
            new[] { IronIngot }, new[] { 1 }, 10f);
        state.AddRestorePoint(new MEPRestorePoint("m0", MEPSystemType.Electrical));
        state.AddRestorePoint(new MEPRestorePoint("m1", MEPSystemType.Plumbing));

        int claimCount = 0;
        state.OnBuildingClaimed += () => claimCount++;

        state.RestorePoint("m0");
        state.RestorePoint("m1");

        // Try restoring again (should be no-ops)
        state.RestorePoint("m0");
        state.RestorePoint("m1");

        Assert.AreEqual(1, claimCount, "Claim event should fire exactly once");
    }

    // -- StorageBehaviour integration --

    [Test]
    public void StorageBehaviour_Initialize_SetsContainerCorrectly()
    {
        var container = new StorageContainer(4, 64);
        container.TryInsert("iron_ingot");

        var def = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        def.storageId = "supply_dock";
        def.displayName = "Supply Dock";
        def.slotCount = 4;
        def.maxStackSize = 64;

        var obj = new GameObject("Dock");
        obj.SetActive(false);
        var beh = obj.AddComponent<StorageBehaviour>();
        beh.Initialize(def, container);
        obj.SetActive(true);

        Assert.AreSame(container, beh.Container, "Should use the injected container");
        Assert.AreEqual(1, beh.Container.GetTotalItemCount(), "Container state preserved");
        StringAssert.Contains("Supply Dock", beh.GetInteractionPrompt());

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(def);
    }

    // -- BuildingLayout struct completeness --

    [Test]
    public void BuildingLayout_GeneratedAtOffset_AllReferencesValid()
    {
        var layout = BuildingLayoutGenerator.GenerateWarehouse(new Vector3(200f, 0f, 200f));

        Assert.IsNotNull(layout.Root);
        Assert.IsNotNull(layout.EntranceSpawn);
        Assert.IsNotNull(layout.ExitSpawn);
        Assert.IsNotNull(layout.EnemySpawnPoints);
        Assert.IsNotNull(layout.MEPPositions);
        Assert.AreEqual(4, layout.EnemySpawnPoints.Length);
        Assert.AreEqual(4, layout.MEPPositions.Length);

        // All are children of root (no orphans)
        Assert.AreSame(layout.Root.transform, layout.EntranceSpawn.parent);
        Assert.AreSame(layout.Root.transform, layout.ExitSpawn.parent);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreSame(layout.Root.transform, layout.EnemySpawnPoints[i].parent,
                $"EnemySpawn[{i}] orphaned");
            Assert.AreSame(layout.Root.transform, layout.MEPPositions[i].parent,
                $"MEP[{i}] orphaned");
        }

        Object.DestroyImmediate(layout.Root);
    }
}
