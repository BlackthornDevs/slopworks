using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class EnvironmentSetup
{
    private const string MaterialsFolder = "Assets/_Slopworks/Materials/Environment";

    [MenuItem("Slopworks/Setup Environment Visuals")]
    public static void SetupEnvironmentVisuals()
    {
        SetupSkybox();
        SetupDirectionalLight();
        SetupFog();
        SetupAmbientLight();

        var mats = CreateMaterials();
        ApplyMaterials(mats);

        AssetDatabase.SaveAssets();
        Debug.Log("environment visuals setup complete");
    }

    // -------------------------------------------------------------------------
    // skybox
    // -------------------------------------------------------------------------

    private static void SetupSkybox()
    {
        string path = MaterialsFolder + "/MAT_Skybox.mat";
        EnsureFolder(MaterialsFolder);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogError("Skybox/Procedural shader not found — skybox skipped");
                return;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("created skybox material at " + path);
        }

        // sun
        mat.SetFloat("_SunSize", 0.04f);
        mat.SetFloat("_SunSizeConvergence", 5f);

        // atmosphere
        mat.SetFloat("_AtmosphereThickness", 1.5f);

        // sky tint — muted amber/orange
        mat.SetColor("_SkyTint", new Color(0.72f, 0.45f, 0.18f, 1f));

        // ground color — dark desaturated brown
        mat.SetColor("_GroundColor", new Color(0.18f, 0.14f, 0.10f, 1f));

        // exposure
        mat.SetFloat("_Exposure", 1.0f);

        RenderSettings.skybox = mat;
        EditorUtility.SetDirty(mat);
        Debug.Log("skybox applied");
    }

    // -------------------------------------------------------------------------
    // directional light
    // -------------------------------------------------------------------------

    private static void SetupDirectionalLight()
    {
        var lightObj = GameObject.Find("Directional Light");
        if (lightObj == null)
        {
            Debug.LogWarning("Directional Light not found in scene — light setup skipped");
            return;
        }

        var light = lightObj.GetComponent<Light>();
        if (light == null)
        {
            Debug.LogWarning("no Light component on Directional Light — skipped");
            return;
        }

        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // warm amber — 255, 220, 180 normalised
        light.color = new Color(1.0f, 0.863f, 0.706f, 1f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;

        EditorUtility.SetDirty(lightObj);
        Debug.Log("directional light configured");
    }

    // -------------------------------------------------------------------------
    // fog
    // -------------------------------------------------------------------------

    private static void SetupFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        // muted brownish grey
        RenderSettings.fogColor = new Color(0.38f, 0.33f, 0.28f, 1f);
        RenderSettings.fogDensity = 0.015f;

        Debug.Log("fog enabled (exponential squared, density 0.015)");
    }

    // -------------------------------------------------------------------------
    // ambient light
    // -------------------------------------------------------------------------

    private static void SetupAmbientLight()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;

        // sky — warm amber
        RenderSettings.ambientSkyColor = new Color(0.68f, 0.48f, 0.22f, 1f);

        // equator — muted brown
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.24f, 0.16f, 1f);

        // ground — dark brown
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.06f, 1f);

        Debug.Log("ambient lighting set to trilight (warm amber / muted brown / dark brown)");
    }

    // -------------------------------------------------------------------------
    // material creation
    // -------------------------------------------------------------------------

    private struct EnvironmentMaterials
    {
        public Material ground;
        public Material wall;
        public Material crate;
        public Material barrel;
        public Material building;
        public Material ramp;
    }

    private static EnvironmentMaterials CreateMaterials()
    {
        EnsureFolder(MaterialsFolder);

        return new EnvironmentMaterials
        {
            ground   = GetOrCreateMaterial("MAT_Ground",    new Color(0.22f, 0.20f, 0.17f), smoothness: 0.15f, metallic: 0.0f),
            wall     = GetOrCreateMaterial("MAT_Wall",      new Color(0.25f, 0.26f, 0.28f), smoothness: 0.10f, metallic: 0.0f),
            crate    = GetOrCreateMaterial("MAT_Crate",     new Color(0.45f, 0.24f, 0.10f), smoothness: 0.10f, metallic: 0.0f),
            barrel   = GetOrCreateMaterial("MAT_Barrel",    new Color(0.30f, 0.12f, 0.10f), smoothness: 0.12f, metallic: 0.0f),
            building = GetOrCreateMaterial("MAT_Building",  new Color(0.28f, 0.30f, 0.26f), smoothness: 0.20f, metallic: 0.0f),
            ramp     = GetOrCreateMaterial("MAT_Ramp",      new Color(0.20f, 0.20f, 0.22f), smoothness: 0.30f, metallic: 0.3f),
        };
    }

    private static Material GetOrCreateMaterial(string name, Color baseColor, float smoothness, float metallic)
    {
        string path = MaterialsFolder + "/" + name + ".mat";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            Debug.Log(name + " already exists, updating properties");
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP/Lit shader not found — " + name + " skipped");
                return null;
            }
            mat = new Material(shader);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("created material " + name + " at " + path);
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // -------------------------------------------------------------------------
    // material application
    // -------------------------------------------------------------------------

    private static void ApplyMaterials(EnvironmentMaterials mats)
    {
        var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int applied = 0;

        foreach (var r in allRenderers)
        {
            var mat = ResolveMaterial(r.gameObject.name, mats);
            if (mat == null) continue;

            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r.gameObject);
            applied++;
        }

        Debug.Log("materials applied to " + applied + " renderers");
    }

    private static Material ResolveMaterial(string objectName, EnvironmentMaterials mats)
    {
        // exact match first, then prefix patterns
        if (objectName == "Ground")
            return mats.ground;

        if (objectName.StartsWith("Wall_"))
            return mats.wall;

        if (objectName.StartsWith("Crate_"))
            return mats.crate;

        if (objectName.StartsWith("Barrel_"))
            return mats.barrel;

        if (objectName.StartsWith("CenterBuilding") || objectName.StartsWith("Building_"))
            return mats.building;

        if (objectName.StartsWith("Ramp_"))
            return mats.ramp;

        return null;
    }

    // -------------------------------------------------------------------------
    // utilities
    // -------------------------------------------------------------------------

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
