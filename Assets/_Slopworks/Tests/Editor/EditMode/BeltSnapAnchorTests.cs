using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltSnapAnchorTests
{
    [Test]
    public void SnapAnchor_Position_MatchesTransformPosition()
    {
        var go = new GameObject("Support");
        go.transform.position = new Vector3(5f, 1f, 3f);
        var anchor = go.AddComponent<BeltSnapAnchor>();

        Assert.AreEqual(new Vector3(5f, 1f, 3f), anchor.WorldPosition);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SnapAnchor_Direction_MatchesTransformForward()
    {
        var go = new GameObject("Support");
        go.transform.rotation = Quaternion.LookRotation(Vector3.right);
        var anchor = go.AddComponent<BeltSnapAnchor>();

        Assert.AreEqual(Vector3.right.x, anchor.WorldDirection.x, 0.001f);
        Assert.AreEqual(Vector3.right.y, anchor.WorldDirection.y, 0.001f);
        Assert.AreEqual(Vector3.right.z, anchor.WorldDirection.z, 0.001f);

        Object.DestroyImmediate(go);
    }
}
