using NUnit.Framework;

[TestFixture]
public class ItemInstanceTests
{
    [Test]
    public void Create_SetsDefinitionIdCorrectly()
    {
        var item = ItemInstance.Create("iron_ore");

        Assert.AreEqual("iron_ore", item.definitionId);
    }

    [Test]
    public void Create_SetsDurabilityToNegativeOne()
    {
        var item = ItemInstance.Create("iron_ore");

        Assert.AreEqual(-1f, item.durability);
    }

    [Test]
    public void EmptyInstance_IsEmpty_ReturnsTrue()
    {
        var item = ItemInstance.Empty;

        Assert.IsTrue(item.IsEmpty);
    }

    [Test]
    public void NonEmptyInstance_IsEmpty_ReturnsFalse()
    {
        var item = ItemInstance.Create("iron_ore");

        Assert.IsFalse(item.IsEmpty);
    }

    [Test]
    public void Create_QualityDefaultsToZero()
    {
        var item = ItemInstance.Create("iron_ore");

        Assert.AreEqual(0, item.quality);
    }

    [Test]
    public void Create_InstanceIdDefaultsToNull()
    {
        var item = ItemInstance.Create("iron_ore");

        Assert.IsNull(item.instanceId);
    }
}
