using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponBehaviour : NetworkBehaviour
{
    [SerializeField] private WeaponDefinitionSO _weaponDefinition;
    [SerializeField] private Camera _camera;

    private SlopworksControls _controls;
    private WeaponController _weapon;

    private CameraRecoil _recoil;
    private CameraShake _shake;
    private MuzzleFlash _muzzleFlash;
    [SerializeField] private HitMarkerUI _hitMarker;

    public WeaponController Weapon => _weapon;

    public void SetHitMarker(HitMarkerUI hitMarker) => _hitMarker = hitMarker;

    private void Awake()
    {
        _controls = new SlopworksControls();
        _weapon = new WeaponController(_weaponDefinition);
    }

    private void Start()
    {
        if (_camera != null)
        {
            _recoil = _camera.GetComponent<CameraRecoil>();
            _shake = _camera.GetComponent<CameraShake>();
            _muzzleFlash = _camera.GetComponentInChildren<MuzzleFlash>();
        }
    }

    private void OnEnable()
    {
        _controls.Exploration.Enable();
        _controls.Exploration.Fire.performed += OnFire;
        _controls.Exploration.Reload.performed += OnReload;
    }

    private void OnDisable()
    {
        _controls.Exploration.Fire.performed -= OnFire;
        _controls.Exploration.Reload.performed -= OnReload;
        _controls.Exploration.Disable();
    }

    private void Update()
    {
        _weapon?.Tick(Time.deltaTime);
    }

    private void OnFire(InputAction.CallbackContext ctx)
    {
        if (_camera == null || _weapon == null) return;
        if (NetworkObject != null && !IsOwner) return;
        if (!_weapon.TryFire()) return;

        // visual feedback runs on the owning client immediately
        if (_recoil != null) _recoil.ApplyRecoil();
        if (_muzzleFlash != null) _muzzleFlash.Fire();

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 origin = ray.origin;
        Vector3 direction = ray.direction;
        Vector3 muzzlePos = _camera.transform.position + _camera.transform.forward * 0.5f;

        // client-side prediction: show tracer and hit marker immediately
        if (Physics.Raycast(ray, out RaycastHit hit, _weapon.Range, PhysicsLayers.WeaponHitMask))
        {
            ProjectileTracer.Spawn(muzzlePos, hit.point);

            if (hit.collider.GetComponent<HealthBehaviour>() != null)
            {
                if (_hitMarker != null) _hitMarker.Show();
            }
        }
        else
        {
            ProjectileTracer.Spawn(muzzlePos, ray.GetPoint(_weapon.Range));
        }

        // server validates the shot and applies damage
        if (NetworkObject != null)
            ServerFireWeapon(origin, direction);
        else
            ApplyDamageLocal(origin, direction);
    }

    [ServerRpc]
    private void ServerFireWeapon(Vector3 origin, Vector3 direction)
    {
        ApplyDamageLocal(origin, direction);
    }

    private void ApplyDamageLocal(Vector3 origin, Vector3 direction)
    {
        if (Physics.Raycast(origin, direction, out RaycastHit hit, _weapon.Range, PhysicsLayers.WeaponHitMask))
        {
            var health = hit.collider.GetComponent<HealthBehaviour>();
            if (health != null)
            {
                var baseDamage = _weapon.BuildDamageData(gameObject.name);
                var damage = new DamageData(baseDamage.amount, baseDamage.sourceId,
                                            baseDamage.type, transform.position);
                health.Health.TakeDamage(damage);
            }
        }
    }

    private void OnReload(InputAction.CallbackContext ctx)
    {
        _weapon.Reload();
    }
}
