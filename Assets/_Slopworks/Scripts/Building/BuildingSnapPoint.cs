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
    /// The outward-facing normal of this snap point in world space.
    /// Transforms _normalOverride by the parent's rotation so normals
    /// stay correct on rotated buildings.
    /// </summary>
    public Vector3 Normal => _normalOverride.sqrMagnitude > 0.001f
        ? transform.TransformDirection(_normalOverride.normalized)
        : transform.forward;

    /// <summary>
    /// Auto-generate snap points from renderer bounds if none exist on the object.
    /// Structural (foundation/wall): 14 points (4 cardinal x 3 heights + Center_Top + Center_Bot).
    /// Ramps: 6 points (3 cardinal _Bot + HighEdge + LowEdge + Center_Bot).
    /// Machines/Storage: 5 placement points (4 cardinal _Bot + Center_Bot).
    /// Skips generation if any BuildingSnapPoint already exists on children.
    /// </summary>
    public static void GenerateFromBounds(GameObject go, BuildingCategory category = BuildingCategory.Foundation)
    {
        if (go.GetComponentInChildren<BuildingSnapPoint>() != null)
            return;

        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // Combine world-space bounds from all renderers
        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        var worldExtents = worldBounds.extents;
        var localCenter = go.transform.InverseTransformPoint(worldBounds.center);
        var ext = worldExtents;

        var cardinals = new[]
        {
            (name: "North", dir: Vector3.forward, offset: new Vector3(0, 0, ext.z)),
            (name: "South", dir: Vector3.back,    offset: new Vector3(0, 0, -ext.z)),
            (name: "East",  dir: Vector3.right,   offset: new Vector3(ext.x, 0, 0)),
            (name: "West",  dir: Vector3.left,    offset: new Vector3(-ext.x, 0, 0)),
        };

        switch (category)
        {
            case BuildingCategory.Foundation:
            case BuildingCategory.Wall:
                // Structural: 14 points (4 cardinal x 3 heights + Center_Top + Center_Bot)
                foreach (var (name, dir, offset) in cardinals)
                {
                    bool isXFace = Mathf.Abs(dir.x) > 0.5f;
                    var faceSize = isXFace
                        ? new Vector2(worldExtents.z * 2, worldExtents.y * 2)
                        : new Vector2(worldExtents.x * 2, worldExtents.y * 2);
                    AddPoint(go, $"{name}_Top", localCenter + offset + new Vector3(0, ext.y, 0), dir, faceSize);
                    AddPoint(go, $"{name}_Mid", localCenter + offset, dir, faceSize);
                    AddPoint(go, $"{name}_Bot", localCenter + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
                {
                    var topBotSize = new Vector2(worldExtents.x * 2, worldExtents.z * 2);
                    AddPoint(go, "Center_Top", localCenter + new Vector3(0, ext.y, 0), Vector3.up, topBotSize);
                    AddPoint(go, "Center_Bot", localCenter + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
                }
                break;

            case BuildingCategory.Ramp:
                // 6 points: 3 cardinal _Bot (no South) + HighEdge + LowEdge + Center_Bot
                foreach (var (name, dir, offset) in cardinals)
                {
                    if (name == "South") continue;
                    bool isXFace = Mathf.Abs(dir.x) > 0.5f;
                    var faceSize = isXFace
                        ? new Vector2(worldExtents.z * 2, worldExtents.y * 2)
                        : new Vector2(worldExtents.x * 2, worldExtents.y * 2);
                    AddPoint(go, $"{name}_Bot", localCenter + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
                {
                    var topBotSize = new Vector2(worldExtents.x * 2, worldExtents.z * 2);
                    AddPoint(go, "HighEdge", localCenter + new Vector3(0, ext.y, ext.z), Vector3.forward,
                        new Vector2(worldExtents.x * 2, 0.1f));
                    AddPoint(go, "LowEdge", localCenter + new Vector3(0, -ext.y, -ext.z), Vector3.back,
                        new Vector2(worldExtents.x * 2, 0.1f));
                    AddPoint(go, "Center_Bot", localCenter + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
                }
                break;

            case BuildingCategory.Machine:
                // 5 points: 4 cardinal _Bot + Center_Bot
                foreach (var (name, dir, offset) in cardinals)
                {
                    bool isXFace = Mathf.Abs(dir.x) > 0.5f;
                    var faceSize = isXFace
                        ? new Vector2(worldExtents.z * 2, worldExtents.y * 2)
                        : new Vector2(worldExtents.x * 2, worldExtents.y * 2);
                    AddPoint(go, $"{name}_Bot", localCenter + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
                {
                    var topBotSize = new Vector2(worldExtents.x * 2, worldExtents.z * 2);
                    AddPoint(go, "Center_Bot", localCenter + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
                }
                break;

            case BuildingCategory.Storage:
                // 6 points: 4 cardinal _Bot + Center_Top + Center_Bot
                foreach (var (name, dir, offset) in cardinals)
                {
                    bool isXFace = Mathf.Abs(dir.x) > 0.5f;
                    var faceSize = isXFace
                        ? new Vector2(worldExtents.z * 2, worldExtents.y * 2)
                        : new Vector2(worldExtents.x * 2, worldExtents.y * 2);
                    AddPoint(go, $"{name}_Bot", localCenter + offset + new Vector3(0, -ext.y, 0), dir, faceSize);
                }
                {
                    var topBotSize = new Vector2(worldExtents.x * 2, worldExtents.z * 2);
                    AddPoint(go, "Center_Top", localCenter + new Vector3(0, ext.y, 0), Vector3.up, topBotSize);
                    AddPoint(go, "Center_Bot", localCenter + new Vector3(0, -ext.y, 0), Vector3.down, topBotSize);
                }
                break;

            // Belt and Support use BeltPort/BeltSnapAnchor, not BuildingSnapPoints.
            // Any future categories also skip snap generation by default.
            default:
                break;
        }
    }

    private static void AddPoint(GameObject parent, string name, Vector3 localPos, Vector3 normal, Vector2 size)
    {
        var child = new GameObject($"SnapPoint_{name}");
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPos;
        child.layer = PhysicsLayers.SnapPoints;
        var snap = child.AddComponent<BuildingSnapPoint>();
        snap._normalOverride = normal;
        snap.SurfaceSize = size;
        var sphere = child.AddComponent<SphereCollider>();
        sphere.radius = 0.5f;
        sphere.isTrigger = true;
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

    /// <summary>
    /// Find the snap point on the given building closest to a world-space point,
    /// filtering to only snap points on the same face as the raycast hit.
    /// Falls back to any closest snap if no face match is found.
    /// </summary>
    public static BuildingSnapPoint FindNearest(GameObject building, Vector3 worldPoint, Vector3 hitNormal)
    {
        var points = building.GetComponentsInChildren<BuildingSnapPoint>();
        if (points.Length == 0) return null;

        BuildingSnapPoint nearest = null;
        float bestDist = float.MaxValue;

        foreach (var p in points)
        {
            if (Vector3.Dot(p.Normal, hitNormal) < 0.5f) continue;

            float dist = Vector3.Distance(p.transform.position, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = p;
            }
        }

        if (nearest == null)
            return FindNearest(building, worldPoint);

        return nearest;
    }
}
