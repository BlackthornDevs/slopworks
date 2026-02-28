using UnityEngine;

// Place on the FPSCamera GameObject.
// Call Shake(intensity) to trigger a shake — 0.0 is imperceptible, 1.0 is maximum.
// Runs in LateUpdate so it layers on top of CameraRecoil and PlayerController.
public class CameraShake : MonoBehaviour
{
    [SerializeField] private float _maxMagnitude = 0.15f;

    // independent seeds so X and Y noise tracks move at different rates
    private const float NoiseSpeedX = 37.3f;
    private const float NoiseSpeedY = 19.7f;
    private const float NoiseSeedY = 100f;

    private float _currentIntensity;
    private float _remainingDuration;

    // time offset so multiple shakes in quick succession sample different noise regions
    private float _noiseOffset;

    // store the original localPosition so we don't overwrite the camera's eye-height offset
    private Vector3 _baseLocalPosition;

    private void Awake()
    {
        _baseLocalPosition = transform.localPosition;
    }

    public void Shake(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        // allow a new shake to override a weaker in-progress shake
        if (intensity >= _currentIntensity)
        {
            _currentIntensity = intensity;
            _remainingDuration = Mathf.Lerp(0.1f, 0.3f, intensity);
            _noiseOffset = Random.value * 100f;
        }
    }

    private void LateUpdate()
    {
        if (_remainingDuration <= 0f)
        {
            transform.localPosition = _baseLocalPosition;
            return;
        }

        _remainingDuration -= Time.deltaTime;

        // exponential decay — the shake drops off quickly at the end
        float decay = _remainingDuration / Mathf.Max(_remainingDuration + Time.deltaTime, 0.001f);
        float magnitude = _currentIntensity * _maxMagnitude * decay;

        float t = Time.time + _noiseOffset;
        float offsetX = (Mathf.PerlinNoise(t * NoiseSpeedX, 0f) - 0.5f) * 2f * magnitude;
        float offsetY = (Mathf.PerlinNoise(t * NoiseSpeedY, NoiseSeedY) - 0.5f) * 2f * magnitude;

        transform.localPosition = _baseLocalPosition + new Vector3(offsetX, offsetY, 0f);

        if (_remainingDuration <= 0f)
        {
            _currentIntensity = 0f;
            transform.localPosition = _baseLocalPosition;
        }
    }
}
