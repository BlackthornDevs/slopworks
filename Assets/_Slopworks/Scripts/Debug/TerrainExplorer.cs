using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Self-contained first-person terrain explorer. No external dependencies.
/// Drop on any GameObject — it creates its own camera, capsule, and CharacterController.
/// WASD to move, mouse to look, Space to jump, Shift to sprint, Escape to unlock cursor.
/// </summary>
public class TerrainExplorer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 6f;
    [SerializeField] private float _sprintSpeed = 30f;
    [SerializeField] private float _jumpHeight = 1.5f;
    [SerializeField] private float _gravity = -20f;

    [Header("Look")]
    [SerializeField] private float _mouseSensitivity = 0.15f;

    private CharacterController _cc;
    private Transform _camTransform;
    private float _pitch;
    private float _verticalVelocity;

    private void Awake()
    {
        // build capsule collider via CharacterController
        _cc = gameObject.AddComponent<CharacterController>();
        _cc.height = 1.8f;
        _cc.radius = 0.3f;
        _cc.center = new Vector3(0f, 0.9f, 0f);
        _cc.slopeLimit = 45f;
        _cc.stepOffset = 0.4f;

        // camera at eye height
        var camGo = new GameObject("ExplorerCamera");
        camGo.transform.SetParent(transform);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        camGo.transform.localRotation = Quaternion.identity;
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1500f;
        cam.fieldOfView = 70f;
        camGo.AddComponent<AudioListener>();
        _camTransform = camGo.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // toggle cursor lock with Escape
        if (kb.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        // mouse look
        var delta = mouse.delta.ReadValue();
        _pitch -= delta.y * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        float yaw = delta.x * _mouseSensitivity;
        transform.Rotate(0f, yaw, 0f);
        _camTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

        // movement
        var move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;

        bool shift = kb.leftShiftKey.isPressed;
        bool ctrl = kb.leftCtrlKey.isPressed;
        float speed = shift && ctrl ? 60f : shift ? _sprintSpeed : _walkSpeed;
        var horizontal = move.normalized * speed;

        // gravity and jump
        if (_cc.isGrounded)
        {
            _verticalVelocity = -2f; // small downward to keep grounded
            if (kb.spaceKey.wasPressedThisFrame)
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
        }
        else
        {
            _verticalVelocity += _gravity * Time.deltaTime;
        }

        var finalMove = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
        _cc.Move(finalMove * Time.deltaTime);

        // position readout (P key)
        if (kb.pKey.wasPressedThisFrame)
        {
            var p = transform.position;
            Debug.Log($"explorer position: ({p.x:F1}, {p.y:F1}, {p.z:F1})");
        }
    }

    private void OnGUI()
    {
        var p = transform.position;
        string info = $"pos: ({p.x:F1}, {p.y:F1}, {p.z:F1})  |  " +
                      $"WASD move, Mouse look, Space jump, Shift sprint, Ctrl+Shift super-sprint, Esc cursor, P log pos";
        GUI.Label(new Rect(10, 10, 800, 25), info);

        if (_cc.isGrounded)
            GUI.Label(new Rect(10, 35, 200, 25), "grounded");
    }
}
