# Tasks for Joe (junior developer)

Assigned by lead. Work on `joe/main`. Merge `master` first to pick up the project skeleton.

---

## TASK J-001: FPS Character Controller + Camera Toggle ✅

**Priority:** High -- needed for the vertical slice
**Branch:** `joe/main`
**Ownership:** `Scripts/Player/`, `Prefabs/Player/`
**Status:** Complete (2026-02-28)
**Commits:** `c251da1` (main implementation), `ac55db6` (visual feedback on test interactable)

### What to build

A first-person character controller with a toggleable isometric camera mode. This is the player's primary way of moving through the world.

### Requirements

1. **Player character prefab** (`Prefabs/Player/PlayerCharacter.prefab`):
   - Capsule collider (radius 0.3, height 1.8)
   - Rigidbody (freeze rotation X/Z)
   - Child camera at eye height (1.6 units)

2. **FPS movement** (`Scripts/Player/PlayerController.cs`):
   - WASD movement
   - Mouse look (cursor locked)
   - Jump (space)
   - Sprint (shift)
   - Responsive feel -- no floaty movement

3. **Camera mode toggle** (`Scripts/Player/CameraModeController.cs`):
   - Toggle between FPS and isometric with a key binding
   - FPS: perspective projection, cursor locked, WASD movement
   - Isometric: orthographic projection, cursor visible, click-to-interact
   - Use URP Camera Stacking per `docs/reference/render-pipeline.md` -- toggle camera GameObjects, don't reconfigure a single camera
   - See `docs/reference/input-system.md` for camera toggle binding

4. **Interaction system** (`Scripts/Player/InteractionController.cs`):
   - Raycast from camera center each frame using `PhysicsLayers.InteractMask`
   - If hit has `IInteractable`, show prompt text
   - Press E to interact
   - Make it work in both FPS and isometric modes

### Architectural constraints

- **Use New Input System only** (decision D-003). Two Action Maps: Factory (isometric) and Exploration (FPS). See `docs/reference/input-system.md`.
- **Never use `Input.GetKeyDown` or legacy Input.** Use `SlopworksControls` generated callbacks.
- **Raycast masks:** Use `PhysicsLayers.InteractMask` for interaction, never magic numbers or SerializeField LayerMasks for standard masks.
- **Player layer:** Set prefab root to layer 8 (`PhysicsLayers.Player`).
- **`IInteractable` interface** is already in `Scripts/Core/IInteractable.cs` on master. Use it directly.

### Reference docs

- `docs/reference/input-system.md` -- Action Maps, generated C# class, camera toggle
- `docs/reference/render-pipeline.md` -- Camera Stacking setup
- `docs/reference/physics-layers.md` -- layer assignments, interaction raycast mask

### Test criteria

- Walk around with WASD, look with mouse, jump, sprint
- Toggle camera mode -- both views render correctly
- Cursor locks in FPS, unlocks in isometric
- Walk up to an object with `IInteractable`, see prompt, press E to interact

### Status: COMPLETE

---

## TASK J-002: Clean up TMP extras and merge to master

**Priority:** High -- blocking master integration
**Branch:** `joe/main`
**Status:** Complete (2026-02-28)

### What to do

Your J-001 player controller work is done and ready for master, but `joe/main` has the full TextMesh Pro Examples & Extras folder committed (hundreds of sample scenes, fonts, textures, shaders). This bloat should not go to master.

### Steps

1. Add the following to `.gitignore`:
   ```
   # TextMesh Pro examples (not needed in repo)
   Assets/TextMesh Pro/Examples & Extras/
   ```

2. Remove it from git tracking (keeps the files locally, just stops tracking):
   ```bash
   git rm -r --cached "Assets/TextMesh Pro/Examples & Extras/"
   ```

3. Commit:
   ```
   Remove TMP Examples & Extras from tracking and gitignore
   ```

4. Merge latest master into joe/main first (Phase 1 factory systems are now on master):
   ```bash
   git fetch origin master
   git merge origin/master
   ```

5. Resolve any conflicts, then merge joe/main into master:
   ```bash
   git checkout master
   git merge joe/main
   git push origin master
   ```

### Why

The TMP Examples & Extras is ~300 files of sample content that Unity imports on demand. It inflates the repo size and clutters diffs. The actual TMP runtime (fonts, shaders, resources) stays tracked -- only the examples get excluded.

### After merging

Master will have both the Phase 1 factory systems (from kevin/main) and the player controller + input system (from joe/main). Both branches should then pull the merged master.

---

## Note for Kevin's Claude (2026-02-28)

J-001 and J-002 are both complete. Here's the current state:

- **Master is up to date.** `joe/main` was merged into `master` via fast-forward (commit `d5dde89`). Master now has both the Phase 1 factory systems and the full player controller stack.
- **Player controller details:** FPS movement (WASD/sprint/jump/mouse look), camera mode toggle (V key, FPS ↔ isometric), interaction system (raycast from camera center, E key, IInteractable interface), HUD canvas with interaction prompt text. All wired via editor menu item `Slopworks/Wire PlayerCharacter References`.
- **Bug fix included:** `InteractionController.OnDisable()` now calls `ClearTarget()` so the prompt text clears when switching to isometric mode.
- **TMP cleanup done.** `Assets/TextMesh Pro/Examples & Extras/` is gitignored and removed from tracking. TMP runtime (fonts, shaders, resources) remains tracked.
- **Dev_Test scene** has a test cube at (0, 0.5, 3) on layer 14 with `TestInteractable` — toggles green on interaction. Good for verifying the interaction loop.
- **Plugins.meta GUID conflict** was resolved during the master merge — kept Joe's GUID (`44395c0d`). If Kevin's project references the other GUID (`df6e3fa2`), Unity will regenerate it on import. No action needed.
- **`joe/main` needs a new task (J-003).** Joe's owned systems: `Scripts/Player/`, `Scripts/UI/`, `Scripts/Combat/`, `Scripts/Network/`, `Prefabs/Player/`, `Prefabs/UI/`.
