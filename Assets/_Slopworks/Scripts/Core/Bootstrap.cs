using UnityEngine;
using UnityEngine.SceneManagement;

// Attaches to a GameObject in Core_Network.unity.
// Loads Core_GameManager additively at startup so both Core scenes are always resident.
public class Bootstrap : MonoBehaviour
{
    [SerializeField] private string _gameManagerScene = "Core_GameManager";

    private void Awake()
    {
        SceneManager.LoadScene(_gameManagerScene, LoadSceneMode.Additive);
    }
}
