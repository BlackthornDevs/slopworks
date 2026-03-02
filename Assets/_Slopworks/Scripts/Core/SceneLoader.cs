using System.Collections.Generic;

/// Pure C# scene loader logic (D-004). Manages scene group definitions and tracks
/// current group. The MonoBehaviour wrapper handles actual Unity scene loading calls.
public class SceneLoader
{
    private readonly Dictionary<string, string[]> _groups;

    public string CurrentGroup { get; private set; }

    public SceneLoader(Dictionary<string, string[]> groups)
    {
        _groups = groups ?? new Dictionary<string, string[]>();
    }

    public string[] GetGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return null;
        _groups.TryGetValue(groupName, out var scenes);
        return scenes;
    }

    public void SetCurrentGroup(string groupName)
    {
        if (!string.IsNullOrEmpty(groupName) && _groups.ContainsKey(groupName))
            CurrentGroup = groupName;
    }
}
