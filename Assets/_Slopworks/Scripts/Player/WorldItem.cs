using UnityEngine;

/// <summary>
/// An item sitting in the world that the player can pick up.
/// Placed on the Interactable physics layer.
/// </summary>
public class WorldItem : MonoBehaviour
{
    [SerializeField] private ItemDefinitionSO _definition;
    [SerializeField] private int _count = 1;

    public ItemDefinitionSO Definition => _definition;
    public int Count => _count;

    public void Initialize(ItemDefinitionSO definition, int count)
    {
        _definition = definition;
        _count = count;
    }

    private void Start()
    {
        gameObject.layer = PhysicsLayers.Interactable;

        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.5f;
        }
    }

    public bool TryCollect(PlayerInventory inventory)
    {
        if (_definition == null || _count <= 0) return false;

        var instance = ItemInstance.Create(_definition.itemId);
        if (!inventory.TryAdd(instance, _count))
            return false;

        Debug.Log($"picked up {_count}x {_definition.displayName}");
        Destroy(gameObject);
        return true;
    }
}
