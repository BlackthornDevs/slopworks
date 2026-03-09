using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltPortTests
{
    [Test]
    public void BeltPort_DefaultDirection_IsInput()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(BeltPortDirection.Input, port.Direction);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_CanSetOutput()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();
        port.Direction = BeltPortDirection.Output;

        Assert.AreEqual(BeltPortDirection.Output, port.Direction);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_SlotIndex_DefaultsToZero()
    {
        var go = new GameObject("TestPort");
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(0, port.SlotIndex);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_WorldDirection_MatchesTransformForward()
    {
        var go = new GameObject("TestPort");
        go.transform.forward = Vector3.right;
        var port = go.AddComponent<BeltPort>();

        Assert.AreEqual(Vector3.right, port.WorldDirection);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BeltPort_WorldPosition_MatchesTransformPosition()
    {
        var parent = new GameObject("Parent");
        var child = new GameObject("Port");
        child.transform.SetParent(parent.transform);
        child.transform.localPosition = new Vector3(1f, 0f, 0f);
        var port = child.AddComponent<BeltPort>();

        Assert.AreEqual(new Vector3(1f, 0f, 0f), port.WorldPosition);

        Object.DestroyImmediate(parent);
    }

    [Test]
    public void FindPorts_ReturnsAllBeltPortsOnGameObject()
    {
        var parent = new GameObject("Machine");
        var input = new GameObject("InputPort");
        input.transform.SetParent(parent.transform);
        input.AddComponent<BeltPort>().Direction = BeltPortDirection.Input;

        var output = new GameObject("OutputPort");
        output.transform.SetParent(parent.transform);
        output.AddComponent<BeltPort>().Direction = BeltPortDirection.Output;

        var ports = parent.GetComponentsInChildren<BeltPort>();

        Assert.AreEqual(2, ports.Length);

        Object.DestroyImmediate(parent);
    }
}
