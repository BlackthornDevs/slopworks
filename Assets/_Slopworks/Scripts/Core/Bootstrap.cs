using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private string _gameManagerScene = "Core_GameManager";
    [SerializeField] private string _initialSceneGroup = "HomeBase";

    private void Awake()
    {
        if (!SceneManager.GetSceneByName(_gameManagerScene).isLoaded)
        {
            SceneManager.sceneLoaded += OnGameManagerLoaded;
            SceneManager.LoadScene(_gameManagerScene, LoadSceneMode.Additive);
        }
        else
        {
            LoadInitialGroup();
        }
    }

    private void OnGameManagerLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != _gameManagerScene)
            return;

        SceneManager.sceneLoaded -= OnGameManagerLoaded;
        LoadInitialGroup();
    }

    private void LoadInitialGroup()
    {
        if (string.IsNullOrEmpty(_initialSceneGroup))
            return;

        var sceneLoader = FindAnyObjectByType<SceneLoaderBehaviour>();
        if (sceneLoader != null)
        {
            sceneLoader.TransitionTo(_initialSceneGroup);
        }
        else
        {
            Debug.LogWarning("bootstrap: no SceneLoaderBehaviour found, skipping initial scene group load");
        }
    }
}
