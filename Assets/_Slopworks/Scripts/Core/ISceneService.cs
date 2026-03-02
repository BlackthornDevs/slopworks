using System;

/// Abstraction over scene loading (D-002). Swap implementation for Addressables later.
public interface ISceneService
{
    string CurrentGroup { get; }
    void LoadScene(string sceneName, Action onComplete = null);
    void UnloadScene(string sceneName, Action onComplete = null);
    void TransitionTo(string groupName, Action onComplete = null);
}
