# FishNet Serialization for Spline Data

Research compiled 2026-03-09. Updated with deep integration findings.

## FishNet Has Built-In Unity.Mathematics Serializers

FishNet ships with conditional serializers for all Unity.Mathematics types:
- Location: `Assets/FishNet/Runtime/Serializing/UnityMathmatics/`
- Guarded by `#if UNITYMATHEMATICS` preprocessor define
- Auto-enabled when `com.unity.mathematics >= 1.2.6` is in the project

### Supported Types (Once Mathematics Package Installed)
- `float2`, `float3`, `float4`
- All matrix types (`float2x2` through `float4x4`)
- `quaternion`
- `Random`
- `RigidTransform`
- `AffineTransform` (v1.3.1+)
- `MinMaxAABB` (v1.3.2+)

### What This Means for Belts

Installing `com.unity.splines` automatically pulls in `com.unity.mathematics` as a dependency. Once installed:
- FishNet can serialize `float3` and `quaternion` out of the box
- `BezierKnot` (float3 position + float3 tangentIn + float3 tangentOut + quaternion rotation) can be synced by decomposing into these primitive types
- No custom serializer code needed

### Recommended Sync Approach

For a 2-knot belt spline (start + end):
```
Sync data per belt:
  - startPos: float3 (12 bytes) -- or Vector3 converted
  - startTangentOut: float3 (12 bytes)
  - endPos: float3 (12 bytes)
  - endTangentIn: float3 (12 bytes)
  Total: 48 bytes per belt
```

Alternatively, sync as Vector3 (which FishNet already handles) and convert to float3 on the client:
```
  - startPos: Vector3 (12 bytes)
  - startTangent: Vector3 (12 bytes)
  - endPos: Vector3 (12 bytes)
  - endTangent: Vector3 (12 bytes)
  Total: 48 bytes per belt
```

Either approach works. The Vector3 approach avoids any dependency on Mathematics package for the sync layer.

### Current NetworkBeltSegment Sync Model

Currently uses:
- `SyncList<BeltItem>` for item data (already works, no change needed)
- `SyncVar<ushort>` for terminal gap (no change needed)
- `Vector3 _startPos, _endPos` for endpoints (would expand to include tangents)

## BezierKnot Custom Serializer

BezierKnot (from com.unity.splines) is NOT covered by FishNet's built-in serializers. But since its fields are all covered types, a custom serializer is trivial (~15 lines):

```csharp
public static class BezierKnotSerializer
{
    public static void WriteBezierKnot(this Writer writer, BezierKnot value)
    {
        writer.Writefloat3(value.Position);
        writer.Writefloat3(value.TangentIn);
        writer.Writefloat3(value.TangentOut);
        writer.Writequaternion(value.Rotation);
    }

    public static BezierKnot ReadBezierKnot(this Reader reader)
    {
        return new BezierKnot
        {
            Position = reader.Readfloat3(),
            TangentIn = reader.Readfloat3(),
            TangentOut = reader.Readfloat3(),
            Rotation = reader.Readquaternion()
        };
    }
}
```

Each knot = 52 bytes (3x float3 + quaternion).

## SplineContainer + NetworkObject Compatibility

**No conflicts.** SplineContainer is a standard MonoBehaviour operating on orthogonal concerns from NetworkObject. They coexist on the same GameObject without issues.

Important: never sync SplineContainer itself -- sync the knot data and reconstruct client-side.

## Recommended Sync Pattern

**Simplest approach (recommended):** Sync only endpoint positions + tangent directions (4x Vector3 = 48 bytes) via SyncVar. Both server and client deterministically construct the same spline. The spline shape is a pure function of port positions and directions.

Alternative: SyncVar with a BeltSplineData struct containing BezierKnot array. Use OnChange callback to reconstruct SplineContainer client-side.

Do NOT use ServerRpc for spline data -- late-joining players wouldn't receive it (hard rule: no RPCs for persistent state).

## Current Project State

- `com.unity.splines`: NOT installed
- `com.unity.mathematics`: NOT installed (but v1.3.3 exists as transitive dependency)
- FishNet serializers: present, auto-activate when Mathematics package installed
- All existing belt sync uses standard Unity types (Vector3, ushort, string)

## PhysicsLayers Status

All slots 8-20 are allocated:
- 8: Player, 9: Fauna, 10: Projectile, 11: BIM_Static
- 12: Terrain, 13: Structures, 14: Interactable, 15: GridPlane
- 16: VolumeTrigger, 17: NavMeshAgent, 18: Decal, 19: FogOfWar, 20: SnapPoints

Slot 21+ available if a BeltItem layer is needed.
Current belt item visuals use layer 18 (Decal) with no colliders.

## Belt Item Visuals (Current)

BeltItemVisualizer.cs creates primitive cubes per item:
- Colliders are immediately destroyed (`Destroy(col)`)
- Layer: Decal (18)
- No physics interaction -- purely visual
- Position updated in LateUpdate from NetworkBeltSegment synced data
