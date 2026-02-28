using System;
using NUnit.Framework;

[TestFixture]
public class StorageContainerTests
{
    private const int DefaultSlotCount = 4;
    private const int DefaultMaxStack = 10;
    private const string IronOre = "iron_ore";
    private const string CopperOre = "copper_ore";
    private const string Coal = "coal";

    private StorageContainer CreateContainer(int slotCount = DefaultSlotCount, int maxStack = DefaultMaxStack)
    {
        return new StorageContainer(slotCount, maxStack);
    }

    // -- Insert single item --

    [Test]
    public void TryInsert_SingleItem_Succeeds()
    {
        var container = CreateContainer();

        bool result = container.TryInsert(IronOre);

        Assert.IsTrue(result);
        Assert.AreEqual(1, container.GetTotalItemCount());
    }

    // -- Stacking up to maxStackSize --

    [Test]
    public void TryInsert_FillsSlotUpToMaxStackSize()
    {
        var container = CreateContainer(slotCount: 1, maxStack: 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(container.TryInsert(IronOre));
        }

        Assert.AreEqual(5, container.GetSlot(0).count);
        Assert.AreEqual(5, container.GetTotalItemCount());
    }

    // -- Overflow to next slot --

    [Test]
    public void TryInsert_OverflowsToNextSlotWhenCurrentSlotFull()
    {
        var container = CreateContainer(slotCount: 2, maxStack: 3);

        // Fill first slot
        for (int i = 0; i < 3; i++)
            container.TryInsert(IronOre);

        // This should go to slot 1
        bool result = container.TryInsert(IronOre);

        Assert.IsTrue(result);
        Assert.AreEqual(3, container.GetSlot(0).count);
        Assert.AreEqual(1, container.GetSlot(1).count);
    }

    // -- Insert fails when full --

    [Test]
    public void TryInsert_FailsWhenAllSlotsFull()
    {
        var container = CreateContainer(slotCount: 1, maxStack: 2);

        container.TryInsert(IronOre);
        container.TryInsert(IronOre);

        bool result = container.TryInsert(IronOre);

        Assert.IsFalse(result);
        Assert.AreEqual(2, container.GetTotalItemCount());
    }

    // -- TryInsertStack atomic success --

    [Test]
    public void TryInsertStack_SucceedsWhenEnoughRoom()
    {
        var container = CreateContainer(slotCount: 2, maxStack: 5);

        bool result = container.TryInsertStack(IronOre, 8);

        Assert.IsTrue(result);
        Assert.AreEqual(8, container.GetTotalItemCount());
        Assert.AreEqual(5, container.GetSlot(0).count);
        Assert.AreEqual(3, container.GetSlot(1).count);
    }

    // -- TryInsertStack atomic failure --

    [Test]
    public void TryInsertStack_FailsAndDoesNotModifyStateWhenNotEnoughRoom()
    {
        var container = CreateContainer(slotCount: 1, maxStack: 5);

        // Pre-fill with 3 items
        container.TryInsertStack(IronOre, 3);

        // Try to insert 5 more (only 2 slots of space remain)
        bool result = container.TryInsertStack(IronOre, 5);

        Assert.IsFalse(result);
        Assert.AreEqual(3, container.GetTotalItemCount());
        Assert.AreEqual(3, container.GetSlot(0).count);
    }

    // -- Extract single item --

    [Test]
    public void TryExtract_RemovesOneItemFromFirstNonEmptySlot()
    {
        var container = CreateContainer();
        container.TryInsert(IronOre);
        container.TryInsert(IronOre);

        bool result = container.TryExtract(out string itemId);

        Assert.IsTrue(result);
        Assert.AreEqual(IronOre, itemId);
        Assert.AreEqual(1, container.GetTotalItemCount());
    }

    // -- Extract reduces count --

    [Test]
    public void TryExtract_ReducesCountByOne()
    {
        var container = CreateContainer();
        container.TryInsertStack(IronOre, 5);

        container.TryExtract(out _);

        Assert.AreEqual(4, container.GetSlot(0).count);
    }

    // -- ExtractAll --

    [Test]
    public void ExtractAll_RemovesAllOfSpecificItemAcrossSlots()
    {
        var container = CreateContainer(slotCount: 3, maxStack: 5);

        // Fill slot 0 with iron, slot 1 with copper, slot 2 with iron
        container.TryInsertStack(IronOre, 5);
        container.TryInsert(CopperOre);
        container.TryInsertStack(IronOre, 3);

        int removed = container.ExtractAll(IronOre);

        Assert.AreEqual(8, removed);
        Assert.AreEqual(0, container.GetCount(IronOre));
        Assert.AreEqual(1, container.GetCount(CopperOre));
    }

    // -- GetCount --

    [Test]
    public void GetCount_ReturnsCorrectCountAcrossSlots()
    {
        var container = CreateContainer(slotCount: 3, maxStack: 5);

        container.TryInsertStack(IronOre, 5);
        container.TryInsert(CopperOre);
        container.TryInsertStack(IronOre, 2);

        Assert.AreEqual(7, container.GetCount(IronOre));
        Assert.AreEqual(1, container.GetCount(CopperOre));
        Assert.AreEqual(0, container.GetCount(Coal));
    }

    // -- IItemSource.HasItemAvailable --

    [Test]
    public void HasItemAvailable_ReturnsTrueWhenItemsExist()
    {
        var container = CreateContainer();
        container.TryInsert(IronOre);

        Assert.IsTrue(container.HasItemAvailable);
    }

    [Test]
    public void HasItemAvailable_ReturnsFalseWhenEmpty()
    {
        var container = CreateContainer();

        Assert.IsFalse(container.HasItemAvailable);
    }

    // -- IItemSource.TryExtract returns correct itemId --

    [Test]
    public void TryExtract_ReturnsCorrectItemId()
    {
        var container = CreateContainer();
        container.TryInsert(CopperOre);

        container.TryExtract(out string itemId);

        Assert.AreEqual(CopperOre, itemId);
    }

    // -- IItemDestination.CanAccept --

    [Test]
    public void CanAccept_ReturnsTrueWhenRoomExists()
    {
        var container = CreateContainer();

        Assert.IsTrue(container.CanAccept(IronOre));
    }

    [Test]
    public void CanAccept_ReturnsTrueForMatchingStackWithRoom()
    {
        var container = CreateContainer(slotCount: 1, maxStack: 5);
        container.TryInsert(IronOre);

        Assert.IsTrue(container.CanAccept(IronOre));
    }

    [Test]
    public void CanAccept_ReturnsFalseWhenFullAndNoMatchingSlot()
    {
        var container = CreateContainer(slotCount: 1, maxStack: 2);
        container.TryInsertStack(CopperOre, 2);

        Assert.IsFalse(container.CanAccept(IronOre));
    }

    // -- IItemDestination.TryInsert adds to matching stack first --

    [Test]
    public void TryInsert_AddsToMatchingStackFirst()
    {
        var container = CreateContainer(slotCount: 3, maxStack: 5);

        // Put iron in slot 0, copper in slot 1
        container.TryInsert(IronOre);
        container.TryInsert(CopperOre);

        // Insert more iron -- should go to slot 0, not slot 2
        container.TryInsert(IronOre);

        Assert.AreEqual(2, container.GetSlot(0).count);
        Assert.AreEqual(IronOre, container.GetSlot(0).item.definitionId);
        Assert.IsTrue(container.GetSlot(2).IsEmpty);
    }

    // -- IsEmpty --

    [Test]
    public void IsEmpty_ReturnsTrueForNewContainer()
    {
        var container = CreateContainer();

        Assert.IsTrue(container.IsEmpty);
    }

    [Test]
    public void IsEmpty_ReturnsFalseAfterInsert()
    {
        var container = CreateContainer();
        container.TryInsert(IronOre);

        Assert.IsFalse(container.IsEmpty);
    }

    // -- IsFull --

    [Test]
    public void IsFull_ReturnsTrueWhenCompletelyFull()
    {
        var container = CreateContainer(slotCount: 2, maxStack: 3);

        container.TryInsertStack(IronOre, 6);

        Assert.IsTrue(container.IsFull);
    }

    [Test]
    public void IsFull_ReturnsFalseWhenNotFull()
    {
        var container = CreateContainer(slotCount: 2, maxStack: 3);

        container.TryInsertStack(IronOre, 5);

        Assert.IsFalse(container.IsFull);
    }

    // -- PeekItemId --

    [Test]
    public void PeekItemId_ReturnsFirstNonEmptySlotDefinitionId()
    {
        var container = CreateContainer();
        container.TryInsert(CopperOre);

        string peeked = container.PeekItemId();

        Assert.AreEqual(CopperOre, peeked);
        // Peek should not remove the item
        Assert.AreEqual(1, container.GetTotalItemCount());
    }

    [Test]
    public void PeekItemId_ReturnsNullWhenEmpty()
    {
        var container = CreateContainer();

        Assert.IsNull(container.PeekItemId());
    }

    // -- Constructor validation --

    [Test]
    public void Constructor_ZeroSlotCount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StorageContainer(0, 10));
    }

    [Test]
    public void Constructor_ZeroMaxStack_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StorageContainer(4, 0));
    }

    // -- Extract empties slot when count reaches zero --

    [Test]
    public void TryExtract_EmptiesSlotWhenCountReachesZero()
    {
        var container = CreateContainer();
        container.TryInsert(IronOre);

        container.TryExtract(out _);

        Assert.IsTrue(container.GetSlot(0).IsEmpty);
        Assert.IsTrue(container.IsEmpty);
    }

    // -- TryExtract on empty container --

    [Test]
    public void TryExtract_ReturnsFalseWhenEmpty()
    {
        var container = CreateContainer();

        bool result = container.TryExtract(out string itemId);

        Assert.IsFalse(result);
        Assert.IsNull(itemId);
    }

    // -- Mixed types in multiple slots --

    [Test]
    public void MixedTypes_StackCorrectlyInSeparateSlots()
    {
        var container = CreateContainer(slotCount: 4, maxStack: 5);

        container.TryInsertStack(IronOre, 5);
        container.TryInsertStack(CopperOre, 3);
        container.TryInsert(IronOre);

        // Iron should overflow to slot 2 (slot 0 full, slot 1 has copper)
        Assert.AreEqual(5, container.GetSlot(0).count);
        Assert.AreEqual(IronOre, container.GetSlot(0).item.definitionId);
        Assert.AreEqual(3, container.GetSlot(1).count);
        Assert.AreEqual(CopperOre, container.GetSlot(1).item.definitionId);
        Assert.AreEqual(1, container.GetSlot(2).count);
        Assert.AreEqual(IronOre, container.GetSlot(2).item.definitionId);
    }
}
