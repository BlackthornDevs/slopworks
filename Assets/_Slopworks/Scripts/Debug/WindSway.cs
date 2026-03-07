using UnityEngine;

/// <summary>
/// Applies subtle wind-driven oscillation to a transform.
/// Attach to trees, grass, and foliage for ambient movement.
/// Each instance gets a unique phase offset based on world position
/// so they don't all sway in unison.
/// </summary>
public class WindSway : MonoBehaviour
{
    [SerializeField] private float _swayAmount = 1.5f;
    [SerializeField] private float _swaySpeed = 0.8f;
    [SerializeField] private float _swayVariation = 0.3f;

    private float _phaseX;
    private float _phaseZ;
    private float _speedMult;
    private Quaternion _baseRotation;

    private void Start()
    {
        _baseRotation = transform.localRotation;

        // derive unique phase from world position so each tree sways differently
        var pos = transform.position;
        _phaseX = pos.x * 0.7f + pos.z * 0.3f;
        _phaseZ = pos.z * 0.7f + pos.x * 0.5f;

        // slight speed variation per instance
        _speedMult = 1f + Mathf.Sin(pos.x * 1.3f + pos.z * 0.9f) * _swayVariation;
    }

    private void Update()
    {
        float t = Time.time * _swaySpeed * _speedMult;

        // layered sine waves for organic motion
        float swayX = Mathf.Sin(t + _phaseX) * 0.6f
                    + Mathf.Sin(t * 1.7f + _phaseX * 0.5f) * 0.3f
                    + Mathf.Sin(t * 0.4f + _phaseZ) * 0.1f;

        float swayZ = Mathf.Sin(t * 0.9f + _phaseZ) * 0.6f
                    + Mathf.Sin(t * 1.4f + _phaseZ * 0.7f) * 0.3f
                    + Mathf.Sin(t * 0.3f + _phaseX) * 0.1f;

        var swayRotation = Quaternion.Euler(swayX * _swayAmount, 0f, swayZ * _swayAmount);
        transform.localRotation = _baseRotation * swayRotation;
    }
}
