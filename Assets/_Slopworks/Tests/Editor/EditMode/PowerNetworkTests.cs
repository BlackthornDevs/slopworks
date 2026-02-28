using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class PowerNetworkTests
{
    private PowerNetworkManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = new PowerNetworkManager();
    }

    // -- Helper methods --

    private SimplePowerNode CreateGenerator(string id, float watts)
    {
        return new SimplePowerNode(id, watts, 0f);
    }

    private SimplePowerNode CreateConsumer(string id, float watts)
    {
        return new SimplePowerNode(id, 0f, watts);
    }

    private SimplePowerNode CreateNode(string id, float generation, float consumption)
    {
        return new SimplePowerNode(id, generation, consumption);
    }

    // ========================================================================
    // Single network tests
    // ========================================================================

    [Test]
    public void SingleGenerator_SatisfactionIsOne()
    {
        var gen = CreateGenerator("gen1", 100f);
        _manager.RegisterNode(gen);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen);

        Assert.IsNotNull(network);
        Assert.AreEqual(100f, network.TotalGeneration);
        Assert.AreEqual(0f, network.TotalConsumption);
        Assert.AreEqual(1f, network.Satisfaction);
    }

    [Test]
    public void SingleConsumer_SatisfactionIsZero()
    {
        var consumer = CreateConsumer("con1", 50f);
        _manager.RegisterNode(consumer);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(consumer);

        Assert.IsNotNull(network);
        Assert.AreEqual(0f, network.TotalGeneration);
        Assert.AreEqual(50f, network.TotalConsumption);
        Assert.AreEqual(0f, network.Satisfaction);
    }

    [Test]
    public void GeneratorAndConsumer_GenerationExceedsConsumption_SatisfactionIsOne()
    {
        var gen = CreateGenerator("gen1", 200f);
        var con = CreateConsumer("con1", 100f);
        gen.ConnectTo(con);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen);

        Assert.AreEqual(1f, network.Satisfaction);
    }

    [Test]
    public void GeneratorAndConsumer_GenerationEqualsConsumption_SatisfactionIsOne()
    {
        var gen = CreateGenerator("gen1", 100f);
        var con = CreateConsumer("con1", 100f);
        gen.ConnectTo(con);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen);

        Assert.AreEqual(1f, network.Satisfaction);
    }

    [Test]
    public void GeneratorAndConsumer_GenerationLessThanConsumption_SatisfactionIsRatio()
    {
        var gen = CreateGenerator("gen1", 75f);
        var con = CreateConsumer("con1", 100f);
        gen.ConnectTo(con);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen);

        Assert.AreEqual(0.75f, network.Satisfaction, 0.001f);
    }

    [Test]
    public void MultipleGenerators_SumCorrectly()
    {
        var gen1 = CreateGenerator("gen1", 50f);
        var gen2 = CreateGenerator("gen2", 75f);
        var con = CreateConsumer("con1", 100f);
        gen1.ConnectTo(gen2);
        gen2.ConnectTo(con);

        _manager.RegisterNode(gen1);
        _manager.RegisterNode(gen2);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen1);

        Assert.AreEqual(125f, network.TotalGeneration, 0.001f);
        Assert.AreEqual(1f, network.Satisfaction);
    }

    [Test]
    public void MultipleConsumers_SumCorrectly()
    {
        var gen = CreateGenerator("gen1", 100f);
        var con1 = CreateConsumer("con1", 40f);
        var con2 = CreateConsumer("con2", 60f);
        gen.ConnectTo(con1);
        gen.ConnectTo(con2);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con1);
        _manager.RegisterNode(con2);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen);

        Assert.AreEqual(100f, network.TotalConsumption, 0.001f);
        Assert.AreEqual(1f, network.Satisfaction);
    }

    [Test]
    public void ZeroConsumptionNetwork_SatisfactionIsOne()
    {
        var gen1 = CreateGenerator("gen1", 50f);
        var gen2 = CreateGenerator("gen2", 50f);
        gen1.ConnectTo(gen2);

        _manager.RegisterNode(gen1);
        _manager.RegisterNode(gen2);
        _manager.Rebuild();

        var network = _manager.GetNetworkForNode(gen1);

        Assert.AreEqual(0f, network.TotalConsumption);
        Assert.AreEqual(1f, network.Satisfaction);
    }

    // ========================================================================
    // Topology tests (BFS flood-fill)
    // ========================================================================

    [Test]
    public void TwoIsolatedNodes_FormTwoSeparateNetworks()
    {
        var gen = CreateGenerator("gen1", 100f);
        var con = CreateConsumer("con1", 50f);
        // No connection between them

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        Assert.AreEqual(2, _manager.NetworkCount);

        var genNetwork = _manager.GetNetworkForNode(gen);
        var conNetwork = _manager.GetNetworkForNode(con);

        Assert.AreNotSame(genNetwork, conNetwork);
        Assert.AreEqual(1, genNetwork.NodeCount);
        Assert.AreEqual(1, conNetwork.NodeCount);
    }

    [Test]
    public void ThreeNodesInChain_FormOneNetwork()
    {
        var a = CreateGenerator("a", 100f);
        var b = CreateNode("b", 0f, 0f); // relay node
        var c = CreateConsumer("c", 50f);
        a.ConnectTo(b);
        b.ConnectTo(c);

        _manager.RegisterNode(a);
        _manager.RegisterNode(b);
        _manager.RegisterNode(c);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);

        var network = _manager.GetNetworkForNode(a);
        Assert.AreEqual(3, network.NodeCount);
        Assert.AreSame(network, _manager.GetNetworkForNode(b));
        Assert.AreSame(network, _manager.GetNetworkForNode(c));
    }

    [Test]
    public void TwoDisconnectedPairs_FormTwoNetworks()
    {
        var gen1 = CreateGenerator("gen1", 100f);
        var con1 = CreateConsumer("con1", 50f);
        gen1.ConnectTo(con1);

        var gen2 = CreateGenerator("gen2", 200f);
        var con2 = CreateConsumer("con2", 150f);
        gen2.ConnectTo(con2);

        _manager.RegisterNode(gen1);
        _manager.RegisterNode(con1);
        _manager.RegisterNode(gen2);
        _manager.RegisterNode(con2);
        _manager.Rebuild();

        Assert.AreEqual(2, _manager.NetworkCount);

        var network1 = _manager.GetNetworkForNode(gen1);
        var network2 = _manager.GetNetworkForNode(gen2);

        Assert.AreNotSame(network1, network2);
        Assert.AreEqual(2, network1.NodeCount);
        Assert.AreEqual(2, network2.NodeCount);
    }

    [Test]
    public void AddingBridgeBetweenNetworks_MergesOnRebuild()
    {
        var gen1 = CreateGenerator("gen1", 100f);
        var con1 = CreateConsumer("con1", 50f);

        var gen2 = CreateGenerator("gen2", 200f);
        var con2 = CreateConsumer("con2", 150f);

        gen1.ConnectTo(con1);
        gen2.ConnectTo(con2);

        _manager.RegisterNode(gen1);
        _manager.RegisterNode(con1);
        _manager.RegisterNode(gen2);
        _manager.RegisterNode(con2);
        _manager.Rebuild();

        Assert.AreEqual(2, _manager.NetworkCount);

        // Bridge the two networks
        con1.ConnectTo(gen2);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);
        Assert.AreEqual(4, _manager.Networks[0].NodeCount);
        Assert.AreEqual(300f, _manager.Networks[0].TotalGeneration, 0.001f);
        Assert.AreEqual(200f, _manager.Networks[0].TotalConsumption, 0.001f);
    }

    [Test]
    public void RemovingNode_SplitsNetworkOnRebuild()
    {
        // Chain: gen1 - relay - con1
        var gen1 = CreateGenerator("gen1", 100f);
        var relay = CreateNode("relay", 0f, 0f);
        var con1 = CreateConsumer("con1", 50f);
        gen1.ConnectTo(relay);
        relay.ConnectTo(con1);

        _manager.RegisterNode(gen1);
        _manager.RegisterNode(relay);
        _manager.RegisterNode(con1);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);

        // Remove the relay, splitting the network
        _manager.UnregisterNode(relay);
        _manager.Rebuild();

        Assert.AreEqual(2, _manager.NetworkCount);

        var genNetwork = _manager.GetNetworkForNode(gen1);
        var conNetwork = _manager.GetNetworkForNode(con1);

        Assert.AreNotSame(genNetwork, conNetwork);
        Assert.AreEqual(1, genNetwork.NodeCount);
        Assert.AreEqual(1, conNetwork.NodeCount);
    }

    // ========================================================================
    // Manager tests
    // ========================================================================

    [Test]
    public void RegisterNode_MarksDirty()
    {
        Assert.IsFalse(_manager.IsDirty);

        _manager.RegisterNode(CreateGenerator("gen1", 100f));

        Assert.IsTrue(_manager.IsDirty);
    }

    [Test]
    public void UnregisterNode_MarksDirty()
    {
        var node = CreateGenerator("gen1", 100f);
        _manager.RegisterNode(node);
        _manager.Rebuild();

        Assert.IsFalse(_manager.IsDirty);

        _manager.UnregisterNode(node);

        Assert.IsTrue(_manager.IsDirty);
    }

    [Test]
    public void RebuildIfDirty_OnlyRebuildsWhenDirty()
    {
        var gen = CreateGenerator("gen1", 100f);
        var con = CreateConsumer("con1", 50f);
        gen.ConnectTo(con);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);

        Assert.IsTrue(_manager.IsDirty);
        Assert.AreEqual(0, _manager.NetworkCount); // not rebuilt yet

        _manager.RebuildIfDirty();

        Assert.IsFalse(_manager.IsDirty);
        Assert.AreEqual(1, _manager.NetworkCount);

        // Calling again should not rebuild (networks stay the same)
        var previousNetwork = _manager.Networks[0];
        _manager.RebuildIfDirty();

        Assert.AreSame(previousNetwork, _manager.Networks[0]);
    }

    [Test]
    public void GetSatisfaction_ReturnsCorrectValueForNode()
    {
        var gen = CreateGenerator("gen1", 60f);
        var con = CreateConsumer("con1", 80f);
        gen.ConnectTo(con);

        _manager.RegisterNode(gen);
        _manager.RegisterNode(con);
        _manager.Rebuild();

        Assert.AreEqual(0.75f, _manager.GetSatisfaction(gen), 0.001f);
        Assert.AreEqual(0.75f, _manager.GetSatisfaction(con), 0.001f);
    }

    [Test]
    public void GetNetworkForNode_ReturnsNullForUnregisteredNode()
    {
        var unregistered = CreateGenerator("unregistered", 100f);

        _manager.Rebuild();

        Assert.IsNull(_manager.GetNetworkForNode(unregistered));
    }

    [Test]
    public void GetSatisfaction_ReturnsOneForUnregisteredNode()
    {
        var unregistered = CreateGenerator("unregistered", 100f);

        Assert.AreEqual(1f, _manager.GetSatisfaction(unregistered));
    }

    // ========================================================================
    // Edge cases
    // ========================================================================

    [Test]
    public void NodeWithNoConnections_FormsNetworkOfOne()
    {
        var lone = CreateGenerator("lone", 50f);
        _manager.RegisterNode(lone);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);

        var network = _manager.GetNetworkForNode(lone);
        Assert.AreEqual(1, network.NodeCount);
        Assert.AreEqual(50f, network.TotalGeneration);
    }

    [Test]
    public void SelfReferencingConnection_DoesNotInfiniteLoop()
    {
        var node = CreateGenerator("self", 100f);
        node.ConnectTo(node); // self-connection

        _manager.RegisterNode(node);
        _manager.Rebuild();

        // Should complete without hanging
        Assert.AreEqual(1, _manager.NetworkCount);
        Assert.AreEqual(1, _manager.Networks[0].NodeCount);
    }

    [Test]
    public void UnregisteredNodesInConnections_AreSkipped()
    {
        var registered = CreateGenerator("reg", 100f);
        var unregistered = CreateConsumer("unreg", 50f);

        // Connect them, but only register one
        registered.ConnectTo(unregistered);
        _manager.RegisterNode(registered);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);
        Assert.AreEqual(1, _manager.Networks[0].NodeCount);

        // The unregistered node should not be in any network
        Assert.IsNull(_manager.GetNetworkForNode(unregistered));
    }

    [Test]
    public void RegisteringSameNodeTwice_DoesNotDuplicate()
    {
        var node = CreateGenerator("gen1", 100f);
        _manager.RegisterNode(node);
        _manager.RegisterNode(node);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);
        Assert.AreEqual(1, _manager.Networks[0].NodeCount);
    }

    [Test]
    public void UnregisteringUnknownNode_DoesNotThrow()
    {
        var unknown = CreateGenerator("unknown", 100f);

        Assert.DoesNotThrow(() => _manager.UnregisterNode(unknown));
        Assert.IsFalse(_manager.IsDirty); // was not registered, so no change
    }

    [Test]
    public void Rebuild_ClearsPreviousNetworks()
    {
        var gen = CreateGenerator("gen1", 100f);
        _manager.RegisterNode(gen);
        _manager.Rebuild();

        Assert.AreEqual(1, _manager.NetworkCount);

        _manager.UnregisterNode(gen);
        _manager.Rebuild();

        Assert.AreEqual(0, _manager.NetworkCount);
        Assert.IsNull(_manager.GetNetworkForNode(gen));
    }

    [Test]
    public void GetNetworkForNode_NullNode_ReturnsNull()
    {
        Assert.IsNull(_manager.GetNetworkForNode(null));
    }

    [Test]
    public void GetSatisfaction_NullNode_ReturnsOne()
    {
        Assert.AreEqual(1f, _manager.GetSatisfaction(null));
    }

    // ========================================================================
    // PowerNetwork direct construction tests
    // ========================================================================

    [Test]
    public void PowerNetwork_NullNodes_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new PowerNetwork(null));
    }

    [Test]
    public void PowerNetwork_EmptyNodeList_HasZeroTotalsAndSatisfactionOne()
    {
        var network = new PowerNetwork(new List<IPowerNode>());

        Assert.AreEqual(0f, network.TotalGeneration);
        Assert.AreEqual(0f, network.TotalConsumption);
        Assert.AreEqual(1f, network.Satisfaction);
        Assert.AreEqual(0, network.NodeCount);
    }

    // ========================================================================
    // SimplePowerNode tests
    // ========================================================================

    [Test]
    public void SimplePowerNode_ConnectTo_CreatesBidirectionalLink()
    {
        var a = CreateGenerator("a", 100f);
        var b = CreateConsumer("b", 50f);

        a.ConnectTo(b);

        Assert.Contains(b, (System.Collections.ICollection)a.GetConnectedNodes());
        Assert.Contains(a, (System.Collections.ICollection)b.GetConnectedNodes());
    }

    [Test]
    public void SimplePowerNode_DisconnectFrom_RemovesBidirectionalLink()
    {
        var a = CreateGenerator("a", 100f);
        var b = CreateConsumer("b", 50f);

        a.ConnectTo(b);
        a.DisconnectFrom(b);

        Assert.AreEqual(0, a.GetConnectedNodes().Count);
        Assert.AreEqual(0, b.GetConnectedNodes().Count);
    }

    [Test]
    public void SimplePowerNode_ConnectToSameNodeTwice_DoesNotDuplicate()
    {
        var a = CreateGenerator("a", 100f);
        var b = CreateConsumer("b", 50f);

        a.ConnectTo(b);
        a.ConnectTo(b);

        Assert.AreEqual(1, a.GetConnectedNodes().Count);
        Assert.AreEqual(1, b.GetConnectedNodes().Count);
    }

    [Test]
    public void SimplePowerNode_NullNodeId_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() => new SimplePowerNode(null, 0f, 0f));
    }
}
