# Team workflow reference

Two-person team: Joe (jamditis) + Kevin (kamditis). Parallel development experiment — each builds their own version from the same design doc, then combine the best parts.

---

## Git setup for Unity

### .gitattributes

Create at repo root. Two jobs: tell git which files are text vs binary, and configure UnityYAMLMerge for scene/prefab conflicts.

```
# Unity YAML files — use Smart Merge tool
*.unity      merge=unityyamlmerge  eol=lf
*.prefab     merge=unityyamlmerge  eol=lf
*.mat        merge=unityyamlmerge  eol=lf
*.anim       merge=unityyamlmerge  eol=lf
*.controller merge=unityyamlmerge  eol=lf
*.asset      merge=unityyamlmerge  eol=lf

# C# and shaders as text
*.cs         diff=csharp text
*.shader     text
*.hlsl       text
*.cginc      text
*.json       text
*.xml        text
*.md         text

# Binary assets via Git LFS
*.png        filter=lfs diff=lfs merge=lfs -text
*.jpg        filter=lfs diff=lfs merge=lfs -text
*.psd        filter=lfs diff=lfs merge=lfs -text
*.fbx        filter=lfs diff=lfs merge=lfs -text
*.obj        filter=lfs diff=lfs merge=lfs -text
*.blend      filter=lfs diff=lfs merge=lfs -text
*.mp3        filter=lfs diff=lfs merge=lfs -text
*.wav        filter=lfs diff=lfs merge=lfs -text
*.ogg        filter=lfs diff=lfs merge=lfs -text
*.dll        filter=lfs diff=lfs merge=lfs -text

# Critical: LightingData and TerrainData must be binary to prevent corruption
LightingData.asset    filter=lfs diff=lfs merge=lfs -text
TerrainData.asset     filter=lfs diff=lfs merge=lfs -text
```

Note: YAMLMerge officially supports only `.unity` and `.prefab`. Other `.asset` types marked with `merge=unityyamlmerge` get best-effort treatment — don't rely on it for complex asset types.

### UnityYAMLMerge (Smart Merge) setup

Add to `~/.gitconfig` or the repo's `.git/config`:

```ini
[merge]
    tool = unityyamlmerge

[mergetool "unityyamlmerge"]
    trustExitCode = false
    cmd = '<path to UnityYAMLMerge>' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```

Paths:
- **Windows:** `C:\Program Files\Unity\Editor\Data\Tools\UnityYAMLMerge.exe`
- **macOS:** `/Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge`

Run `git mergetool` when you hit a scene/prefab conflict.

### .gitignore

Repo already has a solid Unity `.gitignore`. Key items it should include (verify):
- `/[Ll]ibrary/` — never commit, regenerated on project open
- `/[Tt]emp/` — build artifacts
- `*.log` — Unity logs
- `Assets/StreamingAssets/supabase-config.json` — credentials (already added)

---

## Scene organization (merge conflict prevention)

The #1 source of Unity merge hell is two developers editing the same scene file. Prevent this by splitting one scene per functional area. Each developer owns their scene during active work.

### Slopworks scene structure

```
Scenes/
  HomeBase/
    HomeBase_Terrain.unity       — ground, resource nodes, terrain mesh
    HomeBase_Grid.unity          — factory grid system, belt network
    HomeBase_UI.unity            — HUD, build menu, inventory UI
    HomeBase_Lighting.unity      — directional light, ambient settings, baked GI
  Buildings/
    Building_Template.unity      — base prefab for all reclaimed buildings
    [BuildingName].unity         — one scene per building
  Overworld/
    Overworld_Map.unity          — territory tiles, building icons, supply lines
    Overworld_UI.unity           — overworld HUD, dossier panel
  Core/
    Core_Network.unity           — NetworkManager, FishNet setup
    Core_GameManager.unity       — game state, session management
```

These are loaded additively at runtime. The NetworkManager scene is always loaded first. Scene-specific managers load their own scene.

### Cross-scene communication

Unity prohibits direct references between objects in different scenes. Use a ScriptableObject event bus instead:

```csharp
// Define events as ScriptableObjects
[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject {
    private List<GameEventListenerSO> _listeners = new();
    public void Raise() => _listeners.ForEach(l => l.OnEventRaised());
    public void RegisterListener(GameEventListenerSO l) => _listeners.Add(l);
    public void UnregisterListener(GameEventListenerSO l) => _listeners.Remove(l);
}
```

Events like `PlayerDied`, `BuildingClaimed`, `WaveStarted` are ScriptableObject assets. Any scene can raise or subscribe to them without knowing about the other scenes.

---

## Folder structure

```
Assets/
  _Slopworks/           — all game code lives here (underscore sorts to top)
    Scripts/
      Automation/       — belt, machine, grid systems
      Combat/           — weapons, damage, health
      Network/          — FishNet integration, Supabase client
      Player/           — character controller, camera
      World/            — procedural gen, chunk system, BIM import
      UI/               — HUD, menus, inventory UI
      Core/             — game manager, scene loader, save system
    ScriptableObjects/
      Items/            — ItemDefinitionSO assets
      Recipes/          — RecipeSO assets
      Events/           — GameEventSO assets
      Buildings/        — building type definitions
    Prefabs/
      Player/
      Machines/
      Buildings/
      UI/
      FX/
    Materials/
    Shaders/
    Audio/
  Plugins/              — third-party packages installed as source
  StreamingAssets/
    supabase-config.json (gitignored — copy from supabase-config.template.json)
```

---

## Branch strategy

**Trunk-based development.** Short-lived feature branches, merge to `master` frequently.

For the parallel experiment, each developer works on their own branch:
- `joe/main` — Joe's version
- `kevin/main` — Kevin's version

When combining: merge both into `main` scene-by-scene, system-by-system. Use `git difftool` with UnityYAMLMerge to compare scenes.

For normal feature work within each version: `git checkout -b feat/belt-system` → small commits → PR to your main branch.

**Do not let branches run for more than a day or two** without merging. Long-lived branches = merge hell. Better to merge broken work behind a feature flag than to accumulate divergence.

---

## MCP Unity (Claude Code + Unity Editor integration)

**`mcp-unity`** bridges Claude Code directly to the Unity Editor. Claude can create GameObjects, add components, manage scenes, run tests, and access the console — without you touching the Editor.

**Install:** `https://github.com/CoderGamester/mcp-unity`

Setup:
1. Install via Unity Package Manager (git URL)
2. Tools > MCP Unity > Server Window → click Configure for Claude Code
3. Start the WebSocket server in Unity
4. Add to `~/.claude.json` MCP config:
```json
"mcp-unity": {
  "command": "node",
  "args": ["/path/to/mcp-unity/Server~/build/index.js"]
}
```

Requires Node.js 18+.

**What it unlocks:**
- Claude creates and configures GameObjects directly
- Claude adds/removes components with serialized field values
- Claude runs Unit Tests via Unity Test Runner and reads results
- Claude reads the Unity console for errors without you copying them
- Claude installs packages via Package Manager

For Slopworks development: use this to let Claude help scaffold new scenes, configure NetworkObject components, and test systems — it avoids the constant copy-paste between Claude and the Unity Editor.

---

## CI/CD (GameCI)

**GameCI** provides GitHub Actions for building and testing Unity projects.

Basic workflow file at `.github/workflows/build.yml`:

```yaml
name: Build

on:
  push:
    branches: [master]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true

      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          targetPlatform: StandaloneWindows64

      - uses: actions/upload-artifact@v4
        with:
          name: Build-Windows
          path: build/
```

**Docs:** `https://game.ci/docs/github/getting-started`

For Slopworks: minimum useful workflow is a build on push to master so you catch compile errors before they sit. Add test runner action once you have editor tests.

---

## Code architecture for two-person team

**MonoBehaviour + ScriptableObjects, not DOTS.** DOTS is a significant learning investment and adds workflow friction for a small team. Profile first; migrate specific hot paths to Jobs/Burst only if profiling shows you need it.

**Recommended patterns:**
- ScriptableObjects for static data (items, recipes, events, settings)
- `NetworkBehaviour` subclasses for anything that needs to sync over network
- Event-driven cross-system communication via `GameEventSO`
- No dependency injection framework (see `docs/coordination/decisions.md` D-001) — `[SerializeField]` references for MonoBehaviours, constructor params for plain C# simulation classes
- Simulation logic in plain C# classes, MonoBehaviours as thin wrappers (see D-004) — enables EditMode testing

**Avoid:**
- Singletons (use ScriptableObject references instead)
- Direct cross-scene object references
- Physics for game logic (use overlap checks, not OnCollision for item pickup)
