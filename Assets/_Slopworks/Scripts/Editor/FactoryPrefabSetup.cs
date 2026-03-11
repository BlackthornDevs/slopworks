using FishNet.Object;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Machine, Storage, and Belt prefabs for multiplayer factory placement.
/// Run from Slopworks > Create Factory Prefabs.
/// Also adds NetworkFactorySimulation to GridManager if missing.
/// </summary>
public static class FactoryPrefabSetup
{
    [MenuItem("Slopworks/Create Factory Prefabs")]
    public static void CreateAll()
    {
        CreateMachinePrefab();
        CreateStoragePrefab();
        CreateBeltPrefab();
        WireGridManager();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("factory setup: all prefabs created and GridManager wired");
    }

    private static void CreateMachinePrefab()
    {
        string dir = "Assets/_Slopworks/Resources/Prefabs/Buildings/Machines";
        string path = dir + "/Machine.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("factory setup: Machine.prefab already exists, skipping");
            return;
        }

        EnsureDirectory(dir);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Machine";
        go.transform.localScale = new Vector3(
            FactoryGrid.CellSize * 0.8f, 0.5f, FactoryGrid.CellSize * 0.8f);
        go.layer = PhysicsLayers.Interactable;

        // Color it orange
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.8f, 0.5f, 0f);
        renderer.sharedMaterial = mat;
        AssetDatabase.CreateAsset(mat, dir + "/MachineMat.mat");

        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkMachine>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"factory setup: created {path}");
    }

    private static void CreateStoragePrefab()
    {
        string dir = "Assets/_Slopworks/Resources/Prefabs/Buildings/Storage";
        string path = dir + "/Storage.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("factory setup: Storage.prefab already exists, skipping");
            return;
        }

        EnsureDirectory(dir);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Storage";
        go.transform.localScale = new Vector3(
            FactoryGrid.CellSize * 0.8f, 0.4f, FactoryGrid.CellSize * 0.8f);
        go.layer = PhysicsLayers.Interactable;

        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.3f, 1f);
        renderer.sharedMaterial = mat;
        AssetDatabase.CreateAsset(mat, dir + "/StorageMat.mat");

        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkStorage>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"factory setup: created {path}");
    }

    private static void CreateBeltPrefab()
    {
        string dir = "Assets/_Slopworks/Resources/Prefabs/Buildings/Belts";
        string path = dir + "/Belt.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("factory setup: Belt.prefab already exists, skipping");
            return;
        }

        EnsureDirectory(dir);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Belt";
        go.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f);
        go.layer = PhysicsLayers.Interactable;

        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 1f, 0f);
        renderer.sharedMaterial = mat;
        AssetDatabase.CreateAsset(mat, dir + "/BeltMat.mat");

        go.AddComponent<NetworkObject>();
        go.AddComponent<NetworkBeltSegment>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        Debug.Log($"factory setup: created {path}");
    }

    private static void WireGridManager()
    {
        var gridManager = Object.FindObjectOfType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogWarning("factory setup: GridManager not found in scene. Open HomeBase scene and run again.");
            return;
        }

        if (gridManager.GetComponent<NetworkFactorySimulation>() == null)
        {
            gridManager.gameObject.AddComponent<NetworkFactorySimulation>();
            EditorUtility.SetDirty(gridManager.gameObject);
            Debug.Log("factory setup: added NetworkFactorySimulation to GridManager");
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
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
}
