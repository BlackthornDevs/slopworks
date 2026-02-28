using System;
using NUnit.Framework;

[TestFixture]
public class BeltSegmentTests
{
    private const int DefaultLengthTiles = 3;
    private const int ExpectedLength = DefaultLengthTiles * BeltItem.SubdivisionsPerTile; // 300
    private const string IronOre = "iron_ore";
    private const string CopperOre = "copper_ore";
    private const string IronIngot = "iron_ingot";
    private const ushort DefaultSpacing = 50;

    private BeltSegment CreateBelt(int lengthInTiles = DefaultLengthTiles)
    {
        return new BeltSegment(lengthInTiles);
    }

    // -- Initial state --

    [Test]
    public void NewBelt_HasCorrectTotalLength()
    {
        var belt = CreateBelt();

        Assert.AreEqual(ExpectedLength, belt.TotalLength);
    }

    [Test]
    public void NewBelt_IsEmpty()
    {
        var belt = CreateBelt();

        Assert.IsTrue(belt.IsEmpty);
        Assert.AreEqual(0, belt.ItemCount);
    }

    // -- TryInsertAtStart --

    [Test]
    public void TryInsertAtStart_OnEmptyBelt_Succeeds()
    {
        var belt = CreateBelt();

        bool result = belt.TryInsertAtStart(IronOre, DefaultSpacing);

        Assert.IsTrue(result);
    }

    [Test]
    public void TryInsertAtStart_OnEmptyBelt_SetsItemCount()
    {
        var belt = CreateBelt();

        belt.TryInsertAtStart(IronOre, DefaultSpacing);

        Assert.AreEqual(1, belt.ItemCount);
        Assert.IsFalse(belt.IsEmpty);
    }

    [Test]
    public void TryInsertAtStart_RespectsMinSpacing()
    {
        var belt = CreateBelt();

        // Insert first item (distanceToNext = 0, at input edge)
        belt.TryInsertAtStart(IronOre, DefaultSpacing);

        // Immediately try second insert: first item's distanceToNext is 0,
        // which is less than minSpacing (50)
        bool result = belt.TryInsertAtStart(CopperOre, DefaultSpacing);

        Assert.IsFalse(result);
        Assert.AreEqual(1, belt.ItemCount);
    }

    [Test]
    public void TryInsertAtStart_SucceedsWhenGapIsSufficient()
    {
        var belt = CreateBelt();

        // Insert first item at input edge
        belt.TryInsertAtStart(IronOre, DefaultSpacing);

        // Tick moves the first item away from input end by DefaultSpacing subdivisions,
        // increasing items[0].distanceToNext to DefaultSpacing
        belt.Tick(DefaultSpacing);

        // Now first item's distanceToNext == DefaultSpacing, which meets minSpacing
        bool result = belt.TryInsertAtStart(CopperOre, DefaultSpacing);

        Assert.IsTrue(result);
        Assert.AreEqual(2, belt.ItemCount);
    }

    // -- TryExtractFromEnd --

    [Test]
    public void TryExtractFromEnd_OnEmptyBelt_ReturnsNull()
    {
        var belt = CreateBelt();

        string result = belt.TryExtractFromEnd();

        Assert.IsNull(result);
    }

    [Test]
    public void TryExtractFromEnd_WhenTerminalGapPositive_ReturnsNull()
    {
        var belt = CreateBelt();
        belt.TryInsertAtStart(IronOre, 0);

        // Terminal gap is 300 (full belt length), item hasn't reached end
        string result = belt.TryExtractFromEnd();

        Assert.IsNull(result);
    }

    // -- Tick --

    [Test]
    public void Tick_ReducesTerminalGap()
    {
        var belt = CreateBelt();
        belt.TryInsertAtStart(IronOre, 0);

        ushort initialGap = belt.TerminalGap;
        belt.Tick(10);

        Assert.AreEqual(initialGap - 10, belt.TerminalGap);
    }

    [Test]
    public void Tick_DoesNotReduceTerminalGapBelowZero()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        // Tick more than the total length
        belt.Tick(200);

        Assert.AreEqual(0, belt.TerminalGap);
    }

    [Test]
    public void Tick_OnEmptyBelt_DoesNothing()
    {
        var belt = CreateBelt();
        ushort gapBefore = belt.TerminalGap;

        belt.Tick(10);

        Assert.AreEqual(gapBefore, belt.TerminalGap);
    }

    [Test]
    public void Tick_IncreasesFirstItemDistanceFromInputEnd()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        Assert.AreEqual(0, belt.GetItems()[0].distanceToNext);

        belt.Tick(30);

        Assert.AreEqual(30, belt.GetItems()[0].distanceToNext);
    }

    [Test]
    public void Tick_PreservesLengthInvariant()
    {
        var belt = CreateBelt(2); // 200 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        // Invariant: sum(distanceToNext) + terminalGap = totalLength
        Assert.AreEqual(belt.TotalLength, belt.GetItems()[0].distanceToNext + belt.TerminalGap);

        belt.Tick(75);

        Assert.AreEqual(belt.TotalLength, belt.GetItems()[0].distanceToNext + belt.TerminalGap);
    }

    // -- Tick + Extract flow --

    [Test]
    public void TickThenExtract_ItemReachesEnd_ExtractSucceeds()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        // Tick the full length to move item to the output end
        belt.Tick(100);

        Assert.AreEqual(0, belt.TerminalGap);
        Assert.IsTrue(belt.HasItemAtEnd);

        string result = belt.TryExtractFromEnd();

        Assert.AreEqual(IronOre, result);
    }

    [Test]
    public void FullFlow_InsertTickExtract_BeltIsEmpty()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        Assert.IsFalse(belt.IsEmpty);

        belt.Tick(100);

        string result = belt.TryExtractFromEnd();

        Assert.AreEqual(IronOre, result);
        Assert.IsTrue(belt.IsEmpty);
        Assert.AreEqual(0, belt.ItemCount);
    }

    // -- Multiple items --

    [Test]
    public void MultipleItems_MaintainCorrectSpacing()
    {
        var belt = CreateBelt(3); // 300 subdivisions

        // Insert first item, then tick to create a gap
        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(DefaultSpacing); // iron now has distanceToNext = 50

        // Insert second item
        belt.TryInsertAtStart(CopperOre, DefaultSpacing);

        Assert.AreEqual(2, belt.ItemCount);

        var items = belt.GetItems();
        // Copper at input edge, iron further in
        Assert.AreEqual(CopperOre, items[0].itemId);
        Assert.AreEqual(IronOre, items[1].itemId);

        // Copper just inserted at input edge
        Assert.AreEqual(0, items[0].distanceToNext);
        // Iron retains its gap of 50 (distance from copper to iron)
        Assert.AreEqual(DefaultSpacing, items[1].distanceToNext);
    }

    [Test]
    public void MultipleItems_ExtractInFIFOOrder()
    {
        var belt = CreateBelt(2); // 200 subdivisions

        // Build a queue of three items with spacing between them:
        // 1. Insert iron ore, tick to create gap
        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(DefaultSpacing); // iron: d=50, tGap=150

        // 2. Insert copper ore, tick to create gap
        belt.TryInsertAtStart(CopperOre, DefaultSpacing);
        belt.Tick(DefaultSpacing); // copper: d=50, iron: d=50, tGap=100

        // 3. Insert iron ingot
        belt.TryInsertAtStart(IronIngot, DefaultSpacing);
        // ingot: d=0, copper: d=50, iron: d=50, tGap=100

        Assert.AreEqual(3, belt.ItemCount);

        // Tick to move everything to the end (terminalGap = 100, need 100 more ticks)
        belt.Tick(100);
        // ingot: d=100, copper: d=50, iron: d=50, tGap=0

        // Extract iron ore (last item, at output end)
        string first = belt.TryExtractFromEnd();
        Assert.AreEqual(IronOre, first);
        // Terminal gap is now iron's distanceToNext (50)
        Assert.AreEqual(50, belt.TerminalGap);

        // Tick to bring copper to the end
        belt.Tick(50);
        string second = belt.TryExtractFromEnd();
        Assert.AreEqual(CopperOre, second);
        // Terminal gap is now copper's distanceToNext (50)
        Assert.AreEqual(50, belt.TerminalGap);

        // Tick to bring ingot to the end
        belt.Tick(50);
        string third = belt.TryExtractFromEnd();
        Assert.AreEqual(IronIngot, third);

        Assert.IsTrue(belt.IsEmpty);
    }

    // -- Belt capacity --

    [Test]
    public void BeltCapacity_InsertUntilFull()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        ushort spacing = 25;

        // Insert first item (empty belt, always succeeds)
        Assert.IsTrue(belt.TryInsertAtStart(IronOre, spacing));

        // Create gap and insert three more times (100 / 25 = 4 items fit)
        belt.Tick(spacing);
        Assert.IsTrue(belt.TryInsertAtStart(CopperOre, spacing));

        belt.Tick(spacing);
        Assert.IsTrue(belt.TryInsertAtStart(IronIngot, spacing));

        belt.Tick(spacing);
        Assert.IsTrue(belt.TryInsertAtStart(IronOre, spacing));

        // Belt now has 4 items. After one more tick(25), terminalGap would be
        // 100 - 25*4 - 25 tick to first item... let's just check: no more room.
        // The 4th insert consumed the last gap. Next tick(25) would bring
        // terminalGap to 0 (belt is saturated). Another insert would still
        // succeed if first item's gap >= spacing, because insert only checks
        // items[0].distanceToNext. But the belt's invariant means there's
        // physically no space left. The minSpacing check at the input end is
        // the flow-control mechanism.

        // Without ticking, items[0].distanceToNext = 0 < 25, so reject
        bool result = belt.TryInsertAtStart("extra", spacing);
        Assert.IsFalse(result);

        Assert.AreEqual(4, belt.ItemCount);
    }

    // -- HasItemAtEnd --

    [Test]
    public void HasItemAtEnd_TrueWhenTerminalGapZeroAndBeltHasItems()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        Assert.IsFalse(belt.HasItemAtEnd);

        belt.Tick(100);

        Assert.IsTrue(belt.HasItemAtEnd);
    }

    [Test]
    public void HasItemAtEnd_FalseWhenBeltIsEmpty()
    {
        var belt = CreateBelt(1);

        Assert.IsFalse(belt.HasItemAtEnd);
    }

    // -- Constructor validation --

    [Test]
    public void Constructor_ZeroLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeltSegment(0));
    }

    [Test]
    public void Constructor_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BeltSegment(-1));
    }

    // -- Terminal gap after extraction --

    [Test]
    public void TryExtractFromEnd_WhenMultipleItems_SetsTerminalGapToRemovedItemDistance()
    {
        var belt = CreateBelt(2); // 200 subdivisions

        // Insert iron ore, tick to create gap of 50
        belt.TryInsertAtStart(IronOre, 0);
        belt.Tick(DefaultSpacing); // iron: d=50, tGap=150

        // Insert copper ore
        belt.TryInsertAtStart(CopperOre, DefaultSpacing);
        // copper: d=0, iron: d=50, tGap=150

        // Tick all the way to end
        belt.Tick(150);
        // copper: d=150, iron: d=50, tGap=0

        Assert.AreEqual(0, belt.TerminalGap);

        // Extract iron (last item)
        string extracted = belt.TryExtractFromEnd();
        Assert.AreEqual(IronOre, extracted);

        // Terminal gap becomes iron's distanceToNext (50)
        Assert.AreEqual(50, belt.TerminalGap);
        Assert.AreEqual(1, belt.ItemCount);
    }

    [Test]
    public void TryExtractFromEnd_LastItem_RestoresFullTerminalGap()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        // Move to end
        belt.Tick(100);

        // Extract the only item
        belt.TryExtractFromEnd();

        Assert.IsTrue(belt.IsEmpty);
        Assert.AreEqual(100, belt.TerminalGap);
    }

    // -- Tick with incremental movement --

    [Test]
    public void Tick_IncrementalMovement_ItemReachesEnd()
    {
        var belt = CreateBelt(1); // 100 subdivisions
        belt.TryInsertAtStart(IronOre, 0);

        for (int i = 0; i < 10; i++)
        {
            belt.Tick(10);
        }

        Assert.AreEqual(0, belt.TerminalGap);
        Assert.IsTrue(belt.HasItemAtEnd);

        string result = belt.TryExtractFromEnd();
        Assert.AreEqual(IronOre, result);
    }
}
