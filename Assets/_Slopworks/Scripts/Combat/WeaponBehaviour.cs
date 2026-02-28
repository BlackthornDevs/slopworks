using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponBehaviour : MonoBehaviour
{
    [SerializeField] private WeaponDefinitionSO _weaponDefinition;
    [SerializeField] private Camera _camera;

    private SlopworksControls _controls;
    private WeaponController _weapon;

    private CameraRecoil _recoil;
    private CameraShake _shake;
    private MuzzleFlash _muzzleFlash;
    private HitMarkerUI _hitMarker;

    public WeaponController Weapon => _weapon;

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

        var canvas = FindAnyObjectByType<PlayerHUD>();
        if (canvas != null)
            _hitMarker = canvas.GetComponent<HitMarkerUI>();
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
        _weapon.Tick(Time.deltaTime);
    }

    private void OnFire(InputAction.CallbackContext ctx)
    {
        if (_camera == null)
            return;

        if (!_weapon.TryFire())
            return;

        if (_recoil != null) _recoil.ApplyRecoil();
        if (_muzzleFlash != null) _muzzleFlash.Fire();

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 muzzlePos = _camera.transform.position + _camera.transform.forward * 0.5f;

        if (Physics.Raycast(ray, out RaycastHit hit, _weapon.Range, PhysicsLayers.WeaponHitMask))
        {
            ProjectileTracer.Spawn(muzzlePos, hit.point);

            var health = hit.collider.GetComponent<HealthBehaviour>();
            if (health != null)
            {
                health.Health.TakeDamage(_weapon.BuildDamageData(gameObject.name));
                if (_hitMarker != null) _hitMarker.Show();
            }
        }
        else
        {
            // tracer to max range even on miss
            ProjectileTracer.Spawn(muzzlePos, ray.GetPoint(_weapon.Range));
        }
    }

    private void OnReload(InputAction.CallbackContext ctx)
    {
        _weapon.Reload();
    }
}
