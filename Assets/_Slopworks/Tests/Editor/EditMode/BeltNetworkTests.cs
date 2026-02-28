using NUnit.Framework;

[TestFixture]
public class BeltNetworkTests
{
    private const string IronOre = "iron_ore";
    private const string CopperOre = "copper_ore";
    private const ushort DefaultSpeed = 100; // one tile per tick

    private BeltSegment CreateBelt(int lengthInTiles = 1)
    {
        return new BeltSegment(lengthInTiles);
    }

    /// <summary>
    /// Puts an item on the belt and ticks it to the output end.
    /// </summary>
    private void PlaceItemAtEnd(BeltSegment belt, string itemId)
    {
        belt.TryInsertAtStart(itemId, 0);
        belt.Tick((ushort)belt.TotalLength);
    }

    // -- Connect and disconnect --

    [Test]
    public void Connect_TwoBelts_IsConnectedReturnsTrue()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        network.Connect(a, b);

        Assert.IsTrue(network.IsConnected(a, b));
        Assert.AreEqual(1, network.ConnectionCount);
    }

    [Test]
    public void Disconnect_RemovesConnection()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        network.Connect(a, b);
        network.Disconnect(a, b);

        Assert.IsFalse(network.IsConnected(a, b));
        Assert.AreEqual(0, network.ConnectionCount);
    }

    [Test]
    public void Connect_SamePairTwice_DoesNotCreateDuplicate()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        network.Connect(a, b);
        network.Connect(a, b);

        Assert.AreEqual(1, network.ConnectionCount);
    }

    [Test]
    public void IsConnected_ReturnsFalse_WhenNotConnected()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        Assert.IsFalse(network.IsConnected(a, b));
    }

    [Test]
    public void Connect_NullFrom_DoesNotAdd()
    {
        var network = new BeltNetwork();
        var b = CreateBelt();

        network.Connect(null, b);

        Assert.AreEqual(0, network.ConnectionCount);
    }

    [Test]
    public void Connect_NullTo_DoesNotAdd()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();

        network.Connect(a, null);

        Assert.AreEqual(0, network.ConnectionCount);
    }

    // -- Tick: basic transfer --

    [Test]
    public void Tick_TransfersItemFromBeltAEndToBeltBStart()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        PlaceItemAtEnd(a, IronOre);
        Assert.IsTrue(a.HasItemAtEnd);

        network.Connect(a, b);
        network.Tick();

        Assert.IsTrue(a.IsEmpty, "Item should be extracted from belt A");
        Assert.AreEqual(1, b.ItemCount, "Item should be inserted into belt B");
    }

    [Test]
    public void Tick_DoesNotTransfer_WhenBeltAHasNoItemAtEnd()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        // Item on belt A but not at the end
        a.TryInsertAtStart(IronOre, 0);
        // Don't tick -- item is still at input end
        Assert.IsFalse(a.HasItemAtEnd);

        network.Connect(a, b);
        network.Tick();

        Assert.AreEqual(1, a.ItemCount, "Item should remain on belt A");
        Assert.IsTrue(b.IsEmpty, "Belt B should remain empty");
    }

    [Test]
    public void Tick_DoesNotTransfer_WhenBeltBRejects()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        // Put item at end of belt A
        PlaceItemAtEnd(a, IronOre);

        // Fill belt B at input so it rejects (insert an item with 0 spacing, no tick)
        b.TryInsertAtStart(CopperOre, 0);

        network.Connect(a, b);
        network.Tick();

        // Item extracted from A but held in transit (B rejected)
        Assert.IsTrue(a.IsEmpty, "Item was extracted from belt A");
        // Belt B still has only the original item
        Assert.AreEqual(1, b.ItemCount);
    }

    // -- Held item retry --

    [Test]
    public void Tick_HeldItemRetriesOnNextTick()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        PlaceItemAtEnd(a, IronOre);

        // Fill belt B input end so it rejects
        b.TryInsertAtStart(CopperOre, 0);

        network.Connect(a, b);
        network.Tick(); // item extracted from A, held in transit

        Assert.IsTrue(a.IsEmpty);
        Assert.AreEqual(1, b.ItemCount); // still just the copper ore

        // Make room on belt B by ticking it forward
        b.Tick(50); // move copper ore away from input, creating space

        network.Tick(); // retry should succeed now

        Assert.AreEqual(2, b.ItemCount, "Held iron ore should now be on belt B");
    }

    // -- Multiple connections --

    [Test]
    public void Tick_MultipleConnections_TickIndependently()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();
        var c = CreateBelt();
        var d = CreateBelt();

        PlaceItemAtEnd(a, IronOre);
        PlaceItemAtEnd(c, CopperOre);

        network.Connect(a, b);
        network.Connect(c, d);
        network.Tick();

        Assert.IsTrue(a.IsEmpty);
        Assert.AreEqual(1, b.ItemCount);
        Assert.IsTrue(c.IsEmpty);
        Assert.AreEqual(1, d.ItemCount);
    }

    // -- Three-belt chain --

    [Test]
    public void ThreeBeltChain_ItemFlowsThroughAllBelts()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();
        var c = CreateBelt();

        PlaceItemAtEnd(a, IronOre);

        network.Connect(a, b);
        network.Connect(b, c);

        // Tick 1: A -> B
        network.Tick();
        Assert.IsTrue(a.IsEmpty);
        Assert.AreEqual(1, b.ItemCount);
        Assert.IsTrue(c.IsEmpty);

        // Move item on B to its output end
        b.Tick(DefaultSpeed);
        Assert.IsTrue(b.HasItemAtEnd);

        // Tick 2: B -> C
        network.Tick();
        Assert.IsTrue(b.IsEmpty);
        Assert.AreEqual(1, c.ItemCount);
    }

    // -- ConnectionCount tracking --

    [Test]
    public void ConnectionCount_TracksCorrectly()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();
        var c = CreateBelt();

        Assert.AreEqual(0, network.ConnectionCount);

        network.Connect(a, b);
        Assert.AreEqual(1, network.ConnectionCount);

        network.Connect(b, c);
        Assert.AreEqual(2, network.ConnectionCount);

        network.Disconnect(a, b);
        Assert.AreEqual(1, network.ConnectionCount);

        network.Disconnect(b, c);
        Assert.AreEqual(0, network.ConnectionCount);
    }

    // -- Direction matters --

    [Test]
    public void Connect_IsDirectional_ReverseNotConnected()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        network.Connect(a, b);

        Assert.IsTrue(network.IsConnected(a, b));
        Assert.IsFalse(network.IsConnected(b, a), "Connection is one-way");
    }

    // -- Disconnect nonexistent pair --

    [Test]
    public void Disconnect_NonexistentPair_DoesNotThrow()
    {
        var network = new BeltNetwork();
        var a = CreateBelt();
        var b = CreateBelt();

        Assert.DoesNotThrow(() => network.Disconnect(a, b));
    }

    // -- Multiple items flowing through connection --

    [Test]
    public void MultipleItems_FlowThroughSameConnection()
    {
        var network = new BeltNetwork();
        var a = CreateBelt(2);
        var b = CreateBelt(2);

        // Put two items on belt A, spaced apart
        a.TryInsertAtStart(IronOre, 0);
        a.Tick(50);
        a.TryInsertAtStart(CopperOre, 50);
        // Move both to the end
        a.Tick(150);
        Assert.IsTrue(a.HasItemAtEnd);

        network.Connect(a, b);

        // Transfer first item
        network.Tick();
        Assert.AreEqual(1, a.ItemCount);
        Assert.AreEqual(1, b.ItemCount);

        // Move items: second item to end of A, first item away from input on B
        a.Tick(50);
        b.Tick(50); // make room at B's input end for the next insertion
        Assert.IsTrue(a.HasItemAtEnd);

        // Transfer second item
        network.Tick();
        Assert.IsTrue(a.IsEmpty);
        Assert.AreEqual(2, b.ItemCount);
    }
}
