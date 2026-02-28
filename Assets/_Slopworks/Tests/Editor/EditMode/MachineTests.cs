using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MachineTests
{
    private const string SmelterType = "smelter";
    private const string SmeltIronRecipeId = "smelt_iron";
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const string CopperOre = "copper_ore";
    private const string CopperIngot = "copper_ingot";
    private const float DefaultCraftDuration = 2f;

    private MachineDefinitionSO _smelterDef;
    private RecipeSO _smeltIronRecipe;

    [SetUp]
    public void SetUp()
    {
        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter_basic";
        _smelterDef.displayName = "Basic Smelter";
        _smelterDef.size = new Vector2Int(2, 2);
        _smelterDef.machineType = SmelterType;
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 1;
        _smelterDef.processingSpeed = 1f;
        _smelterDef.powerConsumption = 100f;
        _smelterDef.ports = new MachinePort[]
        {
            new MachinePort
            {
                localOffset = new Vector2Int(0, 0),
                direction = new Vector2Int(0, -1),
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = new Vector2Int(1, 1),
                direction = new Vector2Int(0, 1),
                type = PortType.Output
            }
        };

        _smeltIronRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltIronRecipe.recipeId = SmeltIronRecipeId;
        _smeltIronRecipe.displayName = "Smelt Iron";
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

    private Machine CreateMachine()
    {
        return new Machine(_smelterDef);
    }

    private RecipeSO LookupRecipe(string recipeId)
    {
        if (recipeId == SmeltIronRecipeId)
            return _smeltIronRecipe;
        return null;
    }

    // -- Initial state --

    [Test]
    public void NewMachine_StartsIdle()
    {
        var machine = CreateMachine();

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
    }

    [Test]
    public void NewMachine_HasNullActiveRecipe()
    {
        var machine = CreateMachine();

        Assert.IsNull(machine.ActiveRecipeId);
    }

    [Test]
    public void NewMachine_HasZeroCraftProgress()
    {
        var machine = CreateMachine();

        Assert.AreEqual(0f, machine.CraftProgress);
    }

    [Test]
    public void NewMachine_StoresDefinition()
    {
        var machine = CreateMachine();

        Assert.AreSame(_smelterDef, machine.Definition);
    }

    // -- SetRecipe / ClearRecipe --

    [Test]
    public void SetRecipe_SetsActiveRecipeId()
    {
        var machine = CreateMachine();

        machine.SetRecipe(SmeltIronRecipeId);

        Assert.AreEqual(SmeltIronRecipeId, machine.ActiveRecipeId);
    }

    [Test]
    public void SetRecipe_ResetsProgressToZero()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        machine.Tick(1f, LookupRecipe);

        // Machine should be working with some progress
        Assert.Greater(machine.CraftProgress, 0f);

        // Setting a new recipe resets progress
        machine.SetRecipe(SmeltIronRecipeId);

        Assert.AreEqual(0f, machine.CraftProgress);
    }

    [Test]
    public void ClearRecipe_ClearsActiveRecipeIdAndGoesIdle()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);

        machine.ClearRecipe();

        Assert.IsNull(machine.ActiveRecipeId);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
    }

    // -- TryInsertInput --

    [Test]
    public void TryInsertInput_AddsItemsToInputBuffer()
    {
        var machine = CreateMachine();
        var item = ItemInstance.Create(IronOre);

        bool result = machine.TryInsertInput(0, item, 5);

        Assert.IsTrue(result);
        var slot = machine.GetInput(0);
        Assert.AreEqual(IronOre, slot.item.definitionId);
        Assert.AreEqual(5, slot.count);
    }

    [Test]
    public void TryInsertInput_StacksSameItemType()
    {
        var machine = CreateMachine();
        var item = ItemInstance.Create(IronOre);

        machine.TryInsertInput(0, item, 3);
        bool result = machine.TryInsertInput(0, item, 2);

        Assert.IsTrue(result);
        Assert.AreEqual(5, machine.GetInput(0).count);
    }

    [Test]
    public void TryInsertInput_DifferentItemType_ReturnsFalse()
    {
        var machine = CreateMachine();

        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        bool result = machine.TryInsertInput(0, ItemInstance.Create(CopperOre), 1);

        Assert.IsFalse(result);
        Assert.AreEqual(1, machine.GetInput(0).count);
        Assert.AreEqual(IronOre, machine.GetInput(0).item.definitionId);
    }

    [Test]
    public void TryInsertInput_InvalidSlotIndex_ReturnsFalse()
    {
        var machine = CreateMachine();

        Assert.IsFalse(machine.TryInsertInput(-1, ItemInstance.Create(IronOre), 1));
        Assert.IsFalse(machine.TryInsertInput(99, ItemInstance.Create(IronOre), 1));
    }

    [Test]
    public void TryInsertInput_EmptyItem_ReturnsFalse()
    {
        var machine = CreateMachine();

        Assert.IsFalse(machine.TryInsertInput(0, ItemInstance.Empty, 1));
    }

    [Test]
    public void TryInsertInput_ZeroCount_ReturnsFalse()
    {
        var machine = CreateMachine();

        Assert.IsFalse(machine.TryInsertInput(0, ItemInstance.Create(IronOre), 0));
    }

    // -- GetInput / GetOutput boundary --

    [Test]
    public void GetInput_InvalidSlotIndex_ReturnsEmpty()
    {
        var machine = CreateMachine();

        Assert.IsTrue(machine.GetInput(-1).IsEmpty);
        Assert.IsTrue(machine.GetInput(99).IsEmpty);
    }

    [Test]
    public void GetOutput_InvalidSlotIndex_ReturnsEmpty()
    {
        var machine = CreateMachine();

        Assert.IsTrue(machine.GetOutput(-1).IsEmpty);
        Assert.IsTrue(machine.GetOutput(99).IsEmpty);
    }

    // -- Tick with no recipe --

    [Test]
    public void Tick_WithNoRecipe_StaysIdle()
    {
        var machine = CreateMachine();

        machine.Tick(1f, LookupRecipe);

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
    }

    // -- Tick with insufficient inputs --

    [Test]
    public void Tick_WithRecipeAndInsufficientInputs_StaysIdle()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);

        // No inputs added
        machine.Tick(1f, LookupRecipe);

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(0f, machine.CraftProgress);
    }

    // -- Tick starts crafting --

    [Test]
    public void Tick_WithRecipeAndSufficientInputs_ConsumesInputsAndGoesWorking()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        machine.Tick(0.5f, LookupRecipe);

        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.IsTrue(machine.GetInput(0).IsEmpty, "Input should be consumed");
    }

    // -- Tick while working --

    [Test]
    public void Tick_WhileWorking_IncrementsCraftProgress()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        // First tick starts crafting (consumes inputs, progress from deltaTime)
        machine.Tick(0.5f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.AreEqual(0.5f, machine.CraftProgress, 0.001f);

        // Second tick advances progress
        machine.Tick(0.5f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.AreEqual(1.0f, machine.CraftProgress, 0.001f);
    }

    // -- Tick completes crafting --

    [Test]
    public void Tick_WhenCraftCompletes_ProducesOutputAndGoesIdle()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        // Tick past the full craft duration (2 seconds at 1x speed)
        machine.Tick(0.5f, LookupRecipe); // starts crafting, progress = 0.5
        machine.Tick(2f, LookupRecipe);    // progress = 2.5 >= 2.0, craft done

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        var output = machine.GetOutput(0);
        Assert.IsFalse(output.IsEmpty);
        Assert.AreEqual(IronIngot, output.item.definitionId);
        Assert.AreEqual(1, output.count);
    }

    // -- Tick blocked when output full --

    [Test]
    public void Tick_WhenCraftCompletes_ButOutputFull_GoesBlocked()
    {
        // Create a definition with 1 output slot
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);

        // Pre-fill the output buffer by running a complete cycle
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(2f, LookupRecipe);

        // Output should have 1 iron ingot
        Assert.AreEqual(1, machine.GetOutput(0).count);

        // Start a second cycle but put a different item in output to block
        // Actually, iron ingot stacks with iron ingot, so let's use a recipe
        // that outputs a different item to block.
        // Instead, we can manually test by creating a machine with 1 output slot,
        // filling it with a different item type.

        // Simpler approach: use a definition with 0 output slots
        var tinyDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        tinyDef.machineId = "tiny";
        tinyDef.machineType = SmelterType;
        tinyDef.inputBufferSize = 2;
        tinyDef.outputBufferSize = 0;
        tinyDef.processingSpeed = 1f;
        tinyDef.ports = new MachinePort[0];

        var tinyMachine = new Machine(tinyDef);
        tinyMachine.SetRecipe(SmeltIronRecipeId);
        tinyMachine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        tinyMachine.Tick(0.5f, LookupRecipe);
        tinyMachine.Tick(2f, LookupRecipe);

        Assert.AreEqual(MachineStatus.Blocked, tinyMachine.Status);

        ScriptableObject.DestroyImmediate(tinyDef);
    }

    // -- Tick while blocked retries --

    [Test]
    public void Tick_WhileBlocked_RetriesAndTransitionsToIdle_WhenOutputExtracted()
    {
        // Use a machine with 0 output slots to force blocked
        var tinyDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        tinyDef.machineId = "tiny";
        tinyDef.machineType = SmelterType;
        tinyDef.inputBufferSize = 2;
        tinyDef.outputBufferSize = 1;
        tinyDef.processingSpeed = 1f;
        tinyDef.ports = new MachinePort[0];

        // Create a recipe that outputs copper (different from iron) so we can block
        var copperRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        copperRecipe.recipeId = "smelt_copper";
        copperRecipe.inputs = new[] { new RecipeIngredient { itemId = CopperOre, count = 1 } };
        copperRecipe.outputs = new[] { new RecipeIngredient { itemId = CopperIngot, count = 1 } };
        copperRecipe.craftDuration = 1f;
        copperRecipe.requiredMachineType = SmelterType;

        Func<string, RecipeSO> lookup = id =>
        {
            if (id == "smelt_copper") return copperRecipe;
            return LookupRecipe(id);
        };

        var machine = new Machine(tinyDef);

        // First cycle: fill output with copper ingot
        machine.SetRecipe("smelt_copper");
        machine.TryInsertInput(0, ItemInstance.Create(CopperOre), 1);
        machine.Tick(0.5f, lookup);
        machine.Tick(1f, lookup);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(CopperIngot, machine.GetOutput(0).item.definitionId);

        // Second cycle: will block because output has copper and recipe also outputs copper...
        // Actually copper stacks with copper. We need the output to contain a *different* item.
        // Let's manually set up a blocked scenario more carefully.

        // Start iron smelting while output has copper
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        machine.Tick(0.5f, LookupRecipe); // consumes input, working
        machine.Tick(2f, LookupRecipe);    // craft done, but output[0] has copper, can't push iron -> blocked

        Assert.AreEqual(MachineStatus.Blocked, machine.Status);

        // Extract the copper from output
        machine.ExtractOutput(0, 1);

        // Next tick should push the iron output and go idle
        machine.Tick(0f, LookupRecipe);

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(IronIngot, machine.GetOutput(0).item.definitionId);

        ScriptableObject.DestroyImmediate(tinyDef);
        ScriptableObject.DestroyImmediate(copperRecipe);
    }

    // -- ExtractOutput --

    [Test]
    public void ExtractOutput_ReturnsItemsAndClearsSlot()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        // Run through a full craft cycle
        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(2f, LookupRecipe);

        var extracted = machine.ExtractOutput(0, 1);

        Assert.AreEqual(IronIngot, extracted.item.definitionId);
        Assert.AreEqual(1, extracted.count);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);
    }

    [Test]
    public void ExtractOutput_PartialExtract_LeavesRemainder()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);

        // Run two craft cycles to stack 2 ingots in output
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(2f, LookupRecipe);

        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(2f, LookupRecipe);

        Assert.AreEqual(2, machine.GetOutput(0).count);

        var extracted = machine.ExtractOutput(0, 1);

        Assert.AreEqual(1, extracted.count);
        Assert.AreEqual(1, machine.GetOutput(0).count);
    }

    [Test]
    public void ExtractOutput_FromEmptySlot_ReturnsEmpty()
    {
        var machine = CreateMachine();

        var extracted = machine.ExtractOutput(0, 5);

        Assert.IsTrue(extracted.IsEmpty);
    }

    [Test]
    public void ExtractOutput_InvalidSlot_ReturnsEmpty()
    {
        var machine = CreateMachine();

        Assert.IsTrue(machine.ExtractOutput(-1, 1).IsEmpty);
        Assert.IsTrue(machine.ExtractOutput(99, 1).IsEmpty);
    }

    // -- Full craft cycle --

    [Test]
    public void FullCraftCycle_InsertInputs_TickThrough_ExtractOutputs()
    {
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);

        // Step 1: insert input
        Assert.IsTrue(machine.TryInsertInput(0, ItemInstance.Create(IronOre), 3));
        Assert.AreEqual(3, machine.GetInput(0).count);

        // Step 2: first tick -- consumes 1 ore, starts working
        machine.Tick(0.1f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.AreEqual(2, machine.GetInput(0).count); // 3 - 1 = 2 remaining

        // Step 3: tick until craft completes
        machine.Tick(2f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(1, machine.GetOutput(0).count);

        // Step 4: machine goes idle and picks up next craft automatically
        machine.Tick(0.1f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.AreEqual(1, machine.GetInput(0).count); // 2 - 1 = 1 remaining

        // Step 5: complete second craft
        machine.Tick(2f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(2, machine.GetOutput(0).count); // 2 ingots stacked

        // Step 6: third craft
        machine.Tick(0.1f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.IsTrue(machine.GetInput(0).IsEmpty); // all ore consumed

        machine.Tick(2f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(3, machine.GetOutput(0).count);

        // Step 7: no more inputs, stays idle
        machine.Tick(1f, LookupRecipe);
        Assert.AreEqual(MachineStatus.Idle, machine.Status);

        // Step 8: extract all outputs
        var result = machine.ExtractOutput(0, 3);
        Assert.AreEqual(IronIngot, result.item.definitionId);
        Assert.AreEqual(3, result.count);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);
    }

    // -- Processing speed --

    [Test]
    public void ProcessingSpeed_AffectsCraftDuration()
    {
        // Double processing speed should complete in half the time
        _smelterDef.processingSpeed = 2f;
        var machine = CreateMachine();
        machine.SetRecipe(SmeltIronRecipeId);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);

        // At 2x speed, 1 second of real time = 2 seconds of craft progress
        // Recipe needs 2 seconds, so 1 second at 2x should complete it
        machine.Tick(0.5f, LookupRecipe); // starts, progress = 1.0
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.AreEqual(1.0f, machine.CraftProgress, 0.001f);

        machine.Tick(0.5f, LookupRecipe); // progress = 2.0 >= 2.0, done
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(1, machine.GetOutput(0).count);
    }

    // -- Null recipe lookup --

    [Test]
    public void Tick_WithUnresolvableRecipe_StaysIdle()
    {
        var machine = CreateMachine();
        machine.SetRecipe("nonexistent_recipe");

        machine.Tick(1f, _ => null);

        Assert.AreEqual(MachineStatus.Idle, machine.Status);
    }

    // -- Constructor validation --

    [Test]
    public void Constructor_NullDefinition_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Machine(null));
    }
}
