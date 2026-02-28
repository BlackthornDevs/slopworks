using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GridRotationTests
{
    // -- 0 degrees (identity) --

    [Test]
    public void Rotate_0Degrees_ReturnsUnchanged()
    {
        var result = GridRotation.Rotate(new Vector2Int(3, 5), 0);
        Assert.AreEqual(new Vector2Int(3, 5), result);
    }

    // -- 90 degrees clockwise --

    [Test]
    public void Rotate_90Degrees_EastBecomeSouth()
    {
        // (1, 0) east -> (0, -1) south
        var result = GridRotation.Rotate(new Vector2Int(1, 0), 90);
        Assert.AreEqual(new Vector2Int(0, -1), result);
    }

    [Test]
    public void Rotate_90Degrees_NorthBecomesEast()
    {
        // (0, 1) north -> (1, 0) east
        var result = GridRotation.Rotate(new Vector2Int(0, 1), 90);
        Assert.AreEqual(new Vector2Int(1, 0), result);
    }

    // -- 180 degrees --

    [Test]
    public void Rotate_180Degrees_EastBecomesWest()
    {
        var result = GridRotation.Rotate(new Vector2Int(1, 0), 180);
        Assert.AreEqual(new Vector2Int(-1, 0), result);
    }

    [Test]
    public void Rotate_180Degrees_NorthBecomesSouth()
    {
        var result = GridRotation.Rotate(new Vector2Int(0, 1), 180);
        Assert.AreEqual(new Vector2Int(0, -1), result);
    }

    // -- 270 degrees clockwise --

    [Test]
    public void Rotate_270Degrees_EastBecomesNorth()
    {
        // (1, 0) east -> (0, 1) north
        var result = GridRotation.Rotate(new Vector2Int(1, 0), 270);
        Assert.AreEqual(new Vector2Int(0, 1), result);
    }

    [Test]
    public void Rotate_270Degrees_NorthBecomesWest()
    {
        // (0, 1) north -> (-1, 0) west
        var result = GridRotation.Rotate(new Vector2Int(0, 1), 270);
        Assert.AreEqual(new Vector2Int(-1, 0), result);
    }

    // -- Full rotation cycle --

    [Test]
    public void Rotate_FourTimesBy90_ReturnsOriginal()
    {
        var original = new Vector2Int(2, 3);
        var result = original;
        for (int i = 0; i < 4; i++)
            result = GridRotation.Rotate(result, 90);
        Assert.AreEqual(original, result);
    }

    // -- Arbitrary offset --

    [Test]
    public void Rotate_90Degrees_ArbitraryOffset()
    {
        // (2, 1) -> (1, -2)
        var result = GridRotation.Rotate(new Vector2Int(2, 1), 90);
        Assert.AreEqual(new Vector2Int(1, -2), result);
    }

    // -- Invalid degrees --

    [Test]
    public void Rotate_InvalidDegrees_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GridRotation.Rotate(Vector2Int.zero, 45));
    }

    [Test]
    public void Rotate_NegativeDegrees_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GridRotation.Rotate(Vector2Int.zero, -90));
    }

    // -- Zero vector --

    [Test]
    public void Rotate_ZeroVector_ReturnsZero()
    {
        Assert.AreEqual(Vector2Int.zero, GridRotation.Rotate(Vector2Int.zero, 90));
        Assert.AreEqual(Vector2Int.zero, GridRotation.Rotate(Vector2Int.zero, 180));
        Assert.AreEqual(Vector2Int.zero, GridRotation.Rotate(Vector2Int.zero, 270));
    }
}
