using UnityEditor;
using UnityEngine;

/// <summary>
/// Quick scene cleanup utilities. Non-destructive -- only removes specific objects.
/// </summary>
public static class SceneCleanup
{
    [MenuItem("Slopworks/Cleanup/Remove MicroSplat from Terrain")]
    public static void RemoveMicroSplat()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("scene cleanup: no active terrain found");
            return;
        }

        int removed = 0;
        // remove all MicroSplat components by type name (avoids hard dependency on MicroSplat assembly)
        foreach (var comp in terrain.gameObject.GetComponents<MonoBehaviour>())
        {
            if (comp != null && comp.GetType().FullName.Contains("MicroSplat"))
            {
                Undo.DestroyObjectImmediate(comp);
                removed++;
            }
        }

        // reset terrain material to default URP terrain shader
        var urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (urpTerrainShader != null)
        {
            terrain.materialTemplate = new Material(urpTerrainShader);
            Debug.Log("scene cleanup: assigned URP Terrain/Lit shader");
        }
        else
        {
            // fallback: clear material entirely so Unity picks the default
            terrain.materialTemplate = null;
            Debug.LogWarning("scene cleanup: could not find URP terrain shader, cleared material");
        }

        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(terrain.terrainData);

        Debug.Log($"scene cleanup: removed {removed} MicroSplat components, terrain material reset");
    }

    [MenuItem("Slopworks/Cleanup/Remove Scenery Dressing (Trees + Debris)")]
    public static void RemoveSceneryDressing()
    {
        int count = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go == null) continue;
            string lower = go.name.ToLowerInvariant();

            bool isScenery =
                lower.Contains("tree") ||
                lower.Contains("pine") ||
                lower.Contains("birch") ||
                lower.Contains("bush") ||
                lower.Contains("flower") ||
                lower.Contains("rock") ||
                lower.Contains("stump") ||
                lower.Contains("log") ||
                lower.Contains("barrel") ||
                lower.Contains("crate") ||
                lower.Contains("debris") ||
                lower.Contains("ruin") ||
                lower.Contains("grass") ||
                lower.Contains("campfire") ||
                lower.Contains("tent") ||
                lower.Contains("undergrowth") ||
                lower.Contains("scenery") ||
                lower.Contains("prop_");

            // Don't nuke player, buildings, terrain, or cameras
            bool isProtected =
                lower.Contains("terrain") ||
                lower.Contains("camera") ||
                lower.Contains("light") ||
                lower.Contains("player") ||
                lower.Contains("foundation") ||
                lower.Contains("wall") ||
                lower.Contains("ramp") ||
                lower.Contains("machine") ||
                lower.Contains("belt") ||
                lower.Contains("turret") ||
                lower.Contains("storage") ||
                lower.Contains("network") ||
                lower.Contains("canvas") ||
                lower.Contains("eventsystem");

            if (isScenery && !isProtected)
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }
        Debug.Log($"scene cleanup: removed {count} scenery/dressing objects (Ctrl+Z to undo)");
    }

    [MenuItem("Slopworks/Cleanup/Remove Patch-Grass Objects")]
    public static void RemovePatchGrass()
    {
        int count = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            string lower = go.name.ToLowerInvariant();
            if (lower.Contains("patch-grass") || lower.Contains("patch_grass"))
            {
                Undo.DestroyObjectImmediate(go);
                count++;
            }
        }
        Debug.Log($"scene cleanup: removed {count} patch-grass objects");
    }
}
