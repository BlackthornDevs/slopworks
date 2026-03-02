using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class SceneLoaderTests
{
    [Test]
    public void GetGroup_returns_scenes_for_known_group()
    {
        var groups = new Dictionary<string, string[]>
        {
            ["HomeBase"] = new[] { "HomeBase_Terrain", "HomeBase_Grid", "HomeBase_UI" }
        };
        var loader = new SceneLoader(groups);

        var result = loader.GetGroup("HomeBase");

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("HomeBase_Terrain", result[0]);
    }

    [Test]
    public void GetGroup_returns_null_for_unknown_group()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        Assert.IsNull(loader.GetGroup("NonExistent"));
    }

    [Test]
    public void CurrentGroup_starts_null()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        Assert.IsNull(loader.CurrentGroup);
    }

    [Test]
    public void SetCurrentGroup_updates_property()
    {
        var groups = new Dictionary<string, string[]>
        {
            ["HomeBase"] = new[] { "HomeBase_Terrain" }
        };
        var loader = new SceneLoader(groups);

        loader.SetCurrentGroup("HomeBase");

        Assert.AreEqual("HomeBase", loader.CurrentGroup);
    }

    [Test]
    public void SetCurrentGroup_rejects_unknown_group()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        loader.SetCurrentGroup("BadGroup");

        Assert.IsNull(loader.CurrentGroup);
    }
}
