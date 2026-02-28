using System;
using NUnit.Framework;

[TestFixture]
public class InventoryContainerTests
{
    private const string IronOre = "iron_ore";
    private const string CopperOre = "copper_ore";

    // -- TryInsert --

    [Test]
    public void TryInsert_EmptySlot_Succeeds()
    {
        var container = new InventoryContainer(3);

        bool result = container.TryInsert(0, ItemInstance.Create(IronOre), 5);

        Assert.IsTrue(result);
    }

    [Test]
    public void TryInsert_ReturnsCorrectItem_ViaGetSlot()
    {
        var container = new InventoryContainer(3);

        container.TryInsert(0, ItemInstance.Create(IronOre), 5);

        var slot = container.GetSlot(0);
        Assert.AreEqual(IronOre, slot.item.definitionId);
        Assert.AreEqual(5, slot.count);
    }

    [Test]
    public void TryInsert_WithAcceptingFilter_Succeeds()
    {
        var container = new InventoryContainer(3);
        container.SetSlotFilter(0, id => id == IronOre);

        bool result = container.TryInsert(0, ItemInstance.Create(IronOre), 3);

        Assert.IsTrue(result);
        Assert.AreEqual(3, container.GetSlot(0).count);
    }

    [Test]
    public void TryInsert_WithRejectingFilter_ReturnsFalse()
    {
        var container = new InventoryContainer(3);
        container.SetSlotFilter(0, id => id == CopperOre);

        bool result = container.TryInsert(0, ItemInstance.Create(IronOre), 3);

        Assert.IsFalse(result);
        Assert.IsTrue(container.GetSlot(0).IsEmpty);
    }

    [Test]
    public void TryInsert_SameItemType_Stacks()
    {
        var container = new InventoryContainer(3);

        container.TryInsert(0, ItemInstance.Create(IronOre), 3);
        bool result = container.TryInsert(0, ItemInstance.Create(IronOre), 2);

        Assert.IsTrue(result);
        Assert.AreEqual(5, container.GetSlot(0).count);
    }

    [Test]
    public void TryInsert_DifferentItemType_ToOccupiedSlot_ReturnsFalse()
    {
        var container = new InventoryContainer(3);

        container.TryInsert(0, ItemInstance.Create(IronOre), 1);
        bool result = container.TryInsert(0, ItemInstance.Create(CopperOre), 1);

        Assert.IsFalse(result);
        Assert.AreEqual(IronOre, container.GetSlot(0).item.definitionId);
        Assert.AreEqual(1, container.GetSlot(0).count);
    }

    [Test]
    public void TryInsert_InvalidSlotIndex_ReturnsFalse()
    {
        var container = new InventoryContainer(3);

        Assert.IsFalse(container.TryInsert(-1, ItemInstance.Create(IronOre), 1));
        Assert.IsFalse(container.TryInsert(99, ItemInstance.Create(IronOre), 1));
    }

    [Test]
    public void TryInsert_EmptyItem_ReturnsFalse()
    {
        var container = new InventoryContainer(3);

        Assert.IsFalse(container.TryInsert(0, ItemInstance.Empty, 1));
    }

    [Test]
    public void TryInsert_ZeroCount_ReturnsFalse()
    {
        var container = new InventoryContainer(3);

        Assert.IsFalse(container.TryInsert(0, ItemInstance.Create(IronOre), 0));
    }

    // -- Extract --

    [Test]
    public void Extract_ReturnsItems_AndReducesSlotCount()
    {
        var container = new InventoryContainer(3);
        container.TryInsert(0, ItemInstance.Create(IronOre), 5);

        var extracted = container.Extract(0, 5);

        Assert.AreEqual(IronOre, extracted.item.definitionId);
        Assert.AreEqual(5, extracted.count);
        Assert.IsTrue(container.GetSlot(0).IsEmpty);
    }

    [Test]
    public void Extract_Partial_ReturnsCorrectAmount()
    {
        var container = new InventoryContainer(3);
        container.TryInsert(0, ItemInstance.Create(IronOre), 5);

        var extracted = container.Extract(0, 3);

        Assert.AreEqual(3, extracted.count);
        Assert.AreEqual(2, container.GetSlot(0).count);
    }

    [Test]
    public void Extract_FromEmptySlot_ReturnsEmpty()
    {
        var container = new InventoryContainer(3);

        var extracted = container.Extract(0, 5);

        Assert.IsTrue(extracted.IsEmpty);
    }

    [Test]
    public void Extract_MoreThanAvailable_ReturnsOnlyAvailable()
    {
        var container = new InventoryContainer(3);
        container.TryInsert(0, ItemInstance.Create(IronOre), 3);

        var extracted = container.Extract(0, 10);

        Assert.AreEqual(3, extracted.count);
        Assert.IsTrue(container.GetSlot(0).IsEmpty);
    }

    [Test]
    public void Extract_InvalidSlotIndex_ReturnsEmpty()
    {
        var container = new InventoryContainer(3);

        Assert.IsTrue(container.Extract(-1, 1).IsEmpty);
        Assert.IsTrue(container.Extract(99, 1).IsEmpty);
    }

    // -- GetCount --

    [Test]
    public void GetCount_AcrossMultipleSlots()
    {
        var container = new InventoryContainer(3);
        container.TryInsert(0, ItemInstance.Create(IronOre), 3);
        container.TryInsert(1, ItemInstance.Create(IronOre), 2);
        container.TryInsert(2, ItemInstance.Create(CopperOre), 4);

        Assert.AreEqual(5, container.GetCount(IronOre));
        Assert.AreEqual(4, container.GetCount(CopperOre));
    }

    [Test]
    public void GetCount_ItemNotPresent_ReturnsZero()
    {
        var container = new InventoryContainer(3);

        Assert.AreEqual(0, container.GetCount(IronOre));
    }

    // -- Clear --

    [Test]
    public void Clear_EmptiesAllSlots()
    {
        var container = new InventoryContainer(3);
        container.TryInsert(0, ItemInstance.Create(IronOre), 3);
        container.TryInsert(1, ItemInstance.Create(CopperOre), 2);

        container.Clear();

        Assert.IsTrue(container.GetSlot(0).IsEmpty);
        Assert.IsTrue(container.GetSlot(1).IsEmpty);
        Assert.IsTrue(container.GetSlot(2).IsEmpty);
    }

    // -- SetSlotFilter --

    [Test]
    public void SetSlotFilter_Null_AcceptsAnything()
    {
        var container = new InventoryContainer(3);
        // Set a restrictive filter first, then clear it
        container.SetSlotFilter(0, id => id == CopperOre);
        container.SetSlotFilter(0, null);

        bool result = container.TryInsert(0, ItemInstance.Create(IronOre), 1);

        Assert.IsTrue(result);
    }

    [Test]
    public void SetSlotFilter_InvalidIndex_Throws()
    {
        var container = new InventoryContainer(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => container.SetSlotFilter(-1, _ => true));
        Assert.Throws<ArgumentOutOfRangeException>(() => container.SetSlotFilter(99, _ => true));
    }

    // -- Constructor validation --

    [Test]
    public void Constructor_ZeroSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InventoryContainer(0));
    }

    [Test]
    public void Constructor_NegativeSlots_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InventoryContainer(-1));
    }

    // -- SlotCount --

    [Test]
    public void SlotCount_ReturnsCorrectValue()
    {
        var container = new InventoryContainer(5);

        Assert.AreEqual(5, container.SlotCount);
    }
}
