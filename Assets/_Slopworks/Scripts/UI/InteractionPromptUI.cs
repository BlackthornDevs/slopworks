using TMPro;
using UnityEngine;

/// <summary>
/// Shows "Press E to ..." below the crosshair when looking at an interactable.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    private TextMeshProUGUI _promptText;
    private Camera _playerCamera;

    public void Setup(TextMeshProUGUI promptText, Camera playerCamera)
    {
        _promptText = promptText;
        _playerCamera = playerCamera;
    }

    private void Update()
    {
        if (_promptText == null || _playerCamera == null) return;

        var ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        if (Physics.Raycast(ray, out var hit, 3f, PhysicsLayers.InteractMask))
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                _promptText.text = interactable.GetInteractionPrompt();
                _promptText.gameObject.SetActive(true);
                return;
            }
        }

        _promptText.gameObject.SetActive(false);
    }
}
