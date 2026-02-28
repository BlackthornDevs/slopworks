using UnityEngine;

/// <summary>
/// Read-only definition for a storage container building type.
/// Never mutate at runtime -- SOs are shared across all instances.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/Buildings/Storage")]
public class StorageDefinitionSO : ScriptableObject, IPlaceableDefinition
{
    public string storageId;
    public string displayName;
    public int slotCount = 20;
    public int maxStackSize = 50;
    public Vector2Int size = Vector2Int.one;
    public GameObject prefab;
    public Sprite icon;

    /// <summary>
    /// I/O port definitions for belt connections.
    /// </summary>
    public MachinePort[] ports;

    public string PlaceableId => storageId;
    Vector2Int IPlaceableDefinition.Size => size;
}
