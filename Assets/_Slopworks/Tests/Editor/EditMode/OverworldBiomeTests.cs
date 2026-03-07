using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OverworldBiomeTests
{
    [Test]
    public void Lookup_WarmDry_ReturnsWasteland()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.8f, moisture: 0.1f);
        Assert.AreEqual(OverworldBiomeType.Wasteland, biome);
    }

    [Test]
    public void Lookup_WarmWet_ReturnsSwamp()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.8f, moisture: 0.9f);
        Assert.AreEqual(OverworldBiomeType.Swamp, biome);
    }

    [Test]
    public void Lookup_CoolMedium_ReturnsForest()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.5f);
        Assert.AreEqual(OverworldBiomeType.Forest, biome);
    }

    [Test]
    public void Lookup_CoolDry_ReturnsRuins()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.1f);
        Assert.AreEqual(OverworldBiomeType.Ruins, biome);
    }

    [Test]
    public void Lookup_CoolWet_ReturnsOvergrownRuins()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.2f, moisture: 0.9f);
        Assert.AreEqual(OverworldBiomeType.OvergrownRuins, biome);
    }

    [Test]
    public void Lookup_MidMid_ReturnsGrassland()
    {
        var biome = OverworldBiomeLookup.GetBiome(temperature: 0.5f, moisture: 0.5f);
        Assert.AreEqual(OverworldBiomeType.Grassland, biome);
    }

    [Test]
    public void GetColor_AllBiomes_ReturnNonBlack()
    {
        foreach (OverworldBiomeType b in System.Enum.GetValues(typeof(OverworldBiomeType)))
        {
            var c = OverworldBiomeLookup.GetColor(b);
            Assert.IsTrue(c.r > 0f || c.g > 0f || c.b > 0f, $"{b} color is black");
        }
    }
}
