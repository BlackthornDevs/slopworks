# Physics layers reference

Unity physics layer matrix for Slopworks. Layers control collision filtering, raycast masks, and GPU culling visibility. Set this up correctly before building prefabs — changing layers after the fact means touching every prefab.

---

## Layer assignments

Unity has 32 physics layers. Slots 0–7 are built-in or Unity-reserved.

| Slot | Name | Contents |
|------|------|----------|
| 0 | Default | Avoid — use specific layers instead |
| 1 | TransparentFX | Particle effects |
| 2 | Ignore Raycast | Explicitly ignored colliders |
| 3 | (unused) | — |
| 4 | Water | Water volumes |
| 5 | UI | Unity UI canvas elements |
| 6 | (unused) | — |
| 7 | (unused) | — |
| 8 | Player | Player character, hitbox colliders |
| 9 | Fauna | All hostile creatures |
| 10 | Projectile | Bullets, rockets, grenades |
| 11 | BIM_Static | Revit-imported building geometry (walls, floors, ceilings) |
| 12 | TerrainMesh | Procedural and sculpted terrain |
| 13 | Structures | Player-built foundations, walls, machines |
| 14 | Interactable | Machine input ports, doors, pickups |
| 15 | GridPlane | Invisible placement grid — raycast target only, no physics |
| 16 | VolumeTrigger | Audio zones, area events, hazard zones |
| 17 | NavMeshAgent | Fauna NavMesh agents (for OffMeshLink queries) |
| 18 | Decal | DecalProjector receive surfaces |
| 19 | FogOfWar | Fog-of-war reveal mesh |
| 20–31 | (reserved) | Future use |

**Note:** Belt items (BeltItem) have no physics layer. They are rendered via `DrawMeshInstancedProcedural` and are not GameObjects — no colliders, no layers.

---

## Collision matrix

Enable collision in Edit > Project Settings > Physics > Layer Collision Matrix. ✓ = collides, blank = no collision (filtered at broadphase — zero CPU cost).

| | Player | Fauna | Projectile | BIM_Static | Terrain | Structures | Interactable | VolumeTrigger |
|---|---|---|---|---|---|---|---|---|
| **Player** | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Fauna** | ✓ | — | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| **Projectile** | ✓ | ✓ | — | ✓ | ✓ | ✓ | — | — |
| **BIM_Static** | ✓ | ✓ | ✓ | — | — | — | — | — |
| **Terrain** | ✓ | ✓ | ✓ | — | — | — | — | — |
| **Structures** | ✓ | ✓ | ✓ | — | — | — | — | — |
| **Interactable** | ✓ | — | — | — | — | — | — | — |
| **VolumeTrigger** | ✓ | ✓ | — | — | — | — | — | — |
| **GridPlane** | — | — | — | — | — | — | — | — |

Key design decisions:
- **Fauna vs Fauna: OFF.** Fauna don't push each other. Use steering behavior (NPBehave separation node) for spacing.
- **Projectile vs Projectile: OFF.** Bullets don't deflect each other.
- **BIM_Static vs Structures: OFF.** Player-built structures don't physically interact with the building shell.
- **GridPlane: collides with nothing.** Purely a raycast target for build placement queries.

---

## Raycast masks

Define named layer masks as constants. Compute them once:

```csharp
public static class PhysicsLayers
{
    // Layer numbers
    public const int Player = 8;
    public const int Fauna = 9;
    public const int Projectile = 10;
    public const int BIM_Static = 11;
    public const int Terrain = 12;
    public const int Structures = 13;
    public const int Interactable = 14;
    public const int GridPlane = 15;
    public const int VolumeTrigger = 16;

    // Raycast masks — compute once at class load, reuse everywhere
    public static readonly int PlacementMask =
        (1 << Terrain) | (1 << BIM_Static) | (1 << GridPlane);

    public static readonly int WeaponHitMask =
        (1 << Player) | (1 << Fauna) | (1 << BIM_Static) | (1 << Structures);

    public static readonly int InteractMask =
        (1 << Interactable);

    public static readonly int FaunaLOSMask =
        (1 << BIM_Static) | (1 << Structures);    // what blocks line of sight
}
```

Usage:

```csharp
// Build placement: hits terrain, BIM floors, grid plane
if (Physics.Raycast(ray, out hit, 100f, PhysicsLayers.PlacementMask))
    ShowPlacementPreview(hit.point);

// Weapon raycast: hits players, fauna, solid geometry — not triggers
if (Physics.Raycast(muzzle, dir, out hit, 50f, PhysicsLayers.WeaponHitMask))
    ApplyDamage(hit.collider);

// Interact: only interactable zone colliders
if (Physics.Raycast(ray, out hit, 3f, PhysicsLayers.InteractMask))
    hit.collider.GetComponent<IInteractable>()?.Interact(player);
```

---

## Setting layers on prefabs

Set the layer on the root GameObject. Child colliders inherit the layer unless overridden.

For machines: root on Structures layer, with a separate child trigger for the interaction zone:

```
Smelter (layer: Structures)
├── MeshRenderer
├── BodyCollider       (Collider, inherits Structures)
├── InteractZone       (Collider, IsTrigger = true, layer: Interactable)
└── InputPort          (Collider, IsTrigger = true, layer: VolumeTrigger)
```

For BIM geometry: set root to `BIM_Static`. All submesh colliders inherit.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Fauna vs Fauna collision (jitter, performance) | Disable in collision matrix; use steering separation |
| Placement raycast hitting player character | Exclude Player from PlacementMask |
| Weapon raycast hitting volume triggers | Only include solid-geometry layers in WeaponHitMask |
| Magic layer numbers in code (`1 << 8`) | Use `PhysicsLayers` constants class |
| Belt items needing colliders | No — belt items are GPU-instanced, not GameObjects |
| Child collider on wrong layer | Explicitly set child layer when it differs from parent |
