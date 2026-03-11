using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkInventory : NetworkBehaviour
{
    public const int HotbarSlots = 9;
    public const int MainSlots = 36;
    public const int TotalSlots = HotbarSlots + MainSlots;

    private readonly SyncList<ItemSlot> _slots = new();
    private readonly SyncVar<int> _selectedHotbar = new();

    public int SlotCount => TotalSlots;
    public int SelectedHotbarIndex => _selectedHotbar.Value;

    public event Action<int> OnSlotChanged;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        _slots.OnChange += OnSlotsChanged;
        _selectedHotbar.OnChange += OnHotbarChanged;

        if (IsServerInitialized)
        {
            for (int i = 0; i < TotalSlots; i++)
                _slots.Add(ItemSlot.Empty);
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        _slots.OnChange -= OnSlotsChanged;
        _selectedHotbar.OnChange -= OnHotbarChanged;
    }

    public ItemSlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return ItemSlot.Empty;
        return _slots[index];
    }

    public ItemSlot SelectedSlot => GetSlot(_selectedHotbar.Value);

    [ServerRpc]
    public void CmdSelectHotbar(int index)
    {
        if (index < 0 || index >= HotbarSlots) return;
        _selectedHotbar.Value = index;
    }

    [ServerRpc]
    public void CmdPickupItem(NetworkObject worldItemNob)
    {
        if (!IsServerInitialized) return;
        if (worldItemNob == null) return;

        var worldItem = worldItemNob.GetComponent<NetworkWorldItem>();
        if (worldItem == null) return;

        string itemId = worldItem.ItemId;
        int count = worldItem.Count;

        if (string.IsNullOrEmpty(itemId) || count <= 0) return;

        if (TryAddServer(itemId, count))
        {
            ServerManager.Despawn(worldItemNob.gameObject);
            Debug.Log($"inventory: {itemId} x{count} picked up");
        }
    }

    private bool TryAddServer(string definitionId, int count)
    {
        int maxStack = 64; // Default max stack; proper lookup can come later
        int remaining = count;

        // First pass: stack onto existing
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item.definitionId != definitionId) continue;

            int space = maxStack - slot.count;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(remaining, space);
            slot.count += toAdd;
            _slots[i] = slot;
            remaining -= toAdd;
        }

        // Second pass: fill empty
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            int toAdd = Mathf.Min(remaining, maxStack);
            _slots[i] = new ItemSlot
            {
                item = ItemInstance.Create(definitionId),
                count = toAdd
            };
            remaining -= toAdd;
        }

        return remaining == 0;
    }

    [ServerRpc]
    public void CmdDepositIntoStorage(NetworkObject storageNob, int hotbarSlot)
    {
        if (!IsServerInitialized) return;
        if (storageNob == null) return;
        if (hotbarSlot < 0 || hotbarSlot >= _slots.Count) return;

        var netStorage = storageNob.GetComponent<NetworkStorage>();
        if (netStorage == null || netStorage.Container == null) return;

        var slot = _slots[hotbarSlot];
        if (slot.IsEmpty) return;

        if (netStorage.Container.TryInsertStack(slot.item.definitionId, slot.count))
        {
            Debug.Log($"inventory: deposited {slot.item.definitionId} x{slot.count} into storage");
            _slots[hotbarSlot] = ItemSlot.Empty;
        }
    }

    [ServerRpc]
    public void CmdWithdrawFromStorage(NetworkObject storageNob, string itemId, int count)
    {
        if (!IsServerInitialized) return;
        if (storageNob == null || string.IsNullOrEmpty(itemId) || count <= 0) return;

        var netStorage = storageNob.GetComponent<NetworkStorage>();
        if (netStorage == null || netStorage.Container == null) return;

        int available = netStorage.Container.GetCount(itemId);
        int toTake = Mathf.Min(count, available);
        if (toTake <= 0) return;

        if (TryAddServer(itemId, toTake))
        {
            // Extract from storage -- one at a time since ExtractAll takes everything
            for (int i = 0; i < toTake; i++)
                netStorage.Container.TryExtract(out _);
            Debug.Log($"inventory: withdrew {itemId} x{toTake} from storage");
        }
    }

    public int GetCount(string definitionId)
    {
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.IsEmpty && slot.item.definitionId == definitionId)
                total += slot.count;
        }
        return total;
    }

    private void OnSlotsChanged(SyncListOperation op, int index, ItemSlot oldItem, ItemSlot newItem, bool asServer)
    {
        if (asServer && IsHostStarted) return;
        OnSlotChanged?.Invoke(index);
    }

    private void OnHotbarChanged(int prev, int next, bool asServer)
    {
        if (asServer && IsHostStarted) return;
    }
}
