using UnityEngine;

/// <summary>
/// Defines an attachment surface on a building prefab.
/// Place on child GameObjects positioned at each snap location.
/// Normal defaults to transform.forward -- orient the child to face outward.
/// </summary>
public class BuildingSnapPoint : MonoBehaviour
{
    [Tooltip("Outward direction of this snap surface. Defaults to transform.forward if zero.")]
    [SerializeField] private Vector3 _normalOverride;

    [Tooltip("Width and height of the attachment area.")]
    public Vector2 SurfaceSize = Vector2.one;

    /// <summary>
    /// The outward-facing normal of this snap point.
    /// Uses _normalOverride if set, otherwise transform.forward.
    /// </summary>
    public Vector3 Normal => _normalOverride.sqrMagnitude > 0.001f
        ? _normalOverride.normalized
        : transform.forward;

    /// <summary>
    /// Auto-generate snap points from renderer bounds if none exist on the object.
    /// Creates 5 points: north (+Z), south (-Z), east (+X), west (-X), top (+Y).
    /// Skips generation if any BuildingSnapPoint already exists on children.
    /// </summary>
    public static void GenerateFromBounds(GameObject go)
    {
        if (go.GetComponentInChildren<BuildingSnapPoint>() != null)
            return;

        var renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        var lb = renderer.localBounds;
        var s = renderer.transform.lossyScale;
        var extents = new Vector3(
            lb.extents.x * Mathf.Abs(s.x),
            lb.extents.y * Mathf.Abs(s.y),
            lb.extents.z * Mathf.Abs(s.z));
        var center = renderer.transform.TransformPoint(lb.center);

        // Local-space center offset (bounds.center is world-space)
        var localCenter = go.transform.InverseTransformPoint(center);

        var faces = new[]
        {
            (dir: Vector3.forward,  offset: new Vector3(0, 0, extents.z),  size: new Vector2(extents.x * 2, extents.y * 2)),
            (dir: Vector3.back,     offset: new Vector3(0, 0, -extents.z), size: new Vector2(extents.x * 2, extents.y * 2)),
            (dir: Vector3.right,    offset: new Vector3(extents.x, 0, 0),  size: new Vector2(extents.z * 2, extents.y * 2)),
            (dir: Vector3.left,     offset: new Vector3(-extents.x, 0, 0), size: new Vector2(extents.z * 2, extents.y * 2)),
            (dir: Vector3.up,       offset: new Vector3(0, extents.y, 0),  size: new Vector2(extents.x * 2, extents.z * 2)),
        };

        foreach (var (dir, offset, size) in faces)
        {
            var child = new GameObject($"SnapPoint_{dir}");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = localCenter + offset;
            var snap = child.AddComponent<BuildingSnapPoint>();
            snap._normalOverride = dir;
            snap.SurfaceSize = size;
        }
    }

    /// <summary>
    /// Find the snap point on the given building closest to a world-space point.
    /// Returns null if no snap points exist.
    /// </summary>
    public static BuildingSnapPoint FindNearest(GameObject building, Vector3 worldPoint)
    {
        var points = building.GetComponentsInChildren<BuildingSnapPoint>();
        if (points.Length == 0) return null;

        BuildingSnapPoint nearest = null;
        float bestDist = float.MaxValue;

        foreach (var p in points)
        {
            float dist = Vector3.Distance(p.transform.position, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = p;
            }
        }

        return nearest;
    }
}
