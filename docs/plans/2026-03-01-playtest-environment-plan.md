# Playtest environment implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a reusable `PlaytestEnvironment` component that generates a post-apocalyptic arena at runtime from primitives and procedural textures.

**Architecture:** Single MonoBehaviour with a `Generate()` method that creates ground, perimeter walls, interior ruins, props, lighting, and fog. Returns a ground plane reference so bootstrappers can adjust height per level. All geometry is runtime-generated -- no external assets.

**Tech Stack:** Unity primitives, procedural `Texture2D`, `RenderSettings` for fog, `ParticleSystem` for dust.

**Design doc:** `docs/plans/2026-03-01-playtest-environment-design.md`

---

### Task 1: PlaytestEnvironment skeleton with ground slab

**Files:**
- Create: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

**Step 1: Create the class with Generate() and ground slab**

```csharp
using UnityEngine;

/// <summary>
/// Reusable post-apocalyptic arena builder for playtest scenes.
/// Generates environment at runtime from primitives and procedural textures.
/// Drop on a GameObject or create via code, call Generate().
/// </summary>
public class PlaytestEnvironment : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] private float _arenaSize = 40f;
    [SerializeField] private int _seed = 42;
    [SerializeField] private Vector3 _centerOffset = Vector3.zero;

    [Header("Ruins")]
    [SerializeField] private int _perimeterSegments = 20;
    [SerializeField] private int _interiorRuinCount = 8;
    [SerializeField] private int _propCount = 30;

    [Header("Atmosphere")]
    [SerializeField] private bool _enableFog = true;
    [SerializeField] private bool _enableParticles = true;
    [SerializeField] private Color _fogColor = new Color(0.35f, 0.28f, 0.2f);

    private GameObject _root;
    private GameObject _groundPlane;
    private System.Random _rng;

    public GameObject GroundPlane => _groundPlane;

    // Color palette
    private static readonly Color GroundBase = new Color(0.25f, 0.22f, 0.2f);
    private static readonly Color GroundStain = new Color(0.18f, 0.16f, 0.14f);
    private static readonly Color ConcreteLight = new Color(0.45f, 0.4f, 0.35f);
    private static readonly Color ConcreteDark = new Color(0.35f, 0.33f, 0.3f);
    private static readonly Color RustColor = new Color(0.55f, 0.3f, 0.15f);
    private static readonly Color RustDark = new Color(0.4f, 0.2f, 0.1f);
    private static readonly Color WoodColor = new Color(0.35f, 0.25f, 0.15f);
    private static readonly Color DebrisColor = new Color(0.3f, 0.28f, 0.25f);
    private static readonly Color MetalDark = new Color(0.2f, 0.18f, 0.16f);

    /// <summary>
    /// Generate the full environment. Call once in Awake before player creation.
    /// Returns the ground plane reference for level height adjustment.
    /// </summary>
    public GameObject Generate()
    {
        _rng = new System.Random(_seed);
        _root = new GameObject("Environment");
        _root.transform.SetParent(transform);

        CreateGroundSlab();
        CreatePerimeterWalls();
        CreateInteriorRuins();
        CreateProps();
        SetupLighting();
        SetupAtmosphere();

        Debug.Log($"playtest environment: generated ({_perimeterSegments} wall segments, {_interiorRuinCount} ruins, {_propCount} props)");
        return _groundPlane;
    }

    private void CreateGroundSlab()
    {
        _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _groundPlane.name = "GroundSlab";
        _groundPlane.layer = PhysicsLayers.GridPlane;
        _groundPlane.isStatic = true;
        _groundPlane.transform.SetParent(_root.transform);

        _groundPlane.transform.position = _centerOffset + new Vector3(0f, -0.05f, 0f);
        _groundPlane.transform.localScale = new Vector3(_arenaSize, 0.1f, _arenaSize);

        var tex = GenerateGroundTexture(256, _arenaSize);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.mainTexture = tex;
        mat.color = Color.white; // texture carries the color
        mat.SetFloat("_Smoothness", 0.1f);
        _groundPlane.GetComponent<Renderer>().material = mat;
    }

    private Texture2D GenerateGroundTexture(int resolution, float worldSize)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGB24, true);
        float cellSize = FactoryGrid.CellSize;
        float gridLineWidth = 0.02f; // fraction of a cell

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;

                // World-space position
                float wx = u * worldSize;
                float wz = v * worldSize;

                // Base concrete noise
                float noise = Mathf.PerlinNoise(wx * 0.8f, wz * 0.8f) * 0.15f;
                float fineNoise = Mathf.PerlinNoise(wx * 3f, wz * 3f) * 0.05f;
                Color baseColor = Color.Lerp(GroundBase, GroundStain, noise + fineNoise);

                // Edge darkening
                float edgeDist = Mathf.Min(u, v, 1f - u, 1f - v);
                float edgeFade = Mathf.Clamp01(edgeDist * 5f);
                baseColor = Color.Lerp(GroundStain * 0.7f, baseColor, edgeFade);

                // Grid lines
                float cellFracX = (wx % cellSize) / cellSize;
                float cellFracZ = (wz % cellSize) / cellSize;
                bool onGridLine = cellFracX < gridLineWidth || cellFracX > (1f - gridLineWidth)
                               || cellFracZ < gridLineWidth || cellFracZ > (1f - gridLineWidth);
                if (onGridLine)
                    baseColor = Color.Lerp(baseColor, new Color(0.15f, 0.13f, 0.12f), 0.3f);

                // Random stains
                float stainNoise = Mathf.PerlinNoise(wx * 0.3f + 100f, wz * 0.3f + 100f);
                if (stainNoise > 0.65f)
                    baseColor = Color.Lerp(baseColor, GroundStain * 0.6f, (stainNoise - 0.65f) * 2f);

                tex.SetPixel(x, y, baseColor);
            }
        }

        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // Stub methods -- filled in subsequent tasks
    private void CreatePerimeterWalls() { }
    private void CreateInteriorRuins() { }
    private void CreateProps() { }
    private void SetupLighting() { }
    private void SetupAtmosphere() { }

    // -- Helpers --

    private float RandomRange(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }

    private Color RandomColor(Color a, Color b)
    {
        float t = (float)_rng.NextDouble();
        return Color.Lerp(a, b, t);
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position,
        Vector3 scale, Color color, int layer, Transform parent = null)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.layer = layer;
        obj.isStatic = true;
        obj.transform.SetParent(parent != null ? parent : _root.transform);
        obj.transform.position = position;
        obj.transform.localScale = scale;

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;

        return obj;
    }
}
```

**Step 2: Verify compilation**

Run EditMode tests to confirm no compile errors.

**Step 3: Commit**

```
feat: add PlaytestEnvironment skeleton with procedural ground slab
```

---

### Task 2: Perimeter walls

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

**Step 1: Implement CreatePerimeterWalls**

Replace the stub with:

```csharp
private void CreatePerimeterWalls()
{
    var wallParent = new GameObject("PerimeterWalls");
    wallParent.transform.SetParent(_root.transform);

    float half = _arenaSize * 0.5f;
    float perimeter = _arenaSize * 4f;
    float segmentSpacing = perimeter / _perimeterSegments;

    for (int i = 0; i < _perimeterSegments; i++)
    {
        float t = (float)i / _perimeterSegments;
        float dist = t * perimeter;

        // Walk around the perimeter
        Vector3 pos;
        float baseRotation;
        if (dist < _arenaSize)
        {
            pos = _centerOffset + new Vector3(-half + dist, 0f, half);
            baseRotation = 0f;
        }
        else if (dist < _arenaSize * 2f)
        {
            pos = _centerOffset + new Vector3(half, 0f, half - (dist - _arenaSize));
            baseRotation = 90f;
        }
        else if (dist < _arenaSize * 3f)
        {
            pos = _centerOffset + new Vector3(half - (dist - _arenaSize * 2f), 0f, -half);
            baseRotation = 180f;
        }
        else
        {
            pos = _centerOffset + new Vector3(-half, 0f, -half + (dist - _arenaSize * 3f));
            baseRotation = 270f;
        }

        // Random gap (skip ~15% of segments)
        if (_rng.NextDouble() < 0.15f)
            continue;

        // Random height: full, medium, or collapsed
        float height;
        double roll = _rng.NextDouble();
        if (roll < 0.3f)
            height = RandomRange(3f, 4f);      // full height
        else if (roll < 0.7f)
            height = RandomRange(1.5f, 2.5f);  // medium
        else
            height = RandomRange(0.4f, 1.2f);  // collapsed

        float width = RandomRange(segmentSpacing * 0.7f, segmentSpacing * 0.95f);
        float depth = RandomRange(0.3f, 0.6f);
        pos.y = height * 0.5f;

        var color = RandomColor(ConcreteDark, ConcreteLight);
        var wall = CreatePrimitive($"Wall_{i}", PrimitiveType.Cube, pos,
            new Vector3(width, height, depth), color, PhysicsLayers.Structures, wallParent.transform);

        // Slight random rotation for organic feel
        wall.transform.rotation = Quaternion.Euler(
            RandomRange(-2f, 2f),
            baseRotation + RandomRange(-5f, 5f),
            RandomRange(-1f, 1f));
    }
}
```

**Step 2: Verify compilation, run tests**

**Step 3: Commit**

```
feat: add perimeter wall generation to PlaytestEnvironment
```

---

### Task 3: Interior ruins

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

**Step 1: Implement CreateInteriorRuins**

Replace the stub with:

```csharp
private void CreateInteriorRuins()
{
    var ruinParent = new GameObject("InteriorRuins");
    ruinParent.transform.SetParent(_root.transform);

    float half = _arenaSize * 0.5f;
    // Keep a clear zone in the center for the build area (radius ~8 cells)
    float clearZone = 12f;
    int placed = 0;
    int attempts = 0;

    while (placed < _interiorRuinCount && attempts < _interiorRuinCount * 10)
    {
        attempts++;
        float x = RandomRange(-half + 2f, half - 2f);
        float z = RandomRange(-half + 2f, half - 2f);
        Vector3 pos = _centerOffset + new Vector3(x, 0f, z);

        // Skip if too close to center (build area)
        float distFromCenter = new Vector2(x, z).magnitude;
        if (distFromCenter < clearZone)
            continue;

        // Pick a ruin type
        double roll = _rng.NextDouble();
        if (roll < 0.35f)
            CreateBrokenWall(pos, ruinParent.transform);
        else if (roll < 0.6f)
            CreatePillar(pos, ruinParent.transform);
        else
            CreateRuinCluster(pos, ruinParent.transform);

        placed++;
    }
}

private void CreateBrokenWall(Vector3 basePos, Transform parent)
{
    float height = RandomRange(1.5f, 3f);
    float width = RandomRange(2f, 4f);
    float depth = RandomRange(0.25f, 0.4f);
    basePos.y = height * 0.5f;

    var color = RandomColor(ConcreteDark, ConcreteLight);
    var wall = CreatePrimitive("BrokenWall", PrimitiveType.Cube, basePos,
        new Vector3(width, height, depth), color, PhysicsLayers.Structures, parent);
    wall.transform.rotation = Quaternion.Euler(
        RandomRange(-3f, 3f), RandomRange(0f, 360f), RandomRange(-2f, 2f));

    // Rubble at base
    for (int i = 0; i < 3; i++)
    {
        var rubblePos = basePos + new Vector3(
            RandomRange(-width * 0.4f, width * 0.4f), -height * 0.5f + 0.1f,
            RandomRange(-0.5f, 0.5f));
        rubblePos.y = RandomRange(0.05f, 0.15f);
        CreatePrimitive("Rubble", PrimitiveType.Cube, rubblePos,
            new Vector3(RandomRange(0.2f, 0.5f), RandomRange(0.1f, 0.3f), RandomRange(0.2f, 0.5f)),
            RandomColor(DebrisColor, ConcreteDark), PhysicsLayers.Structures, parent);
    }
}

private void CreatePillar(Vector3 basePos, Transform parent)
{
    float height = RandomRange(2f, 5f);
    float radius = RandomRange(0.2f, 0.4f);
    basePos.y = height * 0.5f;

    CreatePrimitive("Pillar", PrimitiveType.Cylinder, basePos,
        new Vector3(radius * 2f, height * 0.5f, radius * 2f),
        RandomColor(ConcreteDark, ConcreteLight), PhysicsLayers.Structures, parent);
}

private void CreateRuinCluster(Vector3 basePos, Transform parent)
{
    // A few wall fragments at varying angles -- looks like a collapsed structure
    int fragments = 2 + _rng.Next(3);
    for (int i = 0; i < fragments; i++)
    {
        var offset = new Vector3(RandomRange(-1.5f, 1.5f), 0f, RandomRange(-1.5f, 1.5f));
        float height = RandomRange(0.5f, 2f);
        float width = RandomRange(0.8f, 2f);
        var pos = basePos + offset;
        pos.y = height * 0.5f;

        var fragment = CreatePrimitive("Fragment", PrimitiveType.Cube, pos,
            new Vector3(width, height, RandomRange(0.2f, 0.4f)),
            RandomColor(ConcreteDark, ConcreteLight), PhysicsLayers.Structures, parent);
        fragment.transform.rotation = Quaternion.Euler(
            RandomRange(-15f, 15f), RandomRange(0f, 360f), RandomRange(-10f, 10f));
    }
}
```

**Step 2: Verify compilation, run tests**

**Step 3: Commit**

```
feat: add interior ruins generation to PlaytestEnvironment
```

---

### Task 4: Props (barrels, crates, debris, pipes)

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

**Step 1: Implement CreateProps**

Replace the stub with:

```csharp
private void CreateProps()
{
    var propParent = new GameObject("Props");
    propParent.transform.SetParent(_root.transform);

    float half = _arenaSize * 0.5f;

    for (int i = 0; i < _propCount; i++)
    {
        // Place near edges (75% chance) or scattered (25% chance)
        float x, z;
        if (_rng.NextDouble() < 0.75f)
        {
            // Near edge
            int side = _rng.Next(4);
            float along = RandomRange(-half + 1f, half - 1f);
            float inset = RandomRange(1f, 5f);
            switch (side)
            {
                case 0: x = along; z = half - inset; break;
                case 1: x = half - inset; z = along; break;
                case 2: x = along; z = -half + inset; break;
                default: x = -half + inset; z = along; break;
            }
        }
        else
        {
            x = RandomRange(-half + 3f, half - 3f);
            z = RandomRange(-half + 3f, half - 3f);
        }

        var pos = _centerOffset + new Vector3(x, 0f, z);

        double roll = _rng.NextDouble();
        if (roll < 0.3f)
            CreateBarrel(pos, propParent.transform);
        else if (roll < 0.55f)
            CreateCrate(pos, propParent.transform);
        else if (roll < 0.8f)
            CreateDebrisPile(pos, propParent.transform);
        else
            CreatePipe(pos, propParent.transform);
    }
}

private void CreateBarrel(Vector3 basePos, Transform parent)
{
    float height = RandomRange(0.8f, 1.2f);
    float radius = RandomRange(0.25f, 0.35f);
    bool tipped = _rng.NextDouble() < 0.3f;

    basePos.y = tipped ? radius : height * 0.5f;
    var barrel = CreatePrimitive("Barrel", PrimitiveType.Cylinder, basePos,
        new Vector3(radius * 2f, height * 0.5f, radius * 2f),
        RandomColor(RustColor, RustDark), PhysicsLayers.BIM_Static, parent);

    if (tipped)
        barrel.transform.rotation = Quaternion.Euler(90f, RandomRange(0f, 360f), 0f);
}

private void CreateCrate(Vector3 basePos, Transform parent)
{
    float size = RandomRange(0.5f, 0.9f);
    basePos.y = size * 0.5f;

    var crate = CreatePrimitive("Crate", PrimitiveType.Cube, basePos,
        new Vector3(size, size, size), RandomColor(WoodColor, WoodColor * 0.8f),
        PhysicsLayers.BIM_Static, parent);
    crate.transform.rotation = Quaternion.Euler(0f, RandomRange(0f, 45f), 0f);

    // Sometimes stack a second crate
    if (_rng.NextDouble() < 0.3f)
    {
        var stackPos = basePos + new Vector3(RandomRange(-0.1f, 0.1f), size, RandomRange(-0.1f, 0.1f));
        float stackSize = size * RandomRange(0.6f, 0.9f);
        var stack = CreatePrimitive("CrateStack", PrimitiveType.Cube, stackPos,
            new Vector3(stackSize, stackSize, stackSize), RandomColor(WoodColor, WoodColor * 0.7f),
            PhysicsLayers.BIM_Static, parent);
        stack.transform.rotation = Quaternion.Euler(
            RandomRange(-5f, 5f), RandomRange(0f, 45f), RandomRange(-5f, 5f));
    }
}

private void CreateDebrisPile(Vector3 basePos, Transform parent)
{
    int pieces = 3 + _rng.Next(5);
    for (int i = 0; i < pieces; i++)
    {
        var offset = new Vector3(RandomRange(-0.5f, 0.5f), 0f, RandomRange(-0.5f, 0.5f));
        var pos = basePos + offset;
        pos.y = RandomRange(0.03f, 0.12f);
        CreatePrimitive("Debris", PrimitiveType.Cube, pos,
            new Vector3(RandomRange(0.15f, 0.4f), RandomRange(0.05f, 0.15f), RandomRange(0.15f, 0.4f)),
            RandomColor(DebrisColor, ConcreteDark * 0.8f), PhysicsLayers.BIM_Static, parent);
    }
}

private void CreatePipe(Vector3 basePos, Transform parent)
{
    float length = RandomRange(1.5f, 4f);
    float radius = RandomRange(0.05f, 0.12f);
    basePos.y = radius + 0.02f;

    var pipe = CreatePrimitive("Pipe", PrimitiveType.Cylinder, basePos,
        new Vector3(radius * 2f, length * 0.5f, radius * 2f),
        RandomColor(MetalDark, MetalDark * 1.3f), PhysicsLayers.BIM_Static, parent);
    pipe.transform.rotation = Quaternion.Euler(90f, RandomRange(0f, 360f), 0f);
}
```

**Step 2: Verify compilation, run tests**

**Step 3: Commit**

```
feat: add prop generation (barrels, crates, debris, pipes) to PlaytestEnvironment
```

---

### Task 5: Lighting and atmosphere

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/PlaytestEnvironment.cs`

**Step 1: Implement SetupLighting and SetupAtmosphere**

Replace both stubs:

```csharp
private void SetupLighting()
{
    // Find or create directional light
    var existingLight = FindAnyObjectByType<Light>();
    Light dirLight;
    if (existingLight != null && existingLight.type == LightType.Directional)
    {
        dirLight = existingLight;
    }
    else
    {
        var lightObj = new GameObject("SunLight");
        lightObj.transform.SetParent(_root.transform);
        dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
    }

    dirLight.color = new Color(1f, 0.85f, 0.65f);
    dirLight.intensity = 1.2f;
    dirLight.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
    dirLight.shadows = LightShadows.Soft;

    // Ambient
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.1f);

    // Point lights (fire glow) at a few positions
    float half = _arenaSize * 0.5f;
    Vector3[] firePositions =
    {
        _centerOffset + new Vector3(half - 3f, 1f, half - 3f),
        _centerOffset + new Vector3(-half + 4f, 1f, -half + 5f),
        _centerOffset + new Vector3(half - 5f, 0.8f, -half + 3f),
    };

    for (int i = 0; i < firePositions.Length; i++)
    {
        var fireObj = new GameObject($"FireGlow_{i}");
        fireObj.transform.SetParent(_root.transform);
        fireObj.transform.position = firePositions[i];

        var fireLight = fireObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.5f, 0.15f);
        fireLight.intensity = 2f;
        fireLight.range = 6f;

        // Ember visual (small orange sphere)
        var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ember.name = "Ember";
        ember.transform.SetParent(fireObj.transform);
        ember.transform.localPosition = Vector3.down * 0.5f;
        ember.transform.localScale = Vector3.one * 0.3f;
        var emberCollider = ember.GetComponent<Collider>();
        if (emberCollider != null) Destroy(emberCollider);
        var emberRenderer = ember.GetComponent<Renderer>();
        if (emberRenderer != null)
        {
            emberRenderer.material.color = new Color(1f, 0.4f, 0.1f);
            emberRenderer.material.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.1f) * 2f);
            emberRenderer.material.EnableKeyword("_EMISSION");
        }
    }
}

private void SetupAtmosphere()
{
    if (_enableFog)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = _fogColor;
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 80f;

        // Match camera background to fog for seamless blending
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _fogColor;
        }
    }

    if (_enableParticles)
    {
        var dustObj = new GameObject("DustParticles");
        dustObj.transform.SetParent(_root.transform);
        dustObj.transform.position = _centerOffset + Vector3.up * 3f;

        var ps = dustObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = new Color(0.5f, 0.45f, 0.4f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 15f);
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f; // slight upward drift

        var emission = ps.emission;
        emission.rateOverTime = 15f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_arenaSize * 0.8f, 4f, _arenaSize * 0.8f);

        // Disable the default renderer module's material -- use a simple particle shader
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
    }
}
```

**Step 2: Verify compilation, run tests**

**Step 3: Commit**

```
feat: add lighting and atmosphere to PlaytestEnvironment
```

---

### Task 6: Integrate into StructuralPlaytestSetup

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Building/StructuralPlaytestSetup.cs`

**Step 1: Replace CreateGroundPlane with PlaytestEnvironment**

In StructuralPlaytestSetup:

1. Add field: `private PlaytestEnvironment _environment;`

2. In `Awake()`, replace `CreateGroundPlane()` with:
```csharp
var envObj = new GameObject("PlaytestEnvironment");
_environment = envObj.AddComponent<PlaytestEnvironment>();
// Center on the factory grid center
var gridCenter = new Vector3(
    FactoryGrid.Width * FactoryGrid.CellSize * 0.5f, 0f,
    FactoryGrid.Height * FactoryGrid.CellSize * 0.5f);
typeof(PlaytestEnvironment).GetField("_centerOffset",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    ?.SetValue(_environment, gridCenter);
typeof(PlaytestEnvironment).GetField("_arenaSize",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    ?.SetValue(_environment, FactoryGrid.Width * FactoryGrid.CellSize);
_groundPlane = _environment.Generate();
```

3. Delete the `CreateGroundPlane()` method entirely.

4. In `BakeNavMesh()`, the existing `_groundPlane.isStatic = true` is fine -- the environment already sets this, but the line is harmless as a double-set.

5. In `UpdateGroundPlaneHeight()`, no changes needed -- still moves `_groundPlane` by level.

**Step 2: Verify compilation, run all EditMode tests (697+)**

**Step 3: Manual playtest**

Enter Play mode in the StructuralPlaytest scene:
- Ground should have procedural concrete texture with grid lines
- Perimeter walls at varying heights around the edges
- Scattered ruins, barrels, crates, debris inside the arena
- Warm directional light, fog, dust particles
- All existing functionality (building, belts, turrets, combat) still works

**Step 4: Commit**

```
feat: integrate PlaytestEnvironment into StructuralPlaytestSetup
```

---

### Task 7: Final verification and cleanup

**Step 1: Run all EditMode tests**

Confirm 697+ passing, 0 failures.

**Step 2: Playtest the full loop**

1. Press B, place foundations, walls, machines, turrets
2. Press G to spawn enemies
3. Verify turrets fire, enemies die, weapon works
4. Verify props/ruins provide visual cover and don't block the build area

**Step 3: Commit any adjustments**
