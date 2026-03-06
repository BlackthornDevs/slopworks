using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class NetworkPickupTrigger : MonoBehaviour
{
    private NetworkInventory _inventory;

    private void Awake()
    {
        _inventory = GetComponentInParent<NetworkInventory>();

        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_inventory == null) return;

        var nob = other.GetComponent<NetworkObject>();
        if (nob == null) return;

        var worldItem = other.GetComponent<NetworkWorldItem>();
        if (worldItem == null) return;

        _inventory.CmdPickupItem(nob);
    }
}
