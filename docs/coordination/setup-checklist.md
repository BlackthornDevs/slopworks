# Project setup checklist

Both developers follow this exactly. Deviations cause merge conflicts.

---

## Prerequisites

- [ ] Unity Hub installed
- [ ] Unity 6.3 LTS installed via Unity Hub
- [ ] Git LFS installed (`git lfs install`)
- [ ] Node.js 18+ (for MCP Unity, optional)

---

## Phase A: Shared skeleton on master (one developer does this, the other pulls)

**Who does this:** Kevin (lead). Joe pulls the result.

### A1. Create Unity project

1. Open Unity Hub > New Project
2. Template: **3D (URP)**
3. Unity version: **6.3 LTS**
4. Location: the repo root (`C:\Users\KevinAmditis\source\repos\Slopworks`)
5. Unity Hub may create a subfolder -- if it does, move all files up to repo root and delete the empty subfolder

### A2. Configure project settings

In the Unity Editor:

1. Edit > Project Settings > Player:
   - Company Name: `BlackthornDevs`
   - Product Name: `Slopworks`
   - Color Space: **Linear**
   - Api Compatibility Level: **.NET Standard 2.1**
   - Scripting Backend: **IL2CPP** (for release builds; Mono is fine for dev if IL2CPP is slow to iterate)

2. Edit > Project Settings > Graphics:
   - Verify SRP Batcher is enabled on the URP Pipeline Asset

3. Select URP Renderer Asset (in Project window):
   - Rendering Path: **Forward+**

4. Edit > Project Settings > Physics:
   - Set up layer collision matrix per `docs/reference/physics-layers.md`

5. Edit > Project Settings > Tags and Layers:
   - Configure layers 8-19 per `docs/reference/physics-layers.md`

6. Edit > Project Settings > Editor:
   - Asset Serialization: **Force Text**
   - Version Control Mode: **Visible Meta Files** (should be default in Unity 6)

### A3. Install packages

In Unity Package Manager (Window > Package Manager):

1. **FishNet** -- Add from git URL: `https://github.com/FirstGearGames/FishNet.git`
   - Or download from Asset Store (free)
   - Verify: `Assets/FishNet/` exists with Runtime, Editor folders
   - Tugboat transport is included (for local dev)

2. **ParrelSync** -- Add from git URL: `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`
   - For multiplayer testing with two editor instances

3. **Input System** -- Add via Package Manager > Unity Registry > Input System
   - When prompted, select "Yes" to enable the new backend
   - Edit > Project Settings > Player > Active Input Handling: **Both** (during dev, legacy fallback helps with debugging)

4. **NPBehave** -- Download from `https://github.com/meniku/NPBehave`
   - Place in `Assets/_Slopworks/Plugins/NPBehave/`

5. **TextMeshPro** -- should be included with URP template. If not, add from Unity Registry.

### A4. Create folder structure

```
Assets/
  _Slopworks/
    Scripts/
      Automation/
      Combat/
      Network/
      Player/
      World/
      UI/
      Core/
    ScriptableObjects/
      Items/
      Recipes/
      Events/
      Buildings/
    Prefabs/
      Player/
      Machines/
      Belt/
      Buildings/
      UI/
      FX/
    Materials/
    Shaders/
    Audio/
    Plugins/
      NPBehave/
    Tests/
      Editor/
        EditMode/
      PlayMode/
  Scenes/
    Core/
    HomeBase/
    Buildings/
    Overworld/
```

Add `.gitkeep` in each leaf folder.

### A5. Create shared base scripts

These go on `master` because both branches depend on them:

1. `Scripts/Core/GameEventSO.cs` -- ScriptableObject event bus
2. `Scripts/Core/GameEventListener.cs` -- MonoBehaviour listener
3. `Scripts/Core/ItemDefinitionSO.cs` -- item definition (read-only SO)
4. `Scripts/Core/ItemInstance.cs` -- per-instance item state (struct)
5. `Scripts/Core/ItemSlot.cs` -- inventory slot (struct)
6. `Scripts/Core/RecipeSO.cs` -- recipe definition (read-only SO)
7. `Scripts/Core/IInteractable.cs` -- interaction interface
8. `Scripts/Core/ISceneService.cs` -- scene loading interface (for future Addressables swap)
9. `Scripts/Core/PhysicsLayers.cs` -- layer constants and raycast masks
10. `Scripts/Automation/MachineStatus.cs` -- enum (Idle, Working, Blocked)

### A6. Create event assets

In `ScriptableObjects/Events/`:
- `SceneTransitionRequested.asset`
- `BuildingClaimed.asset`
- `WaveStarted.asset`
- `WaveEnded.asset`

### A7. Create Core scenes

1. `Scenes/Core/Core_Network.unity`:
   - Empty GameObject `NetworkManager` with FishNet NetworkManager + Tugboat transport
   - Empty GameObject `Bootstrap` with Bootstrap.cs (additive scene loader)

2. `Scenes/Core/Core_GameManager.unity`:
   - Empty GameObject `GameManager` (script added later)
   - Empty GameObject `ItemRegistry` with registry component
   - Empty GameObject `RecipeRegistry` with registry component

3. Set `Core_Network` as scene index 0 in Build Settings.
4. Add `Core_GameManager` to Build Settings.

### A8. Configure input

1. Create `Assets/_Slopworks/SlopworksControls.inputactions`
2. Define two Action Maps:
   - **Factory** (isometric mode): mouse click, WASD pan, scroll zoom, B for build mode, E for interact, Escape for menu
   - **Exploration** (FPS mode): WASD move, mouse look, space jump, shift sprint, left-click shoot, E interact, Tab toggle camera, Escape menu
3. Generate C# class: check "Generate C# Class" in the inspector
   - Path: `Assets/_Slopworks/Scripts/Player/SlopworksControls.cs`
4. Never edit the generated file manually.

### A9. Git LFS and commit

```bash
git lfs install
git lfs track "*.png" "*.jpg" "*.psd" "*.fbx" "*.obj" "*.blend" "*.mp3" "*.wav" "*.ogg" "*.dll"
git add -A
git commit -m "Project skeleton: Unity 6.3 LTS, URP, FishNet, folder structure, shared types"
git push origin master
```

---

## Phase B: Both developers set up their environment

**Both Kevin and Joe do these steps.**

### B1. Pull master

```bash
git pull origin master
```

### B2. Open project in Unity

1. Open Unity Hub
2. Add existing project: point to repo root
3. Unity will regenerate the Library folder (takes a few minutes)
4. Verify: no console errors, FishNet imported, Input System active

### B3. Create your branch (if not already created)

```bash
# Kevin:
git checkout -b kevin/main master

# Joe:
git checkout -b joe/main master
```

### B4. Set up MCP Unity (optional but recommended)

Per `docs/reference/team-workflow.md`:
1. Tools > MCP Unity > Server Window > Configure for Claude Code
2. Start WebSocket server
3. Add MCP config to `~/.claude.json`

### B5. Set up ParrelSync clone (for multiplayer testing)

1. ParrelSync > Clones Manager > Create Clone
2. Clone opens as a second Editor instance
3. One instance runs as Host, other as Client connecting to localhost

---

## Phase C: Ongoing coordination

1. **Merge master into your branch daily.** Coordination updates, shared type changes, and new decisions come through master.
   ```bash
   git fetch origin master
   git merge origin/master
   ```

2. **Never edit shared files on your branch.** If you need to change a shared type (anything in `Scripts/Core/`, `ScriptableObjects/`, `ProjectSettings/`), push the change to `master` first, then merge into your branch.

3. **Check `docs/coordination/decisions.md`** before making architectural choices. If your decision isn't covered, write to `contradictions.md` and wait for lead resolution.

4. **Push your branch frequently.** Short-lived feature branches off your main. Merge back within a day.

---

## Package version lock

Both branches must use identical package versions. Current versions (update this when upgrading):

| Package | Version | Source |
|---------|---------|--------|
| FishNet | latest from git/Asset Store | GitHub or Asset Store |
| ParrelSync | latest from git | GitHub |
| Input System | 1.7+ from Unity Registry | Unity |
| NPBehave | latest from GitHub | Plugins/ folder |
| TextMeshPro | included with URP | Unity |
| URP | 17.x (ships with Unity 6.3) | Unity |

**Deferred packages (not installed yet):**
- Addressables (D-002: deferred)
- VContainer (D-001: not using)
- UniTask (D-006: not needed until Addressables/Supabase)
- FMOD Studio (audio, post-vertical-slice)
- FishySteamworks (post-vertical-slice, needs Facepunch.Steamworks)
- supabase-csharp (D-006: deferred)
