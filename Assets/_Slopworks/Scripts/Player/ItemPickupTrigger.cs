using UnityEngine;

/// <summary>
/// Trigger zone on the player that auto-collects WorldItems on overlap.
/// Add to the player GameObject with a trigger SphereCollider.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ItemPickupTrigger : MonoBehaviour
{
    private PlayerInventory _inventory;

    private void Awake()
    {
        _inventory = GetComponentInParent<PlayerInventory>();

        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_inventory == null) return;

        var worldItem = other.GetComponent<WorldItem>();
        if (worldItem != null)
            worldItem.TryCollect(_inventory);
    }
}
