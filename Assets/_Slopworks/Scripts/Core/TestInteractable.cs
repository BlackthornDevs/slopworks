using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string _prompt = "press E to interact";

    public string GetInteractionPrompt() => _prompt;

    public void Interact(GameObject player)
    {
        Debug.Log($"interacted with {gameObject.name}");
    }
}
