using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MonoBehaviour wrapper for SceneLoader. Lives in Core_GameManager (always resident).
/// Handles coroutine-based async scene loading and fade transitions.
/// </summary>
public class SceneLoaderBehaviour : MonoBehaviour, ISceneService
{
    [SerializeField] private SceneGroupDefinition[] _sceneGroups;
    [SerializeField] private float _fadeDuration = 0.3f;

    private SceneLoader _loader;
    private CanvasGroup _fadePanel;
    private bool _isTransitioning;

    public string CurrentGroup => _loader?.CurrentGroup;

    private void Awake()
    {
        var groups = new Dictionary<string, string[]>();
        if (_sceneGroups != null)
        {
            foreach (var group in _sceneGroups)
            {
                if (!string.IsNullOrEmpty(group.groupName) && group.sceneNames != null)
                    groups[group.groupName] = group.sceneNames;
            }
        }

        _loader = new SceneLoader(groups);
        CreateFadePanel();
    }

    public void TransitionTo(string groupName, Action onComplete = null)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("scene loader: transition already in progress, ignoring");
            return;
        }

        var scenes = _loader.GetGroup(groupName);
        if (scenes == null)
        {
            Debug.LogError($"scene loader: unknown group '{groupName}'");
            return;
        }

        StartCoroutine(TransitionCoroutine(groupName, scenes, onComplete));
    }

    public void LoadScene(string sceneName, Action onComplete = null)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName, onComplete));
    }

    public void UnloadScene(string sceneName, Action onComplete = null)
    {
        StartCoroutine(UnloadSceneCoroutine(sceneName, onComplete));
    }

    private IEnumerator TransitionCoroutine(string groupName, string[] targetScenes, Action onComplete)
    {
        _isTransitioning = true;
        Debug.Log($"scene loader: transitioning to {groupName}");

        // Fade to black
        yield return FadeCoroutine(0f, 1f);

        // Unload current group
        var currentScenes = _loader.GetGroup(_loader.CurrentGroup);
        if (currentScenes != null)
        {
            foreach (var sceneName in currentScenes)
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(scene);
                    if (op != null)
                        yield return op;
                }
            }
        }

        // Load target group
        foreach (var sceneName in targetScenes)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op != null)
                    yield return op;
            }
        }

        _loader.SetCurrentGroup(groupName);

        // Fade from black
        yield return FadeCoroutine(1f, 0f);

        _isTransitioning = false;
        Debug.Log($"scene loader: arrived at {groupName}");
        onComplete?.Invoke();
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, Action onComplete)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
                yield return op;
        }
        onComplete?.Invoke();
    }

    private IEnumerator UnloadSceneCoroutine(string sceneName, Action onComplete)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            var op = SceneManager.UnloadSceneAsync(scene);
            if (op != null)
                yield return op;
        }
        onComplete?.Invoke();
    }

    private IEnumerator FadeCoroutine(float from, float to)
    {
        if (_fadePanel == null) yield break;

        _fadePanel.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadePanel.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }

        _fadePanel.alpha = to;

        if (to <= 0f)
            _fadePanel.gameObject.SetActive(false);
    }

    private void CreateFadePanel()
    {
        var canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();

        var panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        var image = panelObj.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _fadePanel = panelObj.AddComponent<CanvasGroup>();
        _fadePanel.alpha = 0f;
        _fadePanel.blocksRaycasts = false;
        _fadePanel.interactable = false;
        panelObj.SetActive(false);
    }
}
