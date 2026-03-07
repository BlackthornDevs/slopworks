using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class HexGridUtilityTests
{
    private const float Size = 1f;

    [Test]
    public void HexToWorld_Origin_ReturnsZero()
    {
        var pos = HexGridUtility.HexToWorld(0, 0, Size);
        Assert.AreEqual(0f, pos.x, 0.001f);
        Assert.AreEqual(0f, pos.z, 0.001f);
    }

    [Test]
    public void HexToWorld_Q1R0_ReturnsCorrectX()
    {
        var pos = HexGridUtility.HexToWorld(1, 0, Size);
        float expectedX = Mathf.Sqrt(3f) * Size;
        Assert.AreEqual(expectedX, pos.x, 0.001f);
        Assert.AreEqual(0f, pos.z, 0.001f);
    }

    [Test]
    public void HexToWorld_Q0R1_ReturnsCorrectXZ()
    {
        var pos = HexGridUtility.HexToWorld(0, 1, Size);
        float expectedX = Mathf.Sqrt(3f) / 2f * Size;
        float expectedZ = 1.5f * Size;
        Assert.AreEqual(expectedX, pos.x, 0.001f);
        Assert.AreEqual(expectedZ, pos.z, 0.001f);
    }

    [Test]
    public void HexCorners_Returns6Corners()
    {
        var corners = HexGridUtility.HexCorners(Vector3.zero, Size);
        Assert.AreEqual(6, corners.Length);
    }

    [Test]
    public void HexCorners_AllAtCorrectDistance()
    {
        var center = new Vector3(5f, 0f, 5f);
        var corners = HexGridUtility.HexCorners(center, Size);
        foreach (var c in corners)
        {
            float dist = Vector3.Distance(
                new Vector3(center.x, 0f, center.z),
                new Vector3(c.x, 0f, c.z));
            Assert.AreEqual(Size, dist, 0.001f);
        }
    }

    [Test]
    public void Neighbors_Returns6()
    {
        var neighbors = HexGridUtility.Neighbors(3, 4);
        Assert.AreEqual(6, neighbors.Length);
    }

    [Test]
    public void Neighbors_Origin_ContainsExpected()
    {
        var neighbors = HexGridUtility.Neighbors(0, 0);
        Assert.Contains(new Vector2Int(1, 0), neighbors);
        Assert.Contains(new Vector2Int(-1, 1), neighbors);
        Assert.Contains(new Vector2Int(0, -1), neighbors);
    }

    [Test]
    public void HexDistance_SameHex_IsZero()
    {
        Assert.AreEqual(0, HexGridUtility.HexDistance(3, 4, 3, 4));
    }

    [Test]
    public void HexDistance_Adjacent_IsOne()
    {
        Assert.AreEqual(1, HexGridUtility.HexDistance(0, 0, 1, 0));
    }

    [Test]
    public void HexDistance_TwoAway()
    {
        Assert.AreEqual(2, HexGridUtility.HexDistance(0, 0, 2, 0));
    }
}
