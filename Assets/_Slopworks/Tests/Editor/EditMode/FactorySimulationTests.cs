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
}
