using NUnit.Framework;

[TestFixture]
public class InventoryTests
{
    private const string IronOre = "iron_ore";
    private const string CopperOre = "copper_ore";
    private const int DefaultMaxStack = 64;

    private Inventory CreateInventory(int slots = 5)
    {
        return new Inventory(slots, _ => DefaultMaxStack);
    }

    private ItemInstance CreateItem(string definitionId)
    {
        return ItemInstance.Create(definitionId);
    }

    [Test]
    public void TryAdd_ToEmptyInventory_Succeeds()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        bool result = inventory.TryAdd(item, 10);

        Assert.IsTrue(result);
        Assert.AreEqual(10, inventory.GetCount(IronOre));
    }

    [Test]
    public void TryAdd_ReturnsItemInCorrectSlot()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 5);

        var slot = inventory.GetSlot(0);
        Assert.IsFalse(slot.IsEmpty);
        Assert.AreEqual(IronOre, slot.item.definitionId);
        Assert.AreEqual(5, slot.count);
    }

    [Test]
    public void TryAdd_StacksSameItemType()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 10);
        inventory.TryAdd(item, 20);

        Assert.AreEqual(30, inventory.GetCount(IronOre));

        // Should be in one slot, not two
        var slot0 = inventory.GetSlot(0);
        Assert.AreEqual(30, slot0.count);
        Assert.IsTrue(inventory.GetSlot(1).IsEmpty);
    }

    [Test]
    public void TryAdd_RespectsMaxStackSize_SpillsToNextSlot()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        // Add more than one stack can hold
        inventory.TryAdd(item, DefaultMaxStack + 10);

        Assert.AreEqual(DefaultMaxStack + 10, inventory.GetCount(IronOre));

        var slot0 = inventory.GetSlot(0);
        Assert.AreEqual(DefaultMaxStack, slot0.count);

        var slot1 = inventory.GetSlot(1);
        Assert.AreEqual(10, slot1.count);
    }

    [Test]
    public void TryAdd_ToFullInventory_ReturnsFalse()
    {
        // 2 slots, max 64 each = 128 total capacity
        var inventory = CreateInventory(2);
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, DefaultMaxStack * 2);

        // Inventory is now full
        bool result = inventory.TryAdd(item, 1);

        Assert.IsFalse(result);
        Assert.AreEqual(DefaultMaxStack * 2, inventory.GetCount(IronOre));
    }

    [Test]
    public void TryRemove_ReducesCount()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 20);
        bool result = inventory.TryRemove(IronOre, 5);

        Assert.IsTrue(result);
        Assert.AreEqual(15, inventory.GetCount(IronOre));
    }

    [Test]
    public void TryRemove_FromMultipleSlots()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        // Fill two slots
        inventory.TryAdd(item, DefaultMaxStack + 10);

        // Remove enough to span both slots
        bool result = inventory.TryRemove(IronOre, DefaultMaxStack + 5);

        Assert.IsTrue(result);
        Assert.AreEqual(5, inventory.GetCount(IronOre));
    }

    [Test]
    public void TryRemove_MoreThanAvailable_ReturnsFalse()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 10);
        bool result = inventory.TryRemove(IronOre, 20);

        Assert.IsFalse(result);
        // Count should be unchanged since removal failed
        Assert.AreEqual(10, inventory.GetCount(IronOre));
    }

    [Test]
    public void GetCount_ReturnsTotalAcrossAllSlots()
    {
        var inventory = CreateInventory();
        var iron = CreateItem(IronOre);
        var copper = CreateItem(CopperOre);

        inventory.TryAdd(iron, DefaultMaxStack + 20);
        inventory.TryAdd(copper, 15);

        Assert.AreEqual(DefaultMaxStack + 20, inventory.GetCount(IronOre));
        Assert.AreEqual(15, inventory.GetCount(CopperOre));
    }

    [Test]
    public void HasSpace_ReturnsTrueWhenSpaceAvailable()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 10);

        bool result = inventory.HasSpace(IronOre, 20, DefaultMaxStack);

        Assert.IsTrue(result);
    }

    [Test]
    public void HasSpace_ReturnsFalseWhenFull()
    {
        var inventory = CreateInventory(1);
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, DefaultMaxStack);

        bool result = inventory.HasSpace(IronOre, 1, DefaultMaxStack);

        Assert.IsFalse(result);
    }

    [Test]
    public void Clear_EmptiesAllSlots()
    {
        var inventory = CreateInventory();
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 50);
        inventory.Clear();

        Assert.AreEqual(0, inventory.GetCount(IronOre));

        for (int i = 0; i < inventory.SlotCount; i++)
            Assert.IsTrue(inventory.GetSlot(i).IsEmpty);
    }

    [Test]
    public void TryAdd_FullInventory_DoesNotPartiallyAdd()
    {
        // Verify atomic behavior: if TryAdd fails, nothing changes
        var inventory = CreateInventory(1);
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 60);

        // Try to add more than remaining space (4 left, try to add 10)
        bool result = inventory.TryAdd(item, 10);

        Assert.IsFalse(result);
        Assert.AreEqual(60, inventory.GetCount(IronOre));
    }

    [Test]
    public void OnSlotChanged_fires_when_item_added()
    {
        var inventory = new Inventory(9, _ => 64);
        int firedSlot = -1;
        inventory.OnSlotChanged += (index) => firedSlot = index;

        inventory.TryAdd(ItemInstance.Create("iron_scrap"), 1);

        Assert.AreNotEqual(-1, firedSlot);
    }

    [Test]
    public void OnSlotChanged_fires_when_item_removed()
    {
        var inventory = new Inventory(9, _ => 64);
        inventory.TryAdd(ItemInstance.Create("iron_scrap"), 5);
        int firedSlot = -1;
        inventory.OnSlotChanged += (index) => firedSlot = index;

        inventory.TryRemove("iron_scrap", 3);

        Assert.AreNotEqual(-1, firedSlot);
    }

    [Test]
    public void GetAllSlots_ReturnsCopy()
    {
        var inventory = CreateInventory(3);
        var item = CreateItem(IronOre);

        inventory.TryAdd(item, 10);

        var slots = inventory.GetAllSlots();
        Assert.AreEqual(3, slots.Length);
        Assert.AreEqual(10, slots[0].count);

        // Modifying the copy should not affect the inventory
        slots[0].count = 999;
        Assert.AreEqual(10, inventory.GetSlot(0).count);
    }

    [Test]
    public void SetSlot_replaces_contents_and_fires_event()
    {
        var inventory = new Inventory(9, _ => 64);
        inventory.TryAdd(ItemInstance.Create("iron"), 5);

        int firedSlot = -1;
        inventory.OnSlotChanged += (i) => firedSlot = i;

        var newSlot = new ItemSlot { item = ItemInstance.Create("copper"), count = 3 };
        inventory.SetSlot(0, newSlot);

        Assert.AreEqual(0, firedSlot);
        Assert.AreEqual("copper", inventory.GetSlot(0).item.definitionId);
        Assert.AreEqual(3, inventory.GetSlot(0).count);
    }

    [Test]
    public void SwapSlots_exchanges_two_slots()
    {
        var inventory = new Inventory(9, _ => 64);
        inventory.TryAdd(ItemInstance.Create("iron"), 5);

        inventory.SwapSlots(0, 1);

        Assert.IsTrue(inventory.GetSlot(0).IsEmpty);
        Assert.AreEqual("iron", inventory.GetSlot(1).item.definitionId);
        Assert.AreEqual(5, inventory.GetSlot(1).count);
    }
}
