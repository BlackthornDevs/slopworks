using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the StorageContainer simulation class.
/// Owns a StorageContainer instance and exposes it for other systems to drive.
/// All simulation logic lives in StorageContainer (D-004).
/// </summary>
public class StorageBehaviour : MonoBehaviour
{
    [SerializeField] private StorageDefinitionSO _definition;

    private StorageContainer _container;

    public StorageContainer Container => _container;
    public StorageDefinitionSO Definition => _definition;

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("StorageBehaviour: missing storage definition", this);
            return;
        }

        _container = new StorageContainer(_definition.slotCount, _definition.maxStackSize);
    }
}
