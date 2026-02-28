using UnityEditor;
using UnityEngine;
using TMPro;

public static class PlayerCharacterSetup
{
    [MenuItem("Slopworks/Wire PlayerCharacter References")]
    public static void WireReferences()
    {
        var player = GameObject.Find("PlayerCharacter");
        if (player == null)
        {
            Debug.LogError("PlayerCharacter not found in scene");
            return;
        }

        var fpsCamGO = player.transform.Find("FPSCamera");
        var isoCamGO = player.transform.Find("IsometricCamera");

        if (fpsCamGO == null || isoCamGO == null)
        {
            Debug.LogError("FPSCamera or IsometricCamera child not found");
            return;
        }

        var fpsCamera = fpsCamGO.GetComponent<Camera>();
        var isoCamera = isoCamGO.GetComponent<Camera>();

        // wire CameraModeController
        var camMode = player.GetComponent<CameraModeController>();
        if (camMode != null)
        {
            var so = new SerializedObject(camMode);
            so.FindProperty("_fpsCamera").objectReferenceValue = fpsCamera;
            so.FindProperty("_isometricCamera").objectReferenceValue = isoCamera;
            so.FindProperty("_playerController").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("_interactionController").objectReferenceValue = player.GetComponent<InteractionController>();
            so.ApplyModifiedProperties();
            Debug.Log("CameraModeController references wired");
        }

        // wire InteractionController
        var interact = player.GetComponent<InteractionController>();
        if (interact != null)
        {
            var so = new SerializedObject(interact);
            so.FindProperty("_camera").objectReferenceValue = fpsCamera;

            var promptGO = GameObject.Find("InteractionPrompt");
            if (promptGO != null)
            {
                so.FindProperty("_promptText").objectReferenceValue =
                    promptGO.GetComponent<TextMeshProUGUI>();
                Debug.Log("InteractionController _promptText wired");
            }
            else
            {
                Debug.LogWarning("InteractionPrompt not found in scene — _promptText not wired");
            }

            so.ApplyModifiedProperties();
            Debug.Log("InteractionController references wired");
        }

        EditorUtility.SetDirty(player);
        Debug.Log("PlayerCharacter setup complete");
    }

    [MenuItem("Slopworks/Save PlayerCharacter Prefab")]
    public static void SavePrefab()
    {
        var player = GameObject.Find("PlayerCharacter");
        if (player == null)
        {
            Debug.LogError("PlayerCharacter not found in scene");
            return;
        }

        string folder = "Assets/_Slopworks/Prefabs/Player";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Slopworks/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Slopworks", "Prefabs");
            AssetDatabase.CreateFolder("Assets/_Slopworks/Prefabs", "Player");
        }

        string path = folder + "/PlayerCharacter.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, path, InteractionMode.UserAction);
        Debug.Log("Prefab saved to " + path);
    }
}
