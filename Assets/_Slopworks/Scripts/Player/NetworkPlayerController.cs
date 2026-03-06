using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;
    [SerializeField] private float _jumpForce = 7f;
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _groundCheckRadius = 0.25f;
    [SerializeField] private float _groundCheckDistance = 0.15f;

    private SlopworksControls _controls;
    private Rigidbody _rb;
    private Transform _cameraTransform;
    private Camera _camera;
    private AudioListener _audioListener;

    private float _pitch;
    private bool _isGrounded;

    private static readonly int GroundMask =
        (1 << PhysicsLayers.Terrain) |
        (1 << PhysicsLayers.BIM_Static) |
        (1 << PhysicsLayers.Structures) |
        (1 << PhysicsLayers.GridPlane);

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            if (_camera != null) _camera.enabled = false;
            if (_audioListener != null) _audioListener.enabled = false;
            enabled = false;
            return;
        }

        _controls = new SlopworksControls();
        _controls.Exploration.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (IsOwner && _controls != null)
        {
            _controls.Exploration.Disable();
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _camera = GetComponentInChildren<Camera>();
        _cameraTransform = _camera.transform;
        _audioListener = GetComponentInChildren<AudioListener>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        gameObject.layer = PhysicsLayers.Player;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Look();
        CheckJump();
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        CheckGround();
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            return;
        }
        Move();
    }

    private void Look()
    {
        Vector2 look = _controls.Exploration.Look.ReadValue<Vector2>();

        float yaw = look.x * _mouseSensitivity;
        _pitch -= look.y * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        transform.Rotate(0f, yaw, 0f);
        _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = _controls.Exploration.Move.ReadValue<Vector2>();
        bool sprinting = _controls.Exploration.Sprint.IsPressed();
        float speed = sprinting ? _sprintSpeed : _walkSpeed;

        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        Vector3 targetVelocity = direction * speed;

        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
    }

    private void CheckGround()
    {
        float skinOffset = 0.1f;
        Vector3 origin = transform.position + Vector3.up * (_groundCheckRadius + skinOffset);
        _isGrounded = Physics.SphereCast(origin, _groundCheckRadius, Vector3.down,
            out _, _groundCheckDistance + skinOffset, GroundMask);
    }

    private void CheckJump()
    {
        if (_isGrounded && _controls.Exploration.Jump.WasPressedThisFrame())
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, _jumpForce, _rb.linearVelocity.z);
        }
    }
}
