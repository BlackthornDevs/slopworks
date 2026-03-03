# Playtest environment design

## Goal

Replace primitive-only playtest visuals with a post-apocalyptic arena that feels like a real game level. Reusable across all playtest scenes.

## Architecture

### `PlaytestEnvironment : MonoBehaviour`

Location: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

Single component that generates the entire environment at runtime from primitives, procedural textures, and built-in Unity materials. No external assets or prefabs required.

### Configuration

```csharp
[Header("Arena")]
[SerializeField] private float _arenaSize = 40f;
[SerializeField] private int _seed = 42;

[Header("Ruins")]
[SerializeField] private int _perimeterSegments = 20;
[SerializeField] private int _interiorRuinCount = 8;
[SerializeField] private int _propCount = 30;

[Header("Atmosphere")]
[SerializeField] private bool _enableFog = true;
[SerializeField] private bool _enableParticles = true;
[SerializeField] private Color _fogColor = new Color(0.35f, 0.28f, 0.2f);
```

### Generated elements

**1. Ground slab**
- Procedural `Texture2D` with concrete-like noise pattern and subtle grid lines at cell intervals
- Darker staining near edges and around ruin bases
- Layer: `GridPlane` (for placement raycasts)
- Replaces the flat gray cube in existing bootstrappers

**2. Perimeter walls**
- Irregular-height concrete wall segments around the arena edge
- Some sections full-height (3-4m), some collapsed (0.5-1.5m), some gaps
- Slight random rotation for organic feel
- Layer: `Structures` (blocks weapon raycasts, provides cover)
- Material: dark gray with slight brownish tint

**3. Interior ruins**
- Broken wall segments, pillars, half-collapsed structures placed inside the arena
- Pseudo-random placement with seed-based `System.Random` for consistency
- Minimum distance from arena center (keep build area clear)
- Layer: `Structures`

**4. Props (barrels, crates, debris)**
- Barrels: cylinders, rust-orange, various sizes, some tipped over
- Crates: cubes, dark wood brown, stacked or scattered
- Debris piles: clusters of small flattened cubes, gray/brown
- Pipes: thin long cylinders, dark metal, near walls
- Placed near perimeter walls and ruins (not in the central build area)
- Layer: `Structures` or `BIM_Static`

**5. Lighting**
- Directional light: warm orange tint, 35-degree angle (late afternoon)
- Ambient: dark desaturated brown
- Optional point lights at 2-3 locations (orange glow, simulating fires/embers)

**6. Atmosphere**
- Linear fog: brownish, start 20m, end 80m
- Dust particle system: slow-drifting small particles, low density, neutral color
- Parented under environment root for easy toggle

### Integration with bootstrappers

Bootstrappers call the environment early in Awake:

```csharp
private void Awake()
{
    var envObj = new GameObject("PlaytestEnvironment");
    var env = envObj.AddComponent<PlaytestEnvironment>();
    env.Generate();  // creates ground, ruins, props, lighting, fog

    // rest of setup...
}
```

The `Generate()` method returns a reference to the ground plane GameObject so the bootstrapper can skip its own `CreateGroundPlane()` call.

### Color palette

Post-apocalyptic industrial:
- Ground: `(0.25, 0.22, 0.2)` base with `(0.18, 0.16, 0.14)` stains
- Concrete walls: `(0.35, 0.33, 0.3)` to `(0.45, 0.4, 0.35)`
- Rust/metal: `(0.55, 0.3, 0.15)` to `(0.4, 0.2, 0.1)`
- Wood/crates: `(0.35, 0.25, 0.15)`
- Debris: `(0.3, 0.28, 0.25)`
- Fog: `(0.35, 0.28, 0.2)`
- Ambient light: `(0.15, 0.12, 0.1)`
- Directional light: `(1.0, 0.85, 0.65)`

### Files

| File | Action |
|------|--------|
| `Scripts/Building/PlaytestEnvironment.cs` | Create |
| `Scripts/Building/StructuralPlaytestSetup.cs` | Modify -- replace `CreateGroundPlane()` with `PlaytestEnvironment` |

### Not in scope

- NavMesh rebake (existing BakeNavMesh in bootstrapper handles this)
- Networking (local playtest only)
- Asset store models or external textures
- Sound effects
