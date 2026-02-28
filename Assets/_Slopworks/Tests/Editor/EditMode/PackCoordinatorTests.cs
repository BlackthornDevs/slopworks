using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PackCoordinatorTests
{
    [TearDown]
    public void TearDown()
    {
        PackCoordinator.ClearAll();
    }

    // ── confidence math ────────────────────────────────────

    [Test]
    public void Confidence_FourAliveNoDeath_HighConfidence()
    {
        // 4 * 0.25 - 0 * 0.3 + 0.5 * 0.5 = 1.0 + 0.25 = 1.25 → clamped to 1.0
        float result = PackCoordinator.CalculateConfidence(4, 0, 0.5f);
        Assert.AreEqual(1f, result, 0.001f);
    }

    [Test]
    public void Confidence_OneAliveNoDeath_ModerateConfidence()
    {
        // 1 * 0.25 - 0 * 0.3 + 0.5 * 0.5 = 0.25 + 0.25 = 0.5
        float result = PackCoordinator.CalculateConfidence(1, 0, 0.5f);
        Assert.AreEqual(0.5f, result, 0.001f);
    }

    [Test]
    public void Confidence_TwoAliveOneRecentDeath_LowerConfidence()
    {
        // 2 * 0.25 - 1 * 0.3 + 0.5 * 0.5 = 0.5 - 0.3 + 0.25 = 0.45
        float result = PackCoordinator.CalculateConfidence(2, 1, 0.5f);
        Assert.AreEqual(0.45f, result, 0.001f);
    }

    [Test]
    public void Confidence_OneAliveThreeDeaths_ClampsToZero()
    {
        // 1 * 0.25 - 3 * 0.3 + 0.3 * 0.5 = 0.25 - 0.9 + 0.15 = -0.5 → clamped to 0
        float result = PackCoordinator.CalculateConfidence(1, 3, 0.3f);
        Assert.AreEqual(0f, result, 0.001f);
    }

    [Test]
    public void Confidence_ZeroAlive_VeryLow()
    {
        // 0 * 0.25 - 0 * 0.3 + 0.5 * 0.5 = 0.25
        float result = PackCoordinator.CalculateConfidence(0, 0, 0.5f);
        Assert.AreEqual(0.25f, result, 0.001f);
    }

    [Test]
    public void Confidence_HighBravery_RaisesFloor()
    {
        // 1 * 0.25 - 2 * 0.3 + 1.0 * 0.5 = 0.25 - 0.6 + 0.5 = 0.15
        float result = PackCoordinator.CalculateConfidence(1, 2, 1f);
        Assert.AreEqual(0.15f, result, 0.001f);
    }

    [Test]
    public void Confidence_LowBravery_LowersFloor()
    {
        // 1 * 0.25 - 1 * 0.3 + 0.0 * 0.5 = 0.25 - 0.3 + 0.0 = -0.05 → clamped to 0
        float result = PackCoordinator.CalculateConfidence(1, 1, 0f);
        Assert.AreEqual(0f, result, 0.001f);
    }

    // ── flank angles ───────────────────────────────────────

    [Test]
    public void FlankAngle_SingleMember_ReturnsDirectApproach()
    {
        var pack = PackCoordinator.GetOrCreate("test_flank");
        var go = CreateTestFauna("flank_1");
        var controller = go.GetComponent<FaunaController>();
        pack.Register(controller);

        float angle = pack.GetFlankAngle(controller);

        // member 0 = 0 degrees ± 15 jitter
        Assert.That(angle, Is.InRange(-15f, 15f));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FlankAngle_TwoMembers_DifferentBaseAngles()
    {
        var pack = PackCoordinator.GetOrCreate("test_flank2");
        var go1 = CreateTestFauna("flank2_1");
        var go2 = CreateTestFauna("flank2_2");
        var c1 = go1.GetComponent<FaunaController>();
        var c2 = go2.GetComponent<FaunaController>();
        pack.Register(c1);
        pack.Register(c2);

        // member 0 = ~0 degrees, member 1 = ~90 degrees
        // accounting for ±15 jitter
        float angle1 = pack.GetFlankAngle(c1);
        float angle2 = pack.GetFlankAngle(c2);

        Assert.That(angle1, Is.InRange(-15f, 15f));
        Assert.That(angle2, Is.InRange(75f, 105f));

        Object.DestroyImmediate(go1);
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void FlankAngle_FourMembers_CoversAllQuadrants()
    {
        var pack = PackCoordinator.GetOrCreate("test_flank4");
        var gos = new GameObject[4];
        var controllers = new FaunaController[4];

        for (int i = 0; i < 4; i++)
        {
            gos[i] = CreateTestFauna("flank4_" + i);
            controllers[i] = gos[i].GetComponent<FaunaController>();
            pack.Register(controllers[i]);
        }

        // expected base angles: 0, 90, -90, 180
        float a0 = pack.GetFlankAngle(controllers[0]);
        float a1 = pack.GetFlankAngle(controllers[1]);
        float a2 = pack.GetFlankAngle(controllers[2]);
        float a3 = pack.GetFlankAngle(controllers[3]);

        Assert.That(a0, Is.InRange(-15f, 15f));
        Assert.That(a1, Is.InRange(75f, 105f));
        Assert.That(a2, Is.InRange(-105f, -75f));
        Assert.That(a3, Is.InRange(165f, 195f));

        for (int i = 0; i < 4; i++)
            Object.DestroyImmediate(gos[i]);
    }

    [Test]
    public void FlankAngle_UnregisteredMember_ReturnsZero()
    {
        var pack = PackCoordinator.GetOrCreate("test_flank_unreg");
        var go = CreateTestFauna("unreg");
        var controller = go.GetComponent<FaunaController>();
        // not registered

        float angle = pack.GetFlankAngle(controller);
        Assert.AreEqual(0f, angle);

        Object.DestroyImmediate(go);
    }

    // ── register / unregister ──────────────────────────────

    [Test]
    public void Register_IncreasesAliveCount()
    {
        var pack = PackCoordinator.GetOrCreate("test_reg");
        var go = CreateTestFauna("reg_1");
        var controller = go.GetComponent<FaunaController>();

        Assert.AreEqual(0, pack.AliveCount);
        pack.Register(controller);
        Assert.AreEqual(1, pack.AliveCount);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Unregister_DecreasesAliveCount()
    {
        var pack = PackCoordinator.GetOrCreate("test_unreg");
        var go = CreateTestFauna("unreg_1");
        var controller = go.GetComponent<FaunaController>();

        pack.Register(controller);
        Assert.AreEqual(1, pack.AliveCount);

        pack.Unregister(controller);
        Assert.AreEqual(0, pack.AliveCount);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Register_DuplicateDoesNotDoubleCount()
    {
        var pack = PackCoordinator.GetOrCreate("test_dup");
        var go = CreateTestFauna("dup_1");
        var controller = go.GetComponent<FaunaController>();

        pack.Register(controller);
        pack.Register(controller);
        Assert.AreEqual(1, pack.AliveCount);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetOrCreate_SameId_ReturnsSameInstance()
    {
        var pack1 = PackCoordinator.GetOrCreate("same_id");
        var pack2 = PackCoordinator.GetOrCreate("same_id");
        Assert.AreSame(pack1, pack2);
    }

    [Test]
    public void GetOrCreate_DifferentId_ReturnsDifferentInstances()
    {
        var pack1 = PackCoordinator.GetOrCreate("id_a");
        var pack2 = PackCoordinator.GetOrCreate("id_b");
        Assert.AreNotSame(pack1, pack2);
    }

    // ── shared blackboard ──────────────────────────────────

    [Test]
    public void SharedBlackboard_InitializesAllKeys()
    {
        var pack = PackCoordinator.GetOrCreate("test_bb");
        var bb = pack.SharedBlackboard;

        Assert.AreEqual(false, (bool)bb["alert_valid"]);
        Assert.AreEqual(Vector3.zero, (Vector3)bb["alert_position"]);
        Assert.AreEqual(0f, (float)bb["alert_time"]);
        Assert.AreEqual(1f, (float)bb["pack_confidence"]);
        Assert.AreEqual(0f, (float)bb["ally_death_time"]);
    }

    // ── helper ─────────────────────────────────────────────

    private static GameObject CreateTestFauna(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<FaunaController>();

        // set the private _def field via SerializedObject
        var def = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
        def.faunaId = "test_grunt";
        def.baseBravery = 0.5f;

#if UNITY_EDITOR
        var so = new UnityEditor.SerializedObject(go.GetComponent<FaunaController>());
        so.FindProperty("_def").objectReferenceValue = def;
        so.ApplyModifiedProperties();
#endif

        return go;
    }
}
