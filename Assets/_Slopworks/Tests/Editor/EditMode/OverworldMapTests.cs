using NUnit.Framework;

[TestFixture]
public class OverworldMapTests
{
    // -- Registration --

    [Test]
    public void RegisterNode_IncreasesNodeCount()
    {
        var map = new OverworldMap();
        var node = new OverworldNode("home", "Home Base", OverworldNodeType.HomeBase, 0.5f, 0.5f);

        map.RegisterNode(node);

        Assert.AreEqual(1, map.NodeCount);
    }

    [Test]
    public void GetNode_ReturnsRegisteredNode()
    {
        var map = new OverworldMap();
        var node = new OverworldNode("home", "Home Base", OverworldNodeType.HomeBase, 0.5f, 0.5f);
        map.RegisterNode(node);

        var result = map.GetNode("home");

        Assert.AreEqual(node, result);
    }

    [Test]
    public void GetNode_UnknownId_ReturnsNull()
    {
        var map = new OverworldMap();

        var result = map.GetNode("nonexistent");

        Assert.IsNull(result);
    }

    [Test]
    public void GetNodes_ReturnsAllRegistered()
    {
        var map = new OverworldMap();
        map.RegisterNode(new OverworldNode("home", "Home Base", OverworldNodeType.HomeBase, 0.5f, 0.5f));
        map.RegisterNode(new OverworldNode("warehouse", "Warehouse", OverworldNodeType.Building, 0.3f, 0.7f));
        map.RegisterNode(new OverworldNode("tower", "Tower", OverworldNodeType.Tower, 0.7f, 0.3f));

        var nodes = map.GetNodes();

        Assert.AreEqual(3, nodes.Count);
    }

    // -- OverworldNode IsActive --

    [Test]
    public void HomeBase_IsAlwaysActive()
    {
        var node = new OverworldNode("home", "Home Base", OverworldNodeType.HomeBase, 0.5f, 0.5f);

        Assert.IsTrue(node.IsActive);
    }

    [Test]
    public void Building_Unclaimed_IsNotActive()
    {
        var state = new BuildingState("warehouse", "Warehouse", 4,
            new[] { "iron_ingot" }, new[] { 1 }, 30f);
        var node = new OverworldNode("warehouse", "Warehouse", OverworldNodeType.Building, 0.3f, 0.7f, state);

        Assert.IsFalse(node.IsActive);
    }

    [Test]
    public void Building_Claimed_IsActive()
    {
        var state = new BuildingState("warehouse", "Warehouse", 1,
            new[] { "iron_ingot" }, new[] { 1 }, 30f);
        state.AddRestorePoint(new MEPRestorePoint("mep_0", MEPSystemType.Electrical));
        state.RestorePoint("mep_0");

        var node = new OverworldNode("warehouse", "Warehouse", OverworldNodeType.Building, 0.3f, 0.7f, state);

        Assert.IsTrue(node.IsActive);
    }

    [Test]
    public void Tower_IsNotActiveByDefault()
    {
        var node = new OverworldNode("tower", "Tower", OverworldNodeType.Tower, 0.7f, 0.3f);

        Assert.IsFalse(node.IsActive);
    }
}
