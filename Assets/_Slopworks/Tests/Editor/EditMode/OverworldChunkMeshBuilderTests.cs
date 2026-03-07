using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OverworldChunkMeshBuilderTests
{
    [Test]
    public void Build_SingleHex_Returns6Triangles()
    {
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Grassland;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // a hex = 6 triangles = 18 indices
        Assert.AreEqual(18, mesh.triangles.Length);
    }

    [Test]
    public void Build_SingleHex_Has7Vertices()
    {
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Forest;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        Assert.AreEqual(7, mesh.vertexCount);
    }

    [Test]
    public void Build_SingleHex_VertexColorsMatchBiome()
    {
        var heights = new float[1, 1];
        var biomes = new OverworldBiomeType[1, 1];
        biomes[0, 0] = OverworldBiomeType.Wasteland;

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        var expected = OverworldBiomeLookup.GetColor(OverworldBiomeType.Wasteland);
        foreach (var c in mesh.colors)
            Assert.AreEqual(expected.r, c.r, 0.01f);
    }

    [Test]
    public void Build_2x2Chunk_HasCorrectHexCount()
    {
        var heights = new float[2, 2];
        var biomes = new OverworldBiomeType[2, 2];

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // 4 hexes * 7 verts each = 28
        Assert.AreEqual(28, mesh.vertexCount);
    }

    [Test]
    public void Build_WithHeight_CenterVertexAtCorrectY()
    {
        var heights = new float[1, 1];
        heights[0, 0] = 5f;
        var biomes = new OverworldBiomeType[1, 1];

        var mesh = OverworldChunkMeshBuilder.Build(heights, biomes, 1f, 0, 0);

        // center vertex (first) should be at y=5
        Assert.AreEqual(5f, mesh.vertices[0].y, 0.01f);
    }
}
