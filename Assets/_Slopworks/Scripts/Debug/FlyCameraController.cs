using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple fly camera for scene preview using the New Input System.
/// WASD to move, right-click + mouse to look, Q/E for down/up,
/// shift for speed boost, scroll wheel to change base speed.
/// </summary>
public class FlyCameraController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 20f;
    [SerializeField] private float _lookSpeed = 3f;
    [SerializeField] private float _sprintMultiplier = 3f;

    private float _yaw;
    private float _pitch;

    private void Start()
    {
        var euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // scroll wheel adjusts base speed
        _moveSpeed += mouse.scroll.ReadValue().y * 0.01f;
        _moveSpeed = Mathf.Clamp(_moveSpeed, 2f, 200f);

        // right-click to look
        if (mouse.rightButton.isPressed)
        {
            var delta = mouse.delta.ReadValue();
            _yaw += delta.x * _lookSpeed * 0.1f;
            _pitch -= delta.y * _lookSpeed * 0.1f;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // WASD + Q/E movement
        float speed = _moveSpeed * (kb.leftShiftKey.isPressed ? _sprintMultiplier : 1f);
        var move = Vector3.zero;

        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move += Vector3.down;

        transform.position += move.normalized * speed * Time.deltaTime;
    }
}
