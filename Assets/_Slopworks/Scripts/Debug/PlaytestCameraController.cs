using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple fly camera for playtesting. Right-click + mouse to look, WASD to move,
/// scroll wheel to adjust speed. Uses New Input System (D-003).
/// </summary>
public class PlaytestCameraController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 15f;
    [SerializeField] private float _lookSensitivity = 2f;

    private float _pitch;
    private float _yaw;

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

        // Speed adjustment via scroll wheel
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            _moveSpeed = Mathf.Clamp(_moveSpeed * (1f + scroll * 0.002f), 1f, 200f);
        }

        // Look while right-click held
        if (mouse.rightButton.isPressed)
        {
            var delta = mouse.delta.ReadValue();
            _yaw += delta.x * _lookSensitivity * 0.1f;
            _pitch -= delta.y * _lookSensitivity * 0.1f;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // WASD + QE movement
        var move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.qKey.isPressed) move -= Vector3.up;
        if (kb.eKey.isPressed) move += Vector3.up;

        if (move.sqrMagnitude > 0f)
        {
            float speed = _moveSpeed;
            if (kb.leftShiftKey.isPressed) speed *= 2.5f;
            transform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}
