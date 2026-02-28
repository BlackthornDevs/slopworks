using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string _prompt = "press E to interact";
    [SerializeField] private Color _interactColor = Color.green;

    private Renderer _renderer;
    private Color _originalColor;
    private bool _toggled;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _originalColor = _renderer.material.color;
    }

    public string GetInteractionPrompt() => _prompt;

    public void Interact(GameObject player)
    {
        Debug.Log($"interacted with {gameObject.name}");

        if (_renderer == null)
            return;

        _toggled = !_toggled;
        _renderer.material.color = _toggled ? _interactColor : _originalColor;
    }
}
