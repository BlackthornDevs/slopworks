using FishNet.Object;
using UnityEngine;

public class NetworkHotbarHUD : NetworkBehaviour
{
    private NetworkInventory _inventory;

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
        _inventory = GetComponent<NetworkInventory>();
    }

    private void Update()
    {
        if (!IsOwner || _inventory == null) return;

        // Scroll wheel for hotbar selection
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f)
        {
            int current = _inventory.SelectedHotbarIndex;
            int next = current - Mathf.RoundToInt(scroll);
            next = ((next % NetworkInventory.HotbarSlots) + NetworkInventory.HotbarSlots) % NetworkInventory.HotbarSlots;
            _inventory.CmdSelectHotbar(next);
        }
    }

    private void OnGUI()
    {
        if (!IsOwner || _inventory == null) return;

        float slotSize = 50f;
        float padding = 4f;
        float totalWidth = NetworkInventory.HotbarSlots * (slotSize + padding) - padding;
        float startX = (Screen.width - totalWidth) / 2f;
        float y = Screen.height - slotSize - 20f;

        for (int i = 0; i < NetworkInventory.HotbarSlots; i++)
        {
            float x = startX + i * (slotSize + padding);
            var rect = new Rect(x, y, slotSize, slotSize);

            bool selected = i == _inventory.SelectedHotbarIndex;
            GUI.color = selected ? Color.yellow : new Color(0.3f, 0.3f, 0.3f, 0.8f);
            GUI.Box(rect, "");

            var slot = _inventory.GetSlot(i);
            if (!slot.IsEmpty)
            {
                GUI.color = Color.white;
                GUI.Label(rect, $"{slot.item.definitionId}\nx{slot.count}");
            }

            GUI.color = Color.white;
        }
    }
}
