using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the StorageContainer simulation class.
/// Owns a StorageContainer instance and exposes it for other systems to drive.
/// Implements IInteractable so players can press E to open the storage panel.
/// All simulation logic lives in StorageContainer (D-004).
/// </summary>
public class StorageBehaviour : MonoBehaviour, IInteractable
{
    [SerializeField] private StorageDefinitionSO _definition;

    private StorageContainer _container;

    public StorageContainer Container => _container;
    public StorageDefinitionSO Definition => _definition;

    private void Awake()
    {
        // Skip if Initialize() already set the container (bootstrapper path)
        if (_container != null)
            return;

        if (_definition == null)
        {
            Debug.LogError("StorageBehaviour: missing storage definition", this);
            return;
        }

        _container = new StorageContainer(_definition.slotCount, _definition.maxStackSize);
    }

    /// <summary>
    /// Initializes with an existing container (used by bootstrappers that create the
    /// container before adding the component).
    /// </summary>
    public void Initialize(StorageDefinitionSO definition, StorageContainer container)
    {
        _definition = definition;
        _container = container;
    }

    public string GetInteractionPrompt()
    {
        string name = _definition != null ? _definition.displayName : "storage";
        return $"press E to open {name}";
    }

    public void Interact(GameObject player)
    {
        var storageUI = FindAnyObjectByType<StorageUI>();
        if (storageUI == null)
        {
            Debug.LogWarning("StorageBehaviour: no StorageUI found in scene");
            return;
        }

        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("StorageBehaviour: player has no PlayerInventory");
            return;
        }

        storageUI.Open(this, inventory);
    }
}
