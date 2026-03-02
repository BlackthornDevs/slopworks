using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for MEPRestorePointBehaviour. Exercises the MonoBehaviour IInteractable
/// contract: prompt text, interaction flow through to BuildingState, visual color
/// changes, and edge cases (double interact, uninitialized, already restored).
/// </summary>
[TestFixture]
public class MEPRestorePointBehaviourTests
{
    private GameObject _mepObj;
    private MEPRestorePointBehaviour _behaviour;
    private BuildingState _buildingState;
    private MEPRestorePoint _point;

    [SetUp]
    public void SetUp()
    {
        _buildingState = new BuildingState("test_building", "Test", 4,
            new[] { "iron_ingot" }, new[] { 1 }, 30f);

        _point = new MEPRestorePoint("mep_0", MEPSystemType.Electrical);
        _buildingState.AddRestorePoint(_point);

        _mepObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _mepObj.layer = PhysicsLayers.Interactable;
        _behaviour = _mepObj.AddComponent<MEPRestorePointBehaviour>();
        _behaviour.Initialize(_point, _buildingState);
    }

    [TearDown]
    public void TearDown()
    {
        if (_mepObj != null)
            Object.DestroyImmediate(_mepObj);
    }

    // -- IInteractable contract --

    [Test]
    public void GetInteractionPrompt_WhenBroken_ShowsRestorePrompt()
    {
        var prompt = _behaviour.GetInteractionPrompt();
        StringAssert.Contains("restore", prompt.ToLower());
        StringAssert.Contains("Electrical", prompt);
    }

    [Test]
    public void GetInteractionPrompt_WhenRestored_ShowsRestoredStatus()
    {
        _behaviour.Interact(new GameObject("FakePlayer"));

        var prompt = _behaviour.GetInteractionPrompt();
        StringAssert.Contains("restored", prompt.ToLower());
    }

    // -- Interact flow --

    [Test]
    public void Interact_RestoresPointInBuildingState()
    {
        var player = new GameObject("FakePlayer");

        _behaviour.Interact(player);

        Assert.IsTrue(_point.IsRestored);
        Assert.AreEqual(1, _buildingState.RestoredCount);

        Object.DestroyImmediate(player);
    }

    [Test]
    public void Interact_DoubleInteract_DoesNotDoubleRestore()
    {
        var player = new GameObject("FakePlayer");

        _behaviour.Interact(player);
        _behaviour.Interact(player);

        Assert.AreEqual(1, _buildingState.RestoredCount);

        Object.DestroyImmediate(player);
    }

    [Test]
    public void Interact_FiresBuildingStateEvents()
    {
        int restoredEvents = 0;
        _buildingState.OnPointRestored += () => restoredEvents++;
        var player = new GameObject("FakePlayer");

        _behaviour.Interact(player);

        Assert.AreEqual(1, restoredEvents);

        Object.DestroyImmediate(player);
    }

    // -- Visual feedback --

    [Test]
    public void Initialize_SetsBrokenColor()
    {
        var renderer = _mepObj.GetComponent<Renderer>();
        Assert.IsNotNull(renderer);

        var expected = new Color(0.5f, 0.1f, 0.1f);
        AssertColorsApproxEqual(expected, renderer.sharedMaterial.color, "Should start with broken color");
    }

    [Test]
    public void Interact_ChangesToRestoredColor()
    {
        var player = new GameObject("FakePlayer");
        _behaviour.Interact(player);

        var renderer = _mepObj.GetComponent<Renderer>();
        var expected = new Color(0.1f, 0.7f, 0.1f);
        AssertColorsApproxEqual(expected, renderer.sharedMaterial.color, "Should change to restored color");

        Object.DestroyImmediate(player);
    }

    // -- Edge cases --

    [Test]
    public void GetInteractionPrompt_Uninitialized_ReturnsEmpty()
    {
        var obj = new GameObject("Uninitialized");
        var uninit = obj.AddComponent<MEPRestorePointBehaviour>();

        Assert.AreEqual("", uninit.GetInteractionPrompt());

        Object.DestroyImmediate(obj);
    }

    [Test]
    public void Interact_Uninitialized_DoesNotThrow()
    {
        var obj = new GameObject("Uninitialized");
        var uninit = obj.AddComponent<MEPRestorePointBehaviour>();
        var player = new GameObject("FakePlayer");

        Assert.DoesNotThrow(() => uninit.Interact(player));

        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void Interact_NullPlayer_DoesNotThrow()
    {
        // Interact with null player should not crash
        Assert.DoesNotThrow(() => _behaviour.Interact(null));
        // But it should still restore since we only use player for potential future checks
        Assert.IsTrue(_point.IsRestored);
    }

    // -- Multiple MEP points on same building --

    [Test]
    public void MultiplePoints_EachRestoresIndependently()
    {
        var point2 = new MEPRestorePoint("mep_1", MEPSystemType.Plumbing);
        _buildingState.AddRestorePoint(point2);

        var obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var behaviour2 = obj2.AddComponent<MEPRestorePointBehaviour>();
        behaviour2.Initialize(point2, _buildingState);

        var player = new GameObject("FakePlayer");

        _behaviour.Interact(player);
        Assert.AreEqual(1, _buildingState.RestoredCount);

        behaviour2.Interact(player);
        Assert.AreEqual(2, _buildingState.RestoredCount);

        // Verify prompts differ
        StringAssert.Contains("restored", _behaviour.GetInteractionPrompt().ToLower());
        StringAssert.Contains("restored", behaviour2.GetInteractionPrompt().ToLower());

        Object.DestroyImmediate(obj2);
        Object.DestroyImmediate(player);
    }

    [Test]
    public void RestoreAllPoints_ClaimsBuildingThroughBehaviours()
    {
        // Set up a building with 2 required points
        var state = new BuildingState("b", "B", 2,
            new[] { "iron_ingot" }, new[] { 1 }, 30f);
        var p1 = new MEPRestorePoint("p1", MEPSystemType.Mechanical);
        var p2 = new MEPRestorePoint("p2", MEPSystemType.HVAC);
        state.AddRestorePoint(p1);
        state.AddRestorePoint(p2);

        var obj1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var b1 = obj1.AddComponent<MEPRestorePointBehaviour>();
        b1.Initialize(p1, state);

        var obj2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var b2 = obj2.AddComponent<MEPRestorePointBehaviour>();
        b2.Initialize(p2, state);

        bool claimed = false;
        state.OnBuildingClaimed += () => claimed = true;

        var player = new GameObject("FakePlayer");

        b1.Interact(player);
        Assert.IsFalse(claimed);

        b2.Interact(player);
        Assert.IsTrue(claimed);
        Assert.IsTrue(state.IsClaimed);

        Object.DestroyImmediate(obj1);
        Object.DestroyImmediate(obj2);
        Object.DestroyImmediate(player);
    }

    // -- System type coverage --

    [Test]
    public void PromptText_ContainsCorrectSystemType([Values] MEPSystemType type)
    {
        var point = new MEPRestorePoint("test", type);
        var state = new BuildingState("b", "B", 1, null, null, 0f);
        state.AddRestorePoint(point);

        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var beh = obj.AddComponent<MEPRestorePointBehaviour>();
        beh.Initialize(point, state);

        StringAssert.Contains(type.ToString(), beh.GetInteractionPrompt());

        Object.DestroyImmediate(obj);
    }

    // -- Helper --

    private static void AssertColorsApproxEqual(Color expected, Color actual, string message)
    {
        Assert.AreEqual(expected.r, actual.r, 0.01f, $"{message} (red)");
        Assert.AreEqual(expected.g, actual.g, 0.01f, $"{message} (green)");
        Assert.AreEqual(expected.b, actual.b, 0.01f, $"{message} (blue)");
    }
}
