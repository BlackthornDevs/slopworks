# Render pipeline reference

Unity Universal Render Pipeline (URP) for Slopworks. This covers the pipeline choice, camera setup, batching strategy for BIM geometry, belt item instancing, lighting, post-processing volumes, and shader authoring.

---

## Pipeline choice: URP, not HDRP

**Use URP.**

HDRP is built for photorealism and ray tracing. It's overkill for a post-apocalyptic industrial game and breaks Camera Stacking — which is required for the isometric↔FPS camera transition. HDRP also adds GPU cost that provides no aesthetic benefit over URP with good post-processing.

Built-in renderer is legacy. It lacks the SRP Batcher, which is essential for handling Revit-exported FBX geometry (hundreds of submeshes, many unique materials).

URP wins because:
- Camera Stacking works (base camera + overlay camera in one scene)
- SRP Batcher handles BIM geometry draw call cost automatically
- Volume system for per-area post-processing
- GPU instancing for belt items coexists with SRP Batcher
- Shader Graph is the native authoring workflow

**Recommended packages (update as a group):**
```
com.unity.render-pipelines.universal: 17.0+
com.unity.render-pipelines.core: 17.0+
com.unity.shader-graph: 17.0+
```

---

## Camera setup: isometric + first-person

Use URP Camera Stacking — one Base Camera renders the full scene, one Overlay Camera renders on top for first-person elements.

```
Scene/
  ├─ MainCamera (Base)
  │   Rendering path: Forward
  │   Projection: Orthographic (isometric) or Perspective (FPS base)
  │   Clear Flags: Skybox
  │
  └─ FirstPersonCamera (Overlay)
      Rendering path: Forward
      FOV: 80
      Culling Mask: PlayerLayer, WeaponsLayer, UIFPS
      Added to MainCamera's Camera Stack list
```

**Critical:** both cameras must use the same rendering path (Forward). Camera Stacking is incompatible with Deferred. If you need more than 8 real-time lights, use Forward+ (URP 14+) — it clusters lights automatically and has no hard cap.

When switching views, toggle camera GameObjects and swap Volume weights rather than reconfiguring a single camera. Toggle speed is negligible; camera reconfiguration causes a stutter.

---

## Batching: three tiers

### Tier 1 — SRP Batcher (BIM geometry, static buildings)

The SRP Batcher reduces draw calls to roughly one per unique material, regardless of how many meshes share that material. This is the primary batching strategy for Revit-exported geometry.

Enable in the URP Pipeline Asset: Rendering > Batch Rendering > SRP Batcher: On (default).

**Result:** 400 submeshes with 30 unique materials → ~30 draw calls.

Don't merge submeshes into one mega-mesh. Merging destroys per-submesh frustum culling and makes per-element material changes impossible (required for building restoration).

### Tier 2 — GPU instancing (repeated machines)

For machines that repeat across the base (smelters, assemblers, etc.): enable GPU Instancing on the material and use `MaterialPropertyBlock` for per-instance properties. SRP Batcher takes priority over GPU instancing for static geometry — that's fine, you want SRP batching there.

### Tier 3 — DrawMeshInstancedProcedural (belt items)

Belt items are data, not GameObjects. Render them with:

```csharp
Graphics.DrawMeshInstancedProcedural(
    beltItemMesh,
    0,
    beltItemMaterial,
    bounds,
    itemCount,
    materialPropertyBlock
);
```

This bypasses the SRP Batcher entirely — it goes directly to the GPU. The belt item shader must read instance position/rotation from a StructuredBuffer uploaded each frame via `MaterialPropertyBlock`. Write this shader in HLSL (Shader Graph can't express `SV_InstanceID` reads from a compute buffer cleanly).

```hlsl
#pragma target 4.5
#pragma multi_compile_instancing
#pragma instancing_options assumeuniformscaling

StructuredBuffer<float4> _ItemPositions;
StructuredBuffer<float4> _ItemRotations;

Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
{
    float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
    worldPos += _ItemPositions[instanceID].xyz;
    // apply rotation from _ItemRotations[instanceID] as needed
    ...
}
```

---

## BIM geometry: Revit FBX import

### Import settings

In the FBX importer (Model tab):
- Geometry: Import Meshes on
- Optimize Mesh: on
- Smoothing Angle: 60
- Preserve Hierarchies: on
- Optimize Game Objects: off (keep per-submesh control)

### Materials

Revit's FBX exporter encrypts material definitions — they won't appear in Unity. Build a `BIM_Standard.mat` (URP/Lit shader) and batch-assign it post-import with a `AssetPostprocessor`:

```csharp
public class RevitMaterialRemapper : AssetPostprocessor
{
    private static readonly string BimMaterialPath =
        "Assets/_Slopworks/Materials/BIM_Standard.mat";

    void OnPostprocessModel(GameObject g)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(BimMaterialPath);
        if (mat == null) return;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }
}
```

Create material variants per building element type (concrete, metal, glass) as needed. All variants use URP/Lit so SRP Batcher groups them correctly.

---

## Lighting

**Buildings (reclaimed):** Baked GI. Mark all architecture static (Contribute GI). One baked directional light per building simulating exterior daylight. Lightmap resolution: 16 texels/unit. Bake time: 2–10 min per complex building depending on hardware.

**Home Base factory:** Real-time only. The factory is player-built and changes constantly — baking isn't feasible. Use ambient light set to a dim blue-gray (`RenderSettings.ambientLight`) and up to 16 real-time point/spot lights for machinery glow.

**Players, machines, fauna:** Real-time lights always. Shadows: Soft on key lights, Hard or None on fill lights.

Don't use Mixed lighting mode (baked + real-time from the same light). The shadow mismatch between baked indirect and real-time direct looks wrong and adds draw calls. Use separate baked lights for GI and real-time lights for shadows.

**URP real-time light limit:** 32 per renderer by default (configurable in the Pipeline Asset). Forward+ removes the hard limit.

---

## Post-processing volumes

Use the URP Volume system. Avoid Unity's older Post-Processing Stack v2.

### Global volume

One GameObject per scene with `Is Global: on`. Contains baseline overrides for the entire scene:

```
GlobalVolume (Is Global: on, Priority: 0)
└─ Profile:
   ├─ Bloom: Intensity 0.7, Threshold 1.0
   ├─ Color Adjustments: Saturation -30, Post-Exposure -0.3
   ├─ Chromatic Aberration: Intensity 0.4
   └─ Vignette: Intensity 0.2
```

### Local volumes (per area)

Trigger zones with a Collider set to Is Trigger and a Volume with Blend Distance:

```csharp
// Toxic zone — yellow-green fog
Volume toxicVol;
toxicVol.isGlobal = false;
toxicVol.blendDistance = 3f;
toxicVol.priority = 20f;  // always assign explicit priority
// Profile: Fog density 0.08, hue shift +20, vignette 0.4
```

Assign explicit priorities to all volumes. When two volumes overlap with the same priority, Unity's blending is undefined.

URP does not have built-in volumetric (raymarched) fog. Use screen-space fog from the Volume overrides or a custom Renderer Feature for raymarched fog if the aesthetic requires it.

---

## Shaders

**Shader Graph for:** standard materials (BIM geometry, belts with scrolling UVs, UI elements, environmental effects). Shader Graph compiles to efficient HLSL at build time and coexists with SRP Batcher.

**Custom HLSL for:** belt item procedural shader (requires `SV_InstanceID` + StructuredBuffer), any effect that Shader Graph's node set can't express, performance-critical shaders identified by profiler.

Don't rewrite Shader Graph shaders in HLSL speculatively. Profile first; the gap is typically 5–10% and rarely worth the iteration cost.

---

## Two-developer workflow

URP Pipeline Asset and Renderer Asset files are binary ScriptableObjects. Git can't merge them.

**Recommendation:** one developer owns render assets. The other requests changes in a text file (`docs/render-requests.md`) and the owner applies them. For small teams this is faster than fighting merge conflicts on binary assets.

If both developers need to modify render settings:
- Configure `.gitattributes` with `*.asset merge=unityyamlmerge`
- UnityYAMLMerge handles scene files well but is fragile on render assets — treat it as a fallback, not a primary strategy

Lightmaps should not be committed to Git (too large, regenerate locally). Add `**/LightmapData-*` and `**/Lightmap-*` to `.gitignore`. Each developer bakes their own or one developer owns baked assets and distributes via LFS.

---

## Decals (environmental wear)

Enable the Decal Renderer Feature in the URP Renderer asset: Add Renderer Feature > Decal. Set Surface Data to `Albedo Normal MAOS`.

Use `DecalProjector` for bullet holes, rust stains, blast marks. One draw call per ~50 visible decals (GPU instanced). No performance concern unless >500 decals are on screen simultaneously.

---

## Performance targets

| Metric | Target |
|--------|--------|
| Draw calls | 100–300 (SRP Batcher working correctly) |
| Frame time (CPU) | < 16ms @ 60 FPS |
| Triangle count | 1–3M per frame |
| Real-time lights | ≤ 16 (Forward) or uncapped (Forward+) |
| Texture memory | < 800MB |
| Lightmap memory | < 500MB |

Profile with Window > Analysis > Profiler. Watch the Rendering section for SetPass calls (should be close to draw call count if SRP Batcher is active) and the GPU frame time.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| HDRP breaks camera stacking | Use URP |
| Merging Revit FBX submeshes | Don't — SRP Batcher handles it; merging breaks culling |
| Encrypted Revit materials | Use `AssetPostprocessor` to remap to BIM_Standard.mat |
| Mixed lighting mode | Separate baked + real-time lights; don't mix modes |
| Volume priority conflicts | Always set explicit `priority` values |
| Belt item shader in Shader Graph | Write in HLSL — Shader Graph can't read SV_InstanceID from StructuredBuffer |
| Both developers editing URP assets | One developer owns render assets |
| Lightmaps in Git | `.gitignore` them; regenerate locally |
