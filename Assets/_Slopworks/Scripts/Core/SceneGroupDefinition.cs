using UnityEngine;

/// Maps a group name to its additive scenes. Serializable for inspector assignment.
[System.Serializable]
public class SceneGroupDefinition
{
    public string groupName;
    public string[] sceneNames;
}
