using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class AdapterTests
{
    // ════════════════════════════════════════════════════
    // BeltInputAdapter
    // ════════════════════════════════════════════════════

    [Test]
    public void BeltInput_CanAccept_EmptyBelt_ReturnsTrue()
    {
        var belt = new BeltSegment(5);
        var adapter = new BeltInputAdapter(belt);

        Assert.IsTrue(adapter.CanAccept("iron_ore"));
    }

    [Test]
    public void BeltInput_TryInsert_EmptyBelt_Succeeds()
    {
        var belt = new BeltSegment(5);
        var adapter = new BeltInputAdapter(belt);

        bool result = adapter.TryInsert("iron_ore");

        Assert.IsTrue(result);
        Assert.AreEqual(1, belt.ItemCount);
    }

    [Test]
    public void BeltInput_TryInsert_InsufficientSpacing_Fails()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, minSpacing: 200);

        // first insert always succeeds on empty belt
        adapter.TryInsert("iron_ore");

        // second insert needs 200 subdivisions of spacing but belt is only 1 tile (100 subdivisions)
        bool result = adapter.TryInsert("iron_ore");

        Assert.IsFalse(result);
    }

    [Test]
    public void BeltInput_CanAccept_InsufficientSpacing_ReturnsFalse()
    {
        var belt = new BeltSegment(1);
        var adapter = new BeltInputAdapter(belt, minSpacing: 200);

        adapter.TryInsert("iron_ore");

        Assert.IsFalse(adapter.CanAccept("iron_ore"));
    }

    [Test]
    public void BeltInput_NullBelt_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new BeltInputAdapter(null));
    }

    // ════════════════════════════════════════════════════
    // BeltOutputAdapter
    // ════════════════════════════════════════════════════

    [Test]
    public void BeltOutput_EmptyBelt_HasNoItemAvailable()
    {
        var belt = new BeltSegment(5);
        var adapter = new BeltOutputAdapter(belt);

        Assert.IsFalse(adapter.HasItemAvailable);
    }

    [Test]
    public void BeltOutput_PeekItemId_EmptyBelt_ReturnsNull()
    {
        var belt = new BeltSegment(5);
        var adapter = new BeltOutputAdapter(belt);

        Assert.IsNull(adapter.PeekItemId());
    }

    [Test]
    public void BeltOutput_TryExtract_EmptyBelt_Fails()
    {
        var belt = new BeltSegment(5);
        var adapter = new BeltOutputAdapter(belt);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsFalse(result);
        Assert.IsNull(itemId);
    }

    [Test]
    public void BeltOutput_ItemAtEnd_CanExtract()
    {
        var belt = new BeltSegment(1);
        belt.TryInsertAtStart("iron_ore", 0);

        // tick the belt until the item reaches the end
        for (int i = 0; i < 200; i++)
            belt.Tick(1);

        var adapter = new BeltOutputAdapter(belt);

        Assert.IsTrue(adapter.HasItemAvailable);
        Assert.AreEqual("iron_ore", adapter.PeekItemId());

        bool result = adapter.TryExtract(out string extracted);

        Assert.IsTrue(result);
        Assert.AreEqual("iron_ore", extracted);
        Assert.IsTrue(belt.IsEmpty);
    }

    [Test]
    public void BeltOutput_NullBelt_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new BeltOutputAdapter(null));
    }

    // ════════════════════════════════════════════════════
    // MachineInputAdapter
    // ════════════════════════════════════════════════════

    [Test]
    public void MachineInput_EmptySlot_CanAccept()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineInputAdapter(machine, 0);

        Assert.IsTrue(adapter.CanAccept("iron_ore"));
    }

    [Test]
    public void MachineInput_TryInsert_EmptySlot_Succeeds()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineInputAdapter(machine, 0);

        bool result = adapter.TryInsert("iron_ore");

        Assert.IsTrue(result);
        Assert.IsFalse(machine.GetInput(0).IsEmpty);
    }

    [Test]
    public void MachineInput_SameItemType_CanStack()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineInputAdapter(machine, 0);

        adapter.TryInsert("iron_ore");

        Assert.IsTrue(adapter.CanAccept("iron_ore"));
    }

    [Test]
    public void MachineInput_DifferentItemType_CannotAccept()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineInputAdapter(machine, 0);

        adapter.TryInsert("iron_ore");

        Assert.IsFalse(adapter.CanAccept("copper_ore"));
    }

    [Test]
    public void MachineInput_NullMachine_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new MachineInputAdapter(null, 0));
    }

    // ════════════════════════════════════════════════════
    // MachineOutputAdapter
    // ════════════════════════════════════════════════════

    [Test]
    public void MachineOutput_EmptySlot_HasNoItem()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineOutputAdapter(machine, 0);

        Assert.IsFalse(adapter.HasItemAvailable);
        Assert.IsNull(adapter.PeekItemId());
    }

    [Test]
    public void MachineOutput_TryExtract_EmptySlot_Fails()
    {
        var def = CreateMachineDef(inputSize: 1, outputSize: 1);
        var machine = new Machine(def);
        var adapter = new MachineOutputAdapter(machine, 0);

        bool result = adapter.TryExtract(out string itemId);

        Assert.IsFalse(result);
        Assert.IsNull(itemId);
    }

    [Test]
    public void MachineOutput_NullMachine_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new MachineOutputAdapter(null, 0));
    }

    // ── helpers ──────────────────────────────────────────

    private MachineDefinitionSO CreateMachineDef(int inputSize, int outputSize)
    {
        var def = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        def.machineId = "test_machine";
        def.displayName = "Test Machine";
        def.size = new Vector2Int(1, 1);
        def.machineType = "assembler";
        def.inputBufferSize = inputSize;
        def.outputBufferSize = outputSize;
        def.processingSpeed = 1f;
        def.powerConsumption = 0f;
        def.ports = new MachinePort[0];
        return def;
    }
}
