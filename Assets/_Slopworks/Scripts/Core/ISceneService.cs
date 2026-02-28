using System;

public interface ISceneService
{
    void LoadScene(string sceneName, Action onComplete = null);
    void UnloadScene(string sceneName, Action onComplete = null);
}
