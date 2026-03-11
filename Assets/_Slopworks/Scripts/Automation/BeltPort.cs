using UnityEngine;

/// <summary>
/// Direction a belt port faces for item flow.
/// </summary>
public enum BeltPortDirection
{
    Input,
    Output
}

/// <summary>
/// A connection point on a prefab where a belt can attach.
/// Placed as a child GameObject on machine, storage, and belt prefabs.
/// Position and forward direction come from the child transform.
/// </summary>
public class BeltPort : MonoBehaviour
{
    [SerializeField] private BeltPortDirection _direction = BeltPortDirection.Input;
    [SerializeField] private int _slotIndex;
    [SerializeField] private string _slotLabel;

    public BeltPortDirection Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public int SlotIndex
    {
        get => _slotIndex;
        set => _slotIndex = value;
    }

    public string SlotLabel
    {
        get => _slotLabel;
        set => _slotLabel = value;
    }

    /// <summary>
    /// World-space position of this port.
    /// </summary>
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// World-space direction this port faces (outward from the building).
    /// For Output ports, this is the direction items leave.
    /// For Input ports, this is the direction items arrive from.
    /// </summary>
    public Vector3 WorldDirection => transform.forward;
}
