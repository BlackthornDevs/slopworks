using UnityEngine;

/// <summary>
/// Creates ambient floating particles (dust, pollen, leaves) that follow the camera.
/// Attach to the camera or a root object.
/// </summary>
public class AmbientParticles : MonoBehaviour
{
    [SerializeField] private int _dustCount = 200;
    [SerializeField] private int _leafCount = 30;

    private ParticleSystem _dustSystem;
    private ParticleSystem _leafSystem;

    private void Start()
    {
        CreateDustSystem();
        CreateLeafSystem();
    }

    private void CreateDustSystem()
    {
        var go = new GameObject("DustParticles");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        _dustSystem = go.AddComponent<ParticleSystem>();
        var main = _dustSystem.main;
        main.maxParticles = _dustCount;
        main.startLifetime = 8f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new Color(0.9f, 0.85f, 0.7f, 0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.01f; // slight upward drift

        var emission = _dustSystem.emission;
        emission.rateOverTime = _dustCount / 8f;

        var shape = _dustSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 15f, 40f);

        var vel = _dustSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.15f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var colorLife = _dustSystem.colorOverLifetime;
        colorLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.3f, 0.2f),
                    new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = gradient;

        // use default particle material
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(0.9f, 0.85f, 0.7f, 0.3f);
    }

    private void CreateLeafSystem()
    {
        var go = new GameObject("LeafParticles");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * 8f;

        _leafSystem = go.AddComponent<ParticleSystem>();
        var main = _leafSystem.main;
        main.maxParticles = _leafCount;
        main.startLifetime = 12f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.3f, 0.4f, 0.15f, 0.6f),
            new Color(0.5f, 0.35f, 0.1f, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.03f;
        main.startRotation3D = true;

        var emission = _leafSystem.emission;
        emission.rateOverTime = _leafCount / 12f;

        var shape = _leafSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(50f, 5f, 50f);

        var vel = _leafSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var rot = _leafSystem.rotationOverLifetime;
        rot.enabled = true;
        rot.x = new ParticleSystem.MinMaxCurve(-1f, 1f);
        rot.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
        rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var colorLife = _leafSystem.colorOverLifetime;
        colorLife.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.15f),
                    new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(0.4f, 0.35f, 0.15f, 0.5f);
    }

    private void LateUpdate()
    {
        // keep particle emitters centered on camera
        var cam = Camera.main;
        if (cam != null)
        {
            _dustSystem.transform.position = cam.transform.position;
            _leafSystem.transform.position = cam.transform.position + Vector3.up * 5f;
        }
    }
}
