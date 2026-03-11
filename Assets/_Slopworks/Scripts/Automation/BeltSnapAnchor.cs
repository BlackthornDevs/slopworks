using UnityEngine;

/// <summary>
/// Placement guide on a support prefab. Provides a snap position and direction
/// for belt placement. Not a network node -- purely a placement helper.
/// Belt reads position and direction at construction time; no ongoing relationship.
/// </summary>
public class BeltSnapAnchor : MonoBehaviour
{
    /// <summary>
    /// World-space position for belt endpoint placement.
    /// </summary>
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// World-space direction for belt tangent at this anchor.
    /// </summary>
    public Vector3 WorldDirection => transform.forward;
}
