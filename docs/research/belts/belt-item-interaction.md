# Belt Item Interaction Design

Research compiled 2026-03-09.

## Current State: Already Safe

BeltItemVisualizer.cs already prevents accidental pickup:
- Belt item visuals have NO colliders (destroyed on creation)
- Layer 18 (Decal) -- not in any interaction mask
- No NetworkObject or NetworkWorldItem components
- NetworkPickupTrigger.OnTriggerEnter requires both NetworkObject + NetworkWorldItem -- belt visuals have neither

Walking over belt items does nothing. No changes needed for the "no accidental pickup" requirement.

## Satisfactory Reference

- Press E to grab items off belts (intentional crosshair raycast)
- Hold E for continuous collection
- Can only extract, never place back on belt (use storage/splitter for that)
- Dismantling a belt deposits all items to player inventory

## Recommended Interaction: Raycast Belt, Resolve Item by Distance

**Approach A (recommended):** Raycast the belt segment collider, resolve nearest item mathematically.

1. Player presses interact key, raycast hits belt segment collider
2. Get NetworkBeltSegment from hit object
3. Convert hit point to parametric `t` value along the belt spline
4. Find item in _syncItems whose cumulative distance is nearest to `t * totalLength`
5. Send `CmdGrabBeltItem(NetworkBeltSegment, itemIndex)` ServerRpc

Pros:
- Zero additional colliders
- One raycast per interact press
- Works regardless of item count (O(n) scan of small list)
- No physics overhead from hundreds of tiny triggers
- Belt already has a collider for placement raycasts

**Approach B (rejected):** Small trigger collider on each belt item visual.
- Hundreds of colliders moving every frame = expensive broadphase updates
- Collider pool management complexity
- Not worth the pixel-precise hit detection

## Future Optimization: GPU Instancing

For scale (hundreds of items visible), replace GameObjects with `Graphics.RenderMeshInstanced()`:
- Zero GameObjects, zero transforms, zero colliders
- One draw call per item type
- Fill Matrix4x4[] array each frame from synced positions
- Already mentioned in render-pipeline.md as planned optimization

This is compatible with Approach A for interaction -- the raycast still hits the belt collider, not individual items.

## Physics Layer Decision

No new layer needed for Approach A. If Approach B is ever needed, layer 21 is available.

## Sources

- [Conveyor Belts - Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Belts)
- [Factorio Transport Belts/Physics](https://wiki.factorio.com/Transport_belts/Physics)
- [Unity GPU Instancing](https://docs.unity3d.com/Manual/GPUInstancing.html)
