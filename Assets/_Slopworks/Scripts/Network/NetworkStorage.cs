using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkStorage : NetworkBehaviour
{
    private StorageContainer _container;

    private readonly SyncList<ItemSlot> _slots = new();

    public StorageContainer Container => _container;
    public int SlotCount => _slots.Count;

    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return ItemSlot.Empty;
        return _slots[index];
    }

    public void ServerInit(StorageContainer container)
    {
        _container = container;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_container == null)
            _container = new StorageContainer(12, 64);

        for (int i = 0; i < _container.SlotCount; i++)
            _slots.Add(_container.GetSlot(i));

        _container.OnSlotChanged += OnContainerSlotChanged;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_container != null)
            _container.OnSlotChanged -= OnContainerSlotChanged;
    }

    private void OnContainerSlotChanged(int index)
    {
        if (index >= 0 && index < _slots.Count)
            _slots[index] = _container.GetSlot(index);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdInsertItem(string itemId, int count)
    {
        if (!IsServerInitialized || _container == null) return;
        if (_container.TryInsertStack(itemId, count))
            Debug.Log($"storage: inserted {itemId} x{count}");
    }
}
