using UnityEngine;

public class PlayerController : MonoBehaviour
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

    private float _pitch;
    private bool _isGrounded;

    // ground check casts against terrain, static geometry, and structures
    private static readonly int GroundMask =
        (1 << PhysicsLayers.Terrain) |
        (1 << PhysicsLayers.BIM_Static) |
        (1 << PhysicsLayers.Structures);

    private void Awake()
    {
        _controls = new SlopworksControls();
        _rb = GetComponent<Rigidbody>();
        _cameraTransform = GetComponentInChildren<Camera>().transform;

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        gameObject.layer = PhysicsLayers.Player;
    }

    private void OnEnable()
    {
        _controls.Exploration.Enable();
    }

    private void OnDisable()
    {
        _controls.Exploration.Disable();
    }

    private void Update()
    {
        Look();
        CheckJump();
    }

    private void FixedUpdate()
    {
        CheckGround();
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
        // offset origin above the capsule bottom so the sphere never starts
        // overlapping the ground (SphereCast ignores already-overlapping colliders)
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
