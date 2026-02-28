using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneService : MonoBehaviour, ISceneService
{
    public void LoadScene(string sceneName, Action onComplete = null)
    {
        StartCoroutine(LoadSceneAsync(sceneName, onComplete));
    }

    public void UnloadScene(string sceneName, Action onComplete = null)
    {
        StartCoroutine(UnloadSceneAsync(sceneName, onComplete));
    }

    private IEnumerator LoadSceneAsync(string sceneName, Action onComplete)
    {
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            onComplete?.Invoke();
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        yield return op;
        onComplete?.Invoke();
    }

    private IEnumerator UnloadSceneAsync(string sceneName, Action onComplete)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded)
        {
            onComplete?.Invoke();
            yield break;
        }

        var op = SceneManager.UnloadSceneAsync(scene);
        yield return op;
        onComplete?.Invoke();
    }
}
