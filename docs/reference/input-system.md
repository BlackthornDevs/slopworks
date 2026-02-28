# Input system reference

New Input System (com.unity.inputsystem) for all player input in Slopworks. This covers package setup, the two Action Maps, C# class generation, and network integration patterns.

---

## Package choice: New Input System, not legacy

Unity's legacy `Input` class (`Input.GetKey`, `Input.GetAxis`) is deprecated and will be removed. The New Input System provides:
- Action-based input with player rebinding support
- Multiple device abstraction (keyboard/mouse, gamepad)
- Generated C# classes for type-safe access
- Clean separation between input reading and game logic

**Package:** `com.unity.inputsystem 1.7+`

Enable in Project Settings > Player > Active Input Handling: `Input System Package (New)`.

---

## Action Maps

Two Action Maps cover all gameplay contexts. Never have both enabled simultaneously — they share bindings (WASD means different things in each).

### Factory (isometric view)

```
Camera/Pan        → Mouse drag, WASD
Camera/Zoom       → Mouse wheel, shoulder buttons
Camera/Rotate     → Middle mouse drag, right stick
Place/Select      → Mouse click, A button
Place/Rotate      → R key, Y button
Place/Cancel      → Escape, B button
Inventory/Open    → Tab, Start button
UI/Navigate       → Arrow keys, d-pad
Switch/FPS        → V key, Select button
```

### Exploration (first-person)

```
Move              → WASD, left stick
Look              → Mouse, right stick
Jump              → Space, A button
Fire              → Left click, right trigger
Aim               → Right click, left trigger
Interact          → E key, X button
Sprint            → Shift, left stick click
Inventory/Open    → Tab, Start button
Switch/Isometric  → V key, Select button
```

---

## Setup

### 1. Create the InputActionAsset

1. Right-click in Project window: Create > Input Actions
2. Name it `SlopworksInput`
3. Save to `Assets/_Slopworks/Input/SlopworksInput.inputactions`
4. Add `Factory` and `Exploration` Action Maps
5. Add all actions above with bindings

### 2. Generate the C# class

In the InputActionAsset inspector:
- Check **Generate C# Class**
- Class Name: `SlopworksControls`
- Path: `Assets/_Slopworks/Scripts/Player/SlopworksControls.cs`
- Click Apply

This generates a type-safe wrapper. **Never edit the generated file.** Regenerate when the asset changes.

### 3. Use the generated class

```csharp
public class PlayerController : NetworkBehaviour
{
    private SlopworksControls _controls;

    private void Awake()
    {
        _controls = new SlopworksControls();
    }

    private void OnEnable()
    {
        // only the owning client handles input
        if (!IsOwner) return;

        _controls.Exploration.Enable();
        _controls.Exploration.Move.performed += OnMove;
        _controls.Exploration.Fire.performed += OnFire;
        _controls.Exploration.Switch_Isometric.performed += OnSwitchToIsometric;
    }

    private void OnDisable()
    {
        if (!IsOwner) return;

        _controls.Exploration.Move.performed -= OnMove;
        _controls.Exploration.Fire.performed -= OnFire;
        _controls.Exploration.Switch_Isometric.performed -= OnSwitchToIsometric;
        _controls.Exploration.Disable();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        var move = ctx.ReadValue<Vector2>();
        MoveServerRpc(move);
    }

    private void OnFire(InputAction.CallbackContext ctx)
    {
        FireServerRpc(transform.position, _lookDirection);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector2 input) { ... }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction) { ... }
}
```

### 4. Camera mode switching

```csharp
public class CameraModeManager : MonoBehaviour
{
    private SlopworksControls _controls;

    private void Awake()
    {
        _controls = new SlopworksControls();
    }

    public void SwitchToIsometric()
    {
        _controls.Exploration.Disable();
        _controls.Factory.Enable();
        // toggle camera GameObjects and Volume weights (see render-pipeline.md)
    }

    public void SwitchToFPS()
    {
        _controls.Factory.Disable();
        _controls.Exploration.Enable();
    }
}
```

---

## Multiplayer: input is always local

Input reads happen only on the owning client. The server never reads `SlopworksControls`.

```csharp
// Always guard input handling with IsOwner
private void OnEnable()
{
    if (!IsOwner) return;
    _controls.Exploration.Enable();
    // ...
}
```

Never send raw input state over the network. Send **intent** (fire, move to position, interact with machine) via `[ServerRpc]`.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Legacy `Input.GetKey` calls | Replace with action callbacks |
| Both Action Maps enabled at once | Disable Factory before enabling Exploration, and vice versa |
| Forgetting `-=` on `OnDisable` | Always unsubscribe in `OnDisable` |
| Reading input on non-owner clients | Guard with `if (!IsOwner) return;` |
| Sending raw input over network | Send intent ServerRpc instead |
