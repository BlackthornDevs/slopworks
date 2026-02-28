using UnityEngine;
using UnityEngine.InputSystem;

public class CameraModeController : MonoBehaviour
{
    [SerializeField] private Camera _fpsCamera;
    [SerializeField] private Camera _isometricCamera;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private InteractionController _interactionController;

    private SlopworksControls _controls;
    private bool _isFPS;

    private void Awake()
    {
        _controls = new SlopworksControls();
    }

    private void OnEnable()
    {
        _controls.Exploration.SwitchIsometric.performed += OnSwitchToIsometric;
        _controls.Factory.SwitchFPS.performed += OnSwitchToFPS;

        // start in FPS mode
        SwitchToFPS();
    }

    private void OnDisable()
    {
        _controls.Exploration.SwitchIsometric.performed -= OnSwitchToIsometric;
        _controls.Factory.SwitchFPS.performed -= OnSwitchToFPS;

        _controls.Exploration.Disable();
        _controls.Factory.Disable();
    }

    private void OnSwitchToIsometric(InputAction.CallbackContext ctx)
    {
        SwitchToIsometric();
    }

    private void OnSwitchToFPS(InputAction.CallbackContext ctx)
    {
        SwitchToFPS();
    }

    private void SwitchToFPS()
    {
        _isFPS = true;

        _controls.Factory.Disable();
        _controls.Exploration.Enable();

        _fpsCamera.gameObject.SetActive(true);
        _isometricCamera.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_playerController != null) _playerController.enabled = true;
        if (_interactionController != null) _interactionController.enabled = true;
    }

    private void SwitchToIsometric()
    {
        _isFPS = false;

        _controls.Exploration.Disable();
        _controls.Factory.Enable();

        _fpsCamera.gameObject.SetActive(false);
        _isometricCamera.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_playerController != null) _playerController.enabled = false;
        if (_interactionController != null) _interactionController.enabled = false;
    }
}
