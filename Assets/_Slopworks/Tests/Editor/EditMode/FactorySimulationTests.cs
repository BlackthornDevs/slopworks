using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FactorySimulationTests
{
    private const string SmelterType = "smelter";
    private const string SmeltIronRecipeId = "smelt_iron";
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const float DefaultCraftDuration = 2f;

    private MachineDefinitionSO _smelterDef;
    private RecipeSO _smeltIronRecipe;

    [SetUp]
    public void SetUp()
    {
        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter_basic";
        _smelterDef.machineType = SmelterType;
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 1;
        _smelterDef.processingSpeed = 1f;
        _smelterDef.ports = new MachinePort[0];

        _smeltIronRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltIronRecipe.recipeId = SmeltIronRecipeId;
        _smeltIronRecipe.inputs = new[] { new RecipeIngredient { itemId = IronOre, count = 1 } };
        _smeltIronRecipe.outputs = new[] { new RecipeIngredient { itemId = IronIngot, count = 1 } };
        _smeltIronRecipe.craftDuration = DefaultCraftDuration;
        _smeltIronRecipe.requiredMachineType = SmelterType;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_smelterDef);
        UnityEngine.Object.DestroyImmediate(_smeltIronRecipe);
    }

    private RecipeSO LookupRecipe(string recipeId)
    {
        if (recipeId == SmeltIronRecipeId)
            return _smeltIronRecipe;
        return null;
    }

    private FactorySimulation CreateSimulation()
    {
        return new FactorySimulation(LookupRecipe);
    }

    private Machine CreateMachine()
    {
        return new Machine(_smelterDef);
    }

    // -- Registration --

    [Test]
    public void RegisterMachine_IncreasesMachineCount()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();

        sim.RegisterMachine(machine);

        Assert.AreEqual(1, sim.MachineCount);
    }

    [Test]
    public void UnregisterMachine_DecreasesMachineCount()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();
        sim.RegisterMachine(machine);

        sim.UnregisterMachine(machine);

        Assert.AreEqual(0, sim.MachineCount);
    }

    [Test]
    public void RegisterMachine_Duplicate_DoesNotDoubleCount()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();

        sim.RegisterMachine(machine);
        sim.RegisterMachine(machine);

        Assert.AreEqual(1, sim.MachineCount);
    }

    [Test]
    public void UnregisterMachine_NotRegistered_IsNoOp()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();

        sim.UnregisterMachine(machine);

        Assert.AreEqual(0, sim.MachineCount);
    }

    [Test]
    public void RegisterMachine_Null_Throws()
    {
        var sim = CreateSimulation();

        Assert.Throws<ArgumentNullException>(() => sim.RegisterMachine(null));
    }

    // -- Tick drives machines --

    [Test]
    public void Tick_DrivesRegisteredMachine_TransitionsToWorking()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        sim.RegisterMachine(machine);

        sim.Tick(0.5f);

        Assert.AreEqual(MachineStatus.Working, machine.Status);
    }

    [Test]
    public void Tick_WithMultipleMachines_ProcessesAll()
    {
        var sim = CreateSimulation();

        var machine1 = CreateMachine();
        machine1.SetRecipe(SmeltIronRecipeId);
        machine1.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        var machine2 = CreateMachine();
        machine2.SetRecipe(SmeltIronRecipeId);
        machine2.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        sim.RegisterMachine(machine1);
        sim.RegisterMachine(machine2);

        sim.Tick(0.5f);

        Assert.AreEqual(MachineStatus.Working, machine1.Status);
        Assert.AreEqual(MachineStatus.Working, machine2.Status);
    }

    [Test]
    public void Tick_UnregisteredMachine_IsNotTicked()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        sim.RegisterMachine(machine);
        sim.UnregisterMachine(machine);

        sim.Tick(0.5f);

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
    }

    [Test]
    public void MultipleTicks_CompleteCraftCycle()
    {
        var sim = CreateSimulation();
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        sim.RegisterMachine(machine);

        // First tick starts crafting, progress = 0.5
        sim.Tick(0.5f);
        Assert.AreEqual(MachineStatus.Working, machine.Status);

        // Second tick completes the craft (progress = 0.5 + 2.0 = 2.5 >= 2.0)
        sim.Tick(2f);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);

        var output = machine.GetOutput(0);
        Assert.AreEqual(IronIngot, output.item.definitionId);
        Assert.AreEqual(1, output.count);
    }

    [Test]
    public void MultipleTicks_CompleteCraftCycle_AcrossMultipleMachines()
    {
        var sim = CreateSimulation();

        var machine1 = CreateMachine();
        machine1.SetRecipe(SmeltIronRecipeId);
        machine1.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        var machine2 = CreateMachine();
        machine2.SetRecipe(SmeltIronRecipeId);
        machine2.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        sim.RegisterMachine(machine1);
        sim.RegisterMachine(machine2);

        sim.Tick(0.5f);
        sim.Tick(2f);

        Assert.AreEqual(MachineStatus.Idle, machine1.Status);
        Assert.AreEqual(IronIngot, machine1.GetOutput(0).item.definitionId);

        Assert.AreEqual(MachineStatus.Idle, machine2.Status);
        Assert.AreEqual(IronIngot, machine2.GetOutput(0).item.definitionId);
    }

    // -- GetMachines --

    [Test]
    public void GetMachines_ReturnsAllRegisteredMachines()
    {
        var sim = CreateSimulation();
        var machine1 = CreateMachine();
        var machine2 = CreateMachine();
        sim.RegisterMachine(machine1);
        sim.RegisterMachine(machine2);

        var machines = sim.GetMachines();

        Assert.AreEqual(2, machines.Count);
        Assert.AreSame(machine1, machines[0]);
        Assert.AreSame(machine2, machines[1]);
    }

    [Test]
    public void GetMachines_EmptySimulation_ReturnsEmptyList()
    {
        var sim = CreateSimulation();

        var machines = sim.GetMachines();

        Assert.AreEqual(0, machines.Count);
    }

    // -- Constructor validation --

    [Test]
    public void Constructor_NullRecipeLookup_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FactorySimulation(null));
    }

    // ========================================================================
    // Belt registration
    // ========================================================================

    [Test]
    public void RegisterBelt_IncreasesBeltCount()
    {
        var sim = CreateSimulation();
        var belt = new BeltSegment(1);

        sim.RegisterBelt(belt);

        Assert.AreEqual(1, sim.BeltCount);
    }

    [Test]
    public void UnregisterBelt_DecreasesBeltCount()
    {
        var sim = CreateSimulation();
        var belt = new BeltSegment(1);
        sim.RegisterBelt(belt);

        sim.UnregisterBelt(belt);

        Assert.AreEqual(0, sim.BeltCount);
    }

    [Test]
    public void RegisterBelt_Duplicate_DoesNotDoubleCount()
    {
        var sim = CreateSimulation();
        var belt = new BeltSegment(1);

        sim.RegisterBelt(belt);
        sim.RegisterBelt(belt);

        Assert.AreEqual(1, sim.BeltCount);
    }

    [Test]
    public void RegisterBelt_Null_Throws()
    {
        var sim = CreateSimulation();

        Assert.Throws<ArgumentNullException>(() => sim.RegisterBelt(null));
    }

    // ========================================================================
    // Inserter registration
    // ========================================================================

    [Test]
    public void RegisterInserter_IncreasesInserterCount()
    {
        var sim = CreateSimulation();
        var source = new StorageContainer(1, 10);
        var dest = new StorageContainer(1, 10);
        var inserter = new Inserter(source, dest, 1f);

        sim.RegisterInserter(inserter);

        Assert.AreEqual(1, sim.InserterCount);
    }

    [Test]
    public void UnregisterInserter_DecreasesInserterCount()
    {
        var sim = CreateSimulation();
        var source = new StorageContainer(1, 10);
        var dest = new StorageContainer(1, 10);
        var inserter = new Inserter(source, dest, 1f);
        sim.RegisterInserter(inserter);

        sim.UnregisterInserter(inserter);

        Assert.AreEqual(0, sim.InserterCount);
    }

    [Test]
    public void RegisterInserter_Null_Throws()
    {
        var sim = CreateSimulation();

        Assert.Throws<ArgumentNullException>(() => sim.RegisterInserter(null));
    }

    // ========================================================================
    // Tick orchestration: belts advance
    // ========================================================================

    [Test]
    public void Tick_AdvancesBeltItems()
    {
        var sim = CreateSimulation();
        sim.BeltSpeed = 100; // entire 1-tile belt in one tick

        var belt = new BeltSegment(1);
        belt.TryInsertAtStart(IronOre, 0);
        Assert.IsFalse(belt.HasItemAtEnd);

        sim.RegisterBelt(belt);
        sim.Tick(0.5f);

        Assert.IsTrue(belt.HasItemAtEnd, "Belt item should reach output end after one tick at full speed");
    }

    // ========================================================================
    // Tick orchestration: belt network transfers
    // ========================================================================

    [Test]
    public void Tick_BeltNetworkTransfersItems()
    {
        var sim = CreateSimulation();
        sim.BeltSpeed = 100;

        var beltA = new BeltSegment(1);
        var beltB = new BeltSegment(1);

        // Place item at end of belt A
        beltA.TryInsertAtStart(IronOre, 0);
        beltA.Tick(100); // manually advance to end first
        Assert.IsTrue(beltA.HasItemAtEnd);

        sim.RegisterBelt(beltA);
        sim.RegisterBelt(beltB);
        sim.BeltNetwork.Connect(beltA, beltB);

        sim.Tick(0.5f);

        Assert.IsTrue(beltA.IsEmpty, "Item should be extracted from belt A");
        Assert.AreEqual(1, beltB.ItemCount, "Item should be on belt B");
    }

    // ========================================================================
    // Tick orchestration: inserters run
    // ========================================================================

    [Test]
    public void Tick_InsertersTransferItems()
    {
        var sim = CreateSimulation();

        var source = new StorageContainer(1, 10);
        source.TryInsert(IronOre);
        var dest = new StorageContainer(1, 10);

        // Swing duration 0.5s, dt=0.5s -- grab on tick 1, deposit on tick 2
        var inserter = new Inserter(source, dest, 0.5f);
        sim.RegisterInserter(inserter);

        sim.Tick(0.5f); // grab
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        sim.Tick(0.5f); // swing completes, deposit
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, dest.GetTotalItemCount());
    }

    // ========================================================================
    // BeltSpeed property
    // ========================================================================

    [Test]
    public void BeltSpeed_DefaultIsTwo()
    {
        var sim = CreateSimulation();

        Assert.AreEqual(2, sim.BeltSpeed);
    }

    [Test]
    public void BeltSpeed_AffectsMovement()
    {
        var sim = CreateSimulation();
        var belt = new BeltSegment(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);
        sim.RegisterBelt(belt);

        sim.BeltSpeed = 50; // half the belt per tick
        sim.Tick(0.5f);

        // After 50 subdivisions, terminal gap should be 50, not at end yet
        Assert.IsFalse(belt.HasItemAtEnd);
        Assert.AreEqual(50, belt.TerminalGap);

        sim.Tick(0.5f);

        Assert.IsTrue(belt.HasItemAtEnd);
    }

    // ========================================================================
    // Full pipeline integration: storage -> belt -> machine -> belt -> storage
    // ========================================================================

    [Test]
    public void FullPipeline_OreToIngot_EndToEnd()
    {
        // Configuration: fast belts, quick swings, short craft
        var sim = CreateSimulation();
        sim.BeltSpeed = 100; // 1-tile belt clears in one tick
        const float dt = 0.5f;
        const float swingDuration = 0.5f; // completes in one dt

        // Source storage with ore
        var sourceStorage = new StorageContainer(4, 50);
        sourceStorage.TryInsert(IronOre);

        // Belts (1 tile each)
        var beltA = new BeltSegment(1);
        var beltB = new BeltSegment(1);

        // Smelter
        var smelter = CreateMachine();
        smelter.SetRecipe(SmeltIronRecipeId);

        // Output storage
        var outputStorage = new StorageContainer(4, 50);

        // Inserters
        var ins1 = new Inserter(sourceStorage, new BeltInputAdapter(beltA, 50), swingDuration);
        var ins2 = new Inserter(new BeltOutputAdapter(beltA), new MachineInputAdapter(smelter, 0), swingDuration);
        var ins3 = new Inserter(new MachineOutputAdapter(smelter, 0), new BeltInputAdapter(beltB, 50), swingDuration);
        var ins4 = new Inserter(new BeltOutputAdapter(beltB), outputStorage, swingDuration);

        // Register everything
        sim.RegisterBelt(beltA);
        sim.RegisterBelt(beltB);
        sim.RegisterMachine(smelter);
        sim.RegisterInserter(ins1);
        sim.RegisterInserter(ins2);
        sim.RegisterInserter(ins3);
        sim.RegisterInserter(ins4);

        // Tick 1: ins1 grabs ore from source (swing starts)
        sim.Tick(dt);
        Assert.IsTrue(sourceStorage.IsEmpty, "Tick 1: source should be empty after grab");
        Assert.AreEqual(IronOre, ins1.HeldItemId, "Tick 1: ins1 should hold ore");

        // Tick 2: ins1 deposits onto belt A
        sim.Tick(dt);
        Assert.IsNull(ins1.HeldItemId, "Tick 2: ins1 should have deposited");
        Assert.AreEqual(1, beltA.ItemCount, "Tick 2: belt A should have one item");

        // Tick 3: belt A moves item to end, ins2 grabs it
        sim.Tick(dt);
        Assert.IsTrue(beltA.IsEmpty, "Tick 3: belt A should be empty after ins2 grab");
        Assert.AreEqual(IronOre, ins2.HeldItemId, "Tick 3: ins2 should hold ore");

        // Tick 4: ins2 deposits into machine, machine starts crafting
        sim.Tick(dt);
        Assert.IsNull(ins2.HeldItemId, "Tick 4: ins2 should have deposited");
        Assert.AreEqual(MachineStatus.Working, smelter.Status, "Tick 4: smelter should be working");

        // Ticks 5-7: machine crafts (need 2.0s total at speed 1, already 0.5s progress from tick 4)
        // Tick 4 gave 0.5 progress, need 1.5 more = 3 ticks at 0.5
        sim.Tick(dt); // tick 5: progress = 1.0
        sim.Tick(dt); // tick 6: progress = 1.5
        sim.Tick(dt); // tick 7: progress = 2.0, craft completes
        Assert.AreEqual(MachineStatus.Idle, smelter.Status, "Tick 7: craft should be complete");
        Assert.AreEqual(IronIngot, smelter.GetOutput(0).item.definitionId);

        // Tick 8: ins3 grabs ingot from machine output
        sim.Tick(dt);
        Assert.AreEqual(IronIngot, ins3.HeldItemId, "Tick 8: ins3 should hold ingot");
        Assert.IsTrue(smelter.GetOutput(0).IsEmpty, "Tick 8: machine output should be cleared");

        // Tick 9: ins3 deposits onto belt B
        sim.Tick(dt);
        Assert.IsNull(ins3.HeldItemId, "Tick 9: ins3 should have deposited");
        Assert.AreEqual(1, beltB.ItemCount, "Tick 9: belt B should have ingot");

        // Tick 10: belt B moves item to end, ins4 grabs it
        sim.Tick(dt);
        Assert.IsTrue(beltB.IsEmpty, "Tick 10: belt B should be empty after ins4 grab");
        Assert.AreEqual(IronIngot, ins4.HeldItemId, "Tick 10: ins4 should hold ingot");

        // Tick 11: ins4 deposits into output storage
        sim.Tick(dt);
        Assert.IsNull(ins4.HeldItemId, "Tick 11: ins4 should have deposited");

        // Verify final state
        Assert.IsTrue(sourceStorage.IsEmpty, "Source should be empty");
        Assert.IsTrue(beltA.IsEmpty, "Belt A should be empty");
        Assert.IsTrue(beltB.IsEmpty, "Belt B should be empty");
        Assert.IsTrue(smelter.GetInput(0).IsEmpty, "Machine input should be empty");
        Assert.IsTrue(smelter.GetOutput(0).IsEmpty, "Machine output should be empty");
        Assert.AreEqual(1, outputStorage.GetTotalItemCount(), "Output should have 1 ingot");

        outputStorage.TryExtract(out string outputItemId);
        Assert.AreEqual(IronIngot, outputItemId, "Output item should be iron ingot");
    }

    // ========================================================================
    // Multi-item pipeline (verifies continuous flow)
    // ========================================================================

    [Test]
    public void FullPipeline_MultipleItems_FlowContinuously()
    {
        var sim = CreateSimulation();
        sim.BeltSpeed = 100;
        const float dt = 0.5f;
        const float swingDuration = 0.5f;
        const int oreCount = 3;

        var sourceStorage = new StorageContainer(4, 50);
        for (int i = 0; i < oreCount; i++)
            sourceStorage.TryInsert(IronOre);

        var beltA = new BeltSegment(1);
        var beltB = new BeltSegment(1);
        var smelter = CreateMachine();
        smelter.SetRecipe(SmeltIronRecipeId);
        var outputStorage = new StorageContainer(4, 50);

        sim.RegisterBelt(beltA);
        sim.RegisterBelt(beltB);
        sim.RegisterMachine(smelter);
        sim.RegisterInserter(new Inserter(sourceStorage, new BeltInputAdapter(beltA, 50), swingDuration));
        sim.RegisterInserter(new Inserter(new BeltOutputAdapter(beltA), new MachineInputAdapter(smelter, 0), swingDuration));
        sim.RegisterInserter(new Inserter(new MachineOutputAdapter(smelter, 0), new BeltInputAdapter(beltB, 50), swingDuration));
        sim.RegisterInserter(new Inserter(new BeltOutputAdapter(beltB), outputStorage, swingDuration));

        // Run enough ticks for all items to flow through
        // Each item takes ~11 ticks. With pipelining, items overlap.
        // 200 ticks at dt=0.5 = 100 seconds, more than enough.
        for (int i = 0; i < 200; i++)
            sim.Tick(dt);

        Assert.IsTrue(sourceStorage.IsEmpty, "All ore should be consumed");
        Assert.AreEqual(oreCount, outputStorage.GetTotalItemCount(),
            $"All {oreCount} ingots should be in output storage");
    }
}
