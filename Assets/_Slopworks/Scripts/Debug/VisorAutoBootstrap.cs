using UnityEngine;

/// <summary>
/// Auto-spawns the visor HUD test setup when Play is pressed in any scene.
/// Editor-only. Delete this file to stop auto-spawning.
/// </summary>
public static class VisorAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
#if UNITY_EDITOR
        if (Object.FindObjectOfType<ReticleTestSetup>() != null) return;
        var go = new GameObject("[VisorTest]");
        go.AddComponent<ReticleTestSetup>();
        Debug.Log("visor auto-bootstrap: spawned. B=build mode, F/T/Z/C/V=switch modes");
#endif
    }
}
