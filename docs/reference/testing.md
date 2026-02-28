# Testing reference

Unity Test Framework for all automated testing in Slopworks. This covers the two test modes, what belongs in each, FishNet integration patterns, and conventions for the two-developer team.

---

## Test modes

### EditMode

Runs in the editor without entering Play Mode. No MonoBehaviour lifecycle. Test pure C# logic only.

**What belongs here:**
- Belt tick logic (`BeltSegment.Tick()`)
- Recipe validation (`RecipeSO.CanCraft()`)
- Item registry operations (`ItemRegistry.GetDefinition()`)
- Power grid calculations (`PowerGrid.CalculateDraw()`)
- World generation algorithms
- Save data serialization/deserialization

### PlayMode

Runs in Play Mode with full MonoBehaviour lifecycle. Test FishNet network behavior.

**What belongs here:**
- Machine state transitions under network conditions
- SyncVar propagation (server writes → client reads)
- ServerRpc validation (server rejects invalid client requests)
- Building placement validation
- Inventory operations (swap, pickup, drop)
- Scene loading/unloading

---

## File layout

```
Assets/
  _Slopworks/
    Scripts/         — production code (no test dependencies)
    Tests/
      Editor/
        EditMode/
          BeltTickTests.cs
          RecipeTests.cs
          ItemRegistryTests.cs
          PowerGridTests.cs
          SaveSerializationTests.cs
      PlayMode/
        MachineNetworkTests.cs
        InventoryNetworkTests.cs
        BuildPlacementTests.cs
      TestHelpers/
        TestItems.cs         — shared test fixture data
        TestRecipes.cs
        TestNetworkSetup.cs  — minimal FishNet scene setup
```

One test file per production class. `BeltSegment.cs` → `BeltTickTests.cs`.

---

## EditMode test examples

```csharp
// Tests/Editor/EditMode/BeltTickTests.cs
using NUnit.Framework;
using Slopworks.Automation;

[TestFixture]
public class BeltTickTests
{
    [Test]
    public void belt_moves_item_by_speed_per_tick()
    {
        var belt = new BeltSegment(length: 5);
        belt.InsertItem(new BeltItem { definition = TestItems.IronIngot, offset = 0f });

        belt.Tick(deltaTime: 1f, speed: 1f);

        Assert.AreEqual(1f, belt.Items[0].offset);
    }

    [Test]
    public void belt_does_not_push_item_past_blocked_output()
    {
        var belt = new BeltSegment(length: 5);
        belt.InsertItem(new BeltItem { definition = TestItems.IronIngot, offset = 4.9f });
        belt.SetOutputBlocked(true);

        belt.Tick(deltaTime: 1f, speed: 1f);

        Assert.Less(belt.Items[0].offset, 5f, "item should not pass output when blocked");
    }

    [Test]
    public void belt_stops_ticking_without_power()
    {
        var belt = new BeltSegment(length: 5);
        belt.InsertItem(new BeltItem { definition = TestItems.IronIngot, offset = 0f });
        belt.SetPowered(false);

        belt.Tick(deltaTime: 1f, speed: 1f);

        Assert.AreEqual(0f, belt.Items[0].offset, "unpowered belt should not move items");
    }
}
```

---

## PlayMode test examples

```csharp
// Tests/PlayMode/MachineNetworkTests.cs
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class MachineNetworkTests
{
    // Use TestNetworkSetup to spin up minimal FishNet
    private TestNetworkSetup _net;
    private MachineNetworkState _machine;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _net = new TestNetworkSetup();
        yield return _net.StartHostAsync();
        _machine = _net.SpawnMachine("smelter");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        yield return _net.StopAsync();
    }

    [UnityTest]
    public IEnumerator machine_status_is_idle_on_spawn()
    {
        yield return null;
        Assert.AreEqual(MachineStatus.Idle, _machine.Status);
    }

    [UnityTest]
    public IEnumerator machine_transitions_to_working_after_recipe_set()
    {
        _machine.SetRecipeServerRpc("iron-plate");
        yield return null;    // one frame for SyncVar propagation
        Assert.AreEqual(MachineStatus.Working, _machine.Status);
    }

    [UnityTest]
    public IEnumerator machine_ignores_recipe_set_from_non_server_client()
    {
        // Connect a second client without server authority
        _net.ConnectClient();
        _net.ClientSetRecipeRpc(_machine, "invalid-recipe");
        yield return null;
        // Status should remain Idle — server rejected the request
        Assert.AreEqual(MachineStatus.Idle, _machine.Status);
    }
}
```

---

## Bug-fixing workflow

1. Write a **failing** EditMode or PlayMode test that reproduces the bug
2. Fix the bug
3. Verify the test passes

Don't skip step 1. Factory simulation logic is pure C# — it's unusually testable. A passing test is proof the bug is fixed, not just optimism.

---

## What not to test

- FishNet's own RPC delivery mechanism (test your logic, not the framework)
- Unity's MonoBehaviour lifecycle (Awake, Start, Update order)
- Visual/rendering output (what the UI shows)

Test data and behavior, not display.

---

## Running tests

Window > General > Test Runner.

- EditMode: runs without Play Mode, fast (< 1s)
- PlayMode: enters Play Mode, requires a scene in Build Settings

For CI: `unity -runTests -testPlatform EditMode -projectPath . -logFile test-results.xml`

---

## Making code testable

Factory simulation logic belongs in plain C# classes, not MonoBehaviours. The MonoBehaviour wraps the C# class:

```csharp
// Testable: no MonoBehaviour dependency
public class BeltSegment
{
    public void Tick(float deltaTime, float speed) { ... }
}

// MonoBehaviour thin wrapper
public class BeltSegmentBehaviour : MonoBehaviour
{
    private BeltSegment _segment;

    private void Awake()
    {
        _segment = new BeltSegment();
    }

    private void FixedUpdate()
    {
        if (!IsServerInitialized) return;
        _segment.Tick(Time.fixedDeltaTime, _speed);
    }
}
```

The C# class is testable in EditMode. The MonoBehaviour wrapper is thin and tested by existence.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Testing MonoBehaviour in EditMode | Extract logic into plain C# class; MonoBehaviour wraps it |
| Mocking FishNet | Don't — simulate with two-player setup in PlayMode |
| Magic test data | Define `TestItems`, `TestRecipes` in `TestHelpers/` |
| PlayMode test flakiness | `yield return null` to wait for SyncVar propagation |
| EditMode test with GameObject dependency | Remove the MonoBehaviour dependency; test pure logic |
