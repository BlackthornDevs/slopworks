using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class InserterTests
{
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const string CopperOre = "copper_ore";
    private const float DefaultSwingDuration = 1f;
    private const ushort DefaultSpacing = 50;

    // -- Test doubles --

    /// <summary>
    /// Simple item source backed by a queue for testing.
    /// </summary>
    private class TestItemSource : IItemSource
    {
        private readonly Queue<string> _items = new Queue<string>();

        public bool HasItemAvailable => _items.Count > 0;

        public string PeekItemId()
        {
            if (_items.Count == 0) return null;
            return _items.Peek();
        }

        public bool TryExtract(out string itemId)
        {
            if (_items.Count == 0)
            {
                itemId = null;
                return false;
            }
            itemId = _items.Dequeue();
            return true;
        }

        public void Enqueue(string itemId)
        {
            _items.Enqueue(itemId);
        }
    }

    /// <summary>
    /// Simple item destination backed by a list for testing.
    /// Can be configured to reject insertions.
    /// </summary>
    private class TestItemDestination : IItemDestination
    {
        private readonly List<string> _items = new List<string>();
        private bool _acceptItems = true;

        public IReadOnlyList<string> Items => _items;

        public bool AcceptItems
        {
            get => _acceptItems;
            set => _acceptItems = value;
        }

        public bool CanAccept(string itemId) => _acceptItems;

        public bool TryInsert(string itemId)
        {
            if (!_acceptItems) return false;
            _items.Add(itemId);
            return true;
        }
    }

    // ========================================================================
    // Inserter core behavior tests
    // ========================================================================

    [Test]
    public void NewInserter_IsNotHoldingItem()
    {
        var source = new TestItemSource();
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        Assert.IsNull(inserter.HeldItemId);
        Assert.IsFalse(inserter.IsSwinging);
        Assert.AreEqual(0f, inserter.SwingProgress);
    }

    [Test]
    public void Tick_GrabsItemFromSource_WhenAvailable()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        inserter.Tick(0f);

        Assert.AreEqual(IronOre, inserter.HeldItemId);
        Assert.IsTrue(inserter.IsSwinging);
    }

    [Test]
    public void Tick_DoesNotGrab_WhenSourceIsEmpty()
    {
        var source = new TestItemSource();
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        inserter.Tick(0.5f);

        Assert.IsNull(inserter.HeldItemId);
        Assert.IsFalse(inserter.IsSwinging);
    }

    [Test]
    public void Tick_DepositsItem_AfterSwingCompletes()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up
        inserter.Tick(0f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Swing to completion
        inserter.Tick(DefaultSwingDuration);

        Assert.IsNull(inserter.HeldItemId);
        Assert.IsFalse(inserter.IsSwinging);
        Assert.AreEqual(1, destination.Items.Count);
        Assert.AreEqual(IronOre, destination.Items[0]);
    }

    [Test]
    public void Tick_DoesNotLoseItem_WhenDestinationRejects()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        destination.AcceptItems = false;
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up
        inserter.Tick(0f);

        // Swing to completion -- destination rejects
        inserter.Tick(DefaultSwingDuration);

        Assert.AreEqual(IronOre, inserter.HeldItemId, "Item should be kept when destination rejects");
        Assert.IsFalse(inserter.IsSwinging, "Should stop swinging after rejection");
        Assert.AreEqual(0, destination.Items.Count);
    }

    [Test]
    public void Tick_RetriesDeposit_AfterRejection()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        destination.AcceptItems = false;
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up and swing
        inserter.Tick(0f);
        inserter.Tick(DefaultSwingDuration);

        // Still holding after rejection
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Now accept items and tick again
        destination.AcceptItems = true;
        inserter.Tick(0f);

        Assert.IsNull(inserter.HeldItemId, "Item should be deposited on retry");
        Assert.AreEqual(1, destination.Items.Count);
        Assert.AreEqual(IronOre, destination.Items[0]);
    }

    [Test]
    public void SwingDuration_IsRespected_PartialTime()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, 2f);

        // Pick up
        inserter.Tick(0f);
        Assert.IsTrue(inserter.IsSwinging);

        // Half swing
        inserter.Tick(1f);
        Assert.IsTrue(inserter.IsSwinging, "Should still be swinging at 50%");
        Assert.AreEqual(IronOre, inserter.HeldItemId);
        Assert.AreEqual(0.5f, inserter.SwingProgress, 0.001f);
        Assert.AreEqual(0, destination.Items.Count, "Should not deposit before swing completes");

        // Complete the swing
        inserter.Tick(1f);
        Assert.IsFalse(inserter.IsSwinging);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, destination.Items.Count);
    }

    [Test]
    public void SwingProgress_ReportsCorrectValue()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, 4f);

        // Pick up
        inserter.Tick(0f);
        Assert.AreEqual(0f, inserter.SwingProgress, 0.001f);

        // 25%
        inserter.Tick(1f);
        Assert.AreEqual(0.25f, inserter.SwingProgress, 0.001f);

        // 75%
        inserter.Tick(2f);
        Assert.AreEqual(0.75f, inserter.SwingProgress, 0.001f);
    }

    [Test]
    public void Inserter_ExposesSourceAndDestination()
    {
        var source = new TestItemSource();
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        Assert.AreSame(source, inserter.Source);
        Assert.AreSame(destination, inserter.Destination);
    }

    [Test]
    public void Constructor_NullSource_Throws()
    {
        var destination = new TestItemDestination();
        Assert.Throws<ArgumentNullException>(() => new Inserter(null, destination, DefaultSwingDuration));
    }

    [Test]
    public void Constructor_NullDestination_Throws()
    {
        var source = new TestItemSource();
        Assert.Throws<ArgumentNullException>(() => new Inserter(source, null, DefaultSwingDuration));
    }

    [Test]
    public void Constructor_ZeroSwingDuration_Throws()
    {
        var source = new TestItemSource();
        var destination = new TestItemDestination();
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inserter(source, destination, 0f));
    }

    [Test]
    public void Constructor_NegativeSwingDuration_Throws()
    {
        var source = new TestItemSource();
        var destination = new TestItemDestination();
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inserter(source, destination, -1f));
    }

    [Test]
    public void Tick_TransfersMultipleItems_Sequentially()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        source.Enqueue(CopperOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Transfer first item
        inserter.Tick(0f);  // pick up iron_ore
        inserter.Tick(DefaultSwingDuration); // deposit iron_ore

        // Transfer second item
        inserter.Tick(0f);  // pick up copper_ore
        inserter.Tick(DefaultSwingDuration); // deposit copper_ore

        Assert.AreEqual(2, destination.Items.Count);
        Assert.AreEqual(IronOre, destination.Items[0]);
        Assert.AreEqual(CopperOre, destination.Items[1]);
    }

    // ========================================================================
    // BeltOutputAdapter tests
    // ========================================================================

    [Test]
    public void BeltOutputAdapter_HasItemAvailable_MatchesBeltHasItemAtEnd()
    {
        var belt = new BeltSegment(1); // 100 subdivisions
        var adapter = new BeltOutputAdapter(belt);

        Assert.IsFalse(adapter.HasItemAvailable);

        belt.TryInsertAtStart(IronOre, 0);
        Assert.IsFalse(adapter.HasItemAvailable, "Item has not reached end yet");

        belt.Tick(100);
        Assert.IsTrue(adapter.HasItemAvailable, "Item should be at end after full tick");
    }

    [Test]
    public void BeltOutputAdapter_PeekItemId_ReturnsLastItemId()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltOutputAdapter(belt);

        Assert.IsNull(adapter.PeekItemId());

        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(100);

        Assert.AreEqual(IronOre, adapter.PeekItemId());
    }

    [Test]
    public void BeltOutputAdapter_TryExtract_RemovesItemFromBelt()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltOutputAdapter(belt);

        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(100);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsTrue(result);
        Assert.AreEqual(IronOre, itemId);
        Assert.IsTrue(belt.IsEmpty);
    }

    [Test]
    public void BeltOutputAdapter_TryExtract_ReturnsFalse_WhenNoItemAtEnd()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltOutputAdapter(belt);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsFalse(result);
        Assert.IsNull(itemId);
    }

    [Test]
    public void BeltOutputAdapter_NullBelt_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BeltOutputAdapter(null));
    }

    // ========================================================================
    // BeltInputAdapter tests
    // ========================================================================

    [Test]
    public void BeltInputAdapter_CanAccept_TrueOnEmptyBelt()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, DefaultSpacing);

        Assert.IsTrue(adapter.CanAccept(IronOre));
    }

    [Test]
    public void BeltInputAdapter_CanAccept_FalseWhenInsufficientGap()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, DefaultSpacing);

        belt.TryInsertAtStart(IronOre, 0);
        // First item has distanceToNext = 0, less than spacing of 50
        Assert.IsFalse(adapter.CanAccept(CopperOre));
    }

    [Test]
    public void BeltInputAdapter_CanAccept_TrueWhenSufficientGap()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, DefaultSpacing);

        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(DefaultSpacing);
        // First item now has distanceToNext = 50 >= minSpacing
        Assert.IsTrue(adapter.CanAccept(CopperOre));
    }

    [Test]
    public void BeltInputAdapter_TryInsert_InsertsItemOnBelt()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, DefaultSpacing);

        bool result = adapter.TryInsert(IronOre);

        Assert.IsTrue(result);
        Assert.AreEqual(1, belt.ItemCount);
    }

    [Test]
    public void BeltInputAdapter_TryInsert_FailsWhenNoRoom()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, DefaultSpacing);

        adapter.TryInsert(IronOre);
        // No tick -- first item at distanceToNext = 0
        bool result = adapter.TryInsert(CopperOre);

        Assert.IsFalse(result);
        Assert.AreEqual(1, belt.ItemCount);
    }

    [Test]
    public void BeltInputAdapter_NullBelt_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BeltInputAdapter(null));
    }

    // ========================================================================
    // MachineOutputAdapter tests
    // ========================================================================

    private MachineDefinitionSO _machineDef;
    private RecipeSO _smeltRecipe;

    [SetUp]
    public void SetUp()
    {
        _machineDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _machineDef.machineId = "test_smelter";
        _machineDef.inputBufferSize = 1;
        _machineDef.outputBufferSize = 1;
        _machineDef.processingSpeed = 1f;
        _machineDef.ports = new MachinePort[0];

        _smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltRecipe.recipeId = "smelt_iron";
        _smeltRecipe.craftDuration = 1f;
        _smeltRecipe.inputs = new RecipeIngredient[] { new RecipeIngredient { itemId = IronOre, count = 1 } };
        _smeltRecipe.outputs = new RecipeIngredient[] { new RecipeIngredient { itemId = IronIngot, count = 1 } };
    }

    [TearDown]
    public void TearDown()
    {
        if (_machineDef != null) UnityEngine.Object.DestroyImmediate(_machineDef);
        if (_smeltRecipe != null) UnityEngine.Object.DestroyImmediate(_smeltRecipe);
    }

    private RecipeSO LookupRecipe(string recipeId)
    {
        if (recipeId == "smelt_iron") return _smeltRecipe;
        return null;
    }

    private Machine CreateSmelterWithOutput()
    {
        var machine = new Machine(_machineDef);
        machine.SetRecipe("smelt_iron");
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        // Tick through a full craft cycle
        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(1f, LookupRecipe);
        return machine;
    }

    [Test]
    public void MachineOutputAdapter_HasItemAvailable_WhenOutputNotEmpty()
    {
        var machine = CreateSmelterWithOutput();
        var adapter = new MachineOutputAdapter(machine, 0);

        Assert.IsTrue(adapter.HasItemAvailable);
    }

    [Test]
    public void MachineOutputAdapter_HasItemAvailable_FalseWhenEmpty()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineOutputAdapter(machine, 0);

        Assert.IsFalse(adapter.HasItemAvailable);
    }

    [Test]
    public void MachineOutputAdapter_PeekItemId_ReturnsItemDefinitionId()
    {
        var machine = CreateSmelterWithOutput();
        var adapter = new MachineOutputAdapter(machine, 0);

        Assert.AreEqual(IronIngot, adapter.PeekItemId());
    }

    [Test]
    public void MachineOutputAdapter_PeekItemId_ReturnsNull_WhenEmpty()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineOutputAdapter(machine, 0);

        Assert.IsNull(adapter.PeekItemId());
    }

    [Test]
    public void MachineOutputAdapter_TryExtract_RemovesOneItemFromOutput()
    {
        var machine = CreateSmelterWithOutput();
        var adapter = new MachineOutputAdapter(machine, 0);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsTrue(result);
        Assert.AreEqual(IronIngot, itemId);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);
    }

    [Test]
    public void MachineOutputAdapter_TryExtract_ReturnsFalse_WhenEmpty()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineOutputAdapter(machine, 0);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsFalse(result);
        Assert.IsNull(itemId);
    }

    [Test]
    public void MachineOutputAdapter_NullMachine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MachineOutputAdapter(null, 0));
    }

    // ========================================================================
    // MachineInputAdapter tests
    // ========================================================================

    [Test]
    public void MachineInputAdapter_CanAccept_TrueWhenSlotEmpty()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineInputAdapter(machine, 0);

        Assert.IsTrue(adapter.CanAccept(IronOre));
    }

    [Test]
    public void MachineInputAdapter_CanAccept_TrueWhenSameItemType()
    {
        var machine = new Machine(_machineDef);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        var adapter = new MachineInputAdapter(machine, 0);

        Assert.IsTrue(adapter.CanAccept(IronOre));
    }

    [Test]
    public void MachineInputAdapter_CanAccept_FalseWhenDifferentItemType()
    {
        var machine = new Machine(_machineDef);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        var adapter = new MachineInputAdapter(machine, 0);

        Assert.IsFalse(adapter.CanAccept(CopperOre));
    }

    [Test]
    public void MachineInputAdapter_TryInsert_InsertsItem()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineInputAdapter(machine, 0);

        bool result = adapter.TryInsert(IronOre);

        Assert.IsTrue(result);
        Assert.AreEqual(IronOre, machine.GetInput(0).item.definitionId);
        Assert.AreEqual(1, machine.GetInput(0).count);
    }

    [Test]
    public void MachineInputAdapter_TryInsert_StacksSameType()
    {
        var machine = new Machine(_machineDef);
        var adapter = new MachineInputAdapter(machine, 0);

        adapter.TryInsert(IronOre);
        adapter.TryInsert(IronOre);

        Assert.AreEqual(2, machine.GetInput(0).count);
    }

    [Test]
    public void MachineInputAdapter_TryInsert_FailsForDifferentType()
    {
        var machine = new Machine(_machineDef);
        machine.TryInsertInput(0, ItemInstance.Create(IronOre), 1);
        var adapter = new MachineInputAdapter(machine, 0);

        bool result = adapter.TryInsert(CopperOre);

        Assert.IsFalse(result);
        Assert.AreEqual(1, machine.GetInput(0).count);
    }

    [Test]
    public void MachineInputAdapter_NullMachine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MachineInputAdapter(null, 0));
    }

    // ========================================================================
    // Integration: Inserter with belt adapters
    // ========================================================================

    [Test]
    public void Inserter_BeltTobelt_TransfersItem()
    {
        var beltA = new BeltSegment(1);
        var beltB = new BeltSegment(1);

        // Put an item on belt A and move it to the output end
        beltA.TryInsertAtStart(IronOre, 0);
        beltA.Tick(100);

        var source = new BeltOutputAdapter(beltA);
        var destination = new BeltInputAdapter(beltB, DefaultSpacing);
        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up from belt A
        inserter.Tick(0f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);
        Assert.IsTrue(beltA.IsEmpty, "Item should be removed from source belt");

        // Swing and deposit to belt B
        inserter.Tick(DefaultSwingDuration);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, beltB.ItemCount, "Item should be on destination belt");
    }

    // ========================================================================
    // Integration: Inserter with machine adapters
    // ========================================================================

    [Test]
    public void Inserter_MachineToMachine_TransfersItem()
    {
        // Source machine has iron ingot in output
        var sourceMachine = CreateSmelterWithOutput();
        var source = new MachineOutputAdapter(sourceMachine, 0);

        // Destination machine has empty input
        var destMachine = new Machine(_machineDef);
        var destination = new MachineInputAdapter(destMachine, 0);

        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up
        inserter.Tick(0f);
        Assert.AreEqual(IronIngot, inserter.HeldItemId);
        Assert.IsTrue(sourceMachine.GetOutput(0).IsEmpty);

        // Deposit
        inserter.Tick(DefaultSwingDuration);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(IronIngot, destMachine.GetInput(0).item.definitionId);
    }

    // ========================================================================
    // Integration: Belt to Machine (belt -> inserter -> machine)
    // ========================================================================

    [Test]
    public void Inserter_BeltToMachine_TransfersItem()
    {
        var belt = new BeltSegment(1);
        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(100);

        var source = new BeltOutputAdapter(belt);
        var machine = new Machine(_machineDef);
        var destination = new MachineInputAdapter(machine, 0);

        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up from belt
        inserter.Tick(0f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Deposit to machine
        inserter.Tick(DefaultSwingDuration);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(IronOre, machine.GetInput(0).item.definitionId);
        Assert.AreEqual(1, machine.GetInput(0).count);
    }

    // ========================================================================
    // Integration: Machine to Belt (machine -> inserter -> belt)
    // ========================================================================

    [Test]
    public void Inserter_MachineToBelt_TransfersItem()
    {
        var machine = CreateSmelterWithOutput();
        var source = new MachineOutputAdapter(machine, 0);

        var belt = new BeltSegment(1);
        var destination = new BeltInputAdapter(belt, DefaultSpacing);

        var inserter = new Inserter(source, destination, DefaultSwingDuration);

        // Pick up from machine output
        inserter.Tick(0f);
        Assert.AreEqual(IronIngot, inserter.HeldItemId);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);

        // Deposit to belt
        inserter.Tick(DefaultSwingDuration);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, belt.ItemCount);
    }

    // ========================================================================
    // Full chain: Belt A -> Inserter -> Machine -> Inserter -> Belt B
    // ========================================================================

    [Test]
    public void FullChain_BeltToMachineToBelt()
    {
        // Set up belt A with iron ore at the output end
        var beltA = new BeltSegment(1);
        beltA.TryInsertAtStart(IronOre, 0);
        beltA.Tick(100);
        Assert.IsTrue(beltA.HasItemAtEnd);

        // Set up the smelter machine
        var machine = new Machine(_machineDef);
        machine.SetRecipe("smelt_iron");

        // Set up belt B (destination for finished products)
        var beltB = new BeltSegment(1);

        // Create adapters
        var beltAOutput = new BeltOutputAdapter(beltA);
        var machineInput = new MachineInputAdapter(machine, 0);
        var machineOutput = new MachineOutputAdapter(machine, 0);
        var beltBInput = new BeltInputAdapter(beltB, DefaultSpacing);

        // Create inserters
        var inserter1 = new Inserter(beltAOutput, machineInput, 0.5f);
        var inserter2 = new Inserter(machineOutput, beltBInput, 0.5f);

        // Step 1: Inserter 1 grabs iron ore from belt A
        inserter1.Tick(0f);
        Assert.AreEqual(IronOre, inserter1.HeldItemId);
        Assert.IsTrue(beltA.IsEmpty);

        // Step 2: Inserter 1 swings and deposits into machine input
        inserter1.Tick(0.5f);
        Assert.IsNull(inserter1.HeldItemId);
        Assert.AreEqual(IronOre, machine.GetInput(0).item.definitionId);

        // Step 3: Machine processes the iron ore (craft duration = 1s at 1x speed)
        machine.Tick(0.5f, LookupRecipe); // Starts working, consumes input
        Assert.AreEqual(MachineStatus.Working, machine.Status);
        Assert.IsTrue(machine.GetInput(0).IsEmpty);

        machine.Tick(1f, LookupRecipe); // Craft completes (progress = 1.5 >= 1.0)
        Assert.AreEqual(MachineStatus.Idle, machine.Status);
        Assert.AreEqual(IronIngot, machine.GetOutput(0).item.definitionId);

        // Step 4: Inserter 2 grabs iron ingot from machine output
        inserter2.Tick(0f);
        Assert.AreEqual(IronIngot, inserter2.HeldItemId);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);

        // Step 5: Inserter 2 swings and deposits onto belt B
        inserter2.Tick(0.5f);
        Assert.IsNull(inserter2.HeldItemId);
        Assert.AreEqual(1, beltB.ItemCount);

        // Verify the item on belt B is iron ingot
        var beltBItems = beltB.GetItems();
        Assert.AreEqual(IronIngot, beltBItems[0].itemId);
    }

    [Test]
    public void FullChain_MultipleItems_FlowThroughPipeline()
    {
        // Set up belt A with two iron ores
        var beltA = new BeltSegment(2);
        beltA.TryInsertAtStart(IronOre, 0);
        beltA.Tick(DefaultSpacing);
        beltA.TryInsertAtStart(IronOre, DefaultSpacing);

        // Move both to the output end
        // First ore needs to travel 200 - 50 = 150 more to reach end
        beltA.Tick(150);
        Assert.IsTrue(beltA.HasItemAtEnd);

        // Set up the smelter machine
        var machine = new Machine(_machineDef);
        machine.SetRecipe("smelt_iron");

        // Set up belt B
        var beltB = new BeltSegment(2);

        // Create adapters and inserters
        var inserter1 = new Inserter(
            new BeltOutputAdapter(beltA),
            new MachineInputAdapter(machine, 0),
            0.5f);
        var inserter2 = new Inserter(
            new MachineOutputAdapter(machine, 0),
            new BeltInputAdapter(beltB, DefaultSpacing),
            0.5f);

        // Process first item through the pipeline
        inserter1.Tick(0f);   // grab first ore from belt A
        inserter1.Tick(0.5f); // deposit into machine

        machine.Tick(0.5f, LookupRecipe); // start crafting
        machine.Tick(1f, LookupRecipe);   // craft completes

        inserter2.Tick(0f);   // grab ingot from machine
        inserter2.Tick(0.5f); // deposit onto belt B

        Assert.AreEqual(1, beltB.ItemCount);

        // Move second ore to the end of belt A
        // After first extraction, terminal gap became the first ore's distanceToNext (50).
        // Tick 50 to bring the second ore to the end.
        beltA.Tick(50);
        // Move first ingot away from belt B's input end so the next deposit has room
        beltB.Tick(DefaultSpacing);
        Assert.IsTrue(beltA.HasItemAtEnd);

        // Process second item through the pipeline
        inserter1.Tick(0f);
        inserter1.Tick(0.5f);

        machine.Tick(0.5f, LookupRecipe);
        machine.Tick(1f, LookupRecipe);

        inserter2.Tick(0f);
        inserter2.Tick(0.5f);

        Assert.AreEqual(2, beltB.ItemCount);
        Assert.IsTrue(beltA.IsEmpty);
        Assert.IsTrue(machine.GetInput(0).IsEmpty);
        Assert.IsTrue(machine.GetOutput(0).IsEmpty);
    }

    // ========================================================================
    // Edge cases
    // ========================================================================

    [Test]
    public void Inserter_OvershootSwingDuration_StillDeposits()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        var inserter = new Inserter(source, destination, 1f);

        inserter.Tick(0f);   // pick up
        inserter.Tick(5f);   // overshoot the 1s swing duration

        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, destination.Items.Count);
    }

    [Test]
    public void Inserter_MultipleRejections_EventuallyDeposits()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        var destination = new TestItemDestination();
        destination.AcceptItems = false;
        var inserter = new Inserter(source, destination, 0.5f);

        // Pick up and swing
        inserter.Tick(0f);
        inserter.Tick(0.5f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Multiple rejection attempts
        inserter.Tick(0.1f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        inserter.Tick(0.1f);
        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Finally accept
        destination.AcceptItems = true;
        inserter.Tick(0.1f);
        Assert.IsNull(inserter.HeldItemId);
        Assert.AreEqual(1, destination.Items.Count);
    }

    [Test]
    public void Inserter_DoesNotGrabSecondItem_WhileHolding()
    {
        var source = new TestItemSource();
        source.Enqueue(IronOre);
        source.Enqueue(CopperOre);
        var destination = new TestItemDestination();
        destination.AcceptItems = false;
        var inserter = new Inserter(source, destination, 0.5f);

        // Pick up first item
        inserter.Tick(0f);
        inserter.Tick(0.5f); // rejected

        Assert.AreEqual(IronOre, inserter.HeldItemId);

        // Tick again -- should NOT grab copper_ore from source
        inserter.Tick(0.5f);
        Assert.AreEqual(IronOre, inserter.HeldItemId, "Should still hold iron, not grab copper");

        // Source should still have copper
        Assert.IsTrue(source.HasItemAvailable);
        Assert.AreEqual(CopperOre, source.PeekItemId());
    }
}
