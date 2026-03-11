using UnityEditor;
using UnityEngine;

public static class BeltPortEditor
{
    [MenuItem("Slopworks/Add Belt Port/Input")]
    private static void AddInputPort()
    {
        AddBeltPort(BeltPortDirection.Input);
    }

    [MenuItem("Slopworks/Add Belt Port/Output")]
    private static void AddOutputPort()
    {
        AddBeltPort(BeltPortDirection.Output);
    }

    private static void AddBeltPort(BeltPortDirection direction)
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("belt editor: select a GameObject first");
            return;
        }

        var portName = direction == BeltPortDirection.Input ? "BeltPort_Input" : "BeltPort_Output";

        var existing = selected.GetComponentsInChildren<BeltPort>();
        int slotIndex = 0;
        foreach (var p in existing)
        {
            if (p.Direction == direction)
                slotIndex++;
        }

        var child = new GameObject($"{portName}_{slotIndex}");
        child.transform.SetParent(selected.transform);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.layer = PhysicsLayers.SnapPoints;

        var port = child.AddComponent<BeltPort>();
        port.Direction = direction;
        port.SlotIndex = slotIndex;

        var collider = child.AddComponent<SphereCollider>();
        collider.radius = 0.15f;
        collider.isTrigger = true;

        Selection.activeGameObject = child;
        Undo.RegisterCreatedObjectUndo(child, $"Add Belt Port ({direction})");

        Debug.Log($"belt editor: added {direction} port (slot {slotIndex}) to {selected.name}");
    }
}
