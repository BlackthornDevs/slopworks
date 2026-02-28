# UI framework reference

uGUI (Unity UI) as the primary framework for Slopworks, with UI Toolkit reserved for Phase 2 data-heavy screens. This covers the decision rationale, world-space machine panels, FishNet binding patterns, the drag-drop inventory, and the two-developer workflow.

---

## Framework choice: uGUI now, UI Toolkit later

**Use uGUI for Phase 1 (launch).**

| Criterion | uGUI | UI Toolkit |
|-----------|------|------------|
| World-space UI (machine panels) | Native Canvas Render Mode | Requires render-to-texture workaround |
| Animation (health bars, alerts, tweens) | Animator, Timeline, DOTween | CSS transitions only |
| FishNet SyncVar binding | Direct callback pattern (proven) | Custom INotifyPropertyChanged (more complex) |
| Learning curve | Inspector-editable, familiar | UXML + USS (web-like, new to most Unity devs) |
| Performance at Slopworks' scale (50–100 UI elements) | Negligible difference | Negligible difference |

**Blocker:** World-space Canvas rendering (needed for machine status panels floating above machines in 3D) is native in uGUI and requires workarounds in UI Toolkit. uGUI wins on this single point.

**When to introduce UI Toolkit:** Phase 2+, for the overworld network map (50+ building nodes with data binding) and advanced factory analytics screens. Do not migrate existing uGUI screens speculatively.

---

## Canvas setup

Three canvas types for different contexts:

```
Canvas (Screen Space - Overlay)     — HUD, alerts, crosshair (always on top)
Canvas (Screen Space - Camera)      — Build menu, inventory, menus
Canvas (World Space)                — Machine status panels (floating above machines)
```

```csharp
// World-space canvas scale: readable from ~3m away
// RectTransform scale: (0.01, 0.01, 0.01) on a 1x1 canvas = 0.01m per unit
```

---

## World-space machine panels

One Canvas per machine type, parented above the machine:

```
Smelter (NetworkObject, layer: Structures)
└── MachineStatusPanel (Canvas, Render Mode: World Space, scale: 0.01)
    ├── StatusText (Text)
    └── HealthBar (Image, Image Type: Filled)
```

```csharp
public class MachineStatusPanel : MonoBehaviour
{
    [SerializeField] private Text _statusText;
    [SerializeField] private Image _healthBar;

    private MachineNetworkState _machine;

    private void OnEnable()
    {
        _machine = GetComponentInParent<MachineNetworkState>();
        // Read initial state
        Refresh(_machine.Status, _machine.HealthPercent);
    }

    // Called from MachineNetworkState.OnStatusChanged SyncVar callback
    public void Refresh(MachineStatus status, float healthPercent)
    {
        _statusText.text = status.ToString();
        _healthBar.fillAmount = healthPercent;
    }
}
```

---

## FishNet SyncVar → UI binding

**Pattern: direct callback, single controller per machine.**

One MonoBehaviour reads all SyncVar callbacks and updates all dependent UI:

```csharp
public class MachineNetworkState : NetworkBehaviour
{
    [SyncVar(OnChange = nameof(OnStatusChanged))]
    private MachineStatus _status = MachineStatus.Idle;

    [SyncVar(OnChange = nameof(OnHealthChanged))]
    private float _health = 100f;

    public MachineStatus Status => _status;
    public float HealthPercent => _health / 100f;

    // Fires on both server and all clients
    private void OnStatusChanged(MachineStatus old, MachineStatus next, bool asServer)
    {
        if (asServer) return;
        GetComponentInChildren<MachineStatusPanel>()
            ?.Refresh(next, HealthPercent);
    }

    private void OnHealthChanged(float old, float next, bool asServer)
    {
        if (asServer) return;
        GetComponentInChildren<MachineStatusPanel>()
            ?.Refresh(_status, next / 100f);
    }
}
```

Don't update UI in `Update()` every frame. Use callbacks so the UI only re-renders when state actually changes.

---

## SyncList binding (inventory)

```csharp
public class NetworkInventory : NetworkBehaviour
{
    [SyncObject]
    private readonly SyncList<ItemSlot> _slots = new();

    private void OnEnable()
    {
        _slots.OnChange += OnInventoryChanged;
    }

    private void OnDisable()
    {
        _slots.OnChange -= OnInventoryChanged;
    }

    private void OnInventoryChanged(SyncListOperation op, int index,
        ItemSlot oldValue, ItemSlot newValue, bool asServer)
    {
        if (asServer) return;
        // only the changed slot re-renders
        GetComponentInChildren<InventoryUI>()
            ?.RefreshSlot(index, newValue);
    }

    [ServerRpc]
    public void RequestSwapServerRpc(int fromSlot, int toSlot)
    {
        if (!IsServerInitialized) return;
        // validate, then swap
        (_slots[fromSlot], _slots[toSlot]) = (_slots[toSlot], _slots[fromSlot]);
    }
}
```

SyncList sends only the delta (changed slot), not the full list. The `OnChange` callback tells you which index changed.

---

## Drag-drop inventory

```csharp
public class InventorySlot : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public int SlotIndex;
    private NetworkInventory _inventory;
    private Image _icon;

    public void OnBeginDrag(PointerEventData e)
    {
        _icon.raycastTarget = false;    // allow drop targets to receive events
    }

    public void OnDrag(PointerEventData e)
    {
        _icon.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        _icon.raycastTarget = true;
        // icon snaps back via SyncList callback when server confirms swap
    }

    public void OnDrop(PointerEventData e)
    {
        var fromSlot = e.pointerDrag.GetComponent<InventorySlot>();
        if (fromSlot == null) return;
        _inventory.RequestSwapServerRpc(fromSlot.SlotIndex, SlotIndex);
    }
}
```

The UI sends the intent (`RequestSwapServerRpc`). The server validates and modifies `_slots`. The SyncList `OnChange` callback updates the UI. The UI never modifies data directly.

---

## Two-developer prefab workflow

Separate UI prefabs by responsibility to avoid merge conflicts:

```
Prefabs/UI/
  HUD/
    FactoryHUD.prefab       — Kevin owns (machine status, power, belt)
    PlayerHUD.prefab        — Joe owns (health, ammo, inventory button)
    AlertPanel.prefab       — Shared read-only
  Menus/
    BuildMenu.prefab        — Kevin
    InventoryScreen.prefab  — Joe
    SettingsMenu.prefab     — Joe
  WorldSpace/
    MachineStatusPanel.prefab  — Kevin
```

Assemble the HUD from prefabs at runtime to avoid scene conflicts:

```csharp
public class UIComposer : MonoBehaviour
{
    [SerializeField] private FactoryHUD _factoryHUDPrefab;
    [SerializeField] private PlayerHUD _playerHUDPrefab;

    private void Awake()
    {
        Instantiate(_factoryHUDPrefab, transform);
        Instantiate(_playerHUDPrefab, transform);
    }
}
```

---

## Known issues

- **ParrelSync + UI Toolkit:** Input events don't always propagate correctly when running two editor instances (Unity bug IN-54628). uGUI has no such issue.
- **Callback ordering:** When multiple MonoBehaviours subscribe to the same SyncVar callback, order is undefined. Use a single controller that updates all dependent UI from one callback.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Updating UI in `Update()` every frame | Use SyncVar `OnChange` callbacks |
| UI Toolkit for world-space machine panels | Use uGUI Canvas (Render Mode: World Space) |
| Multiple components subscribing to same SyncVar | Single controller updates all UI from one callback |
| Two devs editing same HUD prefab | Assign prefab ownership; assemble at runtime |
| Client modifying inventory data directly | Client sends ServerRpc intent; server modifies SyncList |
