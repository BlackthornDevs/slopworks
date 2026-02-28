using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a temporary bullet tracer line between the weapon muzzle and the hit point.
/// Call ProjectileTracer.Spawn(from, to) from WeaponBehaviour.OnFire.
/// No scene references or material assets required.
/// </summary>
public static class ProjectileTracer
{
    private const float FadeDuration  = 0.1f;
    private const float StartWidth    = 0.02f;
    private const float EndWidth      = 0.005f;

    private static readonly Color ColorBright = new Color(1f, 0.95f, 0.4f, 1f);   // warm yellow-white
    private static readonly Color ColorFaded  = new Color(1f, 0.95f, 0.4f, 0f);

    /// <summary>
    /// Spawns a tracer line from <paramref name="from"/> to <paramref name="to"/>
    /// that fades out over ~0.1 seconds then self-destructs.
    /// </summary>
    public static void Spawn(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ProjectileTracer");

        var lr                = go.AddComponent<LineRenderer>();
        lr.positionCount      = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth         = StartWidth;
        lr.endWidth           = EndWidth;
        lr.useWorldSpace      = true;
        lr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows     = false;

        // Use the URP-compatible sprite/unlit material — no asset reference needed
        lr.material           = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        lr.startColor         = ColorBright;
        lr.endColor           = ColorBright;

        var runner = go.AddComponent<TracerRunner>();
        runner.StartFade(lr, FadeDuration, ColorBright, ColorFaded, go);
    }

    /// <summary>
    /// Internal MonoBehaviour that drives the tracer coroutine.
    /// Lives only on the temporary tracer GameObject.
    /// </summary>
    private sealed class TracerRunner : MonoBehaviour
    {
        public void StartFade(LineRenderer lr, float duration,
            Color start, Color end, GameObject target)
        {
            StartCoroutine(FadeRoutine(lr, duration, start, end, target));
        }

        private static IEnumerator FadeRoutine(LineRenderer lr, float duration,
            Color start, Color end, GameObject target)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Color c = Color.Lerp(start, end, t);
                lr.startColor = c;
                lr.endColor   = c;
                yield return null;
            }

            Destroy(target);
        }
    }
}
