using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ReticleStyleTests
{
    [Test]
    public void Gameplay_has_brackets_and_plus()
    {
        var s = ReticleStyle.Gameplay;
        Assert.AreEqual("[", s.Left);
        Assert.AreEqual("+", s.Center);
        Assert.AreEqual("]", s.Right);
    }

    [Test]
    public void BuildDefault_has_brackets_and_plus_in_orange()
    {
        var s = ReticleStyle.BuildDefault;
        Assert.AreEqual("[", s.Left);
        Assert.AreEqual("+", s.Center);
        Assert.AreEqual("]", s.Right);
        // orange, not cyan
        Assert.Greater(s.Color.r, 0.9f);
        Assert.Less(s.Color.b, 0.3f);
    }

    [Test]
    public void BuildStraight_uses_pipes()
    {
        var s = ReticleStyle.BuildStraight;
        Assert.AreEqual("|", s.Left);
        Assert.AreEqual("+", s.Center);
        Assert.AreEqual("|", s.Right);
    }

    [Test]
    public void BuildZoop_uses_Z_center()
    {
        var s = ReticleStyle.BuildZoop;
        Assert.AreEqual("[", s.Left);
        Assert.AreEqual("Z", s.Center);
        Assert.AreEqual("]", s.Right);
    }

    [Test]
    public void BuildCurved_uses_parens_and_asterisk()
    {
        var s = ReticleStyle.BuildCurved;
        Assert.AreEqual("(", s.Left);
        Assert.AreEqual("*", s.Center);
        Assert.AreEqual(")", s.Right);
    }

    [Test]
    public void BuildVertical_uses_caret()
    {
        var s = ReticleStyle.BuildVertical;
        Assert.AreEqual("[", s.Left);
        Assert.AreEqual("^", s.Center);
        Assert.AreEqual("]", s.Right);
    }

    [Test]
    public void Gameplay_color_is_cyan()
    {
        var s = ReticleStyle.Gameplay;
        // cyan: high blue, low red
        Assert.Greater(s.Color.b, 0.8f);
        Assert.Less(s.Color.r, 0.1f);
    }

    [Test]
    public void Constructor_sets_all_fields()
    {
        var custom = new ReticleStyle("{", "?", "}", Color.magenta);
        Assert.AreEqual("{", custom.Left);
        Assert.AreEqual("?", custom.Center);
        Assert.AreEqual("}", custom.Right);
        Assert.AreEqual(Color.magenta, custom.Color);
    }

    [Test]
    public void All_build_modes_share_orange_family()
    {
        var modes = new[]
        {
            ReticleStyle.BuildDefault,
            ReticleStyle.BuildStraight,
            ReticleStyle.BuildZoop,
            ReticleStyle.BuildCurved,
            ReticleStyle.BuildVertical,
        };

        foreach (var m in modes)
        {
            Assert.Greater(m.Color.r, 0.9f, "build mode should have high red (orange)");
            Assert.Greater(m.Color.g, 0.5f, "build mode should have mid green (orange)");
            Assert.Less(m.Color.b, 0.3f, "build mode should have low blue (orange)");
        }
    }
}
