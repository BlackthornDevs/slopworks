using UnityEngine;

/// <summary>
/// Applies subtle wind-driven oscillation to a transform.
/// When a camera exists, only updates within range and viewport.
/// When no camera exists (editor preview), sways unconditionally.
/// </summary>
public class WindSway : MonoBehaviour
{
    [SerializeField] private float _swayAmount = 1.5f;
    [SerializeField] private float _swaySpeed = 0.8f;
    [SerializeField] private float _swayVariation = 0.3f;

    private const float ActiveRange = 80f;
    private const float ActiveRangeSq = ActiveRange * ActiveRange;
    private const float CheckInterval = 0.5f;

    private float _phaseX;
    private float _phaseZ;
    private float _speedMult;
    private Quaternion _baseRotation;
    private Camera _camera;
    private float _nextCheck;
    private bool _active;
    private bool _hasCamera;

    private void Start()
    {
        _baseRotation = transform.localRotation;

        var pos = transform.position;
        _phaseX = pos.x * 0.7f + pos.z * 0.3f;
        _phaseZ = pos.z * 0.7f + pos.x * 0.5f;
        _speedMult = 1f + Mathf.Sin(pos.x * 1.3f + pos.z * 0.9f) * _swayVariation;

        _nextCheck = Time.time + Random.Range(0f, CheckInterval);
        _active = true; // default to active until camera check says otherwise
    }

    private void Update()
    {
        if (Time.time > _nextCheck)
        {
            _nextCheck = Time.time + CheckInterval;

            if (_camera == null)
            {
                _camera = Camera.main;
                _hasCamera = _camera != null;
            }

            if (_hasCamera)
            {
                bool wasActive = _active;
                _active = false;

                if (_camera != null)
                {
                    var delta = transform.position - _camera.transform.position;
                    if (delta.sqrMagnitude < ActiveRangeSq)
                    {
                        var vp = _camera.WorldToViewportPoint(transform.position);
                        if (vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f)
                            _active = true;
                    }
                }

                if (wasActive && !_active)
                    transform.localRotation = _baseRotation;
            }
            // no camera — _active stays true, all trees sway
        }

        if (!_active) return;

        float t = Time.time * _swaySpeed * _speedMult;

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
