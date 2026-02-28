using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class InteractionController : MonoBehaviour
{
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private Camera _camera;
    [SerializeField] private TextMeshProUGUI _promptText;

    private SlopworksControls _controls;
    private IInteractable _currentTarget;

    private void Awake()
    {
        _controls = new SlopworksControls();
    }

    private void OnEnable()
    {
        _controls.Exploration.Enable();
        _controls.Exploration.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        _controls.Exploration.Interact.performed -= OnInteract;
        _controls.Exploration.Disable();
        ClearTarget();
    }

    private void Update()
    {
        if (_camera == null)
            return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, _interactRange, PhysicsLayers.InteractMask))
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                _currentTarget = interactable;
                if (_promptText != null)
                {
                    _promptText.text = interactable.GetInteractionPrompt();
                    _promptText.enabled = true;
                }
                return;
            }
        }

        ClearTarget();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        _currentTarget?.Interact(gameObject);
    }

    private void ClearTarget()
    {
        _currentTarget = null;
        if (_promptText != null)
            _promptText.enabled = false;
    }
}
