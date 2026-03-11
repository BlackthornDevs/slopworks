using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltPlacementValidatorTests
{
    [Test]
    public void Validate_ValidStraightBelt_ReturnsTrue()
    {
        var start = new Vector3(0, 1, 0);
        var end = new Vector3(10, 1, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_TooShort_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.3f, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooShort, result.Error);
    }

    [Test]
    public void Validate_TooLong_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(60, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooLong, result.Error);
    }

    [Test]
    public void Validate_TooSteep_ReturnsFalse()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(3, 10, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooSteep, result.Error);
    }

    [Test]
    public void Validate_ExactMinLength_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0.5f, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_ExactMaxLength_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(56, 0, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_45DegreeSlopeExactly_ReturnsTrue()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(10, 10, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsTrue(result.IsValid);
    }

    [Test]
    public void Validate_PurelyVertical_ReturnsTooSteep()
    {
        var start = new Vector3(0, 0, 0);
        var end = new Vector3(0, 5, 0);
        var result = BeltPlacementValidator.Validate(start, Vector3.right, end, Vector3.right);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(BeltValidationError.TooSteep, result.Error);
    }
}
