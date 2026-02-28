using System.Collections;
using UnityEngine;

/// <summary>
/// Produces a brief point light flash and particle burst when Fire() is called.
/// Attach to a child of the weapon or FPS camera at the muzzle position.
/// No serialized references needed — all objects are created in Awake.
/// </summary>
public class MuzzleFlash : MonoBehaviour
{
    private const float LightDuration    = 0.05f;
    private const float LightRange       = 3f;
    private const float LightIntensity   = 2f;
    private const int   ParticleCount    = 10;
    private const float ParticleLifetime = 0.1f;
    private const float ParticleSpeed    = 4f;
    private const float ParticleSize     = 0.04f;

    // Warm muzzle flash color: ~2700 K tungsten
    private static readonly Color FlashColor    = new Color(1f, 0.75f, 0.25f);
    private static readonly Color ParticleColor = new Color(1f, 0.65f, 0.15f);

    private Light           _light;
    private ParticleSystem  _particles;

    private void Awake()
    {
        BuildLight();
        BuildParticles();
    }

    /// <summary>
    /// Triggers one muzzle flash. Call this from WeaponBehaviour.OnFire.
    /// </summary>
    public void Fire()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());

        if (_particles != null)
            _particles.Emit(ParticleCount);
    }

    private IEnumerator FlashRoutine()
    {
        _light.enabled    = true;
        _light.intensity  = LightIntensity;

        float elapsed = 0f;
        while (elapsed < LightDuration)
        {
            elapsed          += Time.deltaTime;
            float t           = Mathf.Clamp01(elapsed / LightDuration);
            _light.intensity  = Mathf.Lerp(LightIntensity, 0f, t);
            yield return null;
        }

        _light.enabled = false;
    }

    private void BuildLight()
    {
        var lightObj = new GameObject("MuzzleLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = Vector3.zero;

        _light            = lightObj.AddComponent<Light>();
        _light.type       = LightType.Point;
        _light.color      = FlashColor;
        _light.range      = LightRange;
        _light.intensity  = LightIntensity;
        _light.shadows    = LightShadows.None;
        _light.enabled    = false;
    }

    private void BuildParticles()
    {
        var psObj = new GameObject("MuzzleParticles");
        psObj.transform.SetParent(transform, false);
        psObj.transform.localPosition = Vector3.zero;

        _particles = psObj.AddComponent<ParticleSystem>();

        // Stop the auto-play so we control emission manually
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _particles.main;
        main.loop               = false;
        main.playOnAwake        = false;
        main.maxParticles       = ParticleCount * 2;
        main.startLifetime      = ParticleLifetime;
        main.startSpeed         = ParticleSpeed;
        main.startSize          = ParticleSize;
        main.startColor         = ParticleColor;
        main.simulationSpace    = ParticleSystemSimulationSpace.World;

        var emission = _particles.emission;
        emission.enabled        = false;    // burst-only via Emit()

        var shape = _particles.shape;
        shape.enabled           = true;
        shape.shapeType         = ParticleSystemShapeType.Cone;
        shape.angle             = 25f;
        shape.radius            = 0.01f;

        // Apply a URP-compatible unlit material so particles are visible in URP
        var renderer = _particles.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (mat != null)
        {
            mat.SetColor("_BaseColor", ParticleColor);
            renderer.material = mat;
        }

        renderer.renderMode    = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder  = 0;
    }
}
