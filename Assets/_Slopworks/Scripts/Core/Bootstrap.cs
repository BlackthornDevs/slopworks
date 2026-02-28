using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private string _gameManagerScene = "Core_GameManager";

    private void Awake()
    {
        if (!SceneManager.GetSceneByName(_gameManagerScene).isLoaded)
            SceneManager.LoadScene(_gameManagerScene, LoadSceneMode.Additive);
    }
}
